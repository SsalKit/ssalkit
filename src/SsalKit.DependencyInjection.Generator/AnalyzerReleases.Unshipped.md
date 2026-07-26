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
SSAL016 | SsalKit.DependencyInjection | Error    | [ServiceFactory] can only be applied to an interface
SSAL017 | SsalKit.DependencyInjection | Error    | [ServiceFactory] interface must declare exactly one method and inherit no implementable member
SSAL018 | SsalKit.DependencyInjection | Error    | [ServiceFactory] method must take a single enum parameter and return a service type
SSAL019 | SsalKit.DependencyInjection | Error    | [ServiceFactory] cannot be applied to a generic interface or one nested inside a generic type
SSAL020 | SsalKit.DependencyInjection | Error    | [ServiceFactory] type must be accessible to generated code
SSAL021 | SsalKit.DependencyInjection | Error    | [RegisterImplementationsOf] contract must be an interface
SSAL022 | SsalKit.DependencyInjection | Warning  | [RegisterImplementationsOf] contract matched no class in this assembly
SSAL023 | SsalKit.DependencyInjection | Error    | Duplicate [RegisterImplementationsOf] contract
SSAL024 | SsalKit.DependencyInjection | Error    | Undefined enum value on [RegisterImplementationsOf]
SSAL025 | SsalKit.DependencyInjection | Error    | [RegisterImplementationsOf] contract must be accessible to generated code
SSAL026 | SsalKit.DependencyInjection | Warning  | Overlapping [RegisterImplementationsOf] contracts register the same implementation differently
SSAL027 | SsalKit.DependencyInjection | Warning  | A [Service] registration and a convention scan bind the same service type
SSAL028 | SsalKit.DependencyInjection | Warning  | Registered class has no public constructor

; SSAL003's title/message/description were revised to reflect its narrowed scope (a class nested
; inside a generic type, rather than every open generic class), but its ID, category, and severity
; are unchanged, so release tracking (which tracks ID/category/severity lifecycle, not message
; wording) has no "Changed Rules" entry to record for it.
;
; SSAL017's title/message/description were likewise broadened -- the rule now also rejects a factory
; interface that inherits an interface with implementable members of its own, which the generated
; class would have to implement -- with its ID, category, and severity unchanged.
