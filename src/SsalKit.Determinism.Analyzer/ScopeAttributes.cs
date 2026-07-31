using Microsoft.CodeAnalysis;

namespace SsalKit.Determinism.Analyzer;

/// <summary>
/// The two <c>SsalKit.Determinism</c> attribute symbols, resolved once per compilation.
/// </summary>
/// <remarks>
/// Both are required: a compilation that resolves neither does not reference the runtime package at
/// all, so no scope can exist in it and the analyzer registers nothing. Resolving them once at
/// compilation start rather than per operation is what keeps the per-operation path down to a
/// containing-symbol walk and a dictionary lookup.
/// </remarks>
internal sealed class ScopeAttributes
{
    private const string DeterministicAttributeMetadataName = "SsalKit.Determinism.DeterministicAttribute";
    private const string AllowNonDeterminismAttributeMetadataName = "SsalKit.Determinism.AllowNonDeterminismAttribute";

    private ScopeAttributes(INamedTypeSymbol deterministic, INamedTypeSymbol allowNonDeterminism)
    {
        Deterministic = deterministic;
        AllowNonDeterminism = allowNonDeterminism;
    }

    /// <summary>Gets the <c>[Deterministic]</c> attribute symbol.</summary>
    public INamedTypeSymbol Deterministic { get; }

    /// <summary>Gets the <c>[AllowNonDeterminism]</c> attribute symbol.</summary>
    public INamedTypeSymbol AllowNonDeterminism { get; }

    /// <summary>
    /// Resolves both attribute symbols from <paramref name="compilation"/>, or returns
    /// <see langword="null"/> when either is missing.
    /// </summary>
    /// <param name="compilation">The compilation under analysis.</param>
    /// <returns>The resolved pair, or <see langword="null"/>.</returns>
    public static ScopeAttributes? Resolve(Compilation compilation)
    {
        var deterministic = compilation.GetTypeByMetadataName(DeterministicAttributeMetadataName);
        var allowNonDeterminism = compilation.GetTypeByMetadataName(AllowNonDeterminismAttributeMetadataName);

        return deterministic is null || allowNonDeterminism is null
            ? null
            : new ScopeAttributes(deterministic, allowNonDeterminism);
    }
}
