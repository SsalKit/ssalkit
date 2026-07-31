using SsalKit.Determinism;

// [Desync]
internal static class DesyncSamples
{
    /// <summary>The tick the contaminated run mixes a wall-clock-derived value into its state.</summary>
    private const int InjectionTick = 5;

    public static void Run()
    {
        // ---------------------------------------------------------------------------------------
        // 2. What the analyzer is for, shown from the other side: this is the work you do at run
        //    time when a non-deterministic value reaches simulation state and nobody caught it at
        //    compile time. Two runs of the same seed and the same script are compared checksum by
        //    checksum until they part ways, and the first divergent tick is where to start reading
        //    code. (SsalKit.StableHashing's own sample has the same section from the detection side;
        //    this one is about not needing it.)
        //
        //    The contamination is a single wall-clock reading, mixed into one tick's damage roll --
        //    the smallest realistic version of the bug, and enough to make every later checksum
        //    differ. In real code it arrives as innocuously as a `DateTime.UtcNow.Millisecond % 3`
        //    in a damage formula.
        // ---------------------------------------------------------------------------------------
        var clean = new BattleSimulation(BattleScript.Seed);
        var contaminated = new BattleSimulation(BattleScript.Seed);

        Console.WriteLine($"[Desync]         same seed, same script, but one run mixes a wall-clock value in at tick {InjectionTick}");
        Console.WriteLine("                 tick  clean checksum    checksums match");

        int? divergedAtTick = null;

        for (var i = 0; i < BattleScript.Commands.Length; i++)
        {
            var command = BattleScript.Commands[i];
            var tick = i + 1;

            clean.Advance(command);
            contaminated.Advance(command, tick == InjectionTick ? Contamination.WallClockDrift() : 0);

            var matches = clean.Checksum == contaminated.Checksum;
            divergedAtTick ??= matches ? null : tick;

            Console.WriteLine($"                 {tick,4}  {clean.Checksum}  {matches}");
        }

        Console.WriteLine($"                 first divergent tick -> {divergedAtTick}");
        Console.WriteLine("                 only the clean run's checksums are printed: the contaminated run's differ on every");
        Console.WriteLine("                 run of this program, which is precisely the defect being demonstrated.");
        Console.WriteLine("                 Compile-time equivalent: drop the [AllowNonDeterminism] from Contamination and the");
        Console.WriteLine("                 wall-clock read below is reported as SSALD001 -- no run, no bisect, no checksums.");
        Console.WriteLine();
    }
}

/// <summary>
/// The deliberate defect the group above hunts down, isolated behind the opt-out attribute so the
/// rest of the sample still builds warning-free.
/// </summary>
/// <remarks>
/// The type is <c>[Deterministic]</c> and the member is exempted from it, rather than the type
/// simply being left unmarked: an <c>[AllowNonDeterminism]</c> outside every <c>[Deterministic]</c>
/// scope suppresses nothing and is itself reported as SSALD007.
/// </remarks>
[Deterministic]
internal static class Contamination
{
    /// <summary>Produces a wall-clock-derived value, on purpose, to make the desync above real.</summary>
    /// <returns><c>1</c> or <c>2</c>, depending on the clock.</returns>
    [AllowNonDeterminism(Justification = "deliberate defect: this sample needs one genuinely non-reproducible value to demonstrate desync detection")]
    public static int WallClockDrift() =>
        // Without the attribute above, this line is SSALD001 -- and this is the only reading in the
        // whole sample that is allowed to reach simulation state. The value stays in {1, 2} so the
        // divergence, once it happens, never accidentally cancels itself out on a later tick.
        1 + (int)(DateTime.UtcNow.Ticks & 1L);
}
