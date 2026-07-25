namespace SsalKit.DependencyInjection;

/// <summary>
/// Marks an interface as an <em>enum-keyed service factory</em>: the SsalKit.DependencyInjection
/// source generator emits an implementation of it that resolves a keyed service from the
/// <see cref="IServiceProvider"/>, and registers that implementation as a singleton in the
/// assembly's generated <c>Add{Assembly}Services</c> extension method.
/// </summary>
/// <remarks>
/// <para>
/// A decorated interface must declare exactly one member, and that member must be an ordinary,
/// non-static, non-generic method taking exactly one by-value parameter of an <see langword="enum"/>
/// type and returning a non-<see langword="void"/> service type. The interface itself must be
/// non-generic and not nested inside a generic type. Anything else is rejected at compile time
/// (<c>SSAL016</c>-<c>SSAL020</c>) and no implementation is generated for it; other factories and
/// every <c>[Service]</c> registration are unaffected.
/// </para>
/// <para>
/// For example:
/// </para>
/// <code>
/// public enum PaymentMethod { Card, Bank }
///
/// [ServiceFactory]
/// public interface IPaymentProcessorFactory
/// {
///     IPaymentProcessor Create(PaymentMethod method);
/// }
/// </code>
/// <para>
/// causes an <see langword="internal"/> <see langword="sealed"/> implementation to be generated
/// into the reserved <c>SsalKit.DependencyInjection.Generated</c> namespace. It takes an
/// <see cref="IServiceProvider"/> through its constructor and forwards the call to
/// <c>GetRequiredKeyedService&lt;IPaymentProcessor&gt;(method)</c>, i.e. the enum value is used
/// verbatim as the service key. The generated <c>Add{Assembly}Services</c> method then registers
/// it with <c>services.AddSingleton&lt;IPaymentProcessorFactory, ...&gt;()</c>, so the interface
/// can be injected anywhere without any hand-written wiring.
/// </para>
/// <para>
/// <strong>Unregistered keys.</strong> The factory performs no lookup of its own and adds no
/// fallback: whatever <c>GetRequiredKeyedService</c> throws when nothing is registered for the
/// requested key (an <see cref="InvalidOperationException"/> for the built-in container) is the
/// factory's contract as well, thrown from the <c>Create</c> call itself.
/// </para>
/// <para>
/// <strong>Cross-assembly registrations.</strong> No diagnostic is reported when the compilation
/// declaring the factory contains no keyed <c>[Service(Key = SomeEnum.X)]</c> registration for a
/// given enum value: registering the keyed implementations in a different assembly (or by hand) is
/// a supported, ordinary arrangement. The consequence is that a missing registration surfaces at
/// resolution time rather than at compile time.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class ServiceFactoryAttribute : Attribute
{
}
