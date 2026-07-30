; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID  | Category           | Severity | Notes
---------|--------------------|----------|-------------------------------------------------------------
SSALH001 | SsalKit.StableHashing | Error    | Duplicate [StableHashMember] id within a contract
SSALH002 | SsalKit.StableHashing | Error    | Unsupported [StableHashMember] member type
SSALH003 | SsalKit.StableHashing | Error    | DateTime is not supported; use DateTimeOffset or DateOnly
SSALH004 | SsalKit.StableHashing | Error    | Member type has no [StableHashContract]
SSALH005 | SsalKit.StableHashing | Error    | Circular [StableHashContract] graph
SSALH006 | SsalKit.StableHashing | Error    | class [StableHashContract] must be sealed
SSALH007 | SsalKit.StableHashing | Error    | [StableHashMember] member (or its declaring type) is not accessible to generated code
SSALH008 | SsalKit.StableHashing | Error    | [StableHashMember] id must be 1 or greater
SSALH009 | SsalKit.StableHashing | Error    | [StableHashContract] name must not be null/whitespace and Version must be 1 or greater
SSALH010 | SsalKit.StableHashing | Warning  | [StableHashContract] declares no [StableHashMember]
SSALH011 | SsalKit.StableHashing | Warning  | Duplicate [StableHashContract] name within the compilation
SSALH012 | SsalKit.StableHashing | Warning  | [StableHashMember] on a type with no [StableHashContract]
SSALH013 | SsalKit.StableHashing | Error    | [StableHashContract] cannot be applied to a generic type
