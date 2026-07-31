using System.Collections.Immutable;
using System.Globalization;
using SsalKit.Determinism;
using SsalKit.StableHashing;

// [Replay]
internal static class ReplaySamples
{
    public static void Run()
    {
        // ---------------------------------------------------------------------------------------
        // 3. Input recording and replay. A run is fully described by (seed, commands) -- nothing
        //    else enters the simulation -- so a recording is a couple of dozen bytes of text, and
        //    replaying it reproduces every intermediate state, not just the final one. That is what
        //    makes replays usable as bug reports, as regression fixtures, and as the audit trail for
        //    "what actually happened in match #4172".
        //
        //    The recording below is round-tripped through text on purpose: the replay is driven by
        //    the parsed copy, so nothing in-memory can be quietly shared between the two runs.
        // ---------------------------------------------------------------------------------------
        var recording = new BattleRecording(BattleScript.Seed, BattleScript.Commands);
        var liveTrace = ReplayRunner.Run(recording);

        var persisted = ReplayRunner.Persist(recording);
        var restored = ReplayRunner.Parse(persisted);
        var replayTrace = ReplayRunner.Run(restored);

        var tracesMatch = liveTrace.SequenceEqual(replayTrace);

        Console.WriteLine("[Replay]         a run recorded as (seed, commands), persisted as text, replayed from that text");
        Console.WriteLine($"                 recording        {persisted}");
        Console.WriteLine($"                 live run         {liveTrace.Length} ticks, final checksum {liveTrace[^1]}");
        Console.WriteLine($"                 replayed run     {replayTrace.Length} ticks, final checksum {replayTrace[^1]}");
        Console.WriteLine($"                 final checksums identical:  {liveTrace[^1] == replayTrace[^1]}");
        Console.WriteLine($"                 every intermediate state too: {tracesMatch}  ({liveTrace.Length} of {replayTrace.Length} ticks)");
        Console.WriteLine("                 the entire replay path -- ReplayRunner and BattleSimulation -- is [Deterministic],");
        Console.WriteLine("                 so a clock or a Guid slipping into it fails the build rather than the replay.");
        Console.WriteLine();
    }
}

/// <summary>A recorded run: the only two inputs a <see cref="BattleSimulation"/> has.</summary>
/// <param name="Seed">The seed the run's <c>DeterministicRandom</c> was built from.</param>
/// <param name="Commands">The command issued on each tick, in order.</param>
internal sealed record BattleRecording(ulong Seed, ImmutableArray<BattleCommand> Commands);

/// <summary>
/// The replay path, marked in full: a recording goes in, the per-tick checksum trace comes out.
/// </summary>
/// <remarks>
/// Marking this type as well as <see cref="BattleSimulation"/> is the point. The analysis is shallow
/// -- it only sees direct calls -- so a <c>Guid.NewGuid()</c> used to tag a replayed event, or a
/// <c>DateTimeOffset.UtcNow</c> stamped onto a parsed command here, would be invisible to the
/// marking on the simulation type. Every type on a deterministic path carries its own marking.
/// </remarks>
[Deterministic]
internal static class ReplayRunner
{
    private const char SeedSeparator = '|';
    private const char CommandSeparator = ',';

    /// <summary>Runs a recording and returns the checksum of the state after every tick.</summary>
    /// <param name="recording">The recording to run.</param>
    /// <returns>One checksum per command in <paramref name="recording"/>, in tick order.</returns>
    public static ImmutableArray<StableHash64> Run(BattleRecording recording)
    {
        var simulation = new BattleSimulation(recording.Seed);
        var trace = ImmutableArray.CreateBuilder<StableHash64>(recording.Commands.Length);

        foreach (var command in recording.Commands)
        {
            simulation.Advance(command);
            trace.Add(simulation.Checksum);
        }

        return trace.ToImmutable();
    }

    /// <summary>Renders a recording as the text that would be stored or attached to a bug report.</summary>
    /// <param name="recording">The recording to render.</param>
    /// <returns>The recording as <c>seed|command,command,...</c>.</returns>
    public static string Persist(BattleRecording recording)
    {
        var commands = recording.Commands.Select(c => c.ToString().ToLowerInvariant());

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{recording.Seed}{SeedSeparator}{string.Join(CommandSeparator, commands)}");
    }

    /// <summary>Parses text produced by <see cref="Persist"/> back into a recording.</summary>
    /// <param name="persisted">The persisted recording.</param>
    /// <returns>The parsed recording.</returns>
    public static BattleRecording Parse(string persisted)
    {
        var parts = persisted.Split(SeedSeparator);
        var seed = ulong.Parse(parts[0], CultureInfo.InvariantCulture);
        var commands = parts[1]
            .Split(CommandSeparator)
            .Select(name => Enum.Parse<BattleCommand>(name, ignoreCase: true))
            .ToImmutableArray();

        return new BattleRecording(seed, commands);
    }
}
