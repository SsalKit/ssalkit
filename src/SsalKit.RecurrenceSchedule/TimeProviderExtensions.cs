namespace SsalKit.RecurrenceSchedule;

/// <summary>
/// Convenience overloads that read the current instant from a <see cref="TimeProvider"/> instead of
/// taking it as an argument.
/// </summary>
/// <remarks>
/// <para>
/// The core <see cref="RecurrenceSchedule"/> API deliberately takes the current instant as a
/// parameter — that is what makes every operation a pure function and what keeps schedule logic
/// testable without freezing a global clock. These extensions are sugar over that, forwarding
/// <see cref="TimeProvider.GetUtcNow"/>, for the common case where the caller already holds an
/// injected clock.
/// </para>
/// <para>
/// <see cref="TimeProvider"/> is part of the base class library from .NET 8 onward, so using these
/// overloads adds no package dependency. In tests, pass a fake provider (for example
/// <c>Microsoft.Extensions.Time.Testing.FakeTimeProvider</c>, or a few lines of your own deriving
/// from <see cref="TimeProvider"/>) to drive a schedule across a boundary deterministically.
/// </para>
/// </remarks>
public static class RecurrenceScheduleTimeProviderExtensions
{
    /// <summary>
    /// Returns the window that the provider's current instant belongs to.
    /// </summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="timeProvider">The clock to read the current instant from.</param>
    /// <returns>The result of <see cref="RecurrenceSchedule.CurrentWindow(DateTimeOffset)"/> at
    /// <see cref="TimeProvider.GetUtcNow"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schedule"/> or
    /// <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    public static TimeWindow CurrentWindow(this RecurrenceSchedule schedule, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return schedule.CurrentWindow(timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Determines whether the schedule has fired between <paramref name="lastSeen"/> and the
    /// provider's current instant.
    /// </summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="lastSeen">The previously observed instant, exclusive.</param>
    /// <param name="timeProvider">The clock to read the current instant from.</param>
    /// <returns>The result of
    /// <see cref="RecurrenceSchedule.HasCrossed(DateTimeOffset, DateTimeOffset)"/> at
    /// <see cref="TimeProvider.GetUtcNow"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schedule"/> or
    /// <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    public static bool HasCrossed(
        this RecurrenceSchedule schedule,
        DateTimeOffset lastSeen,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return schedule.HasCrossed(lastSeen, timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Counts the boundaries between <paramref name="lastSeen"/> and the provider's current
    /// instant.
    /// </summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="lastSeen">The previously observed instant, exclusive.</param>
    /// <param name="timeProvider">The clock to read the current instant from.</param>
    /// <returns>The result of
    /// <see cref="RecurrenceSchedule.CountBoundaries(DateTimeOffset, DateTimeOffset)"/> at
    /// <see cref="TimeProvider.GetUtcNow"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schedule"/> or
    /// <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    public static int CountBoundaries(
        this RecurrenceSchedule schedule,
        DateTimeOffset lastSeen,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return schedule.CountBoundaries(lastSeen, timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Returns the next boundary strictly after the provider's current instant.
    /// </summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="timeProvider">The clock to read the current instant from.</param>
    /// <returns>The result of <see cref="RecurrenceSchedule.NextBoundary(DateTimeOffset)"/> at
    /// <see cref="TimeProvider.GetUtcNow"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schedule"/> or
    /// <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    public static DateTimeOffset NextBoundary(this RecurrenceSchedule schedule, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return schedule.NextBoundary(timeProvider.GetUtcNow());
    }
}
