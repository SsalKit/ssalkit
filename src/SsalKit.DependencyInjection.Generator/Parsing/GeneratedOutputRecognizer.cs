using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;

namespace SsalKit.DependencyInjection.Generator.Parsing;

/// <summary>
/// Recognizes the types <em>this</em> generator emits, so the analyzers can leave them alone.
/// </summary>
/// <remarks>
/// <para>
/// The analyzers analyze generated code (<c>GeneratedCodeAnalysisFlags.Analyze |
/// ReportDiagnostics</c>), which is what makes a <c>[Service]</c> written by <em>another</em>
/// generator -- or an <c>[assembly: RegisterImplementationsOf]</c> injected through MSBuild's
/// <c>AssemblyAttribute</c> item, which lands in an auto-generated <c>AssemblyInfo.cs</c> -- get the
/// same diagnostics a hand-written one gets. The cost of that reach is that the analyzers also see
/// this generator's own output, which the generator itself never does: a source generator runs
/// against the compilation as it was before any generator ran, so <c>ConventionScanner</c> cannot
/// observe the factory implementation classes emitted alongside it, while an analyzer running on the
/// final compilation can.
/// </para>
/// <para>
/// Left alone, that asymmetry is a false negative waiting to happen: a contract whose only "match"
/// is a generated factory implementation would look satisfied to the analyzer (no SSAL022) while the
/// generator registered nothing for it. Excluding this generator's output restores the symmetry, and
/// it is applied at the shared <see cref="ConventionImplementationMatcher.IsCandidate"/> gate so the
/// analyzer and the scanner can never disagree about it.
/// </para>
/// <para>
/// Identification is by namespace rather than by hint name or <c>[GeneratedCode]</c>:
/// <see cref="GeneratedNamespaceRoot"/> is reserved for this generator and documented as such, it is
/// visible on the symbol alone (no syntax tree or attribute lookup), and it costs one string
/// comparison per named type. The other file this generator emits -- the
/// <c>Add{Assembly}Services</c> extension class -- needs no rule of its own: it is a
/// <see langword="static"/> class carrying no <c>[Service]</c>, so every gate that could reach it
/// already rejects it.
/// </para>
/// </remarks>
internal static class GeneratedOutputRecognizer
{
    /// <summary>
    /// The namespace root every generated <c>[ServiceFactory]</c> implementation is emitted into.
    /// Reserved for the generator: consumer code is not expected to declare types under it.
    /// </summary>
    public const string GeneratedNamespaceRoot = "SsalKit.DependencyInjection.Generated";

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> was emitted by this generator,
    /// i.e. when it lives in <see cref="GeneratedNamespaceRoot"/> or a namespace nested inside it.
    /// </summary>
    public static bool IsGeneratorOutput(INamedTypeSymbol type)
    {
        // A nested type reports the enclosing *namespace*, not its containing type, so this answers
        // the same question for a nested generated type as for a top-level one.
        var namespaceName = SymbolFacts.GetContainingNamespaceName(type);

        if (!namespaceName.StartsWith(GeneratedNamespaceRoot, StringComparison.Ordinal))
        {
            return false;
        }

        // Exactly the root, or a segment boundary immediately after it -- never a namespace that
        // merely starts with the same characters (e.g. "SsalKit.DependencyInjection.GeneratedFoo").
        return namespaceName.Length == GeneratedNamespaceRoot.Length
            || namespaceName[GeneratedNamespaceRoot.Length] == '.';
    }
}
