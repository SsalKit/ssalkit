; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID  | Category           | Severity | Notes
---------|--------------------|----------|-------------------------------------------------------------
SSALD001 | SsalKit.Determinism | Warning  | Ambient time API used inside a [Deterministic] scope
SSALD002 | SsalKit.Determinism | Warning  | Non-deterministic randomness used inside a [Deterministic] scope
SSALD003 | SsalKit.Determinism | Warning  | GUID generation used inside a [Deterministic] scope
SSALD004 | SsalKit.Determinism | Warning  | Per-process randomized hashing used inside a [Deterministic] scope
SSALD005 | SsalKit.Determinism | Warning  | Environment or process identity read inside a [Deterministic] scope
SSALD006 | SsalKit.Determinism | Warning  | Scheduling or parallelism API used inside a [Deterministic] scope
SSALD007 | SsalKit.Determinism | Warning  | [AllowNonDeterminism] applied outside any [Deterministic] scope
