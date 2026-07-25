; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category                    | Severity | Notes
--------|-----------------------------|----------|-------------------------------------------------------------------
SSAL006 | SsalKit.DependencyInjection | Error    | RegistrationMode.TryAddEnumerable cannot register a type as itself
SSAL007 | SsalKit.DependencyInjection | Error    | [Service] type must be accessible to generated code
SSAL008 | SsalKit.DependencyInjection | Error    | Undefined enum value on [Service]
SSAL009 | SsalKit.DependencyInjection | Error    | Open generic service type must use the class's own type parameters
SSAL010 | SsalKit.DependencyInjection | Warning  | Open generic registrations do not share an instance across service types
SSAL011 | SsalKit.DependencyInjection | Error    | 'Factory' method not found
SSAL012 | SsalKit.DependencyInjection | Error    | 'Factory' method has an unusable signature
SSAL013 | SsalKit.DependencyInjection | Error    | 'Factory' cannot be used on an open generic class
SSAL014 | SsalKit.DependencyInjection | Error    | 'Factory' method is not accessible to generated code
SSAL015 | SsalKit.DependencyInjection | Warning  | Multiple implementations registered for the same service type

; SSAL003's title/message/description were revised to reflect its narrowed scope (a class nested
; inside a generic type, rather than every open generic class), but its ID, category, and severity
; are unchanged, so release tracking (which tracks ID/category/severity lifecycle, not message
; wording) has no "Changed Rules" entry to record for it.
