using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using SsalKit.Guard.IntegrationTests.TestDomain;

namespace SsalKit.Guard.IntegrationTests;

/// <summary>
/// The per-code factory and throw helpers: that they construct the exception the call site names,
/// that they mirror the constructor the exception itself declares, and that
/// <c>[DoesNotReturn]</c> on the throw form actually reaches the compiler's flow analysis.
/// </summary>
public class GeneratedHelperTests
{
    [Fact]
    public void Factory_ReturnsTheExceptionUnthrown_WithTheGivenMessage()
    {
        UserNotFoundException exception = GameErrors.UserNotFound("user 42 is gone");

        Assert.Equal("user 42 is gone", exception.Message);
        Assert.IsType<UserNotFoundException>(exception);
    }

    /// <summary>
    /// The factory is an expression, so it composes with <c>throw</c> -- which is the call shape the
    /// design is after: <c>throw GameErrors.UserNotFound("...")</c>.
    /// </summary>
    [Fact]
    public void Factory_ComposesWithThrow()
    {
        // Typed as an Action first: a lambda whose body is nothing but a throw has no return type
        // for overload resolution to work with.
        Action throwing = static () => throw GameErrors.UserNotFound("user 42 is gone");

        var thrown = Assert.Throws<UserNotFoundException>(throwing);

        Assert.Equal("user 42 is gone", thrown.Message);
        Assert.True(GameErrors.TryMap(thrown, out GameStatusCode code));
        Assert.Equal(GameStatusCode.UserNotFound, code);
    }

    [Fact]
    public void ThrowHelper_ThrowsTheExceptionItNames()
    {
        var thrown = Assert.Throws<NotFoundException>(static () => GameErrors.ThrowNotFound("gone"));

        Assert.Equal("gone", thrown.Message);

        // Exactly NotFoundException, not the derived one -- each helper names one type.
        Assert.Equal(typeof(NotFoundException), thrown.GetType());
    }

    /// <summary>
    /// The message parameter is optional because the exception's own is, and omitting it leaves the
    /// runtime's default message rather than an empty one.
    /// </summary>
    [Fact]
    public void Factory_WithNoMessage_KeepsTheRuntimeDefaultMessage()
    {
        UserNotFoundException exception = GameErrors.UserNotFound();

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    /// <summary>
    /// <see cref="InvalidTeamException"/> declares <c>(string?, Exception?)</c>, so its helpers
    /// mirror both parameters -- the generator picks the widest recognised shape the type offers,
    /// not a fixed one.
    /// </summary>
    [Fact]
    public void Factory_MirrorsTheWidestConstructorShape()
    {
        var cause = new TimeoutException("roster service timed out");

        InvalidTeamException exception = GameErrors.InvalidTeam("team is not valid", cause);

        Assert.Equal("team is not valid", exception.Message);
        Assert.Same(cause, exception.InnerException);
    }

    [Fact]
    public void ThrowHelper_MirrorsTheWidestConstructorShape()
    {
        var cause = new TimeoutException("roster service timed out");

        var thrown = Assert.Throws<InvalidTeamException>(() => GameErrors.ThrowInvalidTeam("bad team", cause));

        Assert.Same(cause, thrown.InnerException);
    }

    /// <summary>
    /// The compile-time half of <c>[DoesNotReturn]</c>: this method only compiles because the
    /// attribute ends the path through the throw helper. Without it the <c>name.Trim()</c> below is
    /// a dereference of a possibly-null reference (CS8602), which this repository treats as an
    /// error.
    /// </summary>
    private static string NormalizeName(string? name)
    {
        if (name is null)
        {
            GameErrors.ThrowUserNotFound("the user has no name");
        }

        return name.Trim();
    }

    [Fact]
    public void ThrowHelper_EndsTheFlowPath_ForNullableAnalysis()
    {
        Assert.Equal("ok", NormalizeName("  ok  "));
        Assert.Throws<UserNotFoundException>(static () => NormalizeName(null));
    }

    /// <summary>
    /// And the attribute is really on the emitted member, not merely respected by the compiler that
    /// happened to build this assembly.
    /// </summary>
    [Fact]
    public void ThrowHelper_CarriesDoesNotReturn()
    {
        MethodInfo? helper = typeof(GameErrors).GetMethod(
            "ThrowUserNotFound", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(helper);
        Assert.NotNull(helper.GetCustomAttribute<DoesNotReturnAttribute>());
    }

    /// <summary>
    /// Externally registered types take part in the mapping only: this library will not vouch for
    /// the constructor contract of a type it does not own, so no factory or throw helper is emitted
    /// for <see cref="TimeoutException"/> or <see cref="GuardViolationException"/>.
    /// </summary>
    [Theory]
    [InlineData("Timeout")]
    [InlineData("ThrowTimeout")]
    [InlineData("GuardViolation")]
    [InlineData("ThrowGuardViolation")]
    public void NoHelperIsGeneratedForAnExternallyRegisteredType(string memberName)
    {
        Assert.Null(typeof(GameErrors).GetMethod(memberName, BindingFlags.Public | BindingFlags.Static));
    }
}
