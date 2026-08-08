using SsalKit.Timekeeping;
using static SampleContext;

// [Combined]
internal static class CombinedSamples
{
    public static void Run()
    {
        // ---------------------------------------------------------------------------------------
        // 20. Combining the two halves of the package: RecurrenceSchedule.HasCrossed detects a calendar
        //     reset, and RechargePool.Refill applies it -- a daily stamina top-up that fires on the wall
        //     clock rather than waiting for the pool's own (much slower) recharge rate to catch up.
        // ---------------------------------------------------------------------------------------
        var dailyStamina = RechargePool.Create(5, TimeSpan.FromHours(6), LastReset, initialCharges: 1);

        Console.WriteLine("[Combined]       HasCrossed detects the reset; Refill applies it to the pool");
        Console.WriteLine($"                 pool created at      {Instant(LastReset)}  (own recharge rate: 1 slot / 6h)");

        if (DailyReset.HasCrossed(LastReset, Now))
        {
            var resetBoundary = DailyReset.PreviousBoundary(Now);
            var refilled = dailyStamina.Refill(resetBoundary);

            Console.WriteLine($"                 HasCrossed(lastReset, now) {DailyReset.HasCrossed(LastReset, Now)}  -> reset boundary {Instant(resetBoundary)}");
            Console.WriteLine($"                 before Refill:       AvailableAt(now) {dailyStamina.AvailableAt(Now)}  (still recharging on its own)");
            Console.WriteLine($"                 after Refill(boundary): AvailableAt(now) {refilled.AvailableAt(Now)}  (full immediately, regardless of the 6-hour rate)");
        }

        Console.WriteLine();
    }
}
