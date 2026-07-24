using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection;

/// <summary>
/// Marks a class as a service to be automatically registered into an
/// <c>IServiceCollection</c> by the SsalKit.DependencyInjection source generator.
/// </summary>
/// <remarks>
/// A class may be decorated with more than one <see cref="ServiceAttribute"/> to register it
/// against multiple service types, lifetimes, or registration modes.
/// <para>
/// An open generic class (e.g. <c>Repository&lt;T&gt;</c>) can be registered too, provided every
/// one of its type parameters is entirely its own -- a class nested inside a generic type is not
/// supported. Only an <em>exact-match</em> service type is valid for an open generic class: the
/// class itself, or an implemented interface/base class whose type arguments are exactly the
/// class's own type parameters, in declaration order (e.g. <c>IRepository&lt;T&gt;</c> for
/// <c>Repository&lt;T&gt;</c>). A closed, reordered, partially-applied, or otherwise non-exact
/// service type is rejected at compile time; see <see cref="As"/> for the explicit escape hatch.
/// Keyed registration works the same way as for a closed class. Unlike a closed class, an open
/// generic registration can never share one instance across 2+ service types (Microsoft.
/// Extensions.DependencyInjection has no forwarding-factory mechanism for open generics), so each
/// resolved closed service type gets its own, independent instance.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ServiceAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAttribute"/> class.
    /// </summary>
    /// <param name="lifetime">
    /// The <see cref="ServiceLifetime"/> to register the service with. Defaults to
    /// <see cref="ServiceLifetime.Singleton"/>.
    /// </param>
    public ServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Lifetime = lifetime;
    }

    /// <summary>
    /// Gets the <see cref="ServiceLifetime"/> that the service is registered with.
    /// </summary>
    public ServiceLifetime Lifetime { get; }

    /// <summary>
    /// Gets or sets the service type that the decorated class should be registered as.
    /// When <see langword="null"/>, the decorated class is registered as itself, or the
    /// generator may infer an implemented interface depending on convention.
    /// </summary>
    /// <remarks>
    /// For an open generic decorated class, this must itself be an unbound generic type reference
    /// (e.g. <c>typeof(IRepository&lt;&gt;)</c>): the class must implement or derive some
    /// instantiation of it, and that instantiation must be an exact-match shape (the class's own
    /// type parameters, in declaration order) -- otherwise the registration is rejected at compile
    /// time. This is the only way to select one specific implemented interface/base class out of
    /// several for an open generic class; unlike a closed class, every directly-implemented
    /// interface must independently be an exact-match shape when <see cref="As"/> is not specified.
    /// </remarks>
    public Type? As { get; set; }

    /// <summary>
    /// Gets or sets how the service registration is applied to the
    /// <c>IServiceCollection</c>. Defaults to <see cref="RegistrationMode.Add"/>.
    /// </summary>
    public RegistrationMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the key used to register the service as a keyed service. When
    /// <see langword="null"/>, the service is registered as a non-keyed service.
    /// </summary>
    public object? Key { get; set; }

    /// <summary>
    /// Gets or sets the name of a static factory method, declared directly on the decorated
    /// class, that the generated registration invokes to construct the implementation instance
    /// instead of letting the container construct it via a constructor. Use
    /// <see langword="nameof"/> (e.g. <c>Factory = nameof(Create)</c>) rather than a string
    /// literal, so a rename of the method is caught by the compiler.
    /// </summary>
    /// <remarks>
    /// The method must be declared directly on the decorated class -- not inherited from a base
    /// class, even if the base class is also in this assembly -- because an inherited method can
    /// live in a different syntax tree, which would break the source generator's incremental
    /// caching for this class. It must be <see langword="static"/>, non-generic, return exactly
    /// the decorated class (not a base type or an interface it implements), and have either no
    /// parameters or a single <see cref="IServiceProvider"/> parameter. When both a parameterless
    /// and an <see cref="IServiceProvider"/>-accepting overload exist, the
    /// <see cref="IServiceProvider"/>-accepting one is used -- deterministically, not as an
    /// ambiguity error. The chosen method must also be accessible from the generated registration
    /// code, i.e. at least <see langword="internal"/>.
    /// <para>
    /// <c>Factory</c> composes with every other option on this attribute -- <see cref="Lifetime"/>,
    /// <see cref="Key"/>, <see cref="As"/>, and every <see cref="RegistrationMode"/> -- and only
    /// changes how the implementation instance is constructed. When the class implements 2+
    /// interfaces and would otherwise share one instance across them, the factory is invoked once
    /// and every interface resolves to that same, factory-constructed instance, exactly as it
    /// would if the container had constructed it directly.
    /// </para>
    /// <para>
    /// Not supported on an open generic decorated class (see the type-level remarks): Microsoft.
    /// Extensions.DependencyInjection has no factory-based registration API for open generics.
    /// </para>
    /// </remarks>
    public string? Factory { get; set; }
}
