// [Showcase]
internal static class ShowcaseSamples
{
    public static void Run()
    {
        // ---------------------------------------------------------------------------------------
        // 7. Where to see the diagnostics themselves. Everything above demonstrates the analyzer
        //    being quiet, which is what it should be doing in code that is already written the right
        //    way -- but "quiet" is hard to learn from. Violations.cs holds one violation from every
        //    category, with the replacement noted beside each line.
        //
        //    It is excluded from the default build by #if DETERMINISM_SHOWCASE, because this
        //    repository builds with TreatWarningsAsErrors: the two demands ("no warnings, ever" and
        //    "show me the warnings") are met by putting the second one behind a constant.
        // ---------------------------------------------------------------------------------------
        Console.WriteLine("[Showcase]       Violations.cs -- one violation per category, excluded from the default build");
        Console.WriteLine("                 enable it with:");
        Console.WriteLine("                   dotnet build samples/SsalKit.Determinism.Sample -p:DefineConstants=DETERMINISM_SHOWCASE");
        Console.WriteLine("                 the build then fails (TreatWarningsAsErrors), reporting:");
        Console.WriteLine("                   SSALD001  ambient time        DateTime.Now, TimeProvider.System, Stopwatch, TickCount");
        Console.WriteLine("                   SSALD002  randomness          Random.Shared, new Random(seed), RandomNumberGenerator");
        Console.WriteLine("                   SSALD003  identifiers         Guid.NewGuid, Guid.CreateVersion7");
        Console.WriteLine("                   SSALD004  randomized hashing  GetHashCode, HashCode.Combine, StringComparer");
        Console.WriteLine("                   SSALD005  environment         MachineName, ProcessId, Process, Thread, temp paths");
        Console.WriteLine("                   SSALD006  scheduling          Task.Run/Delay, Thread.Sleep, Parallel, timers");
        Console.WriteLine("                   SSALD007  orphan exemption    [AllowNonDeterminism] outside any [Deterministic] scope");
        Console.WriteLine("                 in a consuming project these are warnings, not errors -- every SSALD rule defaults to");
        Console.WriteLine("                 Warning and always will; raise the severity per id in .editorconfig for a build gate.");
        Console.WriteLine();
    }
}
