using Microsoft.Extensions.DependencyInjection;
using SsalKit.DependencyInjection.IntegrationTests.TestServices;

namespace SsalKit.DependencyInjection.IntegrationTests;

/// <summary>
/// Verifies the generated <c>[ServiceFactory]</c> implementation end-to-end: the factory interface
/// itself resolves as a singleton, each enum value resolves the implementation registered under it,
/// and an enum value nothing is registered under throws whatever
/// <c>GetRequiredKeyedService</c> throws.
/// </summary>
public class ServiceFactoryTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSsalKitDependencyInjectionIntegrationTestsServices();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Factory_IsResolvableWithoutAnyHandWrittenRegistration()
    {
        using var provider = BuildProvider();

        var factory = provider.GetRequiredService<IPaymentProcessorFactory>();

        Assert.NotNull(factory);
    }

    [Fact]
    public void Factory_IsRegisteredAsASingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IPaymentProcessorFactory>();
        var second = provider.GetRequiredService<IPaymentProcessorFactory>();

        using var scope = provider.CreateScope();
        var fromScope = scope.ServiceProvider.GetRequiredService<IPaymentProcessorFactory>();

        Assert.Same(first, second);
        Assert.Same(first, fromScope);
    }

    /// <summary>
    /// The generated implementation is the only implementation in the assembly, and it lives in the
    /// reserved generated namespace rather than the interface's own.
    /// </summary>
    [Fact]
    public void Factory_ResolvesToTheGeneratedImplementation()
    {
        using var provider = BuildProvider();

        var factory = provider.GetRequiredService<IPaymentProcessorFactory>();

        Assert.Equal(
            "SsalKit.DependencyInjection.Generated.SsalKit.DependencyInjection.IntegrationTests.TestServices",
            factory.GetType().Namespace);
        Assert.Equal("IPaymentProcessorFactoryImplementation", factory.GetType().Name);
    }

    [Theory]
    [InlineData(PaymentMethod.Card, "card:10")]
    [InlineData(PaymentMethod.Bank, "bank:10")]
    public void Factory_ResolvesTheImplementationRegisteredUnderEachKey(PaymentMethod method, string expected)
    {
        using var provider = BuildProvider();

        var factory = provider.GetRequiredService<IPaymentProcessorFactory>();

        Assert.Equal(expected, factory.Create(method).Pay(10m));
    }

    [Fact]
    public void Factory_ReturnsTheSameTypeGetRequiredKeyedServiceWouldHaveReturned()
    {
        using var provider = BuildProvider();

        var factory = provider.GetRequiredService<IPaymentProcessorFactory>();

        Assert.IsType<CardPaymentProcessor>(factory.Create(PaymentMethod.Card));
        Assert.IsType<BankPaymentProcessor>(factory.Create(PaymentMethod.Bank));
    }

    /// <summary>
    /// The factory adds no caching of its own: the keyed registration's lifetime is what decides
    /// whether two calls share an instance.
    /// </summary>
    [Fact]
    public void Factory_HonoursTheKeyedRegistrationsLifetime()
    {
        using var provider = BuildProvider();

        var factory = provider.GetRequiredService<IPaymentProcessorFactory>();

        Assert.Same(factory.Create(PaymentMethod.Card), factory.Create(PaymentMethod.Card));
        Assert.NotSame(factory.Create(PaymentMethod.Bank), factory.Create(PaymentMethod.Bank));
    }

    /// <summary>
    /// The v1 contract for an unregistered key: no fallback, no wrapping -- exactly the exception
    /// <c>GetRequiredKeyedService</c> itself throws.
    /// </summary>
    [Fact]
    public void Factory_UnregisteredKey_ThrowsTheSameExceptionGetRequiredKeyedServiceDoes()
    {
        using var provider = BuildProvider();

        var factory = provider.GetRequiredService<IPaymentProcessorFactory>();

        var fromFactory = Assert.Throws<InvalidOperationException>(() => factory.Create(PaymentMethod.Crypto));
        var fromProvider = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredKeyedService<IPaymentProcessor>(PaymentMethod.Crypto));

        Assert.Equal(fromProvider.Message, fromFactory.Message);
    }

    [Fact]
    public void Factory_UsesTheEnumValueVerbatimAsTheServiceKey()
    {
        using var provider = BuildProvider();

        var factory = provider.GetRequiredService<IPaymentProcessorFactory>();

        // The keyed registrations were declared with Key = PaymentMethod.Card, so a lookup by the
        // enum value (not its name or its underlying integer) is what must match.
        Assert.Same(provider.GetRequiredKeyedService<IPaymentProcessor>(PaymentMethod.Card), factory.Create(PaymentMethod.Card));
        Assert.Null(provider.GetKeyedService<IPaymentProcessor>("Card"));
        Assert.Null(provider.GetKeyedService<IPaymentProcessor>(0));
    }

    [Fact]
    public void SecondFactory_OverADifferentEnumAndServiceType_ResolvesIndependently()
    {
        using var provider = BuildProvider();

        var factory = provider.GetRequiredService<INotifierFactory>();

        Assert.Equal("email:hi", factory.Resolve(NotifierKind.Email).Notify("hi"));
        Assert.Equal("sms:hi", factory.Resolve(NotifierKind.Sms).Notify("hi"));
    }
}
