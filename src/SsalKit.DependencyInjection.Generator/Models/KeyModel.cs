namespace SsalKit.DependencyInjection.Generator.Models;

/// <summary>
/// An equatable, compilation-independent representation of a <c>[Service]</c> attribute's
/// <c>Key</c> argument. <see cref="Expression"/> holds a self-contained C# expression (already
/// fully qualified) that reproduces the constant value, ready to be embedded verbatim into
/// generated source.
/// </summary>
internal readonly record struct KeyModel(bool HasKey, string? Expression)
{
    public static KeyModel None { get; } = new(false, null);
}
