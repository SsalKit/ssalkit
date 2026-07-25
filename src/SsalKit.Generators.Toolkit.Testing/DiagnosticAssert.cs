using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SsalKit.Generators.Toolkit.Testing;

/// <summary>
/// Asserts on the diagnostics a generator or analyzer reported: which one, how severe, and where it
/// landed.
/// </summary>
/// <remarks>
/// The location assertions take a snippet of the source rather than a line and column, so a test
/// says "the squiggle belongs on this attribute" and stays readable and stable when the test source
/// above it is edited.
/// </remarks>
public static class DiagnosticAssert
{
    /// <summary>
    /// Asserts that exactly one diagnostic with the given id was reported, optionally checking its
    /// severity and where it landed, and returns it for further assertions on its message.
    /// </summary>
    /// <param name="diagnostics">The reported diagnostics.</param>
    /// <param name="id">The diagnostic id that must appear exactly once.</param>
    /// <param name="severity">When given, the severity the diagnostic must have. Checking this is
    /// what stops an id from silently being downgraded from an error to a warning.</param>
    /// <param name="locatedOnSnippet">When given, a substring of the source that must occur exactly
    /// once and must span the reported location. See <see cref="LocatedOn"/>.</param>
    /// <param name="source">The source text the diagnostic was reported against, needed for
    /// <paramref name="locatedOnSnippet"/> when the diagnostic's location does not carry its syntax
    /// tree. See <see cref="LocatedOn"/>.</param>
    /// <param name="exclusive">When <c>true</c>, also asserts that this was the <em>only</em>
    /// diagnostic reported, so a second, unexpected diagnostic cannot slip through unnoticed
    /// alongside the expected one. Leave it <c>false</c> when the test source deliberately triggers
    /// more than one thing and this assertion only speaks for its own id.</param>
    /// <returns>The single matching diagnostic.</returns>
    /// <exception cref="GeneratorAssertionException">No diagnostic, or more than one, has that id;
    /// something else was reported too while <paramref name="exclusive"/> was set; or the severity
    /// or location did not match.</exception>
    public static Diagnostic Single(
        ImmutableArray<Diagnostic> diagnostics,
        string id,
        DiagnosticSeverity? severity = null,
        string? locatedOnSnippet = null,
        string? source = null,
        bool exclusive = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var matches = diagnostics
            .Where(diagnostic => string.Equals(diagnostic.Id, id, StringComparison.Ordinal))
            .ToImmutableArray();

        if (matches.Length != 1)
        {
            throw new GeneratorAssertionException(
                $"Expected exactly one '{id}' diagnostic, but found {matches.Length}.{Describe(diagnostics)}");
        }

        if (exclusive && diagnostics.Length != 1)
        {
            throw new GeneratorAssertionException(
                $"Expected '{id}' to be the only diagnostic reported, but {diagnostics.Length} were " +
                $"reported.{Describe(diagnostics)}");
        }

        var match = matches[0];

        if (severity is not null && match.Severity != severity)
        {
            throw new GeneratorAssertionException(
                $"Expected '{id}' to be reported as {severity}, but it was reported as {match.Severity}." +
                $"{Describe(matches)}");
        }

        if (locatedOnSnippet is not null)
        {
            LocatedOn(match, locatedOnSnippet, source);
        }

        return match;
    }

    /// <summary>
    /// Asserts that nothing whose id starts with <paramref name="idPrefix"/> was reported.
    /// </summary>
    /// <param name="diagnostics">The reported diagnostics.</param>
    /// <param name="idPrefix">The id prefix that must not appear -- typically a whole package's
    /// diagnostic prefix, so the assertion also catches a diagnostic nobody thought to name.</param>
    /// <exception cref="GeneratorAssertionException">At least one diagnostic has that prefix; the
    /// message lists them.</exception>
    public static void None(ImmutableArray<Diagnostic> diagnostics, string idPrefix)
    {
        ArgumentException.ThrowIfNullOrEmpty(idPrefix);

        var matches = diagnostics
            .Where(diagnostic => diagnostic.Id.StartsWith(idPrefix, StringComparison.Ordinal))
            .ToImmutableArray();

        if (!matches.IsEmpty)
        {
            throw new GeneratorAssertionException(
                $"Expected no diagnostic whose id starts with '{idPrefix}', but found " +
                $"{matches.Length}.{Describe(matches)}");
        }
    }

    /// <summary>
    /// Asserts that a diagnostic was reported on a given snippet of the source.
    /// </summary>
    /// <param name="diagnostic">The diagnostic whose location is checked.</param>
    /// <param name="snippet">A substring of the source that must occur exactly once in it, and
    /// whose span must contain the diagnostic's span. Requiring uniqueness is what makes the
    /// snippet an unambiguous way to name a position without hard-coding a line and column.</param>
    /// <param name="source">The source the diagnostic was reported against. Optional when the
    /// diagnostic's location carries its syntax tree, which is the case for analyzer diagnostics but
    /// not for generator diagnostics rebuilt from a cache-safe location record -- those keep only a
    /// file path and a span, so the source has to be supplied.</param>
    /// <exception cref="GeneratorAssertionException">The diagnostic has no source location; the
    /// source could not be determined; the snippet is missing or not unique; or the diagnostic
    /// landed outside it.</exception>
    public static void LocatedOn(Diagnostic diagnostic, string snippet, string? source = null)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentException.ThrowIfNullOrEmpty(snippet);

        if (diagnostic.Location.Kind == LocationKind.None)
        {
            throw new GeneratorAssertionException(
                $"Expected '{diagnostic.Id}' to be reported on '{snippet}', but it was reported without a source " +
                "location at all.");
        }

        var text = source ?? diagnostic.Location.SourceTree?.ToString();

        if (text is null)
        {
            throw new GeneratorAssertionException(
                $"Cannot check where '{diagnostic.Id}' was reported: its location does not carry a syntax tree " +
                $"(it is a {diagnostic.Location.Kind} location), so the source text has to be passed in " +
                $"explicitly via the '{nameof(source)}' parameter.");
        }

        var start = text.IndexOf(snippet, StringComparison.Ordinal);

        if (start < 0)
        {
            throw new GeneratorAssertionException(
                $"The snippet '{snippet}' does not occur in the source, so it cannot name where '{diagnostic.Id}' " +
                $"is expected. The diagnostic was reported at {diagnostic.Location.GetLineSpan()}.");
        }

        var duplicate = text.IndexOf(snippet, start + 1, StringComparison.Ordinal);

        if (duplicate >= 0)
        {
            throw new GeneratorAssertionException(
                $"The snippet '{snippet}' occurs more than once in the source (at offsets {start} and " +
                $"{duplicate}), so it cannot name where '{diagnostic.Id}' is expected. Extend it until it is " +
                "unique.");
        }

        var expected = new TextSpan(start, snippet.Length);
        var actual = diagnostic.Location.SourceSpan;

        if (!expected.Contains(actual))
        {
            throw new GeneratorAssertionException(
                $"Expected '{diagnostic.Id}' to be reported on '{snippet}' (offsets {expected.Start}..{expected.End}), " +
                $"but it was reported at offsets {actual.Start}..{actual.End} ({diagnostic.Location.GetLineSpan()}), " +
                $"which covers '{Excerpt(text, actual)}'.");
        }
    }

    private static string Excerpt(string text, TextSpan span)
    {
        if (span.End > text.Length)
        {
            return "(a span outside this source text)";
        }

        return text.Substring(span.Start, span.Length);
    }

    private static string Describe(ImmutableArray<Diagnostic> diagnostics)
    {
        if (diagnostics.IsEmpty)
        {
            return Environment.NewLine + "Reported diagnostics: (none)";
        }

        return Environment.NewLine + "Reported diagnostics:" + Environment.NewLine +
            string.Join(
                Environment.NewLine,
                diagnostics.Select(static diagnostic =>
                    $"  - {diagnostic.Id} ({diagnostic.Severity}) at {diagnostic.Location.GetLineSpan()}: " +
                    diagnostic.GetMessage(CultureInfo.InvariantCulture)));
    }
}
