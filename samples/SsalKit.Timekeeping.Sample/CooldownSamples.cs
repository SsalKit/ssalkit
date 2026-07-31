using SsalKit.Timekeeping;
using static SampleContext;

// [Cooldown] [RechargePool] [Offline] [Partial]
internal static class CooldownSamples
{
    public static void Run()
    {
        // ---------------------------------------------------------------------------------------
        // 7. Cooldown: a single elapsed-time cooldown, stored as the instant it becomes ready rather than
        //    as a countdown. Create is immediately usable; a successful TryUse starts a fresh wait; and the
        //    wait is usable *at* the instant it completes, not only strictly after it -- the same
        //    boundary-inclusive convention RecurrenceSchedule uses for calendar boundaries.
        // ---------------------------------------------------------------------------------------
        var fireball = Cooldown.Create(TimeSpan.FromSeconds(20), Now);

        Console.WriteLine("[Cooldown]       a 20-second skill cooldown");
        Console.WriteLine($"                 Create(20s, now)     ReadyAt {Instant(fireball.ReadyAt)}  (immediately usable)");

        bool firstCast = fireball.TryUse(Now, out var onCooldown);
        Console.WriteLine($"                 TryUse(now)          {firstCast}   -> ReadyAt {Instant(onCooldown.ReadyAt)}");

        bool secondCast = onCooldown.TryUse(Now, out var stillOnCooldown);
        Console.WriteLine($"                 TryUse(now) again    {secondCast}  (still on cooldown)  Remaining {onCooldown.Remaining(Now).TotalSeconds:0}s");

        bool castAtReadyAt = stillOnCooldown.TryUse(onCooldown.ReadyAt, out _);
        Console.WriteLine($"                 TryUse(ReadyAt)      {castAtReadyAt}   (boundary is inclusive -- usable at ReadyAt, not only after it)");
        Console.WriteLine();

        // ---------------------------------------------------------------------------------------
        // 8. RechargePool: a capacity-bounded resource that recharges one unit at a time. Its entire state
        //    is FullAt, the single instant it becomes completely full -- every other quantity is derived
        //    from it and RechargeEvery, in O(1).
        // ---------------------------------------------------------------------------------------
        var energy = RechargePool.Create(3, TimeSpan.FromMinutes(20), Now);

        Console.WriteLine("[RechargePool]   a 3-slot energy pool, one recharge every 20 minutes");
        Console.WriteLine($"                 Create(3, 20m, now)  FullAt {Instant(energy.FullAt)}  AvailableAt(now) {energy.AvailableAt(Now)}");

        bool consumed = energy.TryConsume(Now, 2, out var afterConsume);
        Console.WriteLine($"                 TryConsume(now, 2)   {consumed}   AvailableAt(now) {afterConsume.AvailableAt(Now)}  FullAt {Instant(afterConsume.FullAt)}");
        Console.WriteLine($"                 UntilNextCharge(now) {Elapsed(afterConsume.UntilNextCharge(Now)!.Value)}");
        Console.WriteLine($"                 UntilFull(now)       {Elapsed(afterConsume.UntilFull(Now)!.Value)}");

        var twentyMinutesLater = Now.AddMinutes(20);
        Console.WriteLine($"                 20 minutes later     AvailableAt {afterConsume.AvailableAt(twentyMinutesLater)}  (one more slot has recharged)");
        Console.WriteLine();

        // ---------------------------------------------------------------------------------------
        // 9. Offline recharge: AvailableAt is one subtraction and one division against FullAt, not a loop
        //    over missed recharges -- so a three-day gap and a ten-year gap cost exactly the same to
        //    answer, and the pool re-derives correctly however long it has been offline.
        // ---------------------------------------------------------------------------------------
        var emptyPool = RechargePool.Create(3, TimeSpan.FromMinutes(20), Now, initialCharges: 0);
        var threeDaysLater = Now.AddDays(3);
        var tenYearsLater = Now.AddYears(10);

        Console.WriteLine("[Offline]        an emptied pool, queried long after anyone last looked at it");
        Console.WriteLine($"                 emptied at           {Instant(Now)}  AvailableAt(now) {emptyPool.AvailableAt(Now)}");
        Console.WriteLine($"                 3 days later         AvailableAt {emptyPool.AvailableAt(threeDaysLater)}  (3 * 20m << 3 days, so fully recharged)");
        Console.WriteLine($"                 10 years later       AvailableAt {emptyPool.AvailableAt(tenYearsLater)}  (same O(1) cost as the 3-day query above)");
        Console.WriteLine();

        // ---------------------------------------------------------------------------------------
        // 10. Partial progress toward the next unit is preserved exactly: spending the one slot that is
        //     already available does not restart the progress already made toward the slot that is
        //     mid-recharge, because consume/grant operate on FullAt directly rather than on a per-slot
        //     countdown.
        // ---------------------------------------------------------------------------------------
        var partial = RechargePool.Create(2, TimeSpan.FromMinutes(20), Now, initialCharges: 1); // 1 slot pending
        var halfway = Now.AddMinutes(10); // halfway through the pending slot's recharge

        Console.WriteLine("[Partial]        one slot pending and half-charged when the other slot is spent");
        Console.WriteLine($"                 before spending:     UntilNextCharge(halfway) {Elapsed(partial.UntilNextCharge(halfway)!.Value)}");

        bool spent = partial.TryConsume(halfway, 1, out var afterSpend);
        Console.WriteLine($"                 TryConsume(halfway, 1) {spent}   AvailableAt(halfway) {afterSpend.AvailableAt(halfway)}");
        Console.WriteLine($"                 after spending:      UntilNextCharge(halfway) {Elapsed(afterSpend.UntilNextCharge(halfway)!.Value)}  (unchanged -- the pending slot's progress was not reset)");
        Console.WriteLine();
    }
}
