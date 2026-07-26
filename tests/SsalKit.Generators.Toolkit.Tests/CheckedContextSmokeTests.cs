using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SsalKit.Generators.Toolkit.Tests;

/// <summary>
/// Compiles the embedded sources the way a consumer with
/// <c>&lt;CheckForOverflowUnderflow&gt;true&lt;/CheckForOverflowUnderflow&gt;</c> compiles them, and
/// runs every hash function in the resulting assembly.
/// </summary>
/// <remarks>
/// <para>
/// This package ships source, not a binary, so its arithmetic is compiled under the consumer's
/// options rather than its own. A hash accumulator is meant to wrap around; under a checked
/// compilation it throws <see cref="OverflowException"/> instead -- from a file the consumer cannot
/// edit, on the equality path of a pipeline model, which is roughly the least debuggable place a
/// crash can come from.
/// </para>
/// <para>
/// The sibling <c>EmbeddedSourceConventionTests</c> checks the same rule structurally, which is what
/// catches a new hash function before it ships. This one is the proof that the structural rule is
/// about something real: it reproduces the exception end to end, and
/// <see cref="AnUnwrappedHashAccumulator_ReallyDoesThrowUnderTheSameCompilation"/> confirms the
/// setup would fail if the wrappers were removed.
/// </para>
/// </remarks>
public class CheckedContextSmokeTests
{
    /// <summary>
    /// Exercises every <c>GetHashCode</c> the package ships. <see cref="EquatableArray{T}"/> is
    /// driven with element hashes chosen to overflow on the first iteration; the two hand-written
    /// ones fold in string hash codes, which .NET randomizes per process, so a spread of inputs is
    /// what makes an overflow certain rather than merely likely.
    /// </summary>
    private const string ProbeSource = """
        namespace SsalKit.Generators.Toolkit
        {
            public static class OverflowProbe
            {
                public static int Run()
                {
                    var accumulated = 0;

                    accumulated ^= new EquatableArray<int>(
                        System.Collections.Immutable.ImmutableArray.Create(
                            int.MaxValue, int.MaxValue, int.MaxValue)).GetHashCode();
                    accumulated ^= EquatableArray<int>.Empty.GetHashCode();
                    accumulated ^= default(EquatableArray<int>).GetHashCode();

                    var descriptor = new Microsoft.CodeAnalysis.DiagnosticDescriptor(
                        "PROBE001",
                        "title",
                        "message {0}",
                        "category",
                        Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,
                        true);

                    for (var i = 0; i < 200; i++)
                    {
                        var location = new LocationInfo(
                            "Probe" + i + ".cs",
                            new Microsoft.CodeAnalysis.Text.TextSpan(i, i + 1),
                            default(Microsoft.CodeAnalysis.Text.LinePositionSpan));

                        accumulated ^= location.GetHashCode();
                        accumulated ^= new DiagnosticInfo(descriptor, location, "arg" + i).GetHashCode();
                    }

                    return accumulated;
                }
            }
        }
        """;

    /// <summary>
    /// The shape the toolkit's own accumulators had before they were wrapped, kept here so the
    /// harness can be shown to reproduce the failure it is meant to rule out.
    /// </summary>
    private const string UnwrappedProbeSource = """
        namespace Probe
        {
            public static class UnwrappedProbe
            {
                public static int Run()
                {
                    var hash = 17;
                    hash = (hash * 31) + int.MaxValue;
                    hash = (hash * 31) + int.MaxValue;
                    return hash;
                }
            }
        }
        """;

    [Fact]
    public void EveryShippedHashFunction_SurvivesACheckedCompilation()
    {
        var probe = CompileAndLoad("ToolkitCheckedSmoke", ProbeSource, includeEmbeddedSources: true)
            .GetType("SsalKit.Generators.Toolkit.OverflowProbe", throwOnError: true)!;

        var run = probe.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;

        // Reflection wraps anything the target throws, so an OverflowException from inside would
        // surface here as a TargetInvocationException rather than as a test failure about hashing.
        var exception = Record.Exception(() => run.Invoke(null, null));

        Assert.Null(exception is TargetInvocationException wrapper ? wrapper.InnerException : exception);
    }

    [Fact]
    public void AnUnwrappedHashAccumulator_ReallyDoesThrowUnderTheSameCompilation()
    {
        var probe = CompileAndLoad("ToolkitCheckedSmokeControl", UnwrappedProbeSource, includeEmbeddedSources: false)
            .GetType("Probe.UnwrappedProbe", throwOnError: true)!;

        var run = probe.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;

        var exception = Assert.Throws<TargetInvocationException>(() => run.Invoke(null, null));

        Assert.IsType<OverflowException>(exception.InnerException);
    }

    private static Assembly CompileAndLoad(string assemblyName, string probeSource, bool includeEmbeddedSources)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp10);

        var trees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(probeSource, parseOptions, "Probe.cs") };

        if (includeEmbeddedSources)
        {
            trees.AddRange(
                EmbeddedSourcePaths().Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), parseOptions, path)));
        }

        var compilation = CSharpCompilation.Create(
            assemblyName,
            trees,
            ReferenceAssemblies(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                // The single point of the whole test: compile exactly as a consumer who set
                // CheckForOverflowUnderflow would.
                checkOverflow: true,
                nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);

        Assert.True(
            result.Success,
            string.Join(
                "\n",
                result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));

        return Assembly.Load(stream.ToArray());
    }

    private static IEnumerable<MetadataReference> ReferenceAssemblies() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(File.Exists)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

    private static IEnumerable<string> EmbeddedSourcePaths([CallerFilePath] string testFilePath = "")
    {
        var srcDirectory = Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(testFilePath)!, "..", "..", "src", "SsalKit.Generators.Toolkit"));

        return Directory.GetFiles(srcDirectory, "*.cs", SearchOption.TopDirectoryOnly);
    }
}
