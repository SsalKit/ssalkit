namespace SsalKit.DependencyInjection;

/// <summary>
/// Specifies how a service marked with <see cref="ServiceAttribute"/> should be registered
/// into an <c>IServiceCollection</c> by the generated registration code.
/// </summary>
public enum RegistrationMode
{
    /// <summary>
    /// Registers the service unconditionally, allowing multiple registrations for the same
    /// service type. Equivalent to calling <c>IServiceCollection.Add</c>.
    /// </summary>
    Add,

    /// <summary>
    /// Registers the service only if no implementation has already been registered for the
    /// same service type. Equivalent to calling <c>IServiceCollection.TryAdd</c>.
    /// </summary>
    TryAdd,

    /// <summary>
    /// Registers the service only if the exact same implementation type has not already been
    /// registered for the same service type, allowing multiple distinct implementations to
    /// coexist. Equivalent to calling <c>IServiceCollection.TryAddEnumerable</c>.
    /// </summary>
    TryAddEnumerable,

    /// <summary>
    /// Removes any existing registrations for the same service type before registering this
    /// service. Equivalent to calling <c>IServiceCollection.Replace</c>.
    /// </summary>
    Replace,
}
