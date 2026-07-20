using Microsoft.Extensions.DependencyInjection;

namespace SsalKit.DependencyInjection;

/// <summary>
/// Marks a class as a service to be automatically registered into an
/// <c>IServiceCollection</c> by the SsalKit.DependencyInjection source generator.
/// </summary>
/// <remarks>
/// A class may be decorated with more than one <see cref="ServiceAttribute"/> to register it
/// against multiple service types, lifetimes, or registration modes.
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
}
