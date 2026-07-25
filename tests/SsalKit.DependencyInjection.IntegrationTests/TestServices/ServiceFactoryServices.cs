using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection.IntegrationTests.TestServices;

// Covers: [ServiceFactory]. Two implementations of IPaymentProcessor are registered under distinct
// enum keys, and a third enum member (PaymentMethod.Crypto) is deliberately left unregistered so
// the "no registration for this key" contract can be exercised. The factory interface itself is
// never implemented by hand -- the generator's implementation is the only one in the assembly.

public enum PaymentMethod
{
    Card,
    Bank,

    /// <summary>Deliberately never registered, to exercise the unregistered-key contract.</summary>
    Crypto,
}

public interface IPaymentProcessor
{
    string Pay(decimal amount);
}

[Service(ServiceLifetime.Singleton, As = typeof(IPaymentProcessor), Key = PaymentMethod.Card)]
public sealed class CardPaymentProcessor : IPaymentProcessor
{
    public string Pay(decimal amount) => $"card:{amount}";
}

[Service(ServiceLifetime.Transient, As = typeof(IPaymentProcessor), Key = PaymentMethod.Bank)]
public sealed class BankPaymentProcessor : IPaymentProcessor
{
    public string Pay(decimal amount) => $"bank:{amount}";
}

[ServiceFactory]
public interface IPaymentProcessorFactory
{
    IPaymentProcessor Create(PaymentMethod method);
}

// A second factory in the same assembly, over a different enum and a different service type, with
// a differently-named method -- both must be generated and registered independently.

public enum NotifierKind
{
    Email,
    Sms,
}

public interface INotifier
{
    string Notify(string message);
}

[Service(ServiceLifetime.Singleton, As = typeof(INotifier), Key = NotifierKind.Email)]
public sealed class EmailNotifier : INotifier
{
    public string Notify(string message) => $"email:{message}";
}

[Service(ServiceLifetime.Singleton, As = typeof(INotifier), Key = NotifierKind.Sms)]
public sealed class SmsNotifier : INotifier
{
    public string Notify(string message) => $"sms:{message}";
}

[ServiceFactory]
internal interface INotifierFactory
{
    INotifier Resolve(NotifierKind kind);
}
