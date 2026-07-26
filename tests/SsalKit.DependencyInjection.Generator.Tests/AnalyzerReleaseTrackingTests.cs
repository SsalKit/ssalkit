using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SsalKit.DependencyInjection.Generator.Analysis;
using SsalKit.DependencyInjection.Generator.Diagnostics;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Keeps <c>AnalyzerReleases.Shipped.md</c> and <c>AnalyzerReleases.Unshipped.md</c> in agreement
/// with the descriptor table and with what the analyzers actually declare as supported.
/// </summary>
/// <remarks>
/// These are the checks Microsoft.CodeAnalysis.Analyzers' release-tracking rules RS2000-RS2003
/// used to perform at build time. They stopped working when the descriptor table moved to
/// SsalKit.Generators.Toolkit's <c>DiagnosticDescriptorFactory</c>: that analyzer resolves a
/// diagnostic id only when it is a compile-time constant at the <see cref="DiagnosticDescriptor"/>
/// construction site, and the factory composes the id from a prefix and a number at run time, so
/// none of the SSAL ids is statically visible to it any more (hence the RS2002/RS2003 suppression
/// in the generator project). Doing the same checks from a test is not a downgrade: it reads the
/// same two files, and additionally verifies that the recorded category and severity match the
/// live descriptor, which the analyzer only did for rules it could resolve.
/// </remarks>
public class AnalyzerReleaseTrackingTests
{
    /// <summary>
    /// Matches a rule row of a release-tracking table: <c>Rule ID | Category | Severity | Notes</c>.
    /// The header row and its <c>---|---</c> separator are excluded by requiring an SSAL id.
    /// </summary>
    private static readonly Regex RuleRow = new(
        @"^(?<id>SSAL\d{3})\s*\|\s*(?<category>[^|]+?)\s*\|\s*(?<severity>[^|]+?)\s*\|",
        RegexOptions.Compiled);

    private static readonly DiagnosticDescriptor[] Descriptors = typeof(DiagnosticDescriptors)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(DiagnosticDescriptor))
        .Select(field => (DiagnosticDescriptor)field.GetValue(null)!)
        .ToArray();

    [Fact]
    public void ReleaseFiles_RecordEveryDescriptorExactlyOnce()
    {
        var recorded = ReadReleasedRules().Select(rule => rule.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var declared = Descriptors.Select(descriptor => descriptor.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();

        Assert.Equal(declared, recorded);
    }

    [Fact]
    public void ReleaseFiles_AgreeWithEachDescriptorsCategoryAndSeverity()
    {
        var byId = Descriptors.ToDictionary(descriptor => descriptor.Id, StringComparer.Ordinal);

        foreach (var (id, category, severity) in ReadReleasedRules())
        {
            var descriptor = Assert.Contains(id, byId);

            Assert.Equal(descriptor.Category, category);
            Assert.Equal(descriptor.DefaultSeverity.ToString(), severity);
        }
    }

    /// <summary>
    /// The check behind RS2003: a rule that is recorded as released but that no analyzer reports is
    /// dead documentation, and one an analyzer reports without a release entry is an undocumented
    /// rule.
    /// </summary>
    [Fact]
    public void EveryDescriptor_IsSupportedByExactlyOneAnalyzer()
    {
        var supported = new[]
            {
                (DiagnosticAnalyzer)new ServiceAttributeAnalyzer(),
                new ServiceFactoryAnalyzer(),
                new RegisterImplementationsOfAnalyzer(),
            }
            .SelectMany(analyzer => analyzer.SupportedDiagnostics)
            .ToArray();

        Assert.Equal(supported.Length, supported.Select(descriptor => descriptor.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            Descriptors.Select(descriptor => descriptor.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            supported.Select(descriptor => descriptor.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray());
    }

    private static IEnumerable<(string Id, string Category, string Severity)> ReadReleasedRules()
    {
        foreach (var path in ReleaseFilePaths())
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var match = RuleRow.Match(line);
                if (match.Success)
                {
                    yield return (match.Groups["id"].Value, match.Groups["category"].Value, match.Groups["severity"].Value);
                }
            }
        }
    }

    private static string[] ReleaseFilePaths([CallerFilePath] string testFilePath = "")
    {
        var projectDirectory = Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(testFilePath)!, "..", "..", "src", "SsalKit.DependencyInjection.Generator"));

        return
        [
            Path.Combine(projectDirectory, "AnalyzerReleases.Shipped.md"),
            Path.Combine(projectDirectory, "AnalyzerReleases.Unshipped.md"),
        ];
    }
}
