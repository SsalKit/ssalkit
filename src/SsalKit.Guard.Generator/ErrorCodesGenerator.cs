using Microsoft.CodeAnalysis;

namespace SsalKit.Guard.Generator;

/// <summary>
/// Generates the exception → error-code mapping table, and the per-code factory and throw helpers,
/// for every <see langword="static"/> <see langword="partial"/> class decorated with
/// <c>[SsalKit.Guard.ErrorCodes&lt;TCode&gt;]</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffold only.</b> <see cref="Initialize"/> registers nothing yet, so the generator is inert:
/// it loads, produces no source and no diagnostics, and changes no existing behaviour. The
/// pipeline lands in stage 2; the attribute metadata names below are the contract both stages key
/// on and are already fixed here so the runtime assembly and the generator cannot drift apart.
/// </para>
/// <para>
/// Planned shape (design §5.3), for whoever picks this up:
/// </para>
/// <list type="number">
/// <item>
/// Three <c>ForAttributeWithMetadataName</c> sources — decorated exceptions
/// (<see cref="ErrorCodeAttributeMetadataName"/>), mapping containers
/// (<see cref="ErrorCodesAttributeMetadataName"/>), and the containers' external registrations
/// (<see cref="ExternalErrorCodeAttributeMetadataName"/>, read off the same container symbol as
/// the container source rather than through a separate provider).
/// </item>
/// <item>
/// <c>Collect</c> and <c>Combine</c>, then group by container: an exception joins a container when
/// their <c>TCode</c> match, which is what lets several code enums coexist in one assembly. An
/// exception whose <c>TCode</c> has no container is reported (SSALG008) instead of silently
/// dropped.
/// </item>
/// <item>
/// Order each container's entries by inheritance depth descending, ties broken by fully-qualified
/// name ordinal. That is the derived-before-base guarantee, and the FQN tiebreak keeps output
/// deterministic so incremental caching and snapshot tests stay stable.
/// </item>
/// <item>
/// Split the analysis result into two projections — models and diagnostics — so an edit that only
/// changes a diagnostic leaves every generated file untouched, and vice versa.
/// </item>
/// </list>
/// <para>
/// Models are records over <c>EquatableArray&lt;T&gt;</c>, diagnostics travel as the toolkit's
/// cache-safe <c>DiagnosticInfo</c>/<c>LocationInfo</c>, emission uses <c>IndentedCodeWriter</c>
/// with <c>WriteDocLines</c>, hint names go through <c>HintNameSanitizer</c>, and the descriptor
/// table is built from <c>DiagnosticDescriptorFactory("SSALG", "SsalKit.Guard")</c>.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class ErrorCodesGenerator : IIncrementalGenerator
{
    /// <summary>
    /// The metadata name of <c>SsalKit.Guard.ErrorCodeAttribute&lt;TCode&gt;</c>, which declares an
    /// exception type's code.
    /// </summary>
    /// <remarks>
    /// These are generic attributes, so the name carries the CLR arity suffix
    /// (<c>`1</c>) — <c>ForAttributeWithMetadataName</c> matches on the metadata name and will
    /// silently find nothing if the suffix is omitted.
    /// </remarks>
    internal const string ErrorCodeAttributeMetadataName = "SsalKit.Guard.ErrorCodeAttribute`1";

    /// <summary>
    /// The metadata name of <c>SsalKit.Guard.ErrorCodesAttribute&lt;TCode&gt;</c>, which marks a
    /// mapping container.
    /// </summary>
    internal const string ErrorCodesAttributeMetadataName = "SsalKit.Guard.ErrorCodesAttribute`1";

    /// <summary>
    /// The metadata name of <c>SsalKit.Guard.ExternalErrorCodeAttribute&lt;TCode&gt;</c>, which
    /// registers an exception type the consumer does not own.
    /// </summary>
    internal const string ExternalErrorCodeAttributeMetadataName = "SsalKit.Guard.ExternalErrorCodeAttribute`1";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Intentionally empty until stage 2. Registering nothing is the correct inert state: a
        // half-wired pipeline would emit partial mapping tables into consumers' builds.
        _ = context;
    }
}
