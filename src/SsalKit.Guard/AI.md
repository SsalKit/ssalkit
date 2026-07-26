# SsalKit.Guard — AI contract sheet

Error-code-based domain exceptions: a side-effect-free `ErrorCodedException` base, static `Guard.` clauses that capture the caller's expression text, and a compile-time generated exception→code mapping table ordered derived-before-base.

- **TFM:** `net10.0`. **Package dependencies:** none (BCL only). Generic attributes require C# 11+.
- **Bundled analyzer:** `SsalKit.Guard.Generator` (`netstandard2.0`) ships inside the package under `analyzers/dotnet/cs`. No separate package.
- **Namespace:** `SsalKit.Guard` (all public types).
- This file is written for AI coding agents. Human-facing docs: [`README.md`](README.md) (also `README.ko.md`, `README.ja.md`).

## 1. API surface

### Pick the right construct

| Requirement | Use |
|---|---|
| Public-API **argument** validation | **BCL**: `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentOutOfRangeException.ThrowIf*` — not this package |
| Domain invariant / state precondition | `Guard.That`, `Guard.NotNull`, `Guard.NotNullOrEmpty`, `Guard.NotNullOrWhiteSpace`, `Guard.InRange` |
| Throw a domain-specific exception on failure | the `Func<Exception>` overloads of `Guard.That` / `Guard.NotNull` |
| Declare "this exception type means code X" | `[ErrorCode<TCode>(TCode.X)]` on the exception class |
| Give a BCL/third-party exception a code | `[ExternalErrorCode<TCode>(typeof(Foo), TCode.X)]` on the **container** |
| Get a mapping table + throw helpers | `[ErrorCodes<TCode>]` on a `static partial class` |
| Map at a boundary | `Container.TryMap(exception, out code)` (or `MapOrDefault`) |

### `Guard` — `static class`

| Member | Contract |
|---|---|
| `void That(bool condition, [CallerArgumentExpression] string? expression = null)` | Throws `GuardViolationException` when `false`. Message: `Guard.That ({expression}) failed.` |
| `void That(bool condition, Func<Exception> exceptionFactory)` | Throws the produced exception when `false`. `ArgumentNullException` if the factory is null; a null **result** becomes a `GuardViolationException` naming the clause. |
| `T NotNull<T>([NotNull] T? value, [CallerArgumentExpression] string? expression = null) where T : class` | Returns the non-null value. Detail: `value was null.` |
| `T NotNull<T>([NotNull] T? value, Func<Exception> exceptionFactory) where T : class` | Factory form (reference types only). |
| `T NotNull<T>([NotNull] T? value, [CallerArgumentExpression] string? expression = null) where T : struct` | Returns `value.Value`. **No factory overload for `Nullable<T>`.** |
| `string NotNullOrEmpty(string? value, ...)` | Detail: `value was null or empty.` |
| `string NotNullOrWhiteSpace(string? value, ...)` | Detail: `value was null, empty, or white-space.` |
| `T InRange<T>(T value, T min, T max, ...) where T : IComparable<T>` | Inclusive `[min, max]`. Returns `value`. |

`InRange` exception split: `min`/`max` null → `ArgumentNullException`; `min > max` → `ArgumentException` (the *check* is written wrong); `value` null or out of range → `GuardViolationException` (the *domain* said no). `NaN` never lies in a range; a `NaN` `min` is not treated as a broken range. Value, min, and max are rendered with the invariant culture.

### Runtime types

| Type | Contract |
|---|---|
| `abstract class ErrorCodedException : Exception` | Protected ctors `()`, `(string?)`, `(string?, Exception?)`. **No side effects** — no `Activity` tagging, no logging, no metrics. Carries **no code field**. Doubles as a single `catch (ErrorCodedException)` target. |
| `sealed class GuardViolationException : ErrorCodedException` | Thrown by every non-factory `Guard` clause. Public ctors `()`, `(string?)`, `(string?, Exception?)`. Carries no extra state. |

### Attributes

| Attribute | Target / multiplicity | Members |
|---|---|---|
| `ErrorCodeAttribute<TCode>(TCode code) where TCode : struct, Enum` | `Class`, `AllowMultiple = false`, not inherited | `TCode Code { get; }` |
| `ErrorCodesAttribute<TCode> where TCode : struct, Enum` | `Class`, `AllowMultiple = false`, not inherited | none (marker) |
| `ExternalErrorCodeAttribute<TCode>(Type exceptionType, TCode code) where TCode : struct, Enum` | `Class`, `AllowMultiple = **true**`, not inherited | `Type ExceptionType { get; }`, `TCode Code { get; }` |

### Generated into the `[ErrorCodes<TCode>]` container

| Member | Shape |
|---|---|
| `static bool TryMap(System.Exception? exception, out TCode code)` | Plain `is` chain, most-derived first. `false` (and `default(TCode)`) for an unregistered exception, including `null`. |
| `static TCode MapOrDefault(System.Exception? exception, TCode fallback)` | Same order; returns `fallback` when nothing matched. |
| `static TException {Name}(...)` | One **factory** per `[ErrorCode]` exception that exposes a recognised constructor. |
| `[DoesNotReturn] static void Throw{Name}(...)` | One **throw helper** per factory. |

Helper naming: exception type name minus a trailing `Exception` (`UserNotFoundException` → `UserNotFound` / `ThrowUserNotFound`). If two registered types would collide on the trimmed name, the whole type name is used; if that is still taken, a flattened fully-qualified name, then underscores are appended.

Mirrored constructor shapes (widest declared one only — one factory + one throw helper per exception, never one per constructor):

| Public constructor on the exception | Generated parameter list |
|---|---|
| `()` | `()` |
| `(string? message)` | `(string? message = null)` |
| `(string message)` | `(string message)` — non-nullable stays required |
| `(string?, Exception?)` | `(string? message = null, Exception? innerException = null)` |

Accessibility: the generated part re-declares the container with its own accessibility; `TryMap`/`MapOrDefault` are `public` unless the code enum is narrower, and each helper is `public` unless its exception type is narrower.

## 2. Contracts (versioned / immutable)

- **Derived before base, automatically.** The generated lookup tests registrations ordered by inheritance depth, deepest first, ties broken by fully qualified name (so output is deterministic). A derived exception always matches before its base, including when held in a base-typed variable — which is exactly how it arrives at a `catch`.
- **The code lives on the type, not on the instance.** `ErrorCodedException` has no code field. `throw new SomeException(4001, "...")` is deliberately unsupported: one code corresponds to one declared exception type, which is the `catch` target, the documentation, and the thing the compiler checks.
- **Exceptions are pure data.** No constructor in this package has an observability side effect. Tagging/logging/metrics belong at the boundary (`catch`, filter, middleware), where the request context exists.
- **Ambiguity is refused, not resolved.** Registering one exception type twice in a container (twice via `[ExternalErrorCode]`, or once there and once via its own `[ErrorCode]`) is `SSALG003`, an error, and the whole container's file is suppressed.
- **`TryMap` returns `TCode`, never a transport type.** Converting to HTTP/gRPC/wire integers is the consumer's job.
- **One container per class, one class per container — enforced by the language.** `AllowMultiple = false` is checked against a generic attribute's *definition*, so `[ErrorCodes<A>]` + `[ErrorCodes<B>]` on one class is `CS0579`; likewise `[ErrorCode<A>]` + `[ErrorCode<B>]` on one exception.
- **The generator only sees one compilation.** `[ErrorCode]` exceptions in referenced assemblies are invisible. Keep the container in the same project as its exceptions; use `[ExternalErrorCode]` for anything from elsewhere (it crosses assembly boundaries).
- **Externally registered types never get helpers** — this library cannot vouch for the constructor contract of a type it does not own. They take part in the lookup only.
- **Failure-message contract.** `Guard.{Clause} ({expression}) failed.` for `That`; `Guard.{Clause} ({expression}) failed: {detail}` for the rest. When the expression text is unavailable (a caller explicitly passing `null`/`""`, or a language that ignores `[CallerArgumentExpression]`) the placeholder is `<expression unavailable>`.
- **The success path allocates nothing.** Messages are composed only after a check has failed, and `Func<Exception>` factories are invoked only on failure.

### Diagnostic consequences

| Group | Effect |
|---|---|
| `SSALG001`, `SSALG004`, `SSALG005`, `SSALG009`, `SSALG010` | The offending **registration** is dropped; the rest of the container is generated. |
| `SSALG002`, `SSALG003`, `SSALG007` | The **container's whole generated file** is suppressed. |
| `SSALG006`, `SSALG008`, `SSALG011` | Warnings; the code still compiles but almost certainly does not do what was meant. |

## 3. DO NOT

- **DO NOT invent an ad-hoc code on an instance.** There is no `new SomeException(code, message)` shape — codes are declared on types with `[ErrorCode<TCode>]`. Write the small exception class.
- **DO NOT put two `[ErrorCodes<...>]` (or two `[ErrorCode<...>]`) on one class.** It is `CS0579`, a duplicate-attribute error, not "two containers". A second code enum needs a second container class.
- **DO NOT expect a container to collect `[ErrorCode]` exceptions from referenced assemblies.** It does not (`SSALG011` warns when the arrangement looks like that). Register cross-assembly types explicitly with `[ExternalErrorCode]`.
- **DO NOT register the same exception type twice in one container.** A type carrying `[ErrorCode]` is already registered in every container for its code enum, so adding `[ExternalErrorCode]` for it is always `SSALG003` — and the container then generates nothing at all.
- **DO NOT expect one factory per constructor.** Only the **widest** recognised shape is mirrored, once. An exception with domain-specific constructor parameters gets no helpers at all (`SSALG006`) and still maps fine — construct it directly, including inside a `Guard` factory overload.
- **DO NOT forget to map `GuardViolationException`.** It is declared in this package, so it cannot carry your `[ErrorCode]`. Without `[ExternalErrorCode<TCode>(typeof(GuardViolationException), TCode.X)]` on your container, every guard failure falls through the mapping as unmapped.
- **DO NOT use `Guard` for argument validation.** For parameter contracts use the BCL `ArgumentNullException.ThrowIfNull` / `ArgumentException.ThrowIfNullOrWhiteSpace` / `ArgumentOutOfRangeException.ThrowIf*` family, which throw the types callers and analyzers expect. `Guard` is for domain invariants that map to an error code.
- **DO NOT pass the `expression` parameter explicitly.** It is `[CallerArgumentExpression]`; supplying `null` or `""` degrades the message to `<expression unavailable>`.
- **DO NOT do work in an exception constructor.** No `Activity.Current` tagging, no logging, no metrics — that is the whole point of `ErrorCodedException`. Do it in the `catch`/filter at the boundary.
- **DO NOT declare an `[ErrorCodes]` container as anything but a non-generic `static partial class`** that is not `file`-local and is nested only inside `partial` types (`SSALG002`, `SSALG007`) — the generated part must be able to attach to it and re-declare the whole nesting chain.
- **DO NOT apply `[ErrorCode]` to a type that is abstract, generic, nested in a generic type, not derived from `ErrorCodedException`, or not nameable from a separate file** (`SSALG001`, `SSALG005`, `SSALG009`).
- **DO NOT rely on declaration order for match precedence.** Order is generated from inheritance depth; there is nothing to maintain, and nothing you can override.

## 4. Diagnostics

Prefix `SSALG`, category `SsalKit.Guard`. Reported by `ErrorCodesGenerator` directly (there is no companion `DiagnosticAnalyzer`).

| ID | Trigger | Fix |
|---|---|---|
| `SSALG001` | (Error) `[ErrorCode]` on a type that does not derive from `ErrorCodedException`. | Derive from it, or register the type on the container with `[ExternalErrorCode]`. |
| `SSALG002` | (Error) The `[ErrorCodes]` container is not a `static partial class` a generated file can attach to (not a class, not `static`, not `partial`, `file`-local, or nested in a non-`partial` type). | Declare `static partial class`, drop `file`, make every containing type `partial`. |
| `SSALG003` | (Error) The same exception type is registered more than once in one container. | Remove all but one registration; an `[ErrorCode]` type is already registered implicitly. |
| `SSALG004` | (Error) `[ExternalErrorCode]` names a non-exception type or an unbound generic type. | Name a concrete type deriving from `System.Exception`. |
| `SSALG005` | (Error) The `[ErrorCode]` exception is abstract, generic, or nested inside a generic type. | Make it concrete and non-generic; a closed generic exception can be registered via `[ExternalErrorCode]`. |
| `SSALG006` | (Warning) The exception declares none of `()`, `(string?)`, `(string?, Exception?)`. | It still maps; add one of the shapes if you want helpers, or construct it directly. |
| `SSALG007` | (Error) The container is generic or nested in a generic type, or its code enum is nested in a generic type. | Use a non-generic container and a code enum not nested in a generic type. |
| `SSALG008` | (Warning) An `[ErrorCode<TCode>]` exception exists but the compilation has no `[ErrorCodes<TCode>]` container for that enum. | Add a `static partial class` marked `[ErrorCodes<TCode>]`, or remove the attribute. |
| `SSALG009` | (Error) The `[ErrorCode]` exception is `private`, `protected`, `private protected`, or `file`-local, so the generated file cannot name it. | Make it `internal` or `public`. |
| `SSALG010` | (Warning) `[ExternalErrorCode<TCode>]` names a different code enum from the container's `[ErrorCodes<TCode>]`. | Change the registration's code enum to the container's. |
| `SSALG011` | (Warning) The container's code enum comes from another assembly and this compilation registers nothing in it. | Move the container beside the `[ErrorCode]` exceptions, or register types explicitly with `[ExternalErrorCode]`. |

## 5. Canonical snippets

### Guard clauses

```csharp
using SsalKit.Guard;

Guard.That(order.Status == OrderStatus.Open);
// GuardViolationException: Guard.That (order.Status == OrderStatus.Open) failed.

string teamName = Guard.NotNull(player.Team).Name;      // returns its value, so it composes
string name = Guard.NotNullOrWhiteSpace(player.Name);
int level = Guard.InRange(player.Level, 10, 60);         // inclusive [10, 60]

// Factory overload: invoked only on failure.
Guard.That(balance >= amount, () => new InsufficientFundsException(balance, amount));
```

### Declaring codes and the container

```csharp
using SsalKit.Guard;

namespace Game;

public enum GameStatusCode
{
    Unspecified = 0,
    NotFound = 1000,
    UserNotFound = 1001,
    ServerBusy = 2001,
    GuardViolation = 9001,
}

[ErrorCode<GameStatusCode>(GameStatusCode.NotFound)]
public class NotFoundException : ErrorCodedException
{
    public NotFoundException(string? message = null) : base(message) { }
}

// Derives from the above and carries its own code — the generated lookup tests this one first.
[ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
public sealed class UserNotFoundException : NotFoundException
{
    public UserNotFoundException(string? message = null) : base(message) { }
}

// static partial class, non-generic, not file-local. External registrations live here.
[ErrorCodes<GameStatusCode>]
[ExternalErrorCode<GameStatusCode>(typeof(TimeoutException), GameStatusCode.ServerBusy)]
[ExternalErrorCode<GameStatusCode>(typeof(GuardViolationException), GameStatusCode.GuardViolation)]
public static partial class GameErrors;
```

### Using the generated surface

```csharp
// Factory (an expression, so it composes with `throw`) and [DoesNotReturn] throw helper.
throw GameErrors.UserNotFound("player p-42 no longer exists");
GameErrors.ThrowNotFound("no such record");
```

### At the boundary — map, then observe

```csharp
using System.Diagnostics;

public Response Handle(Func<Response> operation)
{
    try
    {
        return operation();
    }
    catch (Exception exception) when (GameErrors.TryMap(exception, out GameStatusCode code))
    {
        // Observability belongs here, not in the exception's constructor.
        Activity.Current?.SetTag("error.code", (int)code);
        logger.LogWarning(exception, "request failed with {ErrorCode}", code);

        return Response.Failure((int)code, exception.Message);
    }
    // Unmapped exceptions keep unwinding — TryMap in the filter does not swallow them.
}
```
