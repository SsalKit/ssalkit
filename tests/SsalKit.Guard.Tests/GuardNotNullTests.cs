namespace SsalKit.Guard.Tests;

public sealed class GuardNotNullTests
{
    [Fact]
    public void NotNull_ReferenceValue_ReturnsTheSameInstance()
    {
        var instance = new object();

        object returned = Guard.NotNull(instance);

        Assert.Same(instance, returned);
    }

    [Fact]
    public void NotNull_NullReference_MessageEmbedsTheCallerExpressionText()
    {
        string? candidate = null;

        var exception = Assert.Throws<GuardViolationException>(() => Guard.NotNull(candidate));

        Assert.Equal("Guard.NotNull (candidate) failed: value was null.", exception.Message);
    }

    [Fact]
    public void NotNull_NullReferenceWithNullExpression_UsesThePlaceholder()
    {
        string? candidate = null;

        var exception = Assert.Throws<GuardViolationException>(
            () => Guard.NotNull(candidate, expression: null));

        Assert.Equal(
            $"Guard.NotNull ({Guard.UnknownExpression}) failed: value was null.",
            exception.Message);
    }

    /// <summary>
    /// A compile-time assertion as much as a runtime one: the assignment to a non-nullable local
    /// and the dereference of <c>candidate</c> afterwards both compile without <c>!</c> only
    /// because the clause returns a non-nullable <c>T</c> and carries <c>[NotNull]</c> on its
    /// parameter. With warnings-as-errors on, losing either would break this build.
    /// </summary>
    [Fact]
    public void NotNull_FlowsNonNullabilityToTheCaller()
    {
        // Deliberately opaque to flow analysis: the compiler only knows candidate is non-null
        // after the guard, not before.
        string? candidate = MaybeNull("value");

        string returned = Guard.NotNull(candidate);

        Assert.Equal(5, returned.Length);
        Assert.Equal(5, candidate.Length);
    }

    private static string? MaybeNull(string? value) => value;

    [Fact]
    public void NotNull_NullableValueType_ReturnsTheUnderlyingValue()
    {
        int? candidate = 42;

        int returned = Guard.NotNull(candidate);

        Assert.Equal(42, returned);
    }

    [Fact]
    public void NotNull_EmptyNullableValueType_MessageEmbedsTheCallerExpressionText()
    {
        int? candidate = null;

        var exception = Assert.Throws<GuardViolationException>(() => Guard.NotNull(candidate));

        Assert.Equal("Guard.NotNull (candidate) failed: value was null.", exception.Message);
    }

    [Fact]
    public void NotNull_EmptyNullableValueTypeWithNullExpression_UsesThePlaceholder()
    {
        int? candidate = null;

        var exception = Assert.Throws<GuardViolationException>(
            () => Guard.NotNull(candidate, expression: null));

        Assert.Equal(
            $"Guard.NotNull ({Guard.UnknownExpression}) failed: value was null.",
            exception.Message);
    }

    [Fact]
    public void NotNull_NullableValueTypeWithAValue_FlowsNonNullabilityToTheCaller()
    {
        int? candidate = 7;

        _ = Guard.NotNull(candidate);

        Assert.Equal(7, candidate.Value);
    }
}
