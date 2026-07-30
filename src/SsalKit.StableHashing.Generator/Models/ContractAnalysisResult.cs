using SsalKit.Generators.Toolkit;

namespace SsalKit.StableHashing.Generator.Models;

/// <summary>
/// The result of <see cref="Parsing.ContractNameGrouper"/> folding every <see cref="ContractModel"/>
/// collected in the compilation into the two things the source-output stages consume: the
/// contracts to emit, and every diagnostic to report (each contract's own, plus the cross-type
/// SSALH011 duplicate-name diagnostics only this stage can compute).
/// </summary>
/// <param name="Types">The contracts to emit an extension class for.</param>
/// <param name="Diagnostics">Every diagnostic to report, in a deterministic order.</param>
internal sealed record ContractAnalysisResult(
    EquatableArray<ContractModel> Types,
    EquatableArray<DiagnosticInfo> Diagnostics);
