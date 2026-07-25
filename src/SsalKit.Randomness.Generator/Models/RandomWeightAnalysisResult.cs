using SsalKit.Generators.Toolkit;

namespace SsalKit.Randomness.Generator.Models;

/// <summary>
/// The outcome of grouping every collected member by declaring type: the types that get code, and
/// the diagnostics that get reported. Kept as one pipeline node with two projections off it so the
/// grouping work happens once, while source emission and diagnostic reporting still cache
/// independently -- an edit that only changes a diagnostic does not re-emit any source, and vice
/// versa.
/// </summary>
/// <param name="Types">The types to generate an extension class for, ordered by fully qualified name.</param>
/// <param name="Diagnostics">Every diagnostic to report, in a deterministic order.</param>
internal sealed record RandomWeightAnalysisResult(
    EquatableArray<WeightedTypeModel> Types,
    EquatableArray<DiagnosticInfo> Diagnostics);
