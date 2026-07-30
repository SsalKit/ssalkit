using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using SsalKit.StableHashing.Generator.Diagnostics;

namespace SsalKit.StableHashing.Generator.Tests;

/// <summary>
/// Keeps <c>AnalyzerReleases.Shipped.md</c>/<c>AnalyzerReleases.Unshipped.md</c> in agreement with
/// the live <see cref="DiagnosticDescriptors"/> table.
/// </summary>
/// <remarks>
/// <c>SSALH</c> ids are composed at run time by <c>DiagnosticDescriptorFactory</c>
/// (<c>{idPrefix}{id:D3}</c>), so RS2002/RS2003 cannot validate the release-tracking files against
/// them (see the toolkit's AI.md, "DO NOT expect Microsoft.CodeAnalysis.Analyzers' release-tracking
/// rules ... to resolve ids built by DiagnosticDescriptorFactory"). This test replaces that check:
/// it compares every declared descriptor's <c>(id, category, severity)</c> against the row the
/// release files carry for it, by reflection, resolved through <see cref="CallerFilePathAttribute"/>
/// so the files are read as source rather than a build output.
/// </remarks>
public class AnalyzerReleasesSyncTests
{
    private static readonly Regex UnshippedRow = new(
        @"^(?<id>SSALH\d{3})\s*\|\s*(?<category>[^|]+?)\s*\|\s*(?<severity>Error|Warning)\s*\|",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly DiagnosticDescriptor[] DeclaredDescriptors = typeof(DiagnosticDescriptors)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(DiagnosticDescriptor))
        .Select(field => (DiagnosticDescriptor)field.GetValue(null)!)
        .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
        .ToArray();

    [Fact]
    public void EveryDescriptor_IsRegisteredInUnshippedFile_WithMatchingCategoryAndSeverity()
    {
        var rows = ReadUnshippedRows();

        foreach (var descriptor in DeclaredDescriptors)
        {
            Assert.True(rows.ContainsKey(descriptor.Id), $"{descriptor.Id} has no row in AnalyzerReleases.Unshipped.md.");

            var (category, severity) = rows[descriptor.Id];
            Assert.Equal(descriptor.Category, category);
            Assert.Equal(descriptor.DefaultSeverity.ToString(), severity);
        }
    }

    [Fact]
    public void UnshippedFile_DeclaresNoIdOutsideTheDescriptorTable()
    {
        var declaredIds = DeclaredDescriptors.Select(descriptor => descriptor.Id).ToHashSet(StringComparer.Ordinal);
        var rows = ReadUnshippedRows();

        foreach (var id in rows.Keys)
        {
            Assert.Contains(id, declaredIds);
        }
    }

    [Fact]
    public void ShippedFile_DeclaresNoRuleYet()
    {
        // Every SSALH rule is still pending its first release; once one ships, this test's
        // assumption (and its assertion) should be updated together with the file.
        var shippedText = File.ReadAllText(ShippedPath());
        Assert.DoesNotMatch(@"^SSALH\d{3}\s*\|", shippedText);
    }

    [Fact]
    public void DeclaredIds_AreContiguousFrom001()
    {
        var expected = Enumerable.Range(1, DeclaredDescriptors.Length).Select(n => "SSALH" + n.ToString("D3"));
        Assert.Equal(expected, DeclaredDescriptors.Select(descriptor => descriptor.Id));
    }

    private static Dictionary<string, (string Category, string Severity)> ReadUnshippedRows()
    {
        var text = File.ReadAllText(UnshippedPath());
        var result = new Dictionary<string, (string, string)>(StringComparer.Ordinal);

        foreach (Match match in UnshippedRow.Matches(text))
        {
            result[match.Groups["id"].Value] = (match.Groups["category"].Value.Trim(), match.Groups["severity"].Value.Trim());
        }

        return result;
    }

    private static string UnshippedPath([CallerFilePath] string testFilePath = "") =>
        Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(testFilePath)!, "..", "..", "src", "SsalKit.StableHashing.Generator", "AnalyzerReleases.Unshipped.md"));

    private static string ShippedPath([CallerFilePath] string testFilePath = "") =>
        Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(testFilePath)!, "..", "..", "src", "SsalKit.StableHashing.Generator", "AnalyzerReleases.Shipped.md"));
}
