using SsalKit.Timekeeping;
using static SampleContext;

// [TimeProvider]
internal static class TimeProviderSamples
{
    public static void Run()
    {
        // ---------------------------------------------------------------------------------------
        // 6. TimeProvider overloads. The core API always takes the instant as an argument -- that is
        //    what makes it testable without freezing a global clock -- and these extensions are sugar
        //    over it for callers that already have an injected clock. TimeProvider is BCL from .NET 8,
        //    so using them adds no package dependency.
        // ---------------------------------------------------------------------------------------
        TimeProvider clock = new FixedTimeProvider(Now);

        Console.WriteLine("[TimeProvider]   the same questions again, with 'now' read from an injected clock");
        Console.WriteLine($"                 clock.GetUtcNow()    {Instant(clock.GetUtcNow())}");
        Console.WriteLine($"                 PreviousBoundary     {Instant(DailyReset.PreviousBoundary(clock))}");
        Console.WriteLine($"                 NextBoundary(clock)  {Instant(DailyReset.NextBoundary(clock))}");
        Console.WriteLine($"                 UntilNext(clock)     {Elapsed(DailyReset.UntilNext(clock))}  (always strictly positive)");
        Console.WriteLine($"                 CurrentWindow(clock) [{Instant(DailyReset.CurrentWindow(clock).Start)}, {Instant(DailyReset.CurrentWindow(clock).End)})");
        Console.WriteLine($"                 HasCrossed(clock)    {DailyReset.HasCrossed(LastReset, clock)}");
        Console.WriteLine($"                 CountBoundaries      {DailyReset.CountBoundaries(LastLogin, clock)}");
        Console.WriteLine();
        Console.WriteLine("                 In tests, hand in a fake provider (FakeTimeProvider, or the handful of");
        Console.WriteLine("                 lines FixedTimeProvider takes at the bottom of this file) to drive a");
        Console.WriteLine("                 schedule across a boundary deterministically.");
        Console.WriteLine();
    }

    // The whole of a test clock: the extension methods only ever call GetUtcNow(), so there is
    // nothing else to fake. Microsoft.Extensions.TimeProvider.Testing's FakeTimeProvider works just
    // as well and can also advance time.
    private sealed class FixedTimeProvider(DateTimeOffset instant) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = instant.ToUniversalTime();

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
