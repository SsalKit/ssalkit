; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID  | Category           | Severity | Notes
---------|--------------------|----------|-------------------------------------------------------------
SSALR001 | SsalKit.Randomness | Error    | Unsupported [RandomWeight] member type
SSALR002 | SsalKit.Randomness | Error    | A type can declare only one [RandomWeight] member
SSALR003 | SsalKit.Randomness | Error    | [RandomWeight] member must be a readable instance member
SSALR004 | SsalKit.Randomness | Error    | [RandomWeight] member must be accessible to generated code
SSALR005 | SsalKit.Randomness | Error    | [RandomWeight] cannot be applied to a member of a generic type
SSALR006 | SsalKit.Randomness | Error    | [RandomWeight] cannot be applied to a member of a ref struct
