using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Emission;
using SsalKit.DependencyInjection.Generator.Models;
using SsalKit.DependencyInjection.Generator.Parsing;
using SsalKit.Generators.Toolkit;

namespace SsalKit.DependencyInjection.Generator;

/// <summary>
/// Generates a single <c>IServiceCollection</c> extension method per assembly that registers
/// every class decorated with <c>[SsalKit.DependencyInjection.Service]</c> and every class matched
/// by an <c>[assembly: SsalKit.DependencyInjection.RegisterImplementationsOf]</c> contract, plus
/// one implementation class per interface decorated with
/// <c>[SsalKit.DependencyInjection.ServiceFactory]</c> (each of which that same extension method
/// registers as a singleton).
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ServiceRegistrationGenerator : IIncrementalGenerator
{
    private const string ServiceAttributeMetadataName = "SsalKit.DependencyInjection.ServiceAttribute";
    private const string ServiceFactoryAttributeMetadataName = "SsalKit.DependencyInjection.ServiceFactoryAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ClassRegistrationModel?> classes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ServiceAttributeMetadataName,
                // A record class (RecordDeclarationSyntax) is a distinct syntax node from a plain
                // class (ClassDeclarationSyntax), even though the analyzer -- which inspects the
                // bound symbol rather than syntax -- sees both as TypeKind.Class alike. Without
                // matching both node kinds here, a `[Service] public record class Foo : IFoo` would
                // be silently skipped by the generator despite the analyzer treating it as valid.
                // A record *struct* needs no special handling: [AttributeUsage(Class)] and this
                // predicate both already exclude it, and TypeKind.Class in the parser/analyzer
                // rejects it too, for the (impossible) case syntax analysis alone let it through.
                predicate: static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax
                    or Microsoft.CodeAnalysis.CSharp.Syntax.RecordDeclarationSyntax,
                transform: static (ctx, ct) => ServiceAttributeParser.GetModel(ctx, ct))
            .WithTrackingName(TrackingNames.Classes);

        IncrementalValuesProvider<ClassRegistrationModel> validClasses = classes
            .Where(static model => model is not null)
            .WithTrackingName(TrackingNames.ValidClasses)!;

        IncrementalValueProvider<ImmutableArray<ClassRegistrationModel>> collectedClasses = validClasses
            .Collect()
            .WithTrackingName(TrackingNames.CollectedClasses);

        IncrementalValuesProvider<ServiceFactoryModel?> factories = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ServiceFactoryAttributeMetadataName,
                // Deliberately unfiltered on syntax kind. [AttributeUsage(AttributeTargets.Interface)]
                // already restricts what can legally carry the attribute, and an illegal target is
                // still bound onto its symbol (alongside CS0592) -- letting it through to the
                // symbol-based validation keeps this predicate from being the thing that decides
                // which invalid shapes are silently ignored versus rejected by SSAL016.
                predicate: static (_, _) => true,
                transform: static (ctx, ct) => ServiceFactoryParser.GetModel(ctx, ct))
            .WithTrackingName(TrackingNames.Factories);

        IncrementalValuesProvider<ServiceFactoryModel> validFactories = factories
            .Where(static model => model is not null)
            .WithTrackingName(TrackingNames.ValidFactories)!;

        IncrementalValueProvider<ImmutableArray<ServiceFactoryModel>> collectedFactories = validFactories
            .Collect()
            .WithTrackingName(TrackingNames.CollectedFactories);

        // Unlike every other stage, this one is driven by the whole compilation rather than by the
        // syntax nodes that declare it: which classes an [assembly: RegisterImplementationsOf]
        // contract matches is a property of every type in the compilation, so there is no per-node
        // provider that could express it. It therefore re-runs on every compilation change --
        // cheaply for the assemblies that do not use the feature (ConventionScanner.Scan returns in
        // constant time when no contract is declared), and always into an equatable model array, so
        // an unrelated edit leaves the value unchanged and the combine/emit stages below are
        // skipped. See ConventionScanner's remarks for the full reasoning.
        IncrementalValueProvider<EquatableArray<ConventionRegistrationModel>> conventions = context.CompilationProvider
            .Select(static (compilation, ct) => ConventionScanner.Scan(compilation, ct))
            .WithTrackingName(TrackingNames.Conventions);

        IncrementalValueProvider<string?> assemblyName = context.CompilationProvider
            .Select(static (compilation, _) => compilation.AssemblyName)
            .WithTrackingName(TrackingNames.AssemblyName);

        IncrementalValueProvider<(((ImmutableArray<ClassRegistrationModel> Classes, ImmutableArray<ServiceFactoryModel> Factories) Registrations, EquatableArray<ConventionRegistrationModel> Conventions) Scanned, string? AssemblyName)> combined =
            collectedClasses
                .Combine(collectedFactories)
                .Combine(conventions)
                .Combine(assemblyName)
                .WithTrackingName(TrackingNames.Combined);

        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var (classes, factoryModels) = source.Scanned.Registrations;
            var conventionModels = source.Scanned.Conventions.AsImmutableArray();

            if (classes.IsDefaultOrEmpty && factoryModels.IsDefaultOrEmpty && conventionModels.IsEmpty)
            {
                return;
            }

            var (hintName, generatedSource) = ServiceRegistrationEmitter.Emit(
                classes, conventionModels, factoryModels, source.AssemblyName);

            spc.AddSource(hintName, generatedSource);
        });

        // A source output of its own, not folded into the one above: the implementation classes
        // depend on nothing but their own models, so an edit that only changes a [Service] class
        // (or the assembly name) leaves every factory file untouched.
        context.RegisterSourceOutput(collectedFactories, static (spc, models) => EmitFactories(spc, models));
    }

    /// <summary>
    /// Writes one implementation file per distinct <c>[ServiceFactory]</c> interface, under a hint
    /// name guaranteed unique within the run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both guarantees are load-bearing rather than defensive, because <c>AddSource</c> throws
    /// <c>ArgumentException</c> (surfacing as CS8785) on a repeated hint name, and that exception
    /// takes down the <em>whole</em> generator: the registration extension method disappears along
    /// with every factory file, turning a local mistake into an assembly that no longer compiles at
    /// all.
    /// </para>
    /// <para>
    /// Two distinct paths lead there. A <c>partial</c> interface whose parts each carry the
    /// attribute is matched once per declaring part, producing byte-identical models -- deduplicated
    /// here by interface, so the emitted output is the same one part would have produced. And
    /// <c>HintNameSanitizer</c> caps a hint name at 200 characters by keeping the tail, so two
    /// deeply-nested interfaces whose qualified names differ only near the front sanitize to the
    /// same name -- disambiguated here with a counter suffix.
    /// </para>
    /// <para>
    /// The models are sorted by interface name first, so which of two colliding factories keeps the
    /// unsuffixed hint name is decided by the source, not by the order the pipeline produced them.
    /// </para>
    /// </remarks>
    private static void EmitFactories(SourceProductionContext spc, ImmutableArray<ServiceFactoryModel> models)
    {
        if (models.IsDefaultOrEmpty)
        {
            return;
        }

        var ordered = models.Sort(static (a, b) => string.CompareOrdinal(a.InterfaceTypeFqn, b.InterfaceTypeFqn));

        var emittedInterfaces = new HashSet<string>(StringComparer.Ordinal);
        var usedHintNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var model in ordered)
        {
            spc.CancellationToken.ThrowIfCancellationRequested();

            if (!emittedInterfaces.Add(model.InterfaceTypeFqn))
            {
                continue;
            }

            spc.AddSource(GetUniqueHintName(model.HintName, usedHintNames), ServiceFactoryImplementationEmitter.Emit(model));
        }
    }

    /// <summary>
    /// Returns <paramref name="hintName"/> if no file has claimed it yet, or the first
    /// <c>...{n}.g.cs</c> variant of it that is free.
    /// </summary>
    private static string GetUniqueHintName(string hintName, HashSet<string> usedHintNames)
    {
        if (usedHintNames.Add(hintName))
        {
            return hintName;
        }

        const string extension = ".g.cs";

        // The only way two hint names collide is that HintNameSanitizer truncated both to its
        // length cap, so the disambiguated name is kept at that same cap -- growing past it would
        // undo the very trimming that a colliding name is evidence of. Truncating from the front
        // (as the sanitizer does) leaves the counter at the tail, so successive candidates always
        // differ and the loop terminates.
        const int maxLength = 200;

        var stem = hintName.EndsWith(extension, StringComparison.Ordinal)
            ? hintName.Substring(0, hintName.Length - extension.Length)
            : hintName;

        for (var suffix = 2; ; suffix++)
        {
            var candidate = stem + suffix.ToString(CultureInfo.InvariantCulture) + extension;
            if (candidate.Length > maxLength)
            {
                candidate = candidate.Substring(candidate.Length - maxLength);
            }

            if (usedHintNames.Add(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// Names assigned to each pipeline stage via <c>WithTrackingName</c>, so tests can inspect
    /// <see cref="IncrementalGeneratorRunStep"/> results (via
    /// <c>GeneratorDriverRunResult.Results[i].TrackedSteps</c>) to confirm the pipeline caches
    /// correctly across runs that don't change its inputs.
    /// </summary>
    internal static class TrackingNames
    {
        public const string Classes = "Classes";
        public const string ValidClasses = "ValidClasses";
        public const string CollectedClasses = "CollectedClasses";
        public const string Factories = "Factories";
        public const string ValidFactories = "ValidFactories";
        public const string CollectedFactories = "CollectedFactories";
        public const string Conventions = "Conventions";
        public const string AssemblyName = "AssemblyName";
        public const string Combined = "Combined";
    }
}
