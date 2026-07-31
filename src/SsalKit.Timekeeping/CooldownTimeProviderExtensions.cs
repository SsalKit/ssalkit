namespace SsalKit.Timekeeping;

/// <summary>
/// Convenience overloads of <see cref="Cooldown"/> and <see cref="RechargePool"/> that read the
/// current instant from a <see cref="TimeProvider"/> instead of taking it as an argument.
/// </summary>
/// <remarks>
/// <para>
/// The core <see cref="Cooldown"/> and <see cref="RechargePool"/> APIs deliberately take the current
/// instant as a parameter — that is what makes every operation a pure function and what keeps
/// cooldown and recharge logic testable without freezing a global clock. These extensions are sugar
/// over that, forwarding <see cref="TimeProvider.GetUtcNow"/> exactly once per call, for the common
/// case where the caller already holds an injected clock. See
/// <see cref="RecurrenceScheduleTimeProviderExtensions"/> for the same pattern applied to
/// <see cref="RecurrenceSchedule"/>.
/// </para>
/// <para>
/// <see cref="TimeProvider"/> is part of the base class library from .NET 8 onward, so using these
/// overloads adds no package dependency. In tests, pass a fake provider (for example
/// <c>Microsoft.Extensions.Time.Testing.FakeTimeProvider</c>, or a few lines of your own deriving
/// from <see cref="TimeProvider"/>) to drive a cooldown or a pool across a boundary deterministically.
/// </para>
/// </remarks>
public static class CooldownTimeProviderExtensions
{
    /// <summary>
    /// Determines whether the cooldown is usable at the provider's current instant.
    /// </summary>
    /// <param name="cooldown">The cooldown.</param>
    /// <param name="timeProvider">The clock to read the current instant from.</param>
    /// <returns>The result of <see cref="Cooldown.IsReady(DateTimeOffset)"/> at
    /// <see cref="TimeProvider.GetUtcNow"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is
    /// <see langword="null"/>.</exception>
    public static bool IsReady(this Cooldown cooldown, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return cooldown.IsReady(timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Returns how much longer the cooldown has left, measured from the provider's current instant.
    /// </summary>
    /// <param name="cooldown">The cooldown.</param>
    /// <param name="timeProvider">The clock to read the current instant from.</param>
    /// <returns>The result of <see cref="Cooldown.Remaining(DateTimeOffset)"/> at
    /// <see cref="TimeProvider.GetUtcNow"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is
    /// <see langword="null"/>.</exception>
    public static TimeSpan Remaining(this Cooldown cooldown, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return cooldown.Remaining(timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Attempts to use the cooldown at the provider's current instant.
    /// </summary>
    /// <param name="cooldown">The cooldown.</param>
    /// <param name="timeProvider">The clock to read the current instant from.</param>
    /// <param name="updated">The result of <see cref="Cooldown.TryUse(DateTimeOffset, out Cooldown)"/>
    /// at <see cref="TimeProvider.GetUtcNow"/>.</param>
    /// <returns>The result of <see cref="Cooldown.TryUse(DateTimeOffset, out Cooldown)"/> at
    /// <see cref="TimeProvider.GetUtcNow"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is
    /// <see langword="null"/>.</exception>
    public static bool TryUse(this Cooldown cooldown, TimeProvider timeProvider, out Cooldown updated)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return cooldown.TryUse(timeProvider.GetUtcNow(), out updated);
    }

    /// <summary>
    /// Returns a cooldown that is immediately usable at the provider's current instant.
    /// </summary>
    /// <param name="cooldown">The cooldown.</param>
    /// <param name="timeProvider">The clock to read the current instant from.</param>
    /// <returns>The result of <see cref="Cooldown.Reset(DateTimeOffset)"/> at
    /// <see cref="TimeProvider.GetUtcNow"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is
    /// <see langword="null"/>.</exception>
    public static Cooldown Reset(this Cooldown cooldown, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return cooldown.Reset(timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Returns how many units are available at the provider's current instant.
    /// </summary>
    /// <param name="pool">The pool.</param>
    /// <param name="timeProvider">The clock to read the current instant from.</param>
    /// <returns>The result of <see cref="RechargePool.AvailableAt(DateTimeOffset)"/> at
    /// <see cref="TimeProvider.GetUtcNow"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is
    /// <see langword="null"/>.</exception>
    public static int AvailableAt(this RechargePool pool, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return pool.AvailableAt(timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Attempts to consume <paramref name="amount"/> units at the provider's current instant.
    /// </summary>
    /// <param name="pool">The pool.</param>
    /// <param name="timeProvider">The clock to read the current instant from.</param>
    /// <param name="amount">The number of units to consume.</param>
    /// <param name="updated">The result of
    /// <see cref="RechargePool.TryConsume(DateTimeOffset, int, out RechargePool)"/> at
    /// <see cref="TimeProvider.GetUtcNow"/>.</param>
    /// <returns>The result of
    /// <see cref="RechargePool.TryConsume(DateTimeOffset, int, out RechargePool)"/> at
    /// <see cref="TimeProvider.GetUtcNow"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is
    /// <see langword="null"/>.</exception>
    public static bool TryConsume(
        this RechargePool pool,
        TimeProvider timeProvider,
        int amount,
        out RechargePool updated)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return pool.TryConsume(timeProvider.GetUtcNow(), amount, out updated);
    }

    /// <summary>
    /// Returns how long until the next unit becomes available, measured from the provider's current
    /// instant.
    /// </summary>
    /// <param name="pool">The pool.</param>
    /// <param name="timeProvider">The clock to read the current instant from.</param>
    /// <returns>The result of <see cref="RechargePool.UntilNextCharge(DateTimeOffset)"/> at
    /// <see cref="TimeProvider.GetUtcNow"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is
    /// <see langword="null"/>.</exception>
    public static TimeSpan? UntilNextCharge(this RechargePool pool, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return pool.UntilNextCharge(timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Returns how long until the pool is completely full, measured from the provider's current
    /// instant.
    /// </summary>
    /// <param name="pool">The pool.</param>
    /// <param name="timeProvider">The clock to read the current instant from.</param>
    /// <returns>The result of <see cref="RechargePool.UntilFull(DateTimeOffset)"/> at
    /// <see cref="TimeProvider.GetUtcNow"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is
    /// <see langword="null"/>.</exception>
    public static TimeSpan? UntilFull(this RechargePool pool, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return pool.UntilFull(timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Returns a pool with <paramref name="amount"/> units granted at the provider's current instant.
    /// </summary>
    /// <param name="pool">The pool.</param>
    /// <param name="amount">The number of units to grant.</param>
    /// <param name="timeProvider">The clock to read the current instant from.</param>
    /// <returns>The result of <see cref="RechargePool.Grant(int, DateTimeOffset)"/> at
    /// <see cref="TimeProvider.GetUtcNow"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is
    /// <see langword="null"/>.</exception>
    public static RechargePool Grant(this RechargePool pool, int amount, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return pool.Grant(amount, timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Returns a pool that is completely full at the provider's current instant.
    /// </summary>
    /// <param name="pool">The pool.</param>
    /// <param name="timeProvider">The clock to read the current instant from.</param>
    /// <returns>The result of <see cref="RechargePool.Refill(DateTimeOffset)"/> at
    /// <see cref="TimeProvider.GetUtcNow"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is
    /// <see langword="null"/>.</exception>
    public static RechargePool Refill(this RechargePool pool, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return pool.Refill(timeProvider.GetUtcNow());
    }
}
