using SsalKit.Generators.Toolkit;

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
/// <param name="IsOpenGeneric">
/// Whether the decorated class is an open generic (see
/// <see cref="Parsing.ServiceTypeResolver.IsNestedInGenericType(Microsoft.CodeAnalysis.INamedTypeSymbol)"/>
/// for what disqualifies a class from this regardless of its own arity). <see cref="ServiceTypeFqns"/>
/// are typeof-form (e.g. <c>global::Ns.IRepository&lt;&gt;</c>) rather than ordinary closed generic
/// syntax when this is <see langword="true"/>, and the emitter renders Type-based registration
/// calls instead of the closed <c>&lt;TService, TImpl&gt;</c> generic-argument form. Always
/// combined with <see cref="FactoryModel.None"/>: an open generic class cannot specify
/// <c>Factory</c> (SSAL013).
/// </param>
/// <param name="Factory">
/// The resolved <c>Factory</c> method, if any. Only ever changes how the implementation instance
/// is <em>constructed</em> -- which registration statement(s) it applies to is decided by
/// <see cref="RequiresForwarding"/> exactly as for a factory-less entry: a forwarded statement
/// always resolves the already-constructed shared instance via <c>GetRequiredService</c>/
/// <c>GetRequiredKeyedService</c> and never invokes the factory itself.
/// </param>
internal sealed record RegistrationEntryModel(
    EquatableArray<string> ServiceTypeFqns,
    int Lifetime,
    int Mode,
    KeyModel Key,
    bool IsOpenGeneric,
    FactoryModel Factory)
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
    /// <para>
    /// Also never used when <see cref="IsOpenGeneric"/>: Microsoft.Extensions.DependencyInjection
    /// does not allow a factory delegate for an open generic registration (there is no way to
    /// write <c>sp => sp.GetRequiredService&lt;Foo&lt;&gt;&gt;()</c>), so an open generic class
    /// with 2+ exact-match service types instead gets one independent Type-pair registration per
    /// service type, and instances are not shared across them either (see SSAL010).
    /// </para>
    /// </remarks>
    public bool RequiresForwarding =>
        !IsOpenGeneric
        && Mode != (int)WellKnownRegistrationMode.TryAddEnumerable
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
