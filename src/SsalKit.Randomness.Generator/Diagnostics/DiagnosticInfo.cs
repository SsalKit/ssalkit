using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SsalKit.Generators.Toolkit;

namespace SsalKit.Randomness.Generator.Diagnostics;

/// <summary>
/// An equatable, compilation-independent stand-in for a <see cref="Diagnostic"/>, so a diagnostic
/// can travel through the incremental pipeline without pinning a <see cref="SyntaxTree"/> (and,
/// through it, an entire <see cref="Compilation"/>) in the generator's cache.
/// </summary>
/// <param name="Descriptor">
/// The rule that fired. Descriptors are shared static singletons and compare by value, so they are
/// safe to hold here.
/// </param>
/// <param name="Location">Where to report, or <see langword="null"/> to report with no location.</param>
/// <param name="MessageArgs">The arguments spliced into the descriptor's message format.</param>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> MessageArgs)
{
    public DiagnosticInfo(DiagnosticDescriptor descriptor, LocationInfo? location, params string[] messageArgs)
        : this(descriptor, location, EquatableArray.Create(System.Collections.Immutable.ImmutableArray.Create(messageArgs)))
    {
    }

    /// <summary>
    /// Rehydrates a reportable <see cref="Diagnostic"/>. Called only at the source-output stage,
    /// never while a value is sitting in the pipeline cache.
    /// </summary>
    public Diagnostic ToDiagnostic()
    {
        var args = new object?[MessageArgs.Length];
        for (var i = 0; i < MessageArgs.Length; i++)
        {
            args[i] = MessageArgs[i];
        }

        return Diagnostic.Create(Descriptor, Location?.ToLocation(), args);
    }
}

/// <summary>
/// The value-equatable projection of a <see cref="Location"/> in source: everything
/// <see cref="Location.Create(string, TextSpan, LinePositionSpan)"/> needs, and nothing that
/// references a syntax tree.
/// </summary>
internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    /// <summary>
    /// Projects <paramref name="location"/>, or returns <see langword="null"/> when it is not a
    /// location in source (e.g. a metadata or "none" location), which
    /// <see cref="Diagnostic.Create(DiagnosticDescriptor, Location?, object?[])"/> accepts as-is.
    /// </summary>
    public static LocationInfo? CreateFrom(Location? location) =>
        location?.SourceTree is null
            ? null
            : new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);

    public static LocationInfo? CreateFrom(SyntaxNode? node) => node is null ? null : CreateFrom(node.GetLocation());
}
