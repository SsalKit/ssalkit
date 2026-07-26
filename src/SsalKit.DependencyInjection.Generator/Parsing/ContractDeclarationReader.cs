using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Models;
using SsalKit.Generators.Toolkit;

namespace SsalKit.DependencyInjection.Generator.Parsing;

/// <summary>
/// Reads and validates every <c>[assembly: RegisterImplementationsOf]</c> declaration in a
/// compilation. Shared between <c>RegisterImplementationsOfAnalyzer</c> (which reports a diagnostic
/// for each rejected declaration) and <c>ConventionScanner</c> (which simply skips them), so what
/// is reported and what the generator refuses to emit can never drift apart.
/// </summary>
internal static class ContractDeclarationReader
{
    public const string AttributeMetadataName = "SsalKit.DependencyInjection.RegisterImplementationsOfAttribute";
    public const string ServiceAttributeMetadataName = "SsalKit.DependencyInjection.ServiceAttribute";

    /// <summary>
    /// The <c>Mode</c> a declaration that does not specify one registers with. Deliberately
    /// different from <c>[Service]</c>'s <see cref="WellKnownRegistrationMode.Add"/> default; see
    /// <c>RegisterImplementationsOfAttribute.Mode</c> for why.
    /// </summary>
    private const int DefaultMode = (int)WellKnownRegistrationMode.TryAddEnumerable;

    /// <summary>
    /// Returns every declaration found on <paramref name="compilation"/>'s assembly, in the order
    /// <see cref="IAssemblySymbol.GetAttributes"/> reports them (i.e. source order), each already
    /// classified as usable or not. Empty when the attribute is not referenced or not used at all,
    /// which is the fast path every assembly that does not use the feature takes.
    /// </summary>
    public static ImmutableArray<ContractDeclaration> Read(Compilation compilation)
    {
        var attributeSymbol = compilation.GetTypeByMetadataName(AttributeMetadataName);
        if (attributeSymbol is null)
        {
            return ImmutableArray<ContractDeclaration>.Empty;
        }

        return Read(compilation, attributeSymbol);
    }

    /// <inheritdoc cref="Read(Compilation)"/>
    /// <param name="compilation">The compilation whose assembly-level attributes are read.</param>
    /// <param name="attributeSymbol">
    /// The already-resolved <c>RegisterImplementationsOfAttribute</c> symbol, for callers (such as
    /// an analyzer's compilation-start action) that have looked it up once and can avoid a second
    /// metadata-name lookup per call.
    /// </param>
    public static ImmutableArray<ContractDeclaration> Read(Compilation compilation, INamedTypeSymbol attributeSymbol)
    {
        var attributes = compilation.Assembly.GetAttributes();
        if (attributes.IsDefaultOrEmpty)
        {
            return ImmutableArray<ContractDeclaration>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<ContractDeclaration>();

        // Tracks the contracts already claimed by an otherwise-valid earlier declaration, so the
        // first one wins and every later one is marked Duplicate. String identity (the same
        // fully-qualified spelling the emitter uses) rather than symbol identity: an unbound
        // `typeof(IHandler<>)` symbol is a freshly constructed instance at each application site
        // and is not guaranteed to compare equal to another one for the same definition.
        var seenContracts = new HashSet<string>(StringComparer.Ordinal);

        foreach (var attributeData in attributes)
        {
            if (!SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, attributeSymbol))
            {
                continue;
            }

            builder.Add(Classify(attributeData, compilation, seenContracts));
        }

        return builder.ToImmutable();
    }

    private static ContractDeclaration Classify(AttributeData attributeData, Compilation compilation, HashSet<string> seenContracts)
    {
        var lifetime = GetLifetime(attributeData);
        var mode = GetMode(attributeData);

        // SSAL021: the contract argument must be an interface. Checked before the enum ranges
        // because it is the argument the whole declaration is about -- naming a class here is a
        // categorically different mistake than mistyping a lifetime, and reporting the enum first
        // would bury it.
        var contractConstant = attributeData.ConstructorArguments.Length > 0
            ? attributeData.ConstructorArguments[0]
            : default;

        if (contractConstant.Value is not INamedTypeSymbol contract)
        {
            // An explicit `null`, or a type that is not a named type at all (an array or pointer
            // type -- `typeof(IFoo[])` is perfectly legal C# in an attribute argument).
            var (fqn, detail) = contractConstant.Value is ITypeSymbol otherType
                ? (SymbolFacts.ToFqn(otherType), "not an interface")
                : ("null", "null");

            return Invalid(attributeData, lifetime, mode, ContractValidationKind.NotAnInterface, detail, fqn);
        }

        var isUnbound = contract.IsUnboundGenericType;
        var contractFqn = isUnbound
            ? OpenGenericTypeofFormatter.Format(contract)
            : SymbolFacts.ToFqn(contract);

        if (contract.TypeKind != TypeKind.Interface)
        {
            return Invalid(attributeData, lifetime, mode, ContractValidationKind.NotAnInterface, DescribeTypeKind(contract), contractFqn);
        }

        // SSAL024: an out-of-range Lifetime/Mode (e.g. from `(ServiceLifetime)42`) must not reach
        // the emitter, which would either silently mis-render it or emit nothing at all for it.
        if (lifetime is < (int)WellKnownLifetime.Singleton or > (int)WellKnownLifetime.Transient)
        {
            return Invalid(
                attributeData, lifetime, mode, ContractValidationKind.UndefinedEnumValue,
                lifetime.ToString(CultureInfo.InvariantCulture), contractFqn, isUnbound, contract, "ServiceLifetime");
        }

        if (mode is < (int)WellKnownRegistrationMode.Add or > (int)WellKnownRegistrationMode.Replace)
        {
            return Invalid(
                attributeData, lifetime, mode, ContractValidationKind.UndefinedEnumValue,
                mode.ToString(CultureInfo.InvariantCulture), contractFqn, isUnbound, contract, "RegistrationMode");
        }

        // SSAL025: the contract is emitted verbatim into the generated registration code, so it
        // must be nameable from there -- a file-local interface can be named at the attribute
        // application site (same file) but never from the generated one.
        if (!TypeAccessibilityChecker.IsAccessible(contract, compilation))
        {
            return Invalid(
                attributeData, lifetime, mode, ContractValidationKind.Inaccessible,
                detail: string.Empty, contractFqn, isUnbound, contract);
        }

        // SSAL023: last, so that a duplicate of an already-invalid contract is reported for what is
        // actually wrong with it rather than for repeating it.
        if (!seenContracts.Add(contractFqn))
        {
            return Invalid(
                attributeData, lifetime, mode, ContractValidationKind.Duplicate,
                detail: string.Empty, contractFqn, isUnbound, contract);
        }

        return new ContractDeclaration(
            attributeData, contract, contractFqn, isUnbound, lifetime, mode,
            ContractValidationKind.Valid, Detail: string.Empty, EnumTypeName: string.Empty);
    }

    private static ContractDeclaration Invalid(
        AttributeData attributeData,
        int lifetime,
        int mode,
        ContractValidationKind kind,
        string detail,
        string contractFqn,
        bool isUnbound = false,
        INamedTypeSymbol? contract = null,
        string enumTypeName = "") =>
        new(attributeData, contract, contractFqn, isUnbound, lifetime, mode, kind, detail, enumTypeName);

    /// <summary>
    /// Reads the <c>lifetime</c> constructor argument. It is an optional parameter, so the compiler
    /// always supplies the default when it is omitted; the fallback here only covers a malformed
    /// attribute application (e.g. one the compiler has already rejected).
    /// </summary>
    private static int GetLifetime(AttributeData attributeData)
    {
        var constructorArguments = attributeData.ConstructorArguments;
        if (constructorArguments.Length > 1 && constructorArguments[1].Value is int lifetimeValue)
        {
            return lifetimeValue;
        }

        return (int)WellKnownLifetime.Singleton;
    }

    private static int GetMode(AttributeData attributeData)
    {
        foreach (var namedArgument in attributeData.NamedArguments)
        {
            if (namedArgument.Key == "Mode" && namedArgument.Value.Value is int modeValue)
            {
                return modeValue;
            }
        }

        return DefaultMode;
    }

    /// <summary>
    /// A short, article-prefixed description of what the contract type actually is, for SSAL021's
    /// message (which reads "... because it is {1}").
    /// </summary>
    private static string DescribeTypeKind(INamedTypeSymbol contract) => contract.TypeKind switch
    {
        TypeKind.Class => "a class",
        TypeKind.Struct => "a struct",
        TypeKind.Enum => "an enum",
        TypeKind.Delegate => "a delegate type",
        TypeKind.Error => "an unresolved type",
        _ => "not an interface",
    };
}
