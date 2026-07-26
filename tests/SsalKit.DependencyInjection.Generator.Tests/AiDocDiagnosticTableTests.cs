using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Diagnostics;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Keeps the Diagnostics table of <c>src/SsalKit.DependencyInjection/AI.md</c> in agreement with the
/// live descriptor table.
/// </summary>
/// <remarks>
/// <c>AI.md</c> is written to be loaded into an AI agent's context and treated as a contract sheet,
/// so a diagnostic that exists in code but not in the table (or the other way round) is worse than a
/// stale sentence in prose: the agent will confidently reason from a rule list that is wrong. This
/// is the same approach <see cref="AnalyzerReleaseTrackingTests"/> takes to the release-tracking
/// files, and it resolves the document through <see cref="CallerFilePathAttribute"/> for the same
/// reason -- the file is repository content, not a build output, so it is not next to the test
/// assembly.
/// </remarks>
public class AiDocDiagnosticTableTests
{
    /// <summary>
    /// Matches a diagnostics-table row: a markdown row whose first cell is nothing but a
    /// backtick-quoted id. Rows that merely mention several ids in one cell (the grouping tables
    /// elsewhere in the document) do not match, because the closing backtick must be followed by the
    /// cell separator.
    /// </summary>
    private static readonly Regex TableRow = new(
        @"^\|\s*`(?<id>SSAL\d{3})`\s*\|",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly string[] DeclaredIds = typeof(DiagnosticDescriptors)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(DiagnosticDescriptor))
        .Select(field => ((DiagnosticDescriptor)field.GetValue(null)!).Id)
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToArray();

    [Fact]
    public void AiDoc_DocumentsEveryDescriptorExactlyOnce()
    {
        var documented = ReadDocumentedIds();

        Assert.Equal(documented.Length, documented.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(DeclaredIds, documented);
    }

    private static string[] ReadDocumentedIds() =>
        TableRow.Matches(File.ReadAllText(AiDocPath()))
            .Select(match => match.Groups["id"].Value)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

    private static string AiDocPath([CallerFilePath] string testFilePath = "") =>
        Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(testFilePath)!,
                "..",
                "..",
                "src",
                "SsalKit.DependencyInjection",
                "AI.md"));
}
