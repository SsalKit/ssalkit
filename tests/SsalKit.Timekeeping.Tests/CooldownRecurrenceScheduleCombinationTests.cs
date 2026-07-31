namespace SsalKit.Timekeeping.Tests;

/// <summary>
/// The two halves of this package answer different questions -- <see cref="RecurrenceSchedule"/>
/// asks "has the calendar boundary passed?" and <see cref="RechargePool"/> asks "how many units are
/// available?" -- but neither type knows about the other; combining them is just ordinary calling
/// code. These tests pin the combination described in the package's role-boundary table: detect a
/// calendar reset with <see cref="RecurrenceSchedule.HasCrossed"/> and, when it has fired,
/// <see cref="RechargePool.Refill"/> the pool at the boundary instant.
/// </summary>
public sealed class CooldownRecurrenceScheduleCombinationTests
{
    private static readonly RecurrenceSchedule DailyReset = RecurrenceSchedule.Daily(new TimeOnly(4, 30));

    [Fact]
    public void ADailyResetCrossing_RefillsThePoolAtTheBoundaryInstant()
    {
        var lastSeen = new DateTimeOffset(2026, 7, 24, 10, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 7, 25, 9, 0, 0, TimeSpan.Zero); // past today's 04:30 reset

        var pool = RechargePool.Create(5, TimeSpan.FromHours(1), lastSeen, initialCharges: 1);

        Assert.True(DailyReset.HasCrossed(lastSeen, now));

        var boundary = DailyReset.PreviousBoundary(now);
        var refilled = pool.Refill(boundary);

        AssertTime.Exact(boundary, refilled.FullAt);
        Assert.Equal(5, refilled.AvailableAt(boundary));
        // One tick before the refill boundary, exactly one unit is still missing -- see
        // RechargePoolTests.Refill_MakesThePoolFullAtTheGivenInstant for why it is 1, not Capacity.
        Assert.Equal(4, refilled.AvailableAt(boundary.AddTicks(-1)));
    }

    [Fact]
    public void NoResetCrossing_LeavesThePoolAloneOnItsOwnRechargeSchedule()
    {
        var lastSeen = new DateTimeOffset(2026, 7, 25, 5, 0, 0, TimeSpan.Zero); // just after today's reset
        var now = new DateTimeOffset(2026, 7, 25, 6, 0, 0, TimeSpan.Zero); // same day, before tomorrow's reset

        var pool = RechargePool.Create(5, TimeSpan.FromHours(1), lastSeen, initialCharges: 1);

        Assert.False(DailyReset.HasCrossed(lastSeen, now));

        // No calendar boundary crossed, so calling code would not refill; the pool simply continues
        // recharging on its own O(1) schedule.
        Assert.Equal(2, pool.AvailableAt(now));
    }

    [Fact]
    public void ARepeatedDailyLoop_KeepsThePoolInLockstepWithTheCalendarAcrossManyDays()
    {
        var pool = RechargePool.Create(3, TimeSpan.FromHours(2), new DateTimeOffset(2026, 1, 1, 4, 30, 0, TimeSpan.Zero));
        var lastSeen = new DateTimeOffset(2026, 1, 1, 4, 30, 0, TimeSpan.Zero);

        for (var day = 1; day <= 30; day++)
        {
            var now = lastSeen.AddDays(1).AddHours(5); // well past the next 04:30 boundary

            Assert.True(DailyReset.HasCrossed(lastSeen, now));

            var boundary = DailyReset.PreviousBoundary(now);
            pool = pool.Refill(boundary);
            lastSeen = boundary;

            Assert.Equal(3, pool.AvailableAt(boundary));
        }
    }
}
