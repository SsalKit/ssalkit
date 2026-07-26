using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace SsalKit.Generators.Toolkit.Tests;

/// <summary>
/// Keeps the repository-root <c>llms.txt</c> honest: every repository path it links to has to exist,
/// and every per-package <c>AI.md</c> has to be listed.
/// </summary>
/// <remarks>
/// <para>
/// <c>llms.txt</c> is the entry point an AI agent follows to find the per-package contract sheets, so
/// a link that 404s costs the agent the whole document rather than one sentence, and a package
/// missing from the list is a contract sheet that is never found at all. Both failure modes are
/// silent -- nothing else in the build reads this file.
/// </para>
/// <para>
/// The links are absolute GitHub URLs (they have to be: the file is also read from raw URLs, where a
/// relative path resolves against the wrong root), so the check maps the
/// <c>blob/main/</c> and <c>tree/main/</c> prefixes back onto the working tree.
/// </para>
/// <para>
/// It lives in this project rather than in a package's own test project because it is about the
/// repository root, which belongs to no single package; this is the one test project whose subject
/// -- a source-only toolkit with no runtime assembly -- is already repository content rather than a
/// shipped API.
/// </para>
/// </remarks>
public class LlmsTxtTests
{
    private const string RepositoryUrlPrefix = "https://github.com/ssalkit/ssalkit/";

    private static readonly Regex MarkdownLink = new(
        @"\]\((?<url>[^)\s]+)\)",
        RegexOptions.Compiled);

    [Fact]
    public void LlmsTxt_Exists()
    {
        Assert.True(File.Exists(LlmsTxtPath()), $"{LlmsTxtPath()} is missing.");
    }

    [Fact]
    public void LlmsTxt_LinksOnlyToPathsThatExist()
    {
        var root = RepositoryRoot();

        var missing = RepositoryPaths()
            .Where(relative =>
            {
                var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                return !File.Exists(full) && !Directory.Exists(full);
            })
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void LlmsTxt_ListsEveryPackagesAiDoc()
    {
        var root = RepositoryRoot();

        var declared = Directory
            .GetFiles(Path.Combine(root, "src"), "AI.md", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        var linked = RepositoryPaths()
            .Where(path => path.EndsWith("/AI.md", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(declared);
        Assert.Equal(declared, linked);
    }

    /// <summary>
    /// The repository-relative path of every link that points into this repository, with the
    /// <c>blob/main/</c> or <c>tree/main/</c> prefix removed.
    /// </summary>
    private static IEnumerable<string> RepositoryPaths()
    {
        foreach (Match match in MarkdownLink.Matches(File.ReadAllText(LlmsTxtPath())))
        {
            var url = match.Groups["url"].Value;

            if (!url.StartsWith(RepositoryUrlPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var remainder = url.Substring(RepositoryUrlPrefix.Length);

            foreach (var prefix in new[] { "blob/main/", "tree/main/" })
            {
                if (remainder.StartsWith(prefix, StringComparison.Ordinal))
                {
                    yield return remainder.Substring(prefix.Length);
                    break;
                }
            }
        }
    }

    private static string LlmsTxtPath() => Path.Combine(RepositoryRoot(), "llms.txt");

    private static string RepositoryRoot([CallerFilePath] string testFilePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFilePath)!, "..", ".."));
}
