namespace SsalKit.StableHashing.Generator.Models;

/// <summary>
/// Which shape of the v1 encoding table (design §4.4) a member's (or a collection's element's)
/// type resolved to.
/// </summary>
internal enum TypeShapeKind
{
    /// <summary>
    /// A directly-supported scalar: a built-in numeric/bool/char type, <c>string</c>,
    /// <see cref="System.Guid"/>, <see cref="System.DateOnly"/>, <see cref="System.TimeOnly"/>,
    /// <see cref="System.TimeSpan"/>, or <see cref="System.DateTimeOffset"/>. Encoded by calling
    /// the matching <c>StableHashWriter.Append*</c> method directly.
    /// </summary>
    Primitive,

    /// <summary>
    /// An <see langword="enum"/>. Encoded by casting to its underlying integral type and calling
    /// the matching <c>StableHashWriter.Append*</c> method.
    /// </summary>
    Enum,

    /// <summary>
    /// Another <c>[StableHashContract]</c> type. Encoded by calling that type's own generated
    /// <c>AppendStableHash</c> extension, which recursively encodes its full header and members.
    /// </summary>
    Contract,

    /// <summary>
    /// <c>System.Nullable&lt;T&gt;</c> wrapping a supported value-type shape. Encoded as a null
    /// marker followed by <see cref="Inner"/>'s encoding when a value is present.
    /// </summary>
    NullableValue,

    /// <summary>
    /// A nullable-annotated reference type (<c>string?</c>, or a nullable-annotated
    /// <c>[StableHashContract]</c> class) wrapping a supported reference-type shape. Encoded the
    /// same way as <see cref="NullableValue"/>.
    /// </summary>
    NullableReference,

    /// <summary>
    /// <c>T[]</c>, <c>List&lt;T&gt;</c>, <c>IReadOnlyList&lt;T&gt;</c>, or
    /// <c>ImmutableArray&lt;T&gt;</c> of a supported element shape. Encoded as an element count
    /// followed by each element's encoding, in index order.
    /// </summary>
    Collection,
}

/// <summary>
/// Which of the four supported collection forms a <see cref="TypeShapeKind.Collection"/> shape
/// is, since each is read slightly differently in generated code (see
/// <see cref="Emission.StableHashEmitter"/>).
/// </summary>
internal enum CollectionForm
{
    /// <summary>A single-dimensional array (<c>T[]</c>): read via <c>.Length</c> and the indexer.</summary>
    Array,

    /// <summary><c>System.Collections.Generic.List&lt;T&gt;</c>: read via <c>.Count</c> and the indexer.</summary>
    List,

    /// <summary><c>System.Collections.Generic.IReadOnlyList&lt;T&gt;</c>: read via <c>.Count</c> and the indexer.</summary>
    ReadOnlyList,

    /// <summary>
    /// <c>System.Collections.Immutable.ImmutableArray&lt;T&gt;</c>: read via <c>.Length</c> and
    /// the indexer, guarded by <c>.IsDefault</c> (an uninitialized default instance is treated as
    /// a zero-element collection -- design §4.2).
    /// </summary>
    ImmutableArray,
}

/// <summary>
/// A member's (or a collection element's) type, reduced to exactly what
/// <see cref="Emission.StableHashEmitter"/> needs to write the code that encodes a value of it --
/// primitives, enums, and strings only, so the incremental pipeline can compare two runs' models
/// by value (see the toolkit's pipeline-model contract).
/// </summary>
/// <param name="Kind">Which case this shape is.</param>
/// <param name="AppendMethodSuffix">
/// <see cref="TypeShapeKind.Primitive"/>/<see cref="TypeShapeKind.Enum"/>: the suffix of the
/// <c>StableHashWriter.Append*</c> method to call, e.g. <c>"Int32"</c> for
/// <c>AppendInt32</c>.
/// </param>
/// <param name="EnumUnderlyingTypeKeyword">
/// <see cref="TypeShapeKind.Enum"/> only: the C# keyword of the enum's underlying integral type
/// (e.g. <c>"int"</c>), used to write the cast the generated code applies before appending.
/// </param>
/// <param name="ContractExtensionsFqn">
/// <see cref="TypeShapeKind.Contract"/> only: the <c>global::</c>-qualified name of the
/// referenced contract type's own generated extension class, e.g.
/// <c>global::Game.PlayerSnapshotStableHashing</c>.
/// </param>
/// <param name="Inner">
/// <see cref="TypeShapeKind.NullableValue"/>/<see cref="TypeShapeKind.NullableReference"/> only:
/// the wrapped, non-nullable shape.
/// </param>
/// <param name="Form"><see cref="TypeShapeKind.Collection"/> only: which collection form this is.</param>
/// <param name="Element"><see cref="TypeShapeKind.Collection"/> only: the element shape.</param>
internal sealed record TypeShape(
    TypeShapeKind Kind,
    string? AppendMethodSuffix,
    string? EnumUnderlyingTypeKeyword,
    string? ContractExtensionsFqn,
    TypeShape? Inner,
    CollectionForm? Form,
    TypeShape? Element);
