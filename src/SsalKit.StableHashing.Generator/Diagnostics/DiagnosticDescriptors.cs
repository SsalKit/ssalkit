using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;

namespace SsalKit.StableHashing.Generator.Diagnostics;

/// <summary>
/// The <c>SSALH</c> diagnostic table reported by <see cref="StableHashGenerator"/>.
/// </summary>
/// <remarks>
/// Every rule here that is an <see cref="DiagnosticSeverity.Error"/> describes a
/// <c>[StableHashContract]</c>/<c>[StableHashMember]</c> application the generator cannot honour.
/// When a contract type has any <see cref="DiagnosticSeverity.Error"/> diagnostic, no extension
/// class is generated for it at all -- there is no partial generation. A
/// <see cref="DiagnosticSeverity.Warning"/> never blocks generation.
/// </remarks>
internal static class DiagnosticDescriptors
{
    private static readonly DiagnosticDescriptorFactory Factory = new("SSALH", "SsalKit.StableHashing");

    /// <summary>
    /// SSALH001: two or more members of the same contract declare the same
    /// <see cref="StableHashMemberAttribute.Id"/>.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateMemberId = Factory.Error(
        1,
        "Duplicate [StableHashMember] id",
        "'{0}' declares more than one [StableHashMember] with id {1} ({2}); every member id must be unique within a contract",
        "The encoded stream carries a member id immediately before that member's value, so two members sharing an id would be indistinguishable in the encoding. Give each member its own id.");

    /// <summary>
    /// SSALH002: the decorated member's type is not one v1 of the encoding contract supports.
    /// Covers <c>Dictionary</c>/<c>HashSet</c>/arbitrary unordered <c>IEnumerable&lt;T&gt;</c>,
    /// <c>object</c>, delegates, pointers, interfaces, and abstract types.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedMemberType = Factory.Error(
        2,
        "Unsupported [StableHashMember] member type",
        "[StableHashMember] cannot be applied to '{0}' because its type '{1}' is not supported",
        "Supported member types are: the built-in scalar types (bool, the integer types, Int128/UInt128, char, float, double, decimal, string), Guid, DateOnly, TimeOnly, TimeSpan, DateTimeOffset, enums, T[]/List<T>/IReadOnlyList<T>/ImmutableArray<T> of a supported element type, Nullable<T>/nullable reference wrappers of a supported type, and other [StableHashContract] types. Dictionary, HashSet, and other unordered or arbitrary IEnumerable<T> types are rejected because their enumeration order is not guaranteed to be stable; object, delegates, pointers, interfaces, and abstract types are rejected because the generator cannot know the runtime type to encode.");

    /// <summary>
    /// SSALH003: the decorated member is a <see cref="System.DateTime"/>, whose <c>Kind</c> makes
    /// it ambiguous for a portable encoding.
    /// </summary>
    public static readonly DiagnosticDescriptor DateTimeNotSupported = Factory.Error(
        3,
        "DateTime is not supported",
        "[StableHashMember] cannot be applied to '{0}' because DateTime is not supported; use DateTimeOffset (for an instant) or DateOnly (for a calendar date) instead",
        "DateTime.Kind (Utc/Local/Unspecified) is not encoded, so two DateTime values that print identically can compare unequal, and a Local value's meaning depends on the machine's time zone -- neither is safe for a portable, permanent encoding. DateTimeOffset resolves this by encoding UtcTicks alone; DateOnly encodes a calendar date with no time-zone concept.");

    /// <summary>
    /// SSALH004: the decorated member's type is a user-defined type with no
    /// <c>[StableHashContract]</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor MemberTypeHasNoContract = Factory.Error(
        4,
        "Member type has no [StableHashContract]",
        "[StableHashMember] cannot be applied to '{0}' because its type '{1}' has no [StableHashContract]",
        "A member whose type is itself a contract is encoded by recursively encoding that contract (its own header included), which requires the type to declare [StableHashContract]. Add the attribute to '{1}', or remove [StableHashMember] from this member.");

    /// <summary>
    /// SSALH005: following <c>[StableHashContract]</c> member types from this type eventually
    /// reaches this type again.
    /// </summary>
    public static readonly DiagnosticDescriptor CircularContractGraph = Factory.Error(
        5,
        "Circular [StableHashContract] graph",
        "'{0}' cannot be a [StableHashContract]: following its members' contract types eventually reaches '{0}' again",
        "Encoding a contract recursively encodes every nested contract's own header and members. A cycle in that graph would recurse forever, so it is rejected at compile time instead. Break the cycle, for example by removing [StableHashMember] from one of the members on the cycle.");

    /// <summary>
    /// SSALH006: a <see langword="class"/> contract is not <see langword="sealed"/> (or is
    /// <see langword="static"/>, which the "this T value" extension parameter cannot accept).
    /// </summary>
    public static readonly DiagnosticDescriptor ClassContractNotSealed = Factory.Error(
        6,
        "class [StableHashContract] must be sealed",
        "'{0}' must be sealed to be a [StableHashContract]",
        "An instance of a derived class encoded through its base contract would silently drop the derived type's own state from the hash. Requiring the class to be sealed makes that impossible. struct and record struct contracts do not need this rule: they have no derived-instance case.");

    /// <summary>
    /// SSALH007: the decorated member cannot be read from the generated extension class (it is
    /// static, an indexer, write-only, or otherwise not accessible), or the contract type itself
    /// (or a type it is nested inside) is not accessible from generated code.
    /// </summary>
    public static readonly DiagnosticDescriptor MemberNotAccessibleToGeneratedCode = Factory.Error(
        7,
        "Not accessible to generated code",
        "Cannot generate stable-hash code for '{0}' because {1} is not accessible to the generated extension class",
        "The generated extension class is a top-level static class emitted into a separate file, in the same assembly and namespace as the contract type. It is neither nested inside the contract type nor derived from it, so a 'private', 'protected', or 'private protected' member (or a static member, an indexer, or a write-only property) cannot be read from it, and neither can a member of a contract type that is itself inaccessible (private/protected/file-local, or nested inside such a type) from a separate top-level class.");

    /// <summary>
    /// SSALH008: a <c>[StableHashMember(id)]</c> id is less than 1.
    /// </summary>
    public static readonly DiagnosticDescriptor MemberIdOutOfRange = Factory.Error(
        8,
        "[StableHashMember] id must be 1 or greater",
        "[StableHashMember] cannot be applied to '{0}' because its id {1} is less than 1",
        "Member ids must be positive so that the encoded, little-endian id bytes are exactly what StableHashWriter.AppendMemberId documents; there is no reserved meaning for 0 or negative ids to justify the exception.");

    /// <summary>
    /// SSALH009: a <c>[StableHashContract]</c> name is null/whitespace, or its Version is less
    /// than 1.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidContractNameOrVersion = Factory.Error(
        9,
        "[StableHashContract] name must not be null/whitespace and Version must be 1 or greater",
        "[StableHashContract] cannot be applied to '{0}' because {1}",
        "The contract name and version are encoded into the header of every hash produced for this type (see StableHashWriter.AppendContractHeader), so both must be well-formed: the name must carry actual content, and the version must be a positive integer.");

    /// <summary>
    /// SSALH010: a contract declares zero <c>[StableHashMember]</c> members.
    /// </summary>
    public static readonly DiagnosticDescriptor ContractHasNoMembers = Factory.Warning(
        10,
        "[StableHashContract] declares no [StableHashMember]",
        "'{0}' is a [StableHashContract] but declares no [StableHashMember]; every instance will hash to the same value (the header alone)",
        "[StableHashMember] is opt-in by design, so a contract with none is not an error -- but it is unusual enough (every instance producing an identical hash) to be worth flagging, in case a member was meant to be included and the attribute was simply forgotten.");

    /// <summary>
    /// SSALH011: two or more <c>[StableHashContract]</c> types in the compilation declare the
    /// same <see cref="StableHashContractAttribute.Name"/>.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateContractName = Factory.Warning(
        11,
        "Duplicate [StableHashContract] name",
        "'{0}' declares [StableHashContract] name \"{1}\", which is also declared by {2}",
        "The contract name is encoded into every hash this type produces and is meant to identify the contract independently of the CLR type name. Two different types sharing one name means a hash consumer cannot tell which type produced a given hash from the name alone; this is a warning rather than an error because it may be intentional (e.g. a deliberate migration alias).");

    /// <summary>
    /// SSALH012: <c>[StableHashMember]</c> is applied to a member whose declaring type has no
    /// <c>[StableHashContract]</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor OrphanMemberAttribute = Factory.Warning(
        12,
        "[StableHashMember] on a type with no [StableHashContract]",
        "'{0}' has [StableHashMember] but its declaring type '{1}' has no [StableHashContract]; this member is not part of any contract and nothing will be generated for it",
        "[StableHashMember] only has an effect on a member of a type that also declares [StableHashContract]. This is a warning rather than an error because a type may still be mid-migration (contract added later) or the attribute may have been left over after [StableHashContract] was removed.");

    /// <summary>
    /// SSALH013: <c>[StableHashContract]</c> is applied to a generic type, or one nested inside
    /// a generic type.
    /// </summary>
    public static readonly DiagnosticDescriptor GenericContractNotSupported = Factory.Error(
        13,
        "[StableHashContract] cannot be applied to a generic type",
        "[StableHashContract] cannot be applied to '{0}' because it is generic, or is nested inside a generic type",
        "The generated extension methods are non-generic, with a receiver of the contract's own closed type. An open generic contract type has no single closed form to write there. Use a concrete (closed) type, or hand-write encoding logic against StableHashWriter directly.");
}
