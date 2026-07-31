using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;

namespace SsalKit.Determinism.Analyzer.Diagnostics;

/// <summary>
/// The <c>SSALD</c> diagnostic table reported by <see cref="DeterminismAnalyzer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every rule is a <see cref="DiagnosticSeverity.Warning"/>, without exception. This package is an
/// assistive tool rather than a gate: the analysis only sees direct calls, so treating a hit as a
/// build-breaking error would put a false sense of completeness behind a check that cannot be
/// complete. A consumer who wants a gate raises the severity per id (or per category) in
/// <c>.editorconfig</c>, which is also why the catalog is split across six ids by category rather
/// than reported under one -- the id <em>is</em> the tuning knob.
/// </para>
/// <para>
/// Every message follows one shape: <c>'{0}' is non-deterministic: &lt;why&gt;. &lt;what to do
/// instead&gt;.</c> The offending member always appears as <c>{0}</c>, and the replacement clause is
/// fixed per category, naming the concrete SsalKit construct that solves it -- which is the whole
/// difference between this package and a generic banned-API list.
/// </para>
/// </remarks>
internal static class DiagnosticDescriptors
{
    private static readonly DiagnosticDescriptorFactory Factory = new("SSALD", "SsalKit.Determinism");

    /// <summary>
    /// SSALD001: an ambient wall-clock or ambient-timer API is used inside a
    /// <c>[Deterministic]</c> scope.
    /// </summary>
    public static readonly DiagnosticDescriptor AmbientTime = Factory.Warning(
        1,
        "Non-deterministic ambient time",
        "'{0}' is non-deterministic: it reads the ambient clock, so the same code produces a different value on every run. Inject a TimeProvider, or take the instant as an argument (the 'DateTimeOffset asOf' parameter shape SsalKit.Timekeeping uses), so the caller decides what time it is",
        "Reading the current time from a static, ambient source makes a result depend on when it ran rather than on its inputs, which breaks replay, re-simulation and reproducible tests alike. Both replacements move the decision to the caller: a TimeProvider passed into the constructor can be a FakeTimeProvider in a test, and an explicit instant parameter makes the dependency visible in the signature.");

    /// <summary>
    /// SSALD002: a process-seeded or cryptographic random source is used inside a
    /// <c>[Deterministic]</c> scope.
    /// </summary>
    public static readonly DiagnosticDescriptor NonDeterministicRandomness = Factory.Warning(
        2,
        "Non-deterministic randomness",
        "'{0}' is non-deterministic: its sequence cannot be reproduced across runs, processes, or runtime versions. Use SsalKit.Randomness' DeterministicRandom (explicit seed, exportable state) or inject an IRandomSource, so the caller decides where the numbers come from",
        "A reproducible run needs a random sequence that is a function of an explicit seed. System.Random's algorithm is not part of its contract and has already changed between runtime versions, so even the seeded constructor is unsafe for cross-process or cross-version reproducibility; cryptographic sources are non-deterministic by definition. DeterministicRandom pins the algorithm (xoshiro256**) as a versioned contract and can export and restore its state.");

    /// <summary>
    /// SSALD003: a GUID-generating API is used inside a <c>[Deterministic]</c> scope.
    /// </summary>
    public static readonly DiagnosticDescriptor GuidGeneration = Factory.Warning(
        3,
        "Non-deterministic identifier generation",
        "'{0}' is non-deterministic: every generated GUID carries random bits, so the same code produces a different identifier on every run. Derive the identifier from the data instead -- SsalKit.StableHashing's ComputeStableHash(), or bytes drawn from a seeded DeterministicRandom",
        "An identifier that is generated rather than derived cannot be reproduced by re-running the code that produced it, so a replayed run diverges from the original at the first identifier. Guid.CreateVersion7 is included even in its explicit-timestamp form, because the low bits are still random. Deriving the identifier from the data that defines it -- a stable hash of the entity's identity, or bytes from a seeded generator -- keeps the same input producing the same identifier.");

    /// <summary>
    /// SSALD004: a per-process randomized hash API is used inside a <c>[Deterministic]</c> scope.
    /// </summary>
    public static readonly DiagnosticDescriptor RandomizedHashing = Factory.Warning(
        4,
        "Non-deterministic hash code",
        "'{0}' is non-deterministic: .NET randomizes this hash seed per process, so the value differs between runs and between machines. Use SsalKit.StableHashing -- [StableHashContract] plus ComputeStableHash() -- for a checksum that is stable across processes and platforms",
        "GetHashCode is contractually only stable within a single process, and .NET actively randomizes string and HashCode seeds per process to defend against hash-flooding. Persisting such a value, sharding on it, or comparing it across machines therefore produces results that change on restart. Only the calls that resolve to the framework's own randomized implementations (System.Object, System.ValueType, System.String, System.HashCode, System.StringComparer) are reported: a call that resolves to a user-written override is left alone, since that implementation is analyzed in its own right.");

    /// <summary>
    /// SSALD005: an environment, machine, process, or thread identity API is used inside a
    /// <c>[Deterministic]</c> scope.
    /// </summary>
    public static readonly DiagnosticDescriptor EnvironmentIdentity = Factory.Warning(
        5,
        "Non-deterministic environment or process identity",
        "'{0}' is non-deterministic: it reads an identifier of the machine, process, or thread the code happens to be running on. Pass the value in as explicit configuration, so the same input always produces the same result",
        "A result that depends on the host it ran on is reproducible only on that host, which defeats replay across machines and makes a test's outcome depend on the agent it landed on. Taking the value as a constructor argument or an option keeps the dependency visible and lets a test pin it.");

    /// <summary>
    /// SSALD006: a scheduling or parallelism API is used inside a <c>[Deterministic]</c> scope.
    /// </summary>
    public static readonly DiagnosticDescriptor SchedulingAndParallelism = Factory.Warning(
        6,
        "Non-deterministic scheduling or parallelism",
        "'{0}' is non-deterministic: it hands execution order and timing to the thread scheduler, so interleaving differs between runs. Deterministic code has to run sequentially on one thread -- there is no drop-in replacement, the work has to be restructured",
        "Unlike the other categories this one has no substitute API to point at: the non-determinism is the concurrency itself, not a particular call. If the parallel work is genuinely order-independent, keep it outside the deterministic scope and feed its result in; if it is not, it has to become sequential. This is also the category most likely to be tuned down wholesale in .editorconfig, which is why it has an id of its own.");

    /// <summary>
    /// SSALD007: <c>[AllowNonDeterminism]</c> is applied outside any <c>[Deterministic]</c> scope,
    /// where it suppresses nothing.
    /// </summary>
    public static readonly DiagnosticDescriptor OrphanAllowNonDeterminism = Factory.Warning(
        7,
        "[AllowNonDeterminism] outside a [Deterministic] scope",
        "'{0}' has [AllowNonDeterminism] but neither it nor any type or member containing it has [Deterministic], so the attribute suppresses nothing",
        "[AllowNonDeterminism] only has an effect inside a [Deterministic] scope, because that is the only place a diagnostic exists to suppress. An orphan application therefore reads as a deliberate exemption while doing nothing at all -- typically because the [Deterministic] marking it was paired with was removed, or was never added. Remove the attribute, or mark the enclosing type or member [Deterministic].");
}
