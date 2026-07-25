namespace SsalKit.Guard.Tests;

public sealed class GuardThatTests
{
    [Fact]
    public void That_SatisfiedCondition_DoesNotThrow()
    {
        Guard.That(1 + 1 == 2);
    }

    [Fact]
    public void That_FailedCondition_MessageEmbedsTheCallerExpressionText()
    {
        var exception = Assert.Throws<GuardViolationException>(static () => Guard.That(1 + 1 == 3));

        Assert.Equal("Guard.That (1 + 1 == 3) failed.", exception.Message);
    }

    [Fact]
    public void That_FailedCondition_CapturesTheExpressionVerbatimIncludingLocals()
    {
        int level = 3;

        var exception = Assert.Throws<GuardViolationException>(() => Guard.That(level >= 10));

        Assert.Equal("Guard.That (level >= 10) failed.", exception.Message);
    }

    [Fact]
    public void That_FailedConditionWithNullExpression_UsesThePlaceholder()
    {
        var exception = Assert.Throws<GuardViolationException>(
            static () => Guard.That(false, expression: null));

        Assert.Equal($"Guard.That ({Guard.UnknownExpression}) failed.", exception.Message);
    }

    [Fact]
    public void That_FailedConditionWithEmptyExpression_UsesThePlaceholder()
    {
        var exception = Assert.Throws<GuardViolationException>(
            static () => Guard.That(false, expression: string.Empty));

        Assert.Equal($"Guard.That ({Guard.UnknownExpression}) failed.", exception.Message);
    }

    [Fact]
    public void That_Failure_ThrowsAnErrorCodedException()
    {
        var exception = Assert.Throws<GuardViolationException>(static () => Guard.That(false));

        Assert.IsAssignableFrom<ErrorCodedException>(exception);
    }
}
