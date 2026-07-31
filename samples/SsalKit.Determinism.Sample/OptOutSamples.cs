using SsalKit.Determinism;
using SsalKit.StableHashing;

// [OptOut]
internal static class OptOutSamples
{
    public static void Run()
    {
        // ---------------------------------------------------------------------------------------
        // 6. The legitimate escape hatch. Real deterministic code still has to tell an operator what
        //    it is doing, and an operator log wants a wall-clock stamp and a host name -- neither of
        //    which is a determinism bug, because neither ever reaches simulation state.
        //
        //    [AllowNonDeterminism] is the reviewable way to say so: it names the whole member rather
        //    than one call site, it sits in the declaration a reader is already looking at, and its
        //    Justification records why the exemption is safe. Nothing requires the justification --
        //    no diagnostic asks for it -- but an unexplained exemption tells the next reader nothing
        //    and is indistinguishable from a mistake.
        //
        //    Note where the attribute lives: on a member of a [Deterministic] type. Outside every
        //    such scope it suppresses nothing, and an orphan application is itself SSALD007.
        // ---------------------------------------------------------------------------------------
        var simulation = new BattleSimulation(BattleScript.Seed);

        Console.WriteLine("[OptOut]         a deterministic run with a wall-clock operator log attached");

        for (var i = 0; i < 3; i++)
        {
            simulation.Advance(BattleScript.Commands[i]);

            var state = simulation.State;
            Console.WriteLine($"                 {ProgressReporter.Describe(state.Tick, simulation.Checksum)}");
            ProgressReporter.LogForOperator(state.Tick, simulation.Checksum);
        }

        Console.WriteLine($"                 operator log lines written: {ProgressReporter.LoggedLineCount}");
        Console.WriteLine("                 their contents are not printed here: every line carries a wall-clock stamp and this");
        Console.WriteLine("                 host's name, so printing them would make this sample's output differ per run and per");
        Console.WriteLine("                 machine. That is exactly the property the exemption is asserting is harmless.");
        Console.WriteLine();
    }
}

/// <summary>
/// Progress reporting for a deterministic run: one member that is part of the run, and one that is
/// deliberately not.
/// </summary>
[Deterministic]
internal static class ProgressReporter
{
    private static readonly List<string> OperatorLog = [];

    /// <summary>Gets the number of lines written to the operator log so far.</summary>
    public static int LoggedLineCount => OperatorLog.Count;

    /// <summary>
    /// Renders the reproducible progress line -- fully in scope, and analyzed as such: a timestamp
    /// added to this string would be SSALD001.
    /// </summary>
    /// <param name="tick">The tick just completed.</param>
    /// <param name="checksum">The checksum of the state after that tick.</param>
    /// <returns>The rendered line.</returns>
    public static string Describe(long tick, StableHash64 checksum) => $"tick {tick}  checksum {checksum}";

    /// <summary>Appends an operator-facing log line, stamped with the wall clock and the host name.</summary>
    /// <param name="tick">The tick just completed.</param>
    /// <param name="checksum">The checksum of the state after that tick.</param>
    [AllowNonDeterminism(Justification = "wall-clock and host identity for the operator log only; the stamped values never reach simulation state or a checksum")]
    public static void LogForOperator(long tick, StableHash64 checksum)
    {
        // Without the attribute above, these two readings are SSALD001 and SSALD005 respectively.
        // With it, the analyzer stays silent here -- and only here.
        var line = $"{DateTime.UtcNow:O} [{Environment.MachineName}] tick {tick} checksum {checksum}";

        OperatorLog.Add(line);
    }
}
