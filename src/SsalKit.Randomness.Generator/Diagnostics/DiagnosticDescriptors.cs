using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;

namespace SsalKit.Randomness.Generator.Diagnostics;

/// <summary>
/// The <c>SSALR</c> diagnostic table reported by <see cref="RandomWeightGenerator"/>.
/// </summary>
/// <remarks>
/// Every rule is an <see cref="DiagnosticSeverity.Error"/>: each one describes a
/// <c>[RandomWeight]</c> application the generator cannot honour, and silently generating nothing
/// would leave the consumer with an unresolved <c>PickWeighted</c> call site and no explanation.
/// When any rule fires for a type, no extension class is generated for that type at all -- there is
/// no partial generation.
/// </remarks>
internal static class DiagnosticDescriptors
{
    private static readonly DiagnosticDescriptorFactory Factory = new("SSALR", "SsalKit.Randomness");

    /// <summary>
    /// SSALR001: the decorated member's type is not one the runtime weighted-picking APIs accept.
    /// Message argument 2 carries a type-specific note for the deliberately-excluded types
    /// (<see langword="ulong"/> and <see langword="decimal"/>), and is empty otherwise.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedWeightType = Factory.Error(
        1,
        "Unsupported [RandomWeight] member type",
        "[RandomWeight] cannot be applied to '{0}' because its type '{1}' is not a supported weight type; use sbyte, byte, short, ushort, int, uint or long for the full set of generated extensions, or float or double for single draws{2}",
        "The generated extensions delegate to the selector-based runtime overloads, which take either a 'Func<T, long>' or a 'Func<T, double>' weight selector. A member whose type does not implicitly convert to one of those two -- including 'ulong' (converting it to 'long' can overflow), 'decimal' (no weighted-picking overload accepts it), an enum, a nullable numeric, or any non-numeric type -- has nothing to delegate to.");

    /// <summary>
    /// SSALR002: a type declares more than one <c>[RandomWeight]</c> member. Reported once per
    /// decorated member of that type, so every offending declaration is highlighted.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateWeightMember = Factory.Error(
        2,
        "A type can declare only one [RandomWeight] member",
        "'{0}' declares more than one [RandomWeight] member ({1}); exactly one weight member per type is supported",
        "The generated extension class exposes one selector-less overload set per type, built from a single weight member. Combining or choosing between several weight members is out of scope by design; remove the attribute from all but one member, or move the extra weights onto their own types.");

    /// <summary>
    /// SSALR003: the decorated member cannot be read off an instance. Message argument 1 names the
    /// reason (<c>static</c>, a write-only property, or an indexer).
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidWeightMemberKind = Factory.Error(
        3,
        "[RandomWeight] member must be a readable instance member",
        "[RandomWeight] cannot be applied to '{0}' because it is {1}; the weight member must be a readable instance property or field",
        "The generated weight selector is a 'static x => (long)x.Member' lambda evaluated against each item, so the member must be readable on an instance of the declaring type. A static member is not per-item, a write-only property cannot be read, and an indexer has no argument-free form to read.");

    /// <summary>
    /// SSALR004: the decorated member or its declaring type is not reachable from the generated
    /// extension class, which is a separate top-level type in the same assembly.
    /// </summary>
    public static readonly DiagnosticDescriptor InaccessibleWeightMember = Factory.Error(
        4,
        "[RandomWeight] member must be accessible to generated code",
        "[RandomWeight] cannot be applied to '{0}' because {1} is not accessible from the generated extension class; the member, its declaring type, and any containing types must be at least 'internal' and not file-local",
        "The generated extension class is a top-level static class emitted into a separate file in the same assembly and in the same namespace as the declaring type. It is neither nested inside that type nor derived from it, so a 'private', 'protected', or 'private protected' member or nested type cannot be referenced from it, and neither can a file-local type.");

    /// <summary>
    /// SSALR005: the declaring type is generic, or is nested inside a generic type. Mirrors the
    /// intent of SsalKit.DependencyInjection's SSAL003.
    /// </summary>
    public static readonly DiagnosticDescriptor GenericTypeNotSupported = Factory.Error(
        5,
        "[RandomWeight] cannot be applied to a member of a generic type",
        "[RandomWeight] cannot be applied to '{0}' because its declaring type is generic or is nested inside a generic type",
        "The generated extensions are non-generic methods on a non-generic static class whose receiver is 'IReadOnlyList<TheDeclaringType>'. An open generic declaring type has no single closed form to write there, and adding the type parameters to the generated methods would defeat the point of the attribute (the call site would have to state them). Use a concrete type, or call the selector-based runtime overloads directly.");

    /// <summary>
    /// SSALR006: the declaring type is a <c>ref struct</c>, which cannot be used as the generic
    /// type argument the runtime APIs require.
    /// </summary>
    public static readonly DiagnosticDescriptor RefStructNotSupported = Factory.Error(
        6,
        "[RandomWeight] cannot be applied to a member of a ref struct",
        "[RandomWeight] cannot be applied to '{0}' because its declaring type is a ref struct",
        "The generated extensions take an 'IReadOnlyList<T>' and delegate to generic runtime APIs, so the declaring type has to be usable as an ordinary generic type argument. A ref struct cannot be one: it can never be stored on the heap, which is exactly what a collection of it would require.");
}
