using System.Reflection;

namespace SsalKit.Guard.Tests;

public enum SampleStatusCode
{
    None = 0,
    UserNotFound = 1001,
    ServerBusy = 2001,
    GuardViolation = 9001,
}

[ErrorCode<SampleStatusCode>(SampleStatusCode.UserNotFound)]
public sealed class SampleUserNotFoundException : ErrorCodedException
{
    public SampleUserNotFoundException(string? message = null)
        : base(message)
    {
    }
}

[ErrorCodes<SampleStatusCode>]
[ExternalErrorCode<SampleStatusCode>(typeof(GuardViolationException), SampleStatusCode.GuardViolation)]
[ExternalErrorCode<SampleStatusCode>(typeof(TimeoutException), SampleStatusCode.ServerBusy)]
public static class SampleErrors
{
}

public sealed class ErrorCodeAttributeTests
{
    [Fact]
    public void Constructor_ExposesTheCode()
    {
        var attribute = new ErrorCodeAttribute<SampleStatusCode>(SampleStatusCode.UserNotFound);

        Assert.Equal(SampleStatusCode.UserNotFound, attribute.Code);
    }

    [Fact]
    public void ReadsBackOffADecoratedExceptionType()
    {
        var attribute = typeof(SampleUserNotFoundException)
            .GetCustomAttribute<ErrorCodeAttribute<SampleStatusCode>>();

        Assert.NotNull(attribute);
        Assert.Equal(SampleStatusCode.UserNotFound, attribute.Code);
    }

    [Fact]
    public void TargetsClassesOnly_NotMultiple_NotInherited()
    {
        var usage = typeof(ErrorCodeAttribute<SampleStatusCode>)
            .GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    [Fact]
    public void IsSealed()
    {
        Assert.True(typeof(ErrorCodeAttribute<SampleStatusCode>).IsSealed);
    }
}

public sealed class ErrorCodesAttributeTests
{
    [Fact]
    public void CanBeConstructed()
    {
        var attribute = new ErrorCodesAttribute<SampleStatusCode>();

        Assert.IsAssignableFrom<Attribute>(attribute);
    }

    [Fact]
    public void ReadsBackOffAContainerType()
    {
        var attribute = typeof(SampleErrors).GetCustomAttribute<ErrorCodesAttribute<SampleStatusCode>>();

        Assert.NotNull(attribute);
    }

    [Fact]
    public void TargetsClassesOnly_NotMultiple_NotInherited()
    {
        var usage = typeof(ErrorCodesAttribute<SampleStatusCode>)
            .GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    [Fact]
    public void IsSealed()
    {
        Assert.True(typeof(ErrorCodesAttribute<SampleStatusCode>).IsSealed);
    }
}

public sealed class ExternalErrorCodeAttributeTests
{
    [Fact]
    public void Constructor_ExposesTheExceptionTypeAndTheCode()
    {
        var attribute = new ExternalErrorCodeAttribute<SampleStatusCode>(
            typeof(TimeoutException),
            SampleStatusCode.ServerBusy);

        Assert.Equal(typeof(TimeoutException), attribute.ExceptionType);
        Assert.Equal(SampleStatusCode.ServerBusy, attribute.Code);
    }

    [Fact]
    public void ReadsBackAllRegistrationsOffAContainerType()
    {
        var attributes = typeof(SampleErrors)
            .GetCustomAttributes<ExternalErrorCodeAttribute<SampleStatusCode>>()
            .ToDictionary(attribute => attribute.ExceptionType, attribute => attribute.Code);

        Assert.Equal(2, attributes.Count);
        Assert.Equal(SampleStatusCode.GuardViolation, attributes[typeof(GuardViolationException)]);
        Assert.Equal(SampleStatusCode.ServerBusy, attributes[typeof(TimeoutException)]);
    }

    [Fact]
    public void TargetsClassesOnly_AllowsMultiple_NotInherited()
    {
        var usage = typeof(ExternalErrorCodeAttribute<SampleStatusCode>)
            .GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.True(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    [Fact]
    public void IsSealed()
    {
        Assert.True(typeof(ExternalErrorCodeAttribute<SampleStatusCode>).IsSealed);
    }
}
