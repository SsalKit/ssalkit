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
/// covers the code that actually runs.
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
public sealed class DeterministicAttribute : Attribute;
