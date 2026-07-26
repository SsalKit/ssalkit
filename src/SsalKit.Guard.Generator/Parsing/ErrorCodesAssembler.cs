using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using SsalKit.Generators.Toolkit;
using SsalKit.Guard.Generator.Diagnostics;
using SsalKit.Guard.Generator.Models;

namespace SsalKit.Guard.Generator.Parsing;

/// <summary>
/// Joins the collected containers with the collected exceptions -- on the code enum, which is what
/// lets several unrelated code enums coexist in one assembly -- and folds the result into the two
/// things the source-output stages consume: the containers to emit, and the diagnostics to report.
/// </summary>
/// <remarks>
/// This is where every rule that cannot be decided while looking at a single declaration lives:
/// SSALG003 (the same exception registered twice in one container), SSALG006 (whether an exception
/// without a usable constructor actually ended up in a container), SSALG008 (whether a declared code
/// enum has a container at all), and the derived-before-base ordering of the mapping table.
/// </remarks>
internal static class ErrorCodesAssembler
{
    private const string ExceptionSuffix = "Exception";

    public static ErrorCodesAnalysisResult Analyze(
        ImmutableArray<EquatableArray<ErrorCodesContainerCandidate>> containerGroups,
        ImmutableArray<EquatableArray<ErrorCodeExceptionCandidate>> exceptionGroups,
        CancellationToken cancellationToken)
    {
        var containers = Flatten(containerGroups);
        var exceptions = Flatten(exceptionGroups);

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        CollectParseDiagnostics(containers, exceptions, diagnostics);
        ReportExceptionsWithoutContainer(containers, exceptions, diagnostics);

        var models = ImmutableArray.CreateBuilder<ErrorCodesContainerModel>();

        // Ordinal ordering throughout: the order pipeline nodes happened to run in must never leak
        // into the generated output or the diagnostic list.
        foreach (var container in containers
            .Where(candidate => candidate.IsValid)
            .OrderBy(candidate => candidate.ContainerFqn, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.TCodeFqn, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var model = BuildContainer(container, exceptions, diagnostics);
            if (model is not null)
            {
                models.Add(model);
            }
        }

        return new ErrorCodesAnalysisResult(
            EquatableArray.Create(models.ToImmutable()),
            EquatableArray.Create(SymbolFacts.SortForDiagnosticDeterminism(diagnostics.ToImmutable())));
    }

    private static ImmutableArray<T> Flatten<T>(ImmutableArray<EquatableArray<T>> groups)
        where T : IEquatable<T>
    {
        if (groups.IsDefaultOrEmpty)
        {
            return ImmutableArray<T>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<T>();
        foreach (var group in groups)
        {
            builder.AddRange(group.AsImmutableArray());
        }

        return builder.ToImmutable();
    }

    private static void CollectParseDiagnostics(
        ImmutableArray<ErrorCodesContainerCandidate> containers,
        ImmutableArray<ErrorCodeExceptionCandidate> exceptions,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        foreach (var exception in exceptions)
        {
            if (exception.Diagnostic is not null)
            {
                diagnostics.Add(exception.Diagnostic);
            }
        }

        foreach (var container in containers)
        {
            if (container.Diagnostic is not null)
            {
                diagnostics.Add(container.Diagnostic);
            }

            foreach (var registration in container.ExternalRegistrations)
            {
                if (registration.Diagnostic is not null)
                {
                    diagnostics.Add(registration.Diagnostic);
                }
            }
        }
    }

    /// <summary>
    /// SSALG008: a code declared with no container anywhere in the compilation generates nothing at
    /// all, which is indistinguishable from the generator not running. A container that exists but
    /// was itself rejected still counts, so the user fixes one rule rather than reading a warning
    /// per exception on top of it.
    /// </summary>
    private static void ReportExceptionsWithoutContainer(
        ImmutableArray<ErrorCodesContainerCandidate> containers,
        ImmutableArray<ErrorCodeExceptionCandidate> exceptions,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var declaredCodeEnums = new HashSet<string>(
            containers.Select(container => container.TCodeFqn), StringComparer.Ordinal);

        foreach (var exception in exceptions)
        {
            if (exception.IsValid && !declaredCodeEnums.Contains(exception.TCodeFqn))
            {
                diagnostics.Add(new DiagnosticInfo(
                    DiagnosticDescriptors.NoContainerForCodeEnum,
                    exception.Location,
                    exception.ExceptionDisplayName,
                    exception.TCodeDisplayName));
            }
        }
    }

    /// <summary>
    /// Builds one container's model, or returns <see langword="null"/> when an ambiguous
    /// registration (SSALG003) means no mapping should be generated for it at all.
    /// </summary>
    private static ErrorCodesContainerModel? BuildContainer(
        ErrorCodesContainerCandidate container,
        ImmutableArray<ErrorCodeExceptionCandidate> exceptions,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var owned = exceptions
            .Where(exception => exception.IsValid && string.Equals(exception.TCodeFqn, container.TCodeFqn, StringComparison.Ordinal))
            .OrderBy(exception => exception.ExceptionFqn, StringComparer.Ordinal)
            .ToList();

        var external = container.ExternalRegistrations
            .Where(registration => registration.IsValid)
            .OrderBy(registration => registration.ExceptionFqn, StringComparer.Ordinal)
            .ToList();

        if (ReportDuplicateRegistrations(container, owned, external, diagnostics))
        {
            return null;
        }

        foreach (var exception in owned.Where(exception => exception.Constructor == ConstructorShape.None))
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.NoRecognisedConstructor,
                exception.Location,
                exception.ExceptionDisplayName,
                container.ContainerDisplayName));
        }

        var entries = owned
            .Select(exception => (exception.ExceptionFqn, exception.CodeExpression, exception.InheritanceDepth))
            .Concat(external.Select(registration =>
                (registration.ExceptionFqn, registration.CodeExpression, registration.InheritanceDepth)))

            // The derived-before-base guarantee: deepest first, so a derived registration is always
            // tested before its base. The fully qualified name breaks ties, which keeps the emitted
            // file byte-for-byte stable across runs even when no two registrations are related.
            .OrderByDescending(entry => entry.InheritanceDepth)
            .ThenBy(entry => entry.ExceptionFqn, StringComparer.Ordinal)
            .Select(entry => new MappingEntryModel(entry.ExceptionFqn, entry.CodeExpression))
            .ToImmutableArray();

        return new ErrorCodesContainerModel(
            container.Namespace,
            container.ContainingTypeDeclarations,
            container.ContainerDeclaration,
            container.TCodeFqn,
            container.TCodeDisplayName,
            container.TCodeIsEffectivelyPublic ? "public" : "internal",
            EquatableArray.Create(entries),
            BuildHelpers(owned),
            container.HintName);
    }

    /// <summary>
    /// Reports SSALG003 on every registration of an exception type that appears more than once in
    /// the container -- both sites, since which one to delete is the user's decision -- and returns
    /// whether any fired.
    /// </summary>
    private static bool ReportDuplicateRegistrations(
        ErrorCodesContainerCandidate container,
        List<ErrorCodeExceptionCandidate> owned,
        List<ExternalRegistrationCandidate> external,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var registrations = owned
            .Select(exception => (exception.ExceptionFqn, exception.ExceptionDisplayName, exception.Location))
            .Concat(external.Select(registration =>
                (registration.ExceptionFqn, registration.ExceptionDisplayName, registration.Location)))
            .ToList();

        var duplicated = registrations
            .GroupBy(registration => registration.ExceptionFqn, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToList();

        foreach (var registration in duplicated.SelectMany(group => group))
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.DuplicateRegistration,
                registration.Location,
                registration.ExceptionDisplayName,
                container.ContainerDisplayName));
        }

        return duplicated.Count > 0;
    }

    /// <summary>
    /// Names and orders the factory/throw helpers. Only exceptions declared in this compilation get
    /// them: a registration made with <c>[ExternalErrorCode]</c> is somebody else's type, whose
    /// constructor contract this library cannot vouch for.
    /// </summary>
    private static EquatableArray<ExceptionHelperModel> BuildHelpers(List<ErrorCodeExceptionCandidate> owned)
    {
        var withConstructors = owned
            .Where(exception => exception.Constructor != ConstructorShape.None)
            .ToList();

        var trimmedNameCounts = withConstructors
            .GroupBy(exception => TrimExceptionSuffix(exception.ExceptionName), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var taken = new HashSet<string>(StringComparer.Ordinal);
        var helpers = ImmutableArray.CreateBuilder<ExceptionHelperModel>();

        // 'owned' is already ordered by fully qualified name, so which exception keeps the shortest
        // name when two would collide is a property of the code, not of the pipeline's run order.
        foreach (var exception in withConstructors)
        {
            var trimmed = TrimExceptionSuffix(exception.ExceptionName);
            var name = trimmedNameCounts[trimmed] == 1 ? trimmed : exception.ExceptionName;

            if (taken.Contains(name))
            {
                name = exception.ExceptionFlattenedName;
            }

            while (!taken.Add(name))
            {
                name += "_";
            }

            helpers.Add(new ExceptionHelperModel(
                exception.ExceptionFqn,
                CSharpNaming.EscapeKeyword(name),
                CSharpNaming.EscapeKeyword("Throw" + name),
                exception.CodeDisplayName,
                exception.Constructor,
                exception.MessageIsNullable,
                exception.InnerIsNullable,
                exception.IsEffectivelyPublic ? "public" : "internal"));
        }

        return EquatableArray.Create(helpers.ToImmutable());
    }

    /// <summary>
    /// <c>UserNotFoundException</c> becomes <c>UserNotFound</c>, so a call site reads
    /// <c>throw GameErrors.UserNotFound(...)</c>. A name that is nothing but the suffix keeps it:
    /// there is no such thing as a member with an empty name.
    /// </summary>
    private static string TrimExceptionSuffix(string name)
    {
        if (name.Length <= ExceptionSuffix.Length || !name.EndsWith(ExceptionSuffix, StringComparison.Ordinal))
        {
            return name;
        }

        return name.Substring(0, name.Length - ExceptionSuffix.Length);
    }
}
