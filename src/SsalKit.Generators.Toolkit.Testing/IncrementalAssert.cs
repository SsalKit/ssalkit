using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;

namespace SsalKit.Generators.Toolkit.Testing;

/// <summary>
/// Asserts on an incremental generator's caching behaviour -- the property that decides whether the
/// generator is fast in an IDE, and the one thing a snapshot test can never catch.
/// </summary>
/// <remarks>
/// <para>
/// Both assertions read the tracked steps of the <em>second</em> of two runs sharing one driver,
/// which is what <see cref="GeneratorTest.RunTwice{TGenerator}"/> and
/// <see cref="GeneratorTest.RunTwiceWithCompilationChange{TGenerator}"/> produce. Roslyn records a
/// reason per step output: <c>Cached</c> (the step was skipped entirely), <c>Unchanged</c> (the step
/// re-ran but produced a value equal to last time, so nothing downstream re-ran), <c>Modified</c>,
/// and <c>New</c>.
/// </para>
/// <para>
/// The two assertions are the two halves of a caching contract. A model that forgets value equality
/// -- a raw <c>ISymbol</c>, a <c>Location</c>, an array instead of an equatable one -- fails
/// <see cref="AllCachedOrUnchanged"/>; a model that drops a field the emitter actually uses passes
/// it but fails <see cref="SomeOutputRecomputed"/>, because a real edit would silently keep the
/// stale output.
/// </para>
/// </remarks>
public static class IncrementalAssert
{
    /// <summary>
    /// Asserts that no output of the named steps was recomputed in the second run -- every one is
    /// <c>Cached</c> or <c>Unchanged</c>.
    /// </summary>
    /// <param name="secondRun">The second run of a two-run pair sharing one driver.</param>
    /// <param name="trackingNames">The <c>WithTrackingName</c> names to check.</param>
    /// <exception cref="ArgumentException"><paramref name="trackingNames"/> is empty.</exception>
    /// <exception cref="GeneratorAssertionException">A name was never tracked, or one of its
    /// outputs was recomputed; the message tabulates every named step's cache state.</exception>
    public static void AllCachedOrUnchanged(GeneratorTestResult secondRun, params string[] trackingNames)
    {
        ArgumentNullException.ThrowIfNull(secondRun);
        var trackedSteps = RequireNames(secondRun, trackingNames);

        foreach (var trackingName in trackingNames)
        {
            if (!trackedSteps.TryGetValue(trackingName, out var steps))
            {
                throw Failure(secondRun, trackingNames, $"No tracked steps were recorded for '{trackingName}'.");
            }

            Debug.Assert(!steps.IsEmpty, "Roslyn never records a tracking name with zero steps.");

            var recomputed = steps
                .SelectMany(static step => step.Outputs)
                .Where(static output => output.Reason is not IncrementalStepRunReason.Cached)
                .Where(static output => output.Reason is not IncrementalStepRunReason.Unchanged)
                .ToImmutableArray();

            if (!recomputed.IsEmpty)
            {
                throw Failure(
                    secondRun,
                    trackingNames,
                    $"Expected every output of step '{trackingName}' to be Cached or Unchanged after the second " +
                    $"run, but {recomputed.Length} of them was recomputed.");
            }
        }
    }

    /// <summary>
    /// Asserts that at least one output of each named step was recomputed in the second run --
    /// <c>Modified</c> or <c>New</c>.
    /// </summary>
    /// <param name="secondRun">The second run of a two-run pair sharing one driver.</param>
    /// <param name="trackingNames">The <c>WithTrackingName</c> names to check.</param>
    /// <exception cref="ArgumentException"><paramref name="trackingNames"/> is empty.</exception>
    /// <exception cref="GeneratorAssertionException">A name was never tracked, or every one of its
    /// outputs was reused even though the change was supposed to invalidate them; the message
    /// tabulates every named step's cache state.</exception>
    public static void SomeOutputRecomputed(GeneratorTestResult secondRun, params string[] trackingNames)
    {
        ArgumentNullException.ThrowIfNull(secondRun);
        var trackedSteps = RequireNames(secondRun, trackingNames);

        foreach (var trackingName in trackingNames)
        {
            if (!trackedSteps.TryGetValue(trackingName, out var steps))
            {
                throw Failure(secondRun, trackingNames, $"No tracked steps were recorded for '{trackingName}'.");
            }

            Debug.Assert(!steps.IsEmpty, "Roslyn never records a tracking name with zero steps.");

            var recomputed = steps
                .SelectMany(static step => step.Outputs)
                .Where(static output => output.Reason is not IncrementalStepRunReason.Cached)
                .Any(static output => output.Reason is not IncrementalStepRunReason.Unchanged);

            if (!recomputed)
            {
                throw Failure(
                    secondRun,
                    trackingNames,
                    $"Expected at least one output of step '{trackingName}' to be Modified or New after a change " +
                    "the pipeline's models capture, but every output was reused.");
            }
        }
    }

    private static ImmutableDictionary<string, ImmutableArray<IncrementalGeneratorRunStep>> RequireNames(
        GeneratorTestResult secondRun, string[] trackingNames)
    {
        ArgumentNullException.ThrowIfNull(trackingNames);

        if (trackingNames.Length == 0)
        {
            throw new ArgumentException("At least one tracking name must be supplied.", nameof(trackingNames));
        }

        return secondRun.TrackedSteps;
    }

    private static GeneratorAssertionException Failure(
        GeneratorTestResult secondRun, string[] trackingNames, string reason)
    {
        var trackedSteps = secondRun.TrackedSteps;
        var message = new StringBuilder(reason);

        message.AppendLine().AppendLine().AppendLine("Cache state of the requested steps:");

        foreach (var trackingName in trackingNames)
        {
            if (!trackedSteps.TryGetValue(trackingName, out var steps))
            {
                message.Append("  ").Append(trackingName).AppendLine(" -> (never tracked)");
                continue;
            }

            for (var index = 0; index < steps.Length; index++)
            {
                message
                    .Append("  ").Append(trackingName).Append('[').Append(index).Append("] -> ")
                    .AppendLine(string.Join(", ", steps[index].Outputs.Select(static output => output.Reason)));
            }
        }

        message.AppendLine().AppendLine("Tracking names recorded by this run:");

        foreach (var name in trackedSteps.Keys.OrderBy(static name => name, StringComparer.Ordinal))
        {
            message.Append("  - ").AppendLine(name);
        }

        return new GeneratorAssertionException(message.ToString());
    }
}
