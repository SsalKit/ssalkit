using System.Globalization;
using SsalKit.Determinism;
using SsalKit.Randomness;
using SsalKit.Timekeeping;

// [TestableCore]
internal static class TestableCoreSamples
{
    private static readonly DateTimeOffset MondayMorning = new(2026, 7, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset MondayEvening = new(2026, 7, 27, 21, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset TuesdayMorning = new(2026, 7, 28, 6, 0, 0, TimeSpan.Zero);

    public static void Run()
    {
        // ---------------------------------------------------------------------------------------
        // 5. The everyday case: an ordinary domain service, not a simulation. Both of the things
        //    that usually make such a service awkward to test -- what time it is and what the dice
        //    said -- arrive as arguments, so a test names them instead of mocking a static, and the
        //    service reads exactly as it runs.
        //
        //    Marking the type [Deterministic] is how that stays true. The convention "take asOf and
        //    IRandomSource, never reach for the ambient ones" is normally enforced by code review,
        //    which works until the afternoon it does not; here the compiler enforces it, and the
        //    first DateTimeOffset.UtcNow added to this class fails the build.
        // ---------------------------------------------------------------------------------------
        var firstPass = RunClaimScript();
        var secondPass = RunClaimScript();

        Console.WriteLine("[TestableCore]   a reward service whose clock and dice are both parameters");
        Console.WriteLine("                 when         granted  gold  retry in");

        foreach (var line in firstPass)
        {
            Console.WriteLine($"                 {line}");
        }

        Console.WriteLine($"                 the same script, run a second time from the same inputs: identical  {firstPass.SequenceEqual(secondPass)}");
        Console.WriteLine("                 no mocks, no injected clock abstraction, no [ThreadStatic] ambient state -- the two");
        Console.WriteLine("                 non-deterministic inputs are simply parameters, and the analyzer keeps them that way.");
        Console.WriteLine();
    }

    /// <summary>
    /// Plays the same three claim attempts against a fresh service and a freshly seeded source.
    /// </summary>
    /// <returns>One rendered line per attempt.</returns>
    private static string[] RunClaimScript()
    {
        var service = new DailyRewardService();

        // Both "ambient" inputs, created explicitly at the composition root rather than reached for
        // from inside the domain code.
        IRandomSource dice = new DeterministicRandom(7);
        var claim = Cooldown.Create(DailyRewardService.ClaimInterval, MondayMorning);

        var lines = new List<string>();

        foreach (var asOf in new[] { MondayMorning, MondayEvening, TuesdayMorning })
        {
            var outcome = service.Claim(claim, asOf, dice);
            claim = outcome.NextClaim;

            var retry = outcome.RetryAfter == TimeSpan.Zero
                ? "-"
                : $"{outcome.RetryAfter.TotalHours.ToString("0.0", CultureInfo.InvariantCulture)}h";

            lines.Add($"{asOf.ToString("ddd HH:mm", CultureInfo.InvariantCulture),-11}  {outcome.Granted,-7}  {outcome.Gold,4}  {retry}");
        }

        return [.. lines];
    }
}

/// <summary>The result of one claim attempt, including the cooldown to carry forward.</summary>
/// <param name="Granted">Whether the reward was granted.</param>
/// <param name="Gold">The gold granted, or <c>0</c>.</param>
/// <param name="RetryAfter">How long until the next attempt can succeed, or <see cref="TimeSpan.Zero"/>.</param>
/// <param name="NextClaim">The cooldown state to store for the next attempt.</param>
internal readonly record struct RewardOutcome(bool Granted, int Gold, TimeSpan RetryAfter, Cooldown NextClaim);

/// <summary>
/// A domain service with no hidden inputs: the instant and the randomness are both parameters.
/// </summary>
[Deterministic]
internal sealed class DailyRewardService
{
    /// <summary>The wait between two successful claims.</summary>
    public static readonly TimeSpan ClaimInterval = TimeSpan.FromHours(20);

    /// <summary>Attempts to claim the daily reward.</summary>
    /// <param name="claim">The stored cooldown state.</param>
    /// <param name="asOf">The instant the attempt is evaluated at -- an argument, never a reading.
    /// Every SsalKit.Timekeeping type is specified this way for the same reason.</param>
    /// <param name="dice">The randomness for the roll -- an injected source, so a test can hand in a
    /// seeded <see cref="DeterministicRandom"/> and assert on exact gold amounts.</param>
    /// <returns>The outcome, including the cooldown state to store.</returns>
    public RewardOutcome Claim(Cooldown claim, DateTimeOffset asOf, IRandomSource dice)
    {
        if (!claim.TryUse(asOf, out var nextClaim))
        {
            return new RewardOutcome(Granted: false, Gold: 0, claim.Remaining(asOf), claim);
        }

        var gold = 100 + dice.Next(0, 51);

        return new RewardOutcome(Granted: true, gold, TimeSpan.Zero, nextClaim);
    }
}
