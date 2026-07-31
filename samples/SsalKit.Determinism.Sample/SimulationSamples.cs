// [Simulation]
internal static class SimulationSamples
{
    public static void Run()
    {
        // ---------------------------------------------------------------------------------------
        // 1. The lockstep core. Two BattleSimulation instances built from the same seed and fed the
        //    same command script stay in step tick for tick -- not because they are synchronized,
        //    but because neither of them has anything to drift on: no ambient clock, no process
        //    randomness, no per-process hash seed. See BattleSimulation.cs for how each of those
        //    three is replaced, and by which library.
        //
        //    This group is also the sample's regression test. BattleSimulation is [Deterministic],
        //    so if a DateTime.Now or a Random.Shared ever appears in it the analyzer reports it, and
        //    TreatWarningsAsErrors turns that report into a failed build.
        // ---------------------------------------------------------------------------------------
        var alice = new BattleSimulation(BattleScript.Seed);
        var bob = new BattleSimulation(BattleScript.Seed);

        Console.WriteLine($"[Simulation]     two BattleSimulation instances, seed {BattleScript.Seed}, identical command script");
        Console.WriteLine("                 tick  command  player  enemy  pending  checksum          in step");

        var inStep = true;

        for (var i = 0; i < BattleScript.Commands.Length; i++)
        {
            var command = BattleScript.Commands[i];

            alice.Advance(command);
            bob.Advance(command);

            var left = alice.State;
            var matches = alice.Checksum == bob.Checksum;
            inStep &= matches;

            Console.WriteLine(
                $"                 {left.Tick,4}  {BattleScript.Label(command)}  {left.PlayerHp,6}  {left.EnemyHp,5}  {left.PendingEvents,7}  {alice.Checksum}  {matches}");
        }

        Console.WriteLine($"                 in step for all {BattleScript.Commands.Length} ticks: {inStep}");
        Console.WriteLine("                 (the checksum column is reproducible: the same values print on every run and machine)");
        Console.WriteLine();
    }
}
