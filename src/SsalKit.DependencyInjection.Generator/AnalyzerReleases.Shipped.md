## Release 0.1.0

### New Rules

Rule ID | Category                    | Severity | Notes
--------|-----------------------------|----------|-------------------------------------------------------------------
SSAL001 | SsalKit.DependencyInjection | Error    | [Service] cannot be applied to an abstract or static class
SSAL002 | SsalKit.DependencyInjection | Error    | The type specified by 'As' is not implemented or inherited by the decorated class
SSAL003 | SsalKit.DependencyInjection | Error    | [Service] cannot be applied to a class nested inside a generic type
SSAL004 | SsalKit.DependencyInjection | Warning  | Duplicate (ServiceType, ImplementationType, Key) service registration
SSAL005 | SsalKit.DependencyInjection | Error    | 'Key' cannot be combined with RegistrationMode.TryAddEnumerable

; The Notes column carries no lifecycle meaning (release tracking compares ID, category, and
; severity only), so it is kept in step with each rule's current title rather than frozen at the
; wording it shipped with: SSAL003's note said "an open generic class" from before the rule was
; narrowed to a class nested inside a generic type, which described a rule that no longer exists.
