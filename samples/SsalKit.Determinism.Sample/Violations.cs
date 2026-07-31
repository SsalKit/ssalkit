// [Showcase] -- deliberately excluded from the default build.
//
// Every other file in this sample demonstrates the analyzer staying silent. This one is the
// opposite: one violation from every category in the v1 catalog, with the replacement noted beside
// each line, so the diagnostics can actually be seen firing.
//
// It sits behind #if DETERMINISM_SHOWCASE because this repository builds with
// TreatWarningsAsErrors, which the file is designed to trip. To see the diagnostics:
//
//   dotnet build samples/SsalKit.Determinism.Sample -p:DefineConstants=DETERMINISM_SHOWCASE
//
// That build is *expected to fail*, reporting one error per marked line below -- the failure is the
// demonstration. In an IDE, adding DETERMINISM_SHOWCASE to the project's <DefineConstants> shows the
// same set inline as warnings, which is how they appear in a consuming project that has not raised
// their severity.
//
// Every rule here is a Warning by default and none will ever default to Error; a consumer that
// wants a build gate raises the severity per id in .editorconfig.

#if DETERMINISM_SHOWCASE

using System.Diagnostics;
using System.Security.Cryptography;
using SsalKit.Determinism;
using SsalKit.Randomness;

/// <summary>
/// One violation from each catalog category, inside a single <c>[Deterministic]</c> scope.
/// </summary>
[Deterministic]
internal static class Violations
{
    /// <summary>
    /// SSALD001 -- ambient time. Fix: inject a <see cref="TimeProvider"/>, or take the instant as a
    /// <c>DateTimeOffset asOf</c> argument, the way every SsalKit.Timekeeping type does.
    /// </summary>
    public static void AmbientTime()
    {
        Sink(DateTime.Now);            // SSALD001 -> asOf argument (and DateTimeOffset, for the offset)
        Sink(DateTime.UtcNow);         // SSALD001 -> asOf argument
        Sink(DateTime.Today);          // SSALD001 -> DateOnly.FromDateTime(asOf.UtcDateTime), asOf supplied
        Sink(DateTimeOffset.Now);      // SSALD001 -> asOf argument
        Sink(DateTimeOffset.UtcNow);   // SSALD001 -> asOf argument
        Sink(TimeProvider.System);     // SSALD001 -> an injected TimeProvider; .GetUtcNow() on one is fine
        Sink(Stopwatch.StartNew());    // SSALD001 -> measure in logical ticks (TickSchedule), not elapsed real time
        Sink(Stopwatch.GetTimestamp()); // SSALD001 -> as above
        Sink(new Stopwatch());         // SSALD001 -> as above
        Sink(Environment.TickCount64); // SSALD001 -> a tick counter the caller owns and passes in
    }

    /// <summary>
    /// SSALD002 -- randomness. Fix: <see cref="DeterministicRandom"/> with an explicit seed, or an
    /// injected <see cref="IRandomSource"/>. Note that the seeded <c>System.Random</c> is banned
    /// too: its algorithm is not part of its contract and has changed between runtime versions, so
    /// the same seed does not reproduce the same sequence across processes or versions.
    /// </summary>
    public static void Randomness()
    {
        Sink(Random.Shared);                            // SSALD002 -> injected IRandomSource
        Sink(new Random());                             // SSALD002 -> new DeterministicRandom(seed)
        Sink(new Random(42));                           // SSALD002 -> new DeterministicRandom(42)
        Sink(RandomNumberGenerator.GetInt32(100));      // SSALD002 -> crypto randomness is non-deterministic by definition
        Sink(RandomNumberGenerator.Create());           // SSALD002 -> as above
        Sink(Path.GetRandomFileName());                 // SSALD002 -> a name derived from the data (ComputeStableHash())
        Sink(SharedRandomSource.Instance);              // SSALD002 -> injected IRandomSource; SsalKit's own ambient source is not exempt
        Sink(CryptoRandomSource.Instance);              // SSALD002 -> as above
        Sink(DeterministicRandom.CreateRandomlySeeded()); // SSALD002 -> new DeterministicRandom(seed), or FromState(saved)
    }

    /// <summary>
    /// SSALD003 -- identifier generation. Fix: derive the id from the data
    /// (<c>ComputeStableHash()</c>) or from bytes drawn out of a seeded
    /// <see cref="DeterministicRandom"/>.
    /// </summary>
    public static void Identifiers()
    {
        Sink(Guid.NewGuid());          // SSALD003 -> id derived from content
        Sink(Guid.CreateVersion7());   // SSALD003 -> still random in the low bits, even with an explicit timestamp

        // Method-group references are caught as well, not only calls.
        Func<Guid> factory = Guid.NewGuid; // SSALD003
        Sink(factory);
    }

    /// <summary>
    /// SSALD004 -- per-process randomized hashing. Fix: <c>[StableHashContract]</c> +
    /// <c>ComputeStableHash()</c> from SsalKit.StableHashing, whose encoding and algorithm are both
    /// versioned contracts.
    /// </summary>
    public static void Hashing()
    {
        Sink("cache-key".GetHashCode());                        // SSALD004 -> ComputeStableHash()
        Sink(new object().GetHashCode());                       // SSALD004 -> identity hash, not content hash
        Sink(HashCode.Combine(1, "two"));                       // SSALD004 -> a [StableHashContract] over the same fields
        Sink(StringComparer.Ordinal.GetHashCode("cache-key"));  // SSALD004 -> randomized even for Ordinal
    }

    /// <summary>
    /// SSALD005 -- machine, process, and thread identity. Fix: pass the value in as explicit
    /// configuration, so the same configuration reproduces the same result on any host.
    /// </summary>
    public static void EnvironmentIdentity()
    {
        Sink(Environment.MachineName);                    // SSALD005 -> configured node id
        Sink(Environment.ProcessId);                      // SSALD005 -> configured instance id
        Sink(Environment.GetEnvironmentVariable("HOME")); // SSALD005 -> a bound options object
        Sink(Process.GetCurrentProcess());                // SSALD005 -> as above
        Sink(Thread.CurrentThread);                       // SSALD005 -> deterministic code is single-threaded anyway
        Sink(Path.GetTempPath());                         // SSALD005 -> a configured directory
    }

    /// <summary>
    /// SSALD006 -- scheduling and parallelism. There is no substitute API: execution order and
    /// timing depend on the scheduler. Restructure the work to run sequentially, or move it outside
    /// the deterministic scope and feed the finished result in.
    /// </summary>
    public static void Scheduling()
    {
        Sink(Task.Run(() => 1));                            // SSALD006 -> run it sequentially
        Sink(Task.Delay(1));                                // SSALD006 -> advance a logical tick instead of waiting
        Sink(Task.WhenAny(Task.CompletedTask));             // SSALD006 -> completion order is not reproducible
        Sink(Task.Yield());                                 // SSALD006 -> resumption context is not reproducible
        Thread.Sleep(1);                                    // SSALD006 -> as with Task.Delay
        Sink(ThreadPool.QueueUserWorkItem(_ => { }));       // SSALD006 -> run it inline
        Sink(Parallel.For(0, 1, _ => { }));                 // SSALD006 -> a plain for loop
        Sink(Enumerable.Range(0, 1).AsParallel());          // SSALD006 -> plain LINQ
        Sink(new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite)); // SSALD006 -> TickSchedule, on logical ticks
        Sink(new System.Timers.Timer(1000));                // SSALD006 -> as above
    }

    /// <summary>Consumes a value so each line above is a statement rather than an unused expression.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="value">The value to discard.</param>
    private static void Sink<T>(T value)
    {
    }
}

/// <summary>
/// SSALD007 -- the seventh diagnostic, which is about the markings rather than the catalog: an
/// <c>[AllowNonDeterminism]</c> with no enclosing <c>[Deterministic]</c> scope suppresses nothing,
/// so a reader who takes it as "this was reviewed and accepted" is being misled.
/// </summary>
internal static class OrphanExemption
{
    /// <summary>Exempt from nothing: no enclosing scope is marked <c>[Deterministic]</c>.</summary>
    [AllowNonDeterminism(Justification = "nothing to exempt -- this application is itself the diagnostic")] // SSALD007
    public static long Now() => DateTime.UtcNow.Ticks;
}

#endif
