using SsalKit.Generators.Toolkit;

namespace SsalKit.Guard.Generator.Models;

/// <summary>
/// The outcome of joining every collected container with every collected exception: the containers
/// that get code, and the diagnostics that get reported. Kept as one pipeline node with two
/// projections off it so the join happens once, while source emission and diagnostic reporting still
/// cache independently -- an edit that only changes a diagnostic does not re-emit any source, and
/// vice versa.
/// </summary>
/// <param name="Containers">The containers to generate a part for, ordered by fully qualified name.</param>
/// <param name="Diagnostics">Every diagnostic to report, in a deterministic order.</param>
internal sealed record ErrorCodesAnalysisResult(
    EquatableArray<ErrorCodesContainerModel> Containers,
    EquatableArray<DiagnosticInfo> Diagnostics);
