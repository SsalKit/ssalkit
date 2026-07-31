using Microsoft.CodeAnalysis;
using SsalKit.Determinism.Analyzer.Tests.TestSupport;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.Determinism.Analyzer.Tests;

/// <summary>
/// One positive case per row of the v1 banned-API catalog (design §5.3): the member is used inside a
/// <c>[Deterministic]</c> scope, and exactly one diagnostic -- the right id, at the right place --
/// comes back.
/// </summary>
/// <remarks>
/// <c>exclusive: true</c> throughout, so a row that starts reporting a second diagnostic (a catalog
/// entry accidentally added under two categories, say) fails here rather than being averaged away.
/// The probe method receives the objects a call needs as parameters instead of constructing them,
/// which is what keeps every case down to exactly one banned reference.
/// </remarks>
public class CatalogTests
{
    [Theory]
    // SSALD001 -- ambient time.
    [InlineData("var value = DateTime.Now;", "SSALD001", "DateTime.Now")]
    [InlineData("var value = DateTime.UtcNow;", "SSALD001", "DateTime.UtcNow")]
    [InlineData("var value = DateTime.Today;", "SSALD001", "DateTime.Today")]
    [InlineData("var value = DateTimeOffset.Now;", "SSALD001", "DateTimeOffset.Now")]
    [InlineData("var value = DateTimeOffset.UtcNow;", "SSALD001", "DateTimeOffset.UtcNow")]
    [InlineData("var value = TimeProvider.System;", "SSALD001", "TimeProvider.System")]
    [InlineData("var value = Stopwatch.StartNew();", "SSALD001", "Stopwatch.StartNew()")]
    [InlineData("var value = Stopwatch.GetTimestamp();", "SSALD001", "Stopwatch.GetTimestamp()")]
    [InlineData("var value = new Stopwatch();", "SSALD001", "new Stopwatch()")]
    [InlineData("var value = Environment.TickCount;", "SSALD001", "Environment.TickCount")]
    [InlineData("var value = Environment.TickCount64;", "SSALD001", "Environment.TickCount64")]

    // SSALD002 -- randomness.
    [InlineData("var value = Random.Shared;", "SSALD002", "Random.Shared")]
    [InlineData("var value = new Random();", "SSALD002", "new Random()")]
    [InlineData("var value = new Random(42);", "SSALD002", "new Random(42)")]
    [InlineData("var value = RandomNumberGenerator.Create();", "SSALD002", "RandomNumberGenerator.Create()")]
    [InlineData("RandomNumberGenerator.Fill(new byte[4]);", "SSALD002", "RandomNumberGenerator.Fill(new byte[4])")]
    [InlineData("var value = RandomNumberGenerator.GetBytes(4);", "SSALD002", "RandomNumberGenerator.GetBytes(4)")]
    [InlineData("rng.GetNonZeroBytes(new byte[4]);", "SSALD002", "rng.GetNonZeroBytes(new byte[4])")]
    [InlineData("var value = RandomNumberGenerator.GetInt32(10);", "SSALD002", "RandomNumberGenerator.GetInt32(10)")]
    [InlineData("var value = RandomNumberGenerator.GetHexString(8);", "SSALD002", "RandomNumberGenerator.GetHexString(8)")]
    [InlineData("var value = RandomNumberGenerator.GetString(\"abc\", 4);", "SSALD002", "RandomNumberGenerator.GetString(\"abc\", 4)")]
    [InlineData("var value = RandomNumberGenerator.GetItems<int>(new int[] { 1, 2 }, 2);", "SSALD002", "RandomNumberGenerator.GetItems<int>(new int[] { 1, 2 }, 2)")]
    [InlineData("RandomNumberGenerator.Shuffle<int>(new int[] { 1, 2 });", "SSALD002", "RandomNumberGenerator.Shuffle<int>(new int[] { 1, 2 })")]
    [InlineData("var value = Path.GetRandomFileName();", "SSALD002", "Path.GetRandomFileName()")]

    // SSALD003 -- identifier generation.
    [InlineData("var value = Guid.NewGuid();", "SSALD003", "Guid.NewGuid()")]
    [InlineData("var value = Guid.CreateVersion7();", "SSALD003", "Guid.CreateVersion7()")]
    [InlineData("var value = Guid.CreateVersion7(DateTimeOffset.UnixEpoch);", "SSALD003", "Guid.CreateVersion7(DateTimeOffset.UnixEpoch)")]

    // SSALD004 -- per-process randomized hashing.
    [InlineData("var value = boxedObject.GetHashCode();", "SSALD004", "boxedObject.GetHashCode()")]
    [InlineData("var value = boxedValue.GetHashCode();", "SSALD004", "boxedValue.GetHashCode()")]
    [InlineData("var value = text.GetHashCode();", "SSALD004", "text.GetHashCode()")]
    [InlineData("var value = text.GetHashCode(StringComparison.Ordinal);", "SSALD004", "text.GetHashCode(StringComparison.Ordinal)")]
    [InlineData("var value = comparer.GetHashCode(text);", "SSALD004", "comparer.GetHashCode(text)")]
    [InlineData("var value = HashCode.Combine(1, 2);", "SSALD004", "HashCode.Combine(1, 2)")]
    [InlineData("accumulator.Add(1);", "SSALD004", "accumulator.Add(1)")]
    [InlineData("var value = accumulator.ToHashCode();", "SSALD004", "accumulator.ToHashCode()")]

    // SSALD005 -- environment, process, and thread identity.
    [InlineData("var value = Environment.MachineName;", "SSALD005", "Environment.MachineName")]
    [InlineData("var value = Environment.UserName;", "SSALD005", "Environment.UserName")]
    [InlineData("var value = Environment.UserDomainName;", "SSALD005", "Environment.UserDomainName")]
    [InlineData("var value = Environment.ProcessId;", "SSALD005", "Environment.ProcessId")]
    [InlineData("var value = Environment.CurrentManagedThreadId;", "SSALD005", "Environment.CurrentManagedThreadId")]
    [InlineData("var value = Environment.ProcessorCount;", "SSALD005", "Environment.ProcessorCount")]
    [InlineData("var value = Environment.WorkingSet;", "SSALD005", "Environment.WorkingSet")]
    [InlineData("var value = Environment.CommandLine;", "SSALD005", "Environment.CommandLine")]
    [InlineData("var value = Environment.CurrentDirectory;", "SSALD005", "Environment.CurrentDirectory")]
    [InlineData("var value = Environment.GetEnvironmentVariable(\"PATH\");", "SSALD005", "Environment.GetEnvironmentVariable(\"PATH\")")]
    [InlineData("var value = Environment.GetEnvironmentVariables();", "SSALD005", "Environment.GetEnvironmentVariables()")]
    [InlineData("var value = Process.GetCurrentProcess();", "SSALD005", "Process.GetCurrentProcess()")]
    [InlineData("var value = Thread.CurrentThread;", "SSALD005", "Thread.CurrentThread")]
    [InlineData("var value = Path.GetTempPath();", "SSALD005", "Path.GetTempPath()")]
    [InlineData("var value = Path.GetTempFileName();", "SSALD005", "Path.GetTempFileName()")]

    // SSALD006 -- scheduling and parallelism.
    [InlineData("var value = Task.Run(() => 1);", "SSALD006", "Task.Run(() => 1)")]
    [InlineData("await Task.Delay(1);", "SSALD006", "Task.Delay(1)")]
    [InlineData("await Task.WhenAny(Task.CompletedTask);", "SSALD006", "Task.WhenAny(Task.CompletedTask)")]
    [InlineData("await Task.Yield();", "SSALD006", "Task.Yield()")]
    [InlineData("var value = Task.Factory.StartNew(() => 1);", "SSALD006", "Task.Factory.StartNew(() => 1)")]
    [InlineData("var value = Task<int>.Factory.StartNew(() => 1);", "SSALD006", "Task<int>.Factory.StartNew(() => 1)")]
    [InlineData("Thread.Sleep(1);", "SSALD006", "Thread.Sleep(1)")]
    [InlineData("ThreadPool.QueueUserWorkItem(_ => { });", "SSALD006", "ThreadPool.QueueUserWorkItem(_ => { })")]
    [InlineData("Parallel.For(0, 1, i => { });", "SSALD006", "Parallel.For(0, 1, i => { })")]
    [InlineData("Parallel.ForEach(new int[0], i => { });", "SSALD006", "Parallel.ForEach(new int[0], i => { })")]
    [InlineData("Parallel.Invoke(() => { });", "SSALD006", "Parallel.Invoke(() => { })")]
    [InlineData("await Parallel.ForAsync(0, 1, (i, ct) => ValueTask.CompletedTask);", "SSALD006", "Parallel.ForAsync(0, 1, (i, ct) => ValueTask.CompletedTask)")]
    [InlineData("await Parallel.ForEachAsync(new int[0], (i, ct) => ValueTask.CompletedTask);", "SSALD006", "Parallel.ForEachAsync(new int[0], (i, ct) => ValueTask.CompletedTask)")]
    [InlineData("var value = new int[0].AsParallel();", "SSALD006", "new int[0].AsParallel()")]
    [InlineData("var value = new System.Threading.Timer(_ => { });", "SSALD006", "new System.Threading.Timer(_ => { })")]
    [InlineData("var value = new System.Timers.Timer();", "SSALD006", "new System.Timers.Timer()")]
    public async Task CatalogMember_InsideDeterministicScope_ReportsItsCategoryExactlyOnce(
        string statement, string expectedId, string snippet)
    {
        var source = AnalyzerTestSupport.Probe(statement);

        var diagnostics = await AnalyzerTestSupport.RunAsync(source);

        DiagnosticAssert.Single(
            diagnostics,
            expectedId,
            DiagnosticSeverity.Warning,
            locatedOnSnippet: snippet,
            exclusive: true);
    }

    [Fact]
    public async Task Message_NamesTheOffendingMemberAndTheReplacement()
    {
        var source = AnalyzerTestSupport.Probe("var value = DateTime.UtcNow;");

        var diagnostics = await AnalyzerTestSupport.RunAsync(source);

        var message = DiagnosticAssert.Single(diagnostics, "SSALD001").GetMessage();

        Assert.StartsWith("'DateTime.UtcNow' is non-deterministic:", message, StringComparison.Ordinal);
        Assert.Contains("TimeProvider", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Message_NamesAConstructorTheWayItIsCalled()
    {
        var source = AnalyzerTestSupport.Probe("var value = new Random(42);");

        var diagnostics = await AnalyzerTestSupport.RunAsync(source);

        var message = DiagnosticAssert.Single(diagnostics, "SSALD002").GetMessage();

        Assert.StartsWith("'new Random' is non-deterministic:", message, StringComparison.Ordinal);
        Assert.Contains("DeterministicRandom", message, StringComparison.Ordinal);
    }
}
