using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SsalKit.Generators.Toolkit.Testing.Tests;

/// <summary>
/// Regression guard for the one place in this package where <see cref="Environment.NewLine"/> is
/// forbidden: the text a caller checks into source control as a snapshot.
/// </summary>
/// <remarks>
/// <para>
/// A snapshot is written on one machine and compared on another. A separator that is <c>"\r\n"</c>
/// on Windows and <c>"\n"</c> on Linux therefore turns "the generator's output changed" into "the
/// test ran somewhere else", which is a failure no amount of reading the diff explains.
/// </para>
/// <para>
/// The ban is deliberately scoped to that method rather than applied to the whole package: the
/// failure messages of every assertion here are read by a human in a terminal on the machine that
/// produced them, and there <see cref="Environment.NewLine"/> is the right answer.
/// </para>
/// </remarks>
public class SnapshotTextConventionTests
{
    private const string SnapshotMember = "ToSnapshotText";

    [Fact]
    public void ToSnapshotText_DoesNotMentionEnvironmentNewLine()
    {
        var method = FindSnapshotMember();

        var mentions = method.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(access => access.ToString().EndsWith("Environment.NewLine", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(mentions);
    }

    /// <summary>
    /// The other half of the rule: the ban is on the snapshot text alone, so a test that simply
    /// forbade the token package-wide would be wrong and would have to be weakened later.
    /// </summary>
    [Fact]
    public void FailureMessages_MayStillUseEnvironmentNewLine()
    {
        var text = File.ReadAllText(SourcePath("GeneratorTestResult.cs"));

        Assert.Contains("Environment.NewLine", text, StringComparison.Ordinal);
    }

    private static MethodDeclarationSyntax FindSnapshotMember()
    {
        var root = CSharpSyntaxTree
            .ParseText(File.ReadAllText(SourcePath("GeneratorTestResult.cs")), new CSharpParseOptions(LanguageVersion.Latest))
            .GetRoot();

        return Assert.Single(
            root.DescendantNodes().OfType<MethodDeclarationSyntax>(),
            method => method.Identifier.ValueText == SnapshotMember);
    }

    private static string SourcePath(string fileName, [CallerFilePath] string testFilePath = "")
    {
        var testsDirectory = Path.GetDirectoryName(testFilePath)!;

        return Path.GetFullPath(
            Path.Combine(testsDirectory, "..", "..", "src", "SsalKit.Generators.Toolkit.Testing", fileName));
    }
}
