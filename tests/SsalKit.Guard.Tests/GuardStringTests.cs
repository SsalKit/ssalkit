namespace SsalKit.Guard.Tests;

public sealed class GuardStringTests
{
    [Fact]
    public void NotNullOrEmpty_NonEmptyValue_ReturnsIt()
    {
        string returned = Guard.NotNullOrEmpty("abc");

        Assert.Equal("abc", returned);
    }

    [Fact]
    public void NotNullOrEmpty_WhiteSpaceOnly_IsAccepted()
    {
        string returned = Guard.NotNullOrEmpty("   ");

        Assert.Equal("   ", returned);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NotNullOrEmpty_NullOrEmpty_MessageEmbedsTheCallerExpressionText(string? value)
    {
        var exception = Assert.Throws<GuardViolationException>(() => Guard.NotNullOrEmpty(value));

        Assert.Equal("Guard.NotNullOrEmpty (value) failed: value was null or empty.", exception.Message);
    }

    [Fact]
    public void NotNullOrEmpty_WithNullExpression_UsesThePlaceholder()
    {
        var exception = Assert.Throws<GuardViolationException>(
            static () => Guard.NotNullOrEmpty(null, expression: null));

        Assert.Equal(
            $"Guard.NotNullOrEmpty ({Guard.UnknownExpression}) failed: value was null or empty.",
            exception.Message);
    }

    [Fact]
    public void NotNullOrEmpty_FlowsNonNullabilityToTheCaller()
    {
        string? candidate = MaybeNull("abc");

        _ = Guard.NotNullOrEmpty(candidate);

        Assert.Equal(3, candidate.Length);
    }

    [Fact]
    public void NotNullOrWhiteSpace_NonBlankValue_ReturnsIt()
    {
        string returned = Guard.NotNullOrWhiteSpace(" a ");

        Assert.Equal(" a ", returned);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void NotNullOrWhiteSpace_BlankValue_MessageEmbedsTheCallerExpressionText(string? value)
    {
        var exception = Assert.Throws<GuardViolationException>(() => Guard.NotNullOrWhiteSpace(value));

        Assert.Equal(
            "Guard.NotNullOrWhiteSpace (value) failed: value was null, empty, or white-space.",
            exception.Message);
    }

    [Fact]
    public void NotNullOrWhiteSpace_WithNullExpression_UsesThePlaceholder()
    {
        var exception = Assert.Throws<GuardViolationException>(
            static () => Guard.NotNullOrWhiteSpace(null, expression: null));

        Assert.Equal(
            $"Guard.NotNullOrWhiteSpace ({Guard.UnknownExpression}) failed: value was null, empty, or white-space.",
            exception.Message);
    }

    [Fact]
    public void NotNullOrWhiteSpace_FlowsNonNullabilityToTheCaller()
    {
        string? candidate = MaybeNull("abc");

        _ = Guard.NotNullOrWhiteSpace(candidate);

        Assert.Equal(3, candidate.Length);
    }

    private static string? MaybeNull(string? value) => value;
}
