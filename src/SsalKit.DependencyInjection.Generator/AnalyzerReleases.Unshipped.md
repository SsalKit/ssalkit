; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category                    | Severity | Notes
--------|-----------------------------|----------|-------------------------------------------------------------------
SSAL006 | SsalKit.DependencyInjection | Error    | RegistrationMode.TryAddEnumerable cannot register a type as itself
SSAL007 | SsalKit.DependencyInjection | Error    | [Service] type must be accessible to generated code
SSAL008 | SsalKit.DependencyInjection | Error    | Undefined enum value on [Service]
