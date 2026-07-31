// SsalKit.Timekeeping sample
//
// Walks through the library in the order the questions usually come up: has the daily reset
// happened since we last looked, how many resets did a player miss while they were away, what do
// weekly and monthly cadences look like (including the month-end clamp), what happens on the two
// days a year a wall clock misbehaves, how the half-open TimeWindow arithmetic composes, how the
// TimeProvider overloads read "now" for code that already holds a clock, and -- the elapsed-time
// half of the package -- a single skill Cooldown, a capacity-bounded RechargePool, and the two
// combined with a calendar reset.
//
// Every instant used below is a fixed literal rather than DateTimeOffset.UtcNow (see
// SampleContext.cs): the whole API is a pure function of (schedule, instant), so this output is
// byte-for-byte reproducible from run to run, and the daylight-saving section can sit on the
// exact 2026 transition dates.
//
// The sample is split by topic, one group per file: RecurrenceSamples, DstSamples,
// TimeWindowSamples, TimeProviderSamples, CooldownSamples, CombinedSamples. Run without arguments
// to execute every group, in the canonical order below. Pass one or more group names
// (case-insensitive, any order) to run only those groups, e.g.:
//   dotnet run -- cooldowns
//   dotnet run -- dst windows
// An unrecognized group name prints the list of available groups instead of running anything.

using System.Globalization;

// Formatting only: keeps the month names and separators below identical on every machine.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// The canonical order: also the order groups run in when no filter is given, regardless of the
// order group names are passed on the command line.
var groups = new (string Name, Action Run)[]
{
    ("recurrence", RecurrenceSamples.Run),
    ("dst", DstSamples.Run),
    ("windows", TimeWindowSamples.Run),
    ("timeprovider", TimeProviderSamples.Run),
    ("cooldowns", CooldownSamples.Run),
    ("combined", CombinedSamples.Run),
};

string[] selected;
if (args.Length == 0)
{
    selected = groups.Select(g => g.Name).ToArray();
}
else
{
    var requested = args.Select(a => a.ToLowerInvariant()).ToArray();
    var unknown = requested.Where(a => groups.All(g => g.Name != a)).ToArray();
    if (unknown.Length > 0)
    {
        Console.WriteLine($"Unknown group(s): {string.Join(", ", unknown)}");
        Console.WriteLine($"Available groups: {string.Join(", ", groups.Select(g => g.Name))}");
        return;
    }

    selected = groups.Select(g => g.Name).Where(requested.Contains).ToArray();
}

Console.WriteLine("== SsalKit.Timekeeping sample ==");
Console.WriteLine();

foreach (var name in selected)
{
    groups.First(g => g.Name == name).Run();
}
