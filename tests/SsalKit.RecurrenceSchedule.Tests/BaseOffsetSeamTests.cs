namespace SsalKit.RecurrenceSchedule.Tests;

/// <summary>
/// Rule 3 of the daylight-saving contract: the wall-clock times a zone loses when its <i>base</i>
/// UTC offset changes permanently — Libya at the turn of 2012, Venezuela in 2007, Samoa's skipped
/// 30 December 2011, North Korea in 2015, and the several Russian zones that were re-based.
/// <para>
/// These seams are not daylight saving and <see cref="TimeZoneInfo.IsInvalidTime(DateTime)"/> does
/// not report them: the zone's local-time view says the scheduled wall clock is perfectly ordinary,
/// while its instant view is governed by a different offset there. Pairing the two would build a
/// boundary that does not sit where it claims to, and every invariant that re-derives an occurrence
/// from a boundary — idempotence of <see cref="RecurrenceSchedule.PreviousBoundary"/>,
/// <c>CurrentWindow(b).Start == b</c>, <c>CountBoundaries(b, b + ε) == 0</c> — would come apart.
/// </para>
/// <para>
/// Which zones carry a seam is a property of the platform's time-zone data, not of this library:
/// the seams below are visible in the data Windows ships and absent from a tzdata build that
/// records the same history as plain transitions. The properties asserted here hold either way, so
/// the tests run everywhere; only the golden value at the end is conditioned on the seam actually
/// being present, and a zone the platform cannot resolve at all is skipped.
/// </para>
/// </summary>
public sealed class BaseOffsetSeamTests
{
    /// <summary>
    /// Zone identifier and the local date its base-offset seam falls on. IANA identifiers
    /// throughout, which .NET 6+ resolves on Windows as well.
    /// </summary>
    public static TheoryData<string, int, int, int> Seams => new()
    {
        { "America/Caracas", 2007, 1, 1 },
        { "Africa/Sao_Tome", 2019, 1, 1 },
        { "Africa/Tripoli", 2012, 1, 1 },
        { "Europe/Volgograd", 2019, 1, 1 },
        { "Europe/Saratov", 2014, 1, 1 },
        { "Europe/Astrakhan", 2014, 1, 1 },
        { "Europe/Samara", 2010, 1, 1 },
        { "Asia/Novosibirsk", 2014, 1, 1 },
        { "Asia/Barnaul", 2014, 1, 1 },
        { "Asia/Tomsk", 2014, 1, 1 },
        { "Asia/Pyongyang", 2015, 1, 1 },
        { "Asia/Sakhalin", 2014, 1, 1 },
        { "Asia/Kamchatka", 2010, 1, 1 },
        { "Pacific/Apia", 2011, 12, 31 },
    };

    /// <summary>
    /// Every schedule whose wall-clock time falls on or next to a seam still produces boundaries the
    /// zone agrees with, and those boundaries are fixed points of the whole API.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seams))]
    public void EverySeamBoundaryIsAFixedPointOfTheApi(string zoneId, int year, int month, int day)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var zone))
        {
            return;
        }

        var seamDay = new DateTime(year, month, day);

        for (var offsetInDays = -1; offsetInDays <= 1; offsetInDays++)
        {
            var date = seamDay.AddDays(offsetInDays);

            for (var minutes = 0; minutes < 24 * 60; minutes += 15)
            {
                var schedule = RecurrenceSchedule.Daily(
                    TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutes)),
                    zone);

                // Noon on the day in question, and one instant in each quarter of it.
                var noon = new DateTimeOffset(date.AddHours(12), zone.GetUtcOffset(date.AddHours(12)));

                foreach (var hours in new[] { -12.0, -6.0, 0.0, 6.0 })
                {
                    AssertFixedPoint(zone, schedule, noon.AddHours(hours));
                }
            }
        }
    }

    private static void AssertFixedPoint(TimeZoneInfo zone, RecurrenceSchedule schedule, DateTimeOffset asOf)
    {
        var boundary = schedule.PreviousBoundary(asOf);

        // The zone agrees the boundary sits where it says it does. Everything below follows from
        // this one property, which is precisely what a base-offset seam used to break.
        Assert.Equal(boundary.Offset, zone.GetUtcOffset(boundary));

        AssertTime.Exact(boundary, schedule.PreviousBoundary(boundary));
        AssertTime.Exact(boundary, schedule.CurrentWindow(boundary).Start);
        Assert.Equal(0, schedule.CountBoundaries(boundary, boundary.AddSeconds(1)));
        Assert.False(schedule.HasCrossed(boundary, boundary.AddSeconds(1)));
        Assert.True(schedule.NextBoundary(boundary) > boundary);
    }

    /// <summary>
    /// The resolution rule itself, stated directly: the boundary is the <i>first</i> instant at
    /// which the zone's wall clock reaches the scheduled time — the same rule a skipped daylight-
    /// saving time follows, and the reason the answer is well defined even where the wall clock runs
    /// backwards for an hour before jumping forwards.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seams))]
    public void ASeamBoundaryIsTheFirstInstantTheWallClockReachesTheScheduledTime(
        string zoneId,
        int year,
        int month,
        int day)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var zone))
        {
            return;
        }

        var seamDay = new DateTime(year, month, day);

        for (var minutes = 0; minutes < 24 * 60; minutes += 15)
        {
            var scheduled = seamDay.AddMinutes(minutes);
            var schedule = RecurrenceSchedule.Daily(TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutes)), zone);

            // An independent, deliberately dumb oracle: sweep the bracketing window a minute at a
            // time and take the first minute whose wall clock has reached the scheduled time. It
            // locates the answer to within a minute, which is enough to name an instant that lies
            // in the previous window — and from there NextBoundary has to return this occurrence's
            // boundary and no other, occurrences being a day apart.
            var withinAMinute = FirstMinuteReaching(zone, scheduled);
            var boundary = schedule.NextBoundary(withinAMinute.AddMinutes(-1));

            Assert.True(
                WallClockAt(zone, boundary) >= scheduled,
                $"{zoneId} @ {scheduled:O}: the boundary's wall clock has not reached the scheduled time.");
            Assert.True(
                WallClockAt(zone, boundary.AddTicks(-1)) < scheduled,
                $"{zoneId} @ {scheduled:O}: an earlier instant already reaches the scheduled time.");
            Assert.True(
                boundary > withinAMinute.AddMinutes(-1) && boundary <= withinAMinute,
                $"{zoneId} @ {scheduled:O}: the boundary is outside the minute the oracle brackets it to.");
        }
    }

    /// <summary>
    /// The first minute-aligned instant whose wall clock in <paramref name="zone"/> has reached
    /// <paramref name="scheduled"/>. UTC offsets are bounded by ±14 hours, so the answer is within
    /// fifteen hours of the scheduled wall clock read as a UTC instant.
    /// </summary>
    private static DateTimeOffset FirstMinuteReaching(TimeZoneInfo zone, DateTime scheduled)
    {
        var probe = DateTime.SpecifyKind(scheduled.AddHours(-15), DateTimeKind.Utc);
        var limit = DateTime.SpecifyKind(scheduled.AddHours(15), DateTimeKind.Utc);

        while (probe <= limit)
        {
            if (probe + zone.GetUtcOffset(probe) >= scheduled)
            {
                return new DateTimeOffset(probe, TimeSpan.Zero);
            }

            probe = probe.AddMinutes(1);
        }

        throw new InvalidOperationException($"{zone.Id} never reaches {scheduled:O} within ±15 hours of it.");
    }

    /// <summary>
    /// The Libya golden case, which is the one the whole rule was written from: Windows-shipped data
    /// re-bases <c>Africa/Tripoli</c> from +02:00 to +01:00 at the turn of 2012 and, between the
    /// dip and the jump either side of it, the wall clock never reads 2012-01-01 00:00 at all.
    /// </summary>
    [Fact]
    public void Libya_AtTheTurnOf2012_IsPinnedToTheFirstInstantAfterTheSeam()
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById("Africa/Tripoli", out var zone))
        {
            return;
        }

        var schedule = RecurrenceSchedule.Daily(new TimeOnly(0, 0), zone);
        var midday = new DateTimeOffset(2012, 1, 1, 12, 0, 0, TimeSpan.FromHours(2));
        var boundary = schedule.PreviousBoundary(midday);

        if (!HasSeamAt(zone, new DateTime(2012, 1, 1)))
        {
            // Platform data without the seam: midnight exists and is simply the boundary.
            AssertTime.Exact(new DateTimeOffset(2012, 1, 1, 0, 0, 0, TimeSpan.FromHours(2)), boundary);
            return;
        }

        // 2011-12-31T21:00Z reads 23:00, 22:00Z reads 23:00 again (the dip to +01:00), 23:00Z reads
        // 01:00 (back to +02:00). The first instant to reach 2012-01-01 00:00 is 23:00Z, reported at
        // the offset in force there.
        AssertTime.Exact(new DateTimeOffset(2012, 1, 1, 1, 0, 0, TimeSpan.FromHours(2)), boundary);
        Assert.Equal(new DateTime(2011, 12, 31, 23, 0, 0, DateTimeKind.Utc), boundary.UtcDateTime);

        // And it is a fixed point, which the naive reading — 2011-12-31T22:00Z labelled +02:00 —
        // was not: that instant is governed by +01:00, so re-deriving from it landed elsewhere.
        AssertTime.Exact(boundary, schedule.PreviousBoundary(boundary));
        Assert.Equal(0, schedule.CountBoundaries(boundary, boundary.AddSeconds(1)));
        AssertTime.Exact(new DateTimeOffset(2012, 1, 2, 0, 0, 0, TimeSpan.FromHours(2)), schedule.NextBoundary(boundary));
    }

    /// <summary>
    /// Whether the platform's data for <paramref name="zone"/> disagrees with itself at
    /// <paramref name="wallClock"/>: neither invalid nor ambiguous by the zone's own account, yet
    /// the instant its offset points at is governed by a different offset.
    /// </summary>
    private static bool HasSeamAt(TimeZoneInfo zone, DateTime wallClock)
    {
        if (zone.IsInvalidTime(wallClock) || zone.IsAmbiguousTime(wallClock))
        {
            return false;
        }

        var offset = zone.GetUtcOffset(wallClock);
        return zone.GetUtcOffset(new DateTimeOffset(wallClock, offset)) != offset;
    }

    private static DateTime WallClockAt(TimeZoneInfo zone, DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, zone).DateTime;
}
