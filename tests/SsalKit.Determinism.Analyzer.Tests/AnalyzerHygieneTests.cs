using SsalKit.Determinism.Analyzer.Tests.TestSupport;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.Determinism.Analyzer.Tests;

/// <summary>
/// Analyzer hygiene (design §7.6): the opt-in scope means an ordinary codebase sees nothing, and no
/// run may end with the analyzer having thrown.
/// </summary>
/// <remarks>
/// The harness turns an <c>AD0001</c> ("an analyzer threw") into a failed assertion by itself, and
/// never filters that id out, so every test in this project is also a crash test. These add the
/// cases most likely to reach an unusual code path -- a large unmarked source that touches every
/// category, and a compilation with no <c>SsalKit.Determinism</c> reference at all.
/// </remarks>
public class AnalyzerHygieneTests
{
    private const string EveryCategoryOutOfScope = """
        #pragma warning disable CS0219, CS1998
        using System;
        using System.Collections.Generic;
        using System.Diagnostics;
        using System.IO;
        using System.Linq;
        using System.Security.Cryptography;
        using System.Threading;
        using System.Threading.Tasks;

        public sealed class OrdinaryApplication
        {
            private readonly Guid _id = Guid.NewGuid();

            public DateTime CreatedAt { get; } = DateTime.UtcNow;

            public async Task<string> RunAsync()
            {
                var stopwatch = Stopwatch.StartNew();
                var random = new Random();
                var shared = Random.Shared;
                var bytes = RandomNumberGenerator.GetBytes(16);
                var host = Environment.MachineName;
                var pid = Environment.ProcessId;
                var temp = Path.GetTempPath();
                var hash = HashCode.Combine(_id, CreatedAt);
                var boxed = ((object)_id).GetHashCode();

                await Task.Delay(1);
                var computed = await Task.Run(() => shared.Next() + random.Next());
                Parallel.For(0, 2, i => { });
                var query = new[] { 1, 2, 3 }.AsParallel().Select(x => x * 2).ToList();

                Thread.Sleep(0);
                stopwatch.Stop();

                return $"{host}{pid}{temp}{hash}{boxed}{bytes.Length}{computed}{query.Count}";
            }
        }
        """;

    [Fact]
    public async Task LargeUnmarkedSource_TouchingEveryCategory_ReportsNothing()
    {
        var diagnostics = await AnalyzerTestSupport.RunAsync(EveryCategoryOutOfScope);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task RunningTheAnalyzerSetTogether_ReportsNothingForUnmarkedCode()
    {
        var diagnostics = await AnalyzerTestSupport.RunAllAsync(EveryCategoryOutOfScope);

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task CompilationWithoutTheRuntimePackage_ReportsNothingAndDoesNotCrash()
    {
        // No AdditionalAssemblies at all: neither attribute resolves, so CompilationStart bails out
        // before registering a single action.
        var diagnostics = await AnalyzerTestSupport.RunAsync(
            EveryCategoryOutOfScope, GeneratorTestOptions.Default with { DiagnosticIdPrefix = AnalyzerTestSupport.Prefix });

        DiagnosticAssert.None(diagnostics, AnalyzerTestSupport.Prefix);
    }

    [Fact]
    public async Task GeneratedCode_InsideAMarkedType_IsStillReported()
    {
        // ConfigureGeneratedCodeAnalysis(Analyze | ReportDiagnostics): a generator emitting into a
        // user's [Deterministic] type produces code that runs in the deterministic core, so staying
        // silent about it would hide a real bug behind "it wasn't hand-written".
        const string Source = """
            using System;
            using SsalKit.Determinism;

            [Deterministic]
            public sealed class Simulation
            {
                [System.CodeDom.Compiler.GeneratedCode("TestGenerator", "1.0")]
                public Guid Generated() => Guid.NewGuid();
            }
            """;

        var diagnostics = await AnalyzerTestSupport.RunAsync(Source);

        DiagnosticAssert.Single(diagnostics, "SSALD003", locatedOnSnippet: "Guid.NewGuid()", exclusive: true);
    }
}
