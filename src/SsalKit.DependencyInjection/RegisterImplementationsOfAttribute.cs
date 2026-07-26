using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection;

/// <summary>
/// Declares a <em>convention scan</em>: every class in the assembly this attribute is applied to
/// that implements <see cref="Contract"/> is registered into the <c>IServiceCollection</c> by the
/// assembly's generated <c>Add{Assembly}Services</c> extension method, without needing a
/// <see cref="ServiceAttribute"/> of its own.
/// </summary>
/// <remarks>
/// <para>
/// For example:
/// </para>
/// <code>
/// [assembly: RegisterImplementationsOf(typeof(IRequestHandler&lt;,&gt;), ServiceLifetime.Scoped)]
/// [assembly: RegisterImplementationsOf(typeof(IStartupTask))]
/// </code>
/// <para>
/// <strong>Scan scope: the current compilation only.</strong> The scan is performed at compile
/// time, by a source generator, over the types declared in the assembly being compiled. Classes in
/// <em>referenced</em> assemblies are never discovered, even when they implement
/// <see cref="Contract"/> and even when the reference is a project reference. To register a
/// referenced assembly's implementations, declare this attribute in that assembly too and call its
/// own generated <c>Add{Assembly}Services</c> method.
/// </para>
/// <para>
/// <strong>What matches.</strong> <see cref="Contract"/> must be an interface (<c>SSAL021</c>); a
/// class, struct, enum, or delegate is rejected at compile time.
/// </para>
/// <list type="bullet">
/// <item>
/// A non-generic or closed generic contract (e.g. <c>typeof(IStartupTask)</c>,
/// <c>typeof(IRequestHandler&lt;Ping, Pong&gt;)</c>) matches every class that implements it, and
/// registers <c>(contract, class)</c>.
/// </item>
/// <item>
/// An unbound generic contract (e.g. <c>typeof(IRequestHandler&lt;,&gt;)</c>) matches every class
/// that implements <em>any</em> instantiation of it, and registers one <c>(instantiation, class)</c>
/// pair per implemented instantiation -- a class implementing both
/// <c>IRequestHandler&lt;A, B&gt;</c> and <c>IRequestHandler&lt;C, D&gt;</c> is registered twice,
/// once under each.
/// </item>
/// <item>
/// An open generic class (e.g. <c>Handler&lt;T&gt; : IRequestHandler&lt;T, Unit&gt;</c>) is only
/// registered when the same exact-match rule <see cref="ServiceAttribute"/> applies to open generic
/// registrations is satisfied -- the implemented instantiation's type arguments must be exactly the
/// class's own type parameters, in declaration order -- in which case the <c>typeof</c>-based
/// (unbound contract, unbound class) pair is registered. A shape Microsoft.Extensions.
/// DependencyInjection cannot express as an open generic registration is skipped.
/// </item>
/// </list>
/// <para>
/// Inherited implementations count: a class matches when the contract appears anywhere in its
/// interface set, including through a base class.
/// </para>
/// <para>
/// <strong>What is skipped, silently.</strong> A convention scan describes a shape, not a specific
/// type, so a class that merely fails to fit is passed over rather than reported: abstract and
/// static classes, classes not accessible from the generated code (a <see langword="private"/>
/// nested or file-local class, and everything nested inside one), classes nested inside a generic
/// type (see <c>SSAL003</c>), and open generic classes whose implemented instantiation is not an
/// exact match. Only mistakes in the <em>declaration</em> itself are diagnosed -- including a
/// contract that nothing in the assembly implements (<c>SSAL022</c>), so that a typo or a
/// namespace mix-up does not silently register nothing at all.
/// </para>
/// <para>
/// <strong>An explicit <c>[Service]</c> opts a class out of the scan.</strong> A class carrying at
/// least one <see cref="ServiceAttribute"/> is excluded from every convention scan in the assembly,
/// so <em>that class</em> is never registered twice. This doubles as the opt-out for a single class:
/// give it the <c>[Service]</c> registration you actually want (which may specify a different
/// lifetime, <c>As</c> type, <c>Key</c>, or <c>Mode</c>) and the scan will leave it alone.
/// </para>
/// <para>
/// <strong>It is not a resolution-priority rule.</strong> The exclusion is per class, so a contract
/// still matches every <em>other</em> implementation of the same service type. The generated method
/// emits the <c>[Service]</c> registrations first and the convention registrations second, so
/// Microsoft.Extensions.DependencyInjection's last-registration-wins rule gives a single-instance
/// resolution to the convention rather than to the explicit registration. <c>SSAL027</c> reports
/// that overlap for every <see cref="Mode"/> except <see cref="RegistrationMode.TryAddEnumerable"/>,
/// which is additive and shadows nothing. <see cref="RegistrationMode.Replace"/> goes further still:
/// <c>IServiceCollection.Replace</c> removes every existing descriptor for the service type before
/// adding its own, so a <c>Replace</c> contract deletes an explicit registration of the same service
/// type elsewhere in the assembly rather than merely out-ranking it.
/// </para>
/// <para>
/// <strong>Why <see cref="RegistrationMode.TryAddEnumerable"/> is the default.</strong> Registering
/// "every implementation of X" is by nature a multi-implementation pattern whose result is consumed
/// as <c>IEnumerable&lt;X&gt;</c>; <c>TryAddEnumerable</c> is the mode that makes that work without
/// implementations shadowing one another, and it is also the one mode <c>SSAL015</c> never reports
/// as a conflict. Set <see cref="Mode"/> explicitly for the (rarer) case where the scan is meant to
/// bind a single implementation.
/// </para>
/// <para>
/// Each matched service type gets its own, independent registration: unlike a multi-interface
/// <see cref="ServiceAttribute"/> registration, a convention-scanned class is never registered
/// once as its concrete type with the service types forwarded to it, so two service types matched
/// on the same class do not resolve to a shared instance.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class RegisterImplementationsOfAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterImplementationsOfAttribute"/> class.
    /// </summary>
    /// <param name="contract">
    /// The interface to scan for implementations of. May be non-generic, a closed generic type, or
    /// an unbound generic type reference such as <c>typeof(IRequestHandler&lt;,&gt;)</c>.
    /// </param>
    /// <param name="lifetime">
    /// The <see cref="ServiceLifetime"/> every matched implementation is registered with. Defaults
    /// to <see cref="ServiceLifetime.Singleton"/>, matching <see cref="ServiceAttribute"/>'s own
    /// default.
    /// </param>
    public RegisterImplementationsOfAttribute(Type contract, ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Contract = contract;
        Lifetime = lifetime;
    }

    /// <summary>
    /// Gets the interface that matched classes are scanned for and registered against.
    /// </summary>
    public Type Contract { get; }

    /// <summary>
    /// Gets the <see cref="ServiceLifetime"/> every matched implementation is registered with.
    /// </summary>
    public ServiceLifetime Lifetime { get; }

    /// <summary>
    /// Gets or sets how each matched registration is applied to the <c>IServiceCollection</c>.
    /// Defaults to <see cref="RegistrationMode.TryAddEnumerable"/> -- unlike
    /// <see cref="ServiceAttribute.Mode"/>, whose default is <see cref="RegistrationMode.Add"/>.
    /// </summary>
    public RegistrationMode Mode { get; set; } = RegistrationMode.TryAddEnumerable;
}
