using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SsalKit.Generators.Toolkit.Testing.Tests.Harness;

/// <summary>
/// A cache-safe stand-in for <see cref="Location"/>: a real one pins the syntax tree it came from,
/// which would make every model carrying it reference-unequal across runs and defeat the caching
/// the harness is supposed to be able to observe.
/// </summary>
/// <remarks>
/// Rebuilding a location from a file path and spans produces an <c>ExternalFile</c> location whose
/// <see cref="Location.SourceTree"/> is <c>null</c> -- exactly the shape real generators report, and
/// the reason <see cref="DiagnosticAssert.LocatedOn"/> needs the source passed in.
/// </remarks>
public sealed record MiniLocation(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
{
    public static MiniLocation From(SyntaxReference syntaxReference)
    {
        var location = Location.Create(syntaxReference.SyntaxTree, syntaxReference.Span);

        return new MiniLocation(location.SourceTree!.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }

    public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);
}
