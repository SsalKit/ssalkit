namespace SsalKit.DependencyInjection.Generator.Models;

/// <summary>
/// An equatable, compilation-independent representation of a single valid <c>[Service]</c>
/// attribute application (one instance of the model is produced per attribute, since a class may
/// carry more than one).
/// </summary>
/// <param name="ServiceTypeFqns">
/// The fully-qualified (<c>global::</c>-prefixed), sorted set of service types this registration
/// targets. Contains exactly one entry when <c>As</c> was specified or the implementation has no
/// directly-implemented interfaces (self registration); otherwise one entry per directly
/// implemented interface.
/// </param>
/// <param name="Lifetime">The underlying integral value of <c>Microsoft.Extensions.DependencyInjection.ServiceLifetime</c>.</param>
/// <param name="Mode">The underlying integral value of <c>SsalKit.DependencyInjection.RegistrationMode</c>.</param>
/// <param name="Key">The keyed-service key, if any.</param>
internal sealed record RegistrationEntryModel(
    EquatableArray<string> ServiceTypeFqns,
    int Lifetime,
    int Mode,
    KeyModel Key)
{
    /// <summary>
    /// Whether this entry registers the implementation type once (as itself) and forwards every
    /// other service type to that single, shared instance via a factory delegate, instead of
    /// registering each service type with its own independent <c>&lt;TService, TImpl&gt;</c>
    /// descriptor.
    /// </summary>
    /// <remarks>
    /// Forwarding only makes sense -- and is only safe -- when there are 2+ service types to
    /// share a Singleton/Scoped instance across. It is never used for
    /// <see cref="WellKnownRegistrationMode.TryAddEnumerable"/>: a forwarding factory descriptor
    /// has no fixed implementation type for
    /// <c>Microsoft.Extensions.DependencyInjection.ServiceCollectionDescriptorExtensions.TryAddEnumerable</c>
    /// to compare against, so it can never suppress a duplicate the way a direct
    /// <c>&lt;TService, TImpl&gt;</c> descriptor can. Instead, each service type gets its own
    /// direct <c>TryAddEnumerable(ServiceDescriptor.Xxx&lt;TService, TImpl&gt;())</c> descriptor;
    /// this is intentional, documented behavior and means instances are not shared across service
    /// types the way they are for the other three registration modes.
    /// </remarks>
    public bool RequiresForwarding =>
        Mode != (int)WellKnownRegistrationMode.TryAddEnumerable
        && ServiceTypeFqns.Length >= 2
        && Lifetime is (int)WellKnownLifetime.Singleton or (int)WellKnownLifetime.Scoped;
}

/// <summary>
/// Mirrors <c>Microsoft.Extensions.DependencyInjection.ServiceLifetime</c> without requiring the
/// generator to reference that assembly; the generator resolves attribute constants purely by
/// their underlying integral value.
/// </summary>
internal enum WellKnownLifetime
{
    Singleton = 0,
    Scoped = 1,
    Transient = 2,
}

/// <summary>
/// Mirrors <c>SsalKit.DependencyInjection.RegistrationMode</c> without requiring the generator to
/// reference that assembly.
/// </summary>
internal enum WellKnownRegistrationMode
{
    Add = 0,
    TryAdd = 1,
    TryAddEnumerable = 2,
    Replace = 3,
}
