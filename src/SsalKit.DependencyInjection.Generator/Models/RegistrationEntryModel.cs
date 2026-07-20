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
    public bool RequiresForwarding => ServiceTypeFqns.Length >= 2 && Lifetime is (int)WellKnownLifetime.Singleton or (int)WellKnownLifetime.Scoped;
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
