using Microsoft.CodeAnalysis;

namespace SsalKit.Determinism.Analyzer;

/// <summary>
/// Answers the question every <c>SSALD</c> diagnostic starts from -- is this code inside a
/// <c>[Deterministic]</c> scope? -- and the weaker variant of it that SSALD007 and SSALD008 ask
/// about a symbol instead of about a scope.
/// </summary>
/// <remarks>
/// <para>
/// The rule is purely lexical and nearest-wins. Starting at the symbol the code under analysis
/// belongs to, the chain of containing symbols (member, then containing type, then its containing
/// type, ...) is walked outward, and whichever of <c>[Deterministic]</c> and
/// <c>[AllowNonDeterminism]</c> is met first decides. Meeting neither by the time the walk leaves
/// the type nesting means the code is outside every scope.
/// </para>
/// <para>
/// Base types and implemented interfaces are deliberately <b>not</b> walked, matching
/// <c>Inherited = false</c> on both attributes. The scope is where it was written and nowhere else,
/// which is the only rule simple enough to stay predictable next to an analysis that already only
/// sees direct calls. <c>partial</c> declarations need no special handling: Roslyn merges a type's
/// attributes across its parts, so a marking on any part applies to the whole type.
/// </para>
/// <para>
/// Lambdas, local functions, and field or property initializers also need no special handling. None
/// of them is a symbol the containing-symbol walk stops at in a way that hides the enclosing
/// marking: an operation inside a lambda or a local function reports the enclosing member as its
/// containing symbol, and one inside an initializer reports the field or property, whose own walk
/// continues into the declaring type.
/// </para>
/// </remarks>
internal static class DeterministicScope
{
    /// <summary>
    /// The name of <c>DeterministicAttribute.Strict</c>. It is an <c>init</c>-only property, so it
    /// can only ever reach an application as a named argument.
    /// </summary>
    private const string StrictPropertyName = "Strict";

    /// <summary>
    /// Whether code belonging to <paramref name="symbol"/> is inside a <c>[Deterministic]</c> scope,
    /// and whether that scope asked for strict mode.
    /// </summary>
    /// <remarks>
    /// <paramref name="strict"/> is carried out of the same single walk rather than resolved by a
    /// second one, because it is a property of the marking that <em>won</em>: strict mode obeys the
    /// nearest-wins rule like everything else about a scope, so a nested <c>[Deterministic]</c>
    /// without <c>Strict</c> switches it off inside that nested scope. Deliberately narrowing there
    /// is the point -- it keeps the scope rule to one sentence ("the nearest marking decides")
    /// instead of introducing a third, inherited state.
    /// </remarks>
    /// <param name="symbol">The containing symbol of the operation under analysis.</param>
    /// <param name="attributes">The resolved attribute symbols for this compilation.</param>
    /// <param name="strict">Whether the winning <c>[Deterministic]</c> set <c>Strict = true</c>.
    /// Always <see langword="false"/> when the return value is <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the nearest marking is <c>[Deterministic]</c>.</returns>
    public static bool IsInsideDeterministicScope(ISymbol? symbol, ScopeAttributes attributes, out bool strict)
    {
        for (var current = symbol; current is not null && !(current is INamespaceSymbol); current = Enclosing(current))
        {
            switch (Classify(current, attributes, out strict))
            {
                // Classify already reports strict as false for an exemption, however the
                // [Deterministic] half of a contradictory pair was written.
                case ScopeMarking.Exempt:
                    return false;

                case ScopeMarking.Deterministic:
                    return true;

                default:
                    continue;
            }
        }

        strict = false;
        return false;
    }

    /// <summary>
    /// Whether <paramref name="symbol"/> or any symbol lexically containing it declares
    /// <c>[Deterministic]</c> -- the test behind both SSALD007 and SSALD008.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This asks a deliberately weaker question than
    /// <see cref="IsInsideDeterministicScope(ISymbol, ScopeAttributes, out bool)"/>: it looks for a
    /// <c>[Deterministic]</c> anywhere in the chain rather than for the nearest marking. The two
    /// rules that use it pass different symbols in and read the answer in opposite directions, but
    /// it is one question -- <em>is this covered by the contract at all?</em>
    /// </para>
    /// <para>
    /// <b>SSALD007</b> asks it about a symbol carrying <c>[AllowNonDeterminism]</c>, and reports
    /// when the answer is no. An <c>[AllowNonDeterminism]</c> nested inside another one is
    /// redundant, not orphaned, and redundant markings are not reported (design §5.1) -- only ones
    /// with no <c>[Deterministic]</c> above them at all, which is what makes them do nothing
    /// whatsoever. The symbol itself is part of the chain so that carrying both attributes at once
    /// does not read as an orphan.
    /// </para>
    /// <para>
    /// <b>SSALD008</b> asks it about a member a strict scope calls, and reports when the answer is
    /// no. Sharing the predicate is what keeps the two rules from contradicting each other: a bare
    /// <c>[AllowNonDeterminism]</c> on a standalone helper is an orphan to one rule and no coverage
    /// to the other, so both point the same way instead of one silencing what the other reports.
    /// </para>
    /// </remarks>
    /// <param name="symbol">The symbol whose coverage is in question.</param>
    /// <param name="attributes">The resolved attribute symbols for this compilation.</param>
    /// <returns><see langword="true"/> when some enclosing symbol declares <c>[Deterministic]</c>.</returns>
    public static bool HasDeterministicMarkingInChain(ISymbol symbol, ScopeAttributes attributes)
    {
        for (var current = symbol; current is not null && !(current is INamespaceSymbol); current = Enclosing(current))
        {
            if (HasAttribute(current, attributes.Deterministic))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The next symbol outward in the lexical chain.
    /// </summary>
    /// <remarks>
    /// This is <see cref="ISymbol.ContainingSymbol"/> with one correction: a property or event
    /// accessor's containing symbol is the declaring <em>type</em>, not the property or event it
    /// belongs to, so walking the raw chain would step straight past the very declaration the user
    /// wrote <c>[Deterministic]</c> on. <see cref="IMethodSymbol.AssociatedSymbol"/> is the missing
    /// link.
    /// </remarks>
    private static ISymbol? Enclosing(ISymbol symbol)
    {
        if (symbol is IMethodSymbol method && method.AssociatedSymbol is not null)
        {
            return method.AssociatedSymbol;
        }

        return symbol.ContainingSymbol;
    }

    /// <summary>
    /// Whether <paramref name="symbol"/> itself carries <c>[AllowNonDeterminism]</c>, returning the
    /// application so a diagnostic can be reported on the attribute the user wrote.
    /// </summary>
    /// <param name="symbol">The symbol to inspect.</param>
    /// <param name="attributes">The resolved attribute symbols for this compilation.</param>
    /// <param name="attributeData">The matching application, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the attribute is applied.</returns>
    public static bool TryGetAllowNonDeterminism(
        ISymbol symbol, ScopeAttributes attributes, out AttributeData attributeData)
    {
        foreach (var candidate in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attributes.AllowNonDeterminism))
            {
                attributeData = candidate;
                return true;
            }
        }

        attributeData = null!;
        return false;
    }

    /// <summary>
    /// The marking a single symbol declares.
    /// </summary>
    /// <remarks>
    /// A symbol carrying both attributes is classified as <see cref="ScopeMarking.Exempt"/>. The
    /// combination is contradictory and nothing sensible can be read from it, so the tie goes to the
    /// quieter outcome: the pair almost always arises from an exemption being added later to silence
    /// an existing marking, and choosing silence cannot produce a false positive. For the same
    /// reason <paramref name="strict"/> comes back <see langword="false"/> there, however the
    /// <c>[Deterministic]</c> half was written.
    /// </remarks>
    private static ScopeMarking Classify(ISymbol symbol, ScopeAttributes attributes, out bool strict)
    {
        var marking = ScopeMarking.None;
        strict = false;

        foreach (var attribute in symbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;

            if (SymbolEqualityComparer.Default.Equals(attributeClass, attributes.AllowNonDeterminism))
            {
                strict = false;
                return ScopeMarking.Exempt;
            }

            if (SymbolEqualityComparer.Default.Equals(attributeClass, attributes.Deterministic))
            {
                marking = ScopeMarking.Deterministic;
                strict = IsStrict(attribute);
            }
        }

        return marking;
    }

    /// <summary>
    /// Whether a <c>[Deterministic]</c> application set <c>Strict = true</c>.
    /// </summary>
    /// <remarks>
    /// An absent argument, an explicit <c>false</c>, and a value the compiler could not bind (an
    /// erroneous application, whose <see cref="TypedConstant.Value"/> is not a
    /// <see cref="bool"/>) all mean the same thing here: the default, which is the existing
    /// behaviour of every scope written before strict mode existed.
    /// </remarks>
    private static bool IsStrict(AttributeData attribute)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, StrictPropertyName, StringComparison.Ordinal))
            {
                return argument.Value.Value is bool strict && strict;
            }
        }

        return false;
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
            {
                return true;
            }
        }

        return false;
    }

    private enum ScopeMarking
    {
        None,
        Deterministic,
        Exempt,
    }
}
