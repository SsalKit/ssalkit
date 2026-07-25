; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID  | Category      | Severity | Notes
---------|---------------|----------|------------------------------------------------------------
SSALG001 | SsalKit.Guard | Error    | [ErrorCode] requires an ErrorCodedException
SSALG002 | SsalKit.Guard | Error    | [ErrorCodes] container must be a static partial class
SSALG003 | SsalKit.Guard | Error    | Duplicate error-code registration
SSALG004 | SsalKit.Guard | Error    | [ExternalErrorCode] requires an exception type
SSALG005 | SsalKit.Guard | Error    | [ErrorCode] exception must be concrete and non-generic
SSALG006 | SsalKit.Guard | Warning  | No factory or throw helper is generated for the exception
SSALG007 | SsalKit.Guard | Error    | [ErrorCodes] container cannot be generic
SSALG008 | SsalKit.Guard | Warning  | No mapping container for the declared code enum
