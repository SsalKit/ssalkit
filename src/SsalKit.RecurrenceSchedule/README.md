[← SsalKit](https://github.com/ssalkit/ssalkit)

# SsalKit.RecurrenceSchedule

[![NuGet](https://img.shields.io/nuget/v/SsalKit.RecurrenceSchedule.svg)](https://www.nuget.org/packages/SsalKit.RecurrenceSchedule/)

Time-zone-aware recurring reset boundaries (daily / weekly / monthly) and half-open time-window
arithmetic, as pure functions over an instant you supply — with `TimeProvider` overloads for code
that already holds a clock. Zero dependencies.

```csharp
var reset = RecurrenceSchedule.Daily(new TimeOnly(4, 30), TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"));

if (reset.HasCrossed(player.LastQuotaReset, now))
{
    player.Quota = DailyQuota;
    player.LastQuotaReset = reset.PreviousBoundary(now);
}

var missedDays = reset.CountBoundaries(player.LastLogin, now);
```

The daylight-saving resolution rules are a fixed versioned contract: a scheduled wall-clock time
that a forward transition skips moves to the first valid instant after the gap (so a boundary is
never lost), and one that a backward transition repeats resolves to its first occurrence (so the
schedule never fires twice).

> Full documentation is in progress.
