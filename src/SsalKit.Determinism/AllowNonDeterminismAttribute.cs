namespace SsalKit.Determinism;

/// <summary>
/// Excludes this member (or nested type) from the surrounding
/// <see cref="DeterministicAttribute"/> scope, so the analyzer stops reporting <c>SSALD</c>
/// diagnostics inside it.
/// </summary>
/// <remarks>
/// <para>
/// This is the reviewable escape hatch: unlike a <c>#pragma warning disable</c> it names the whole
/// member rather than one call site, and it shows up in the declaration where a reader is already
/// looking. Use <see cref="Justification"/> to record why the exemption is legitimate -- it is not
/// required and no diagnostic asks for it, but a bare exemption tells the next reader nothing.
/// </para>
/// <para>
/// Scope resolution is lexical and nearest-wins, so exemptions nest both ways: a
/// <c>[AllowNonDeterminism]</c> type inside a <c>[Deterministic]</c> one is exempt, and a
/// <c>[Deterministic]</c> member inside that exempt type is analyzed again, because it is the
/// nearer marking.
/// </para>
/// <para>
/// Outside every <c>[Deterministic]</c> scope this attribute does nothing -- there is no diagnostic
/// to suppress there -- so such an orphan application is itself reported as <c>SSALD007</c>, on the
/// theory that a marking which silently does nothing is worse than no marking at all.
/// </para>
/// <para>
/// The attribute has no runtime behaviour; it is inert whenever the analyzer is not running.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Deterministic]
/// public sealed class BattleSimulation
/// {
///     public void Tick(DateTimeOffset asOf) { /* ... */ }
///
///     [AllowNonDeterminism(Justification = "wall-clock logging only; never feeds simulation state")]
///     private static void LogTick(int tick) =&gt; Console.WriteLine($"{DateTime.UtcNow:O} tick {tick}");
/// }
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method
        | AttributeTargets.Constructor | AttributeTargets.Property,
    AllowMultiple = false,
    Inherited = false)]
public sealed class AllowNonDeterminismAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the reason this scope is exempt from the surrounding
    /// <see cref="DeterministicAttribute"/> scope.
    /// </summary>
    /// <remarks>
    /// Purely documentary: nothing reads it at run time and no diagnostic requires it. It exists so
    /// the exemption carries its own justification into code review instead of being an unexplained
    /// marking. Recording <em>why</em> the non-determinism is harmless (it never feeds persisted or
    /// replayed state, say) is what lets a later reader decide whether it still is.
    /// </remarks>
    public string? Justification { get; set; }
}
