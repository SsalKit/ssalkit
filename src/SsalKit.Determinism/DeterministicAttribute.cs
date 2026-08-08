namespace SsalKit.Determinism;

/// <summary>
/// Declares that the code in this scope has to be deterministic, so the bundled analyzer reports a
/// <c>SSALD</c> diagnostic for every non-deterministic API it is called on directly.
/// </summary>
/// <remarks>
/// <para>
/// Applied to a type, the scope covers every member of that type <em>and</em> every nested type,
/// lexically. Applied to a method, constructor, or property, it covers that member alone -- including
/// the lambdas, local functions, and initializer expressions written inside it, since those are
/// lexically part of the member.
/// </para>
/// <para>
/// <b>The analysis is deliberately shallow: it only sees direct calls.</b> A
/// <c>[Deterministic]</c> method that calls an unmarked helper, which in turn reads
/// <see cref="System.DateTime.Now"/>, produces no diagnostic at all. No diagnostics is therefore
/// <em>not</em> a proof of determinism -- this is an assistive tool, not a guarantee. Mark the
/// helper types your deterministic core depends on with <c>[Deterministic]</c> too, so the scope
/// covers the code that actually runs. <see cref="Strict"/> turns that last sentence from a
/// discipline you have to remember into one the compiler checks; it does not make the analysis any
/// deeper.
/// </para>
/// <para>
/// Scope resolution is lexical and nearest-wins: walking outward from the code under analysis
/// through its containing members and containing types, whichever of <c>[Deterministic]</c> and
/// <see cref="AllowNonDeterminismAttribute"/> is found first decides. Nothing found means the code
/// is outside every scope and nothing is reported. Base types, implemented interfaces, and the
/// attribute's own inheritance play no part: <see cref="System.AttributeUsageAttribute.Inherited"/>
/// is <see langword="false"/> and the analyzer does not walk base types either, so a derived type
/// that needs the scope has to declare it again. That keeps the rule to a single sentence -- "the
/// scope is where you wrote it" -- which matches the shallowness of the analysis itself.
/// </para>
/// <para>
/// Interfaces are not a valid target: an attribute on an interface would not reach its
/// implementations, so allowing it would only create a marking that silently does nothing.
/// </para>
/// <para>
/// The attribute has no runtime behaviour. It carries no state that anything reads at run time, and
/// when the analyzer is not running (an older compiler host, or a reference assembly consumed
/// without its analyzers) it is simply inert.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Deterministic]
/// public sealed class BattleSimulation
/// {
///     private readonly DeterministicRandom _random;
///
///     public BattleSimulation(ulong seed) =&gt; _random = new DeterministicRandom(seed);
///
///     // Time arrives as an argument instead of being read from the ambient clock.
///     public void Tick(DateTimeOffset asOf) { /* ... */ }
/// }
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method
        | AttributeTargets.Constructor | AttributeTargets.Property,
    AllowMultiple = false,
    Inherited = false)]
public sealed class DeterministicAttribute : Attribute
{
    /// <summary>
    /// Gets a value indicating whether this scope additionally reports <c>SSALD008</c> for every
    /// member of the same assembly it calls directly that no <c>[Deterministic]</c> marking covers.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to require that everything this scope calls into is itself covered by
    /// a <c>[Deterministic]</c> marking. <see langword="false"/> (the default) reports only the
    /// banned APIs named directly in the scope, which is the behaviour of a plain
    /// <c>[Deterministic]</c>.
    /// </value>
    /// <remarks>
    /// <para>
    /// Because the analysis only sees direct calls, keeping a deterministic core honest normally
    /// depends on remembering to mark the helper types it leans on. Strict mode checks that instead
    /// of trusting it: a call into a member that no <c>[Deterministic]</c> covers -- neither on the
    /// member nor on any type containing it -- is reported, because that member's body is never
    /// analyzed by anything. There are two ways to answer it, and both are ordinary idioms:
    /// </para>
    /// <list type="number">
    /// <item><description>Mark the callee (or its containing type) <c>[Deterministic]</c>, and carve
    /// out the individual members that genuinely need the clock with
    /// <see cref="AllowNonDeterminismAttribute"/> <em>inside</em> it. This is the recommended
    /// shape.</description></item>
    /// <item><description>Mark the <em>calling</em> member
    /// <see cref="AllowNonDeterminismAttribute"/>, when the call is itself the deliberate
    /// non-determinism -- exactly how a direct call to a banned API is exempted.</description></item>
    /// </list>
    /// <para>
    /// What does <em>not</em> answer it is an <see cref="AllowNonDeterminismAttribute"/> standing
    /// alone on the callee. With no <c>[Deterministic]</c> above it that attribute suppresses
    /// nothing and is reported as an orphan in its own right, so it cannot be the thing that
    /// silences this rule either; both diagnostics say the same thing about it.
    /// </para>
    /// <para>
    /// <b>Strict mode does not deepen the analysis.</b> It never looks inside the member it reports:
    /// the question is whether a marking covers that member, not whether it is deterministic. The
    /// call graph is not walked, the check is exactly one hop, and silence still means only that no
    /// banned API is named where the analyzer can see it.
    /// </para>
    /// <para>
    /// Only members of the same assembly are reported, because those are the only ones you can mark.
    /// Interface members, compiler-synthesized ones (a record's <c>Equals</c> and
    /// <c>Deconstruct</c>, an implicit constructor, a delegate's <c>Invoke</c>) and source-generated
    /// code are left alone because there is nowhere to write the attribute, and declarations with no
    /// body of their own -- an auto-implemented property, an <c>abstract</c> or <c>extern</c>
    /// member, and everything about a positional record -- because there is nothing behind them to
    /// analyze.
    /// </para>
    /// <para>
    /// Strict is part of the scope, so it follows the same nearest-wins rule as the marking that
    /// carries it: a nested <c>[Deterministic]</c> without <c>Strict</c> turns it off inside that
    /// nested scope, which is the supported way to relax it locally.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Deterministic(Strict = true)]
    /// public sealed class ReplayRunner
    /// {
    ///     // Reported: DamageTable carries no marking, so nothing has ever looked inside it.
    ///     public int Apply(int roll) =&gt; DamageTable.Lookup(roll);
    /// }
    ///
    /// [Deterministic]
    /// internal static class DamageTable
    /// {
    ///     public static int Lookup(int roll) =&gt; roll * 2;
    /// }
    /// </code>
    /// </example>
    public bool Strict { get; init; }
}
