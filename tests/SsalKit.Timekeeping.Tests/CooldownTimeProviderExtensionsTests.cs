namespace SsalKit.Timekeeping.Tests;

public sealed class CooldownTimeProviderExtensionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeProvider Clock = new FixedTimeProvider(Now);

    // ---- Cooldown overloads forward the provider's instant ----

    [Fact]
    public void CooldownOverloads_ForwardTheProvidersInstant()
    {
        var cooldown = Cooldown.Create(TimeSpan.FromMinutes(5), Now.AddMinutes(-1));

        Assert.Equal(cooldown.IsReady(Now), cooldown.IsReady(Clock));
        Assert.Equal(cooldown.Remaining(Now), cooldown.Remaining(Clock));

        Assert.Equal(cooldown.TryUse(Now, out var expected), cooldown.TryUse(Clock, out var actual));
        Assert.Equal(expected, actual);

        Assert.Equal(cooldown.Reset(Now), cooldown.Reset(Clock));
    }

    [Fact]
    public void CooldownOverloads_RejectANullTimeProvider()
    {
        var cooldown = Cooldown.Create(TimeSpan.FromMinutes(5), Now);

        Assert.Throws<ArgumentNullException>(() => cooldown.IsReady(null!));
        Assert.Throws<ArgumentNullException>(() => cooldown.Remaining(null!));
        Assert.Throws<ArgumentNullException>(() => cooldown.TryUse(null!, out _));
        Assert.Throws<ArgumentNullException>(() => cooldown.Reset(null!));
    }

    [Fact]
    public void CooldownOverloads_EachReadTheClockExactlyOnce()
    {
        var cooldown = Cooldown.Create(TimeSpan.FromMinutes(5), Now.AddMinutes(-1));
        var counting = new CountingTimeProvider(Now);

        cooldown.IsReady(counting);
        cooldown.Remaining(counting);
        cooldown.TryUse(counting, out _);
        cooldown.Reset(counting);

        Assert.Equal(4, counting.Reads);
    }

    // ---- RechargePool overloads forward the provider's instant ----

    [Fact]
    public void RechargePoolOverloads_ForwardTheProvidersInstant()
    {
        var pool = RechargePool.Create(5, TimeSpan.FromMinutes(20), Now.AddHours(-1), initialCharges: 2);

        Assert.Equal(pool.AvailableAt(Now), pool.AvailableAt(Clock));
        Assert.Equal(pool.UntilNextCharge(Now), pool.UntilNextCharge(Clock));
        Assert.Equal(pool.UntilFull(Now), pool.UntilFull(Clock));

        Assert.Equal(pool.TryConsume(Now, 1, out var expectedConsume), pool.TryConsume(Clock, 1, out var actualConsume));
        Assert.Equal(expectedConsume, actualConsume);

        Assert.Equal(pool.Grant(1, Now), pool.Grant(1, Clock));
        Assert.Equal(pool.Refill(Now), pool.Refill(Clock));
    }

    [Fact]
    public void RechargePoolOverloads_RejectANullTimeProvider()
    {
        var pool = RechargePool.Create(5, TimeSpan.FromMinutes(20), Now);

        Assert.Throws<ArgumentNullException>(() => pool.AvailableAt(null!));
        Assert.Throws<ArgumentNullException>(() => pool.TryConsume(null!, 1, out _));
        Assert.Throws<ArgumentNullException>(() => pool.UntilNextCharge(null!));
        Assert.Throws<ArgumentNullException>(() => pool.UntilFull(null!));
        Assert.Throws<ArgumentNullException>(() => pool.Grant(1, null!));
        Assert.Throws<ArgumentNullException>(() => pool.Refill(null!));
    }

    [Fact]
    public void RechargePoolOverloads_EachReadTheClockExactlyOnce()
    {
        var pool = RechargePool.Create(5, TimeSpan.FromMinutes(20), Now.AddHours(-1), initialCharges: 2);
        var counting = new CountingTimeProvider(Now);

        pool.AvailableAt(counting);
        pool.UntilNextCharge(counting);
        pool.UntilFull(counting);
        pool.TryConsume(counting, 1, out _);
        pool.Grant(1, counting);
        pool.Refill(counting);

        Assert.Equal(6, counting.Reads);
    }

    private sealed class CountingTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public int Reads { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            Reads++;
            return utcNow;
        }
    }
}
