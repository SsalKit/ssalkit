// SsalKit.Determinism sample
//
// The one sample that uses the whole family at once: a [Deterministic] simulation core written
// against DeterministicRandom (SsalKit.Randomness), Cooldown/TickSchedule (SsalKit.Timekeeping),
// and ComputeStableHash() (SsalKit.StableHashing) -- the three replacements the analyzer's messages
// point at -- with the analyzer itself wired in as an analyzer on this project.
//
// The most important thing this sample outputs is nothing at all: because the repository builds
// with TreatWarningsAsErrors, the fact that it compiles is the proof that no SSALD diagnostic fires
// anywhere in the deterministic cores below. Violations.cs holds the opposite demonstration -- one
// violation from every category -- and is excluded from the default build by an #if; see the
// [Showcase] group and the comment at the top of that file for how to turn it on.
//
// The groups follow the README's usage guide one for one: a lockstep simulation core, run-to-run
// desync detection, input recording and replay, deterministic cache keys and bucketing, a testable
// domain core that takes its clock and its randomness as arguments, and the legitimate escape
// hatch. Run without arguments to execute every group, in the canonical order below. Pass one or
// more group names (case-insensitive, any order) to run only those, e.g.:
//   dotnet run -- simulation
//   dotnet run -- desync replay
//   dotnet run -- showcase
// An unrecognized group name prints the list of available groups instead of running anything.
//
// Every input below is a fixed literal -- seeds, command scripts, instants -- so the output is
// byte-for-byte identical from run to run and machine to machine. The two places that deliberately
// touch the wall clock ([Desync]'s corruption injection and [OptOut]'s logging path) are isolated
// behind [AllowNonDeterminism] and never print a value derived from it, only whether the reading
// happened and which tick the resulting divergence was caught at.

using System.Globalization;

// Formatting only: keeps number and separator rendering identical on every machine.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

// The canonical order: also the order groups run in when no filter is given, regardless of the
// order group names are passed on the command line.
var groups = new (string Name, Action Run)[]
{
    ("simulation", SimulationSamples.Run),
    ("desync", DesyncSamples.Run),
    ("replay", ReplaySamples.Run),
    ("fingerprint", FingerprintSamples.Run),
    ("testablecore", TestableCoreSamples.Run),
    ("optout", OptOutSamples.Run),
    ("showcase", ShowcaseSamples.Run),
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

Console.WriteLine("== SsalKit.Determinism sample ==");
Console.WriteLine();

foreach (var name in selected)
{
    groups.First(g => g.Name == name).Run();
}
