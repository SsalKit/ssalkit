namespace SsalKit.Guard.Tests;

public sealed class ErrorCodedExceptionTests
{
    [Fact]
    public void Parameterless_UsesTheRuntimeSuppliedDefaultMessage()
    {
        var exception = new SampleErrorCodedException();

        Assert.NotNull(exception.Message);
        Assert.NotEmpty(exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void MessageOnly_KeepsTheMessage()
    {
        var exception = new SampleErrorCodedException("boom");

        Assert.Equal("boom", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void MessageAndInnerException_KeepsBoth()
    {
        var inner = new InvalidOperationException("cause");

        var exception = new SampleErrorCodedException("boom", inner);

        Assert.Equal("boom", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void IsAnException()
    {
        Assert.True(typeof(Exception).IsAssignableFrom(typeof(ErrorCodedException)));
        Assert.True(typeof(ErrorCodedException).IsAbstract);
    }

    /// <summary>
    /// The base is meant to be a pure data carrier, so a consumer must be able to expose exactly
    /// the constructors it wants without inheriting anything else. Nothing here asserts the absence
    /// of side effects directly — the guarantee is structural: the base declares no state and no
    /// members beyond <see cref="Exception"/>'s own.
    /// </summary>
    [Fact]
    public void DeclaresNoMembersOfItsOwnBeyondItsConstructors()
    {
        var declared = typeof(ErrorCodedException).GetMembers(
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.DeclaredOnly);

        Assert.All(declared, member => Assert.IsAssignableFrom<System.Reflection.ConstructorInfo>(member));
        Assert.Equal(3, declared.Length);
    }

    private sealed class SampleErrorCodedException : ErrorCodedException
    {
        public SampleErrorCodedException()
        {
        }

        public SampleErrorCodedException(string? message)
            : base(message)
        {
        }

        public SampleErrorCodedException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}

public sealed class GuardViolationExceptionTests
{
    [Fact]
    public void Parameterless_UsesTheRuntimeSuppliedDefaultMessage()
    {
        var exception = new GuardViolationException();

        Assert.NotNull(exception.Message);
        Assert.NotEmpty(exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void MessageOnly_KeepsTheMessage()
    {
        var exception = new GuardViolationException("boom");

        Assert.Equal("boom", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void MessageAndInnerException_KeepsBoth()
    {
        var inner = new InvalidOperationException("cause");

        var exception = new GuardViolationException("boom", inner);

        Assert.Equal("boom", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void IsASealedErrorCodedException()
    {
        Assert.True(typeof(ErrorCodedException).IsAssignableFrom(typeof(GuardViolationException)));
        Assert.True(typeof(GuardViolationException).IsSealed);
    }
}
