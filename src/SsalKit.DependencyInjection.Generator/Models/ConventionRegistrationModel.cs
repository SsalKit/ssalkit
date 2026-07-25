namespace SsalKit.DependencyInjection.Generator.Models;

/// <summary>
/// An equatable, compilation-independent representation of a single registration produced by an
/// <c>[assembly: RegisterImplementationsOf]</c> convention scan: one (service type, implementation
/// type) pair, already resolved to the fully-qualified spellings the emitter writes verbatim.
/// </summary>
/// <param name="ContractFqn">
/// The fully-qualified spelling of the <em>declared</em> contract -- typeof-form (e.g.
/// <c>global::Ns.IHandler&lt;,&gt;</c>) when it was declared as an unbound generic type reference.
/// Never emitted; it exists only as the primary sort key, which groups every registration produced
/// by one declaration together and keeps emission order independent of the order the attributes
/// happen to appear in source.
/// </param>
/// <param name="ServiceTypeFqn">
/// The service type actually registered: the contract itself for a non-generic/closed contract, or
/// the specific instantiation the implementation was found to implement for an unbound one (in
/// typeof-form when <see cref="IsOpenGeneric"/>).
/// </param>
/// <param name="ImplementationTypeFqn">
/// The matched class, in typeof-form when <see cref="IsOpenGeneric"/> and in ordinary
/// fully-qualified form otherwise.
/// </param>
/// <param name="Lifetime">The underlying integral value of <c>Microsoft.Extensions.DependencyInjection.ServiceLifetime</c>.</param>
/// <param name="Mode">The underlying integral value of <c>SsalKit.DependencyInjection.RegistrationMode</c>.</param>
/// <param name="IsOpenGeneric">
/// Whether both the service and implementation type are open generics, and the emitter must
/// therefore render the <c>Type</c>-based registration overloads instead of the closed
/// <c>&lt;TService, TImpl&gt;</c> generic-argument form. A convention scan only ever produces this
/// pairing for a matched open generic class, and only when the same exact-match rule that governs
/// an open generic <c>[Service]</c> registration (SSAL009) holds.
/// </param>
internal sealed record ConventionRegistrationModel(
    string ContractFqn,
    string ServiceTypeFqn,
    string ImplementationTypeFqn,
    int Lifetime,
    int Mode,
    bool IsOpenGeneric);
