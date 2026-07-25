namespace SsalKit.Guard.Tests;

public sealed class GuardExceptionFactoryTests
{
    [Fact]
    public void That_SatisfiedCondition_NeverInvokesTheFactory()
    {
        int invocations = 0;

        Guard.That(true, () =>
        {
            invocations++;
            return new InvalidOperationException();
        });

        Assert.Equal(0, invocations);
    }

    [Fact]
    public void That_FailedCondition_ThrowsExactlyWhatTheFactoryReturned()
    {
        var expected = new InvalidOperationException("domain says no");

        var actual = Assert.Throws<InvalidOperationException>(() => Guard.That(false, () => expected));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void That_FailedCondition_InvokesTheFactoryExactlyOnce()
    {
        int invocations = 0;

        Assert.Throws<InvalidOperationException>(() => Guard.That(false, () =>
        {
            invocations++;
            return new InvalidOperationException();
        }));

        Assert.Equal(1, invocations);
    }

    [Fact]
    public void That_NullFactory_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            static () => Guard.That(true, (Func<Exception>)null!));

        Assert.Equal("exceptionFactory", exception.ParamName);
    }

    [Fact]
    public void That_FactoryReturningNull_ThrowsAGuardViolationNamingTheClause()
    {
        var exception = Assert.Throws<GuardViolationException>(
            static () => Guard.That(false, static () => null!));

        Assert.Equal(
            "Guard.That failed, but the supplied exception factory returned null.",
            exception.Message);
    }

    [Fact]
    public void NotNull_NonNullValue_NeverInvokesTheFactory()
    {
        int invocations = 0;

        object returned = Guard.NotNull(new object(), () =>
        {
            invocations++;
            return new InvalidOperationException();
        });

        Assert.NotNull(returned);
        Assert.Equal(0, invocations);
    }

    [Fact]
    public void NotNull_NullValue_ThrowsExactlyWhatTheFactoryReturned()
    {
        var expected = new InvalidOperationException("no player");
        string? candidate = null;

        var actual = Assert.Throws<InvalidOperationException>(
            () => Guard.NotNull(candidate, () => expected));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void NotNull_NullFactory_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            static () => Guard.NotNull(new object(), (Func<Exception>)null!));

        Assert.Equal("exceptionFactory", exception.ParamName);
    }

    [Fact]
    public void NotNull_FactoryReturningNull_ThrowsAGuardViolationNamingTheClause()
    {
        string? candidate = null;

        var exception = Assert.Throws<GuardViolationException>(
            () => Guard.NotNull(candidate, static () => null!));

        Assert.Equal(
            "Guard.NotNull failed, but the supplied exception factory returned null.",
            exception.Message);
    }

    [Fact]
    public void NotNull_WithFactory_FlowsNonNullabilityToTheCaller()
    {
        string? candidate = MaybeNull("value");

        string returned = Guard.NotNull(candidate, static () => new InvalidOperationException());

        Assert.Equal(5, returned.Length);
        Assert.Equal(5, candidate.Length);
    }

    private static string? MaybeNull(string? value) => value;
}
