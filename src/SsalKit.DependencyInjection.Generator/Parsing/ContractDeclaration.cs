using Microsoft.CodeAnalysis;

namespace SsalKit.DependencyInjection.Generator.Parsing;

/// <summary>
/// Why a single <c>[assembly: RegisterImplementationsOf]</c> declaration was rejected, or
/// <see cref="Valid"/> when it was not.
/// </summary>
internal enum ContractValidationKind
{
    /// <summary>The declaration is usable; a convention scan is performed for it.</summary>
    Valid,

    /// <summary>SSAL021: the contract argument is not an interface (or is not a type at all).</summary>
    NotAnInterface,

    /// <summary>SSAL024: the <c>Lifetime</c> or <c>Mode</c> argument is not a defined enum value.</summary>
    UndefinedEnumValue,

    /// <summary>SSAL025: the contract cannot be named from the generated registration code.</summary>
    Inaccessible,

    /// <summary>SSAL023: an earlier declaration in this assembly already declared the same contract.</summary>
    Duplicate,
}

/// <summary>
/// One <c>[assembly: RegisterImplementationsOf]</c> declaration, resolved against the compilation
/// that carries it. Holds live symbols and is therefore strictly transient -- it is produced and
/// consumed within a single analyzer callback or a single generator pipeline step, and never
/// stored in an incremental model (see <see cref="Models.ConventionRegistrationModel"/> for the
/// equatable form the pipeline actually carries).
/// </summary>
/// <param name="Attribute">The attribute application, used only to locate the diagnostics reported for it.</param>
/// <param name="Contract">
/// The contract interface, or <see langword="null"/> when the argument was not a type symbol at all
/// (e.g. an explicit <c>null</c>). Never <see langword="null"/> when <see cref="Kind"/> is
/// <see cref="ContractValidationKind.Valid"/>.
/// </param>
/// <param name="ContractFqn">
/// The contract's fully-qualified spelling: typeof-form (<c>global::Ns.IHandler&lt;,&gt;</c>) when
/// <see cref="IsUnbound"/>, ordinary fully-qualified form otherwise. Doubles as the identity used
/// for duplicate detection, which is why an unbound declaration and a closed instantiation of the
/// same generic definition are correctly treated as two different contracts.
/// </param>
/// <param name="IsUnbound">
/// Whether the contract was declared as an unbound generic type reference
/// (<c>typeof(IHandler&lt;,&gt;)</c>), which is what makes it match every instantiation rather than
/// one specific type.
/// </param>
/// <param name="Lifetime">The underlying integral value of the <c>ServiceLifetime</c> argument.</param>
/// <param name="Mode">The underlying integral value of the <c>Mode</c> argument.</param>
/// <param name="Kind">Whether -- and if not, why not -- this declaration is usable.</param>
/// <param name="Detail">
/// The message argument the reporting diagnostic needs beyond the contract name: a description of
/// the offending type kind for <see cref="ContractValidationKind.NotAnInterface"/>, or the
/// out-of-range value for <see cref="ContractValidationKind.UndefinedEnumValue"/>. Empty otherwise.
/// </param>
/// <param name="EnumTypeName">
/// The name of the enum the out-of-range <see cref="Detail"/> value was supplied for
/// (<c>ServiceLifetime</c> or <c>RegistrationMode</c>). Empty unless <see cref="Kind"/> is
/// <see cref="ContractValidationKind.UndefinedEnumValue"/>.
/// </param>
internal readonly record struct ContractDeclaration(
    AttributeData Attribute,
    INamedTypeSymbol? Contract,
    string ContractFqn,
    bool IsUnbound,
    int Lifetime,
    int Mode,
    ContractValidationKind Kind,
    string Detail,
    string EnumTypeName);
