[← SsalKit](https://github.com/ssalkit/ssalkit)

**English** | [한국어](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Guard/README.ko.md) | [日本語](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Guard/README.ja.md)

# SsalKit.Guard

Error-code-based domain exceptions: a side-effect-free `ErrorCodedException` base, static guard clauses that capture the caller's expression text, and a compile-time generated exception-to-code mapping table with derived-before-base ordering. Zero dependencies.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Guard.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Guard)

## Why SsalKit.Guard?

Any service that answers to the outside world eventually grows the same layer: a boundary that catches whatever the domain threw and turns it into a code the caller understands. Written by hand, that layer tends to acquire three problems.

- **Exceptions that do work when they are constructed.** Tagging `Activity.Current` (or logging, or bumping a counter) inside an exception constructor looks convenient exactly once. Constructing an exception is not the same moment as handling it; a caught exception may be rethrown or wrapped much later, so the tags end up describing the wrong moment; and a test that merely constructs one perturbs ambient telemetry it never asked about.
- **Guard helpers that colonise IntelliSense, and failure context typed out by hand.** A `this T` extension — `value.ThrowIfNull(...)` — surfaces on every reference type in the codebase, whether or not the call site has anything to do with validation. And the values that made the check fail get appended to the message from a hand-maintained `(string Name, object Value)[]`, which drifts from the condition it is supposed to describe.
- **A mapping switch whose correctness is a comment.** The exception-to-code `switch` has to place derived types before their bases, and stays correct only as long as everyone remembers to — in practice defended by a comment along the lines of "this one is a subtype of the next one, so it must be matched first". Forget to register a newly added exception at all and the compiler has nothing to say about it.

SsalKit.Guard is those three, taken apart:

- **`ErrorCodedException` is pure data.** No constructor here tags an `Activity`, writes a log, or emits a metric. Observability belongs at the boundary — the only place that knows the surrounding request context — and this document shows what that looks like.
- **`Guard.` is a static entry point, and the failure context is captured by the compiler.** Every clause takes a trailing `[CallerArgumentExpression]` parameter, so the source text of what you checked lands in the message for free: `Guard.That (order.Status == OrderStatus.Open) failed.`
- **The mapping table is generated.** Put `[ErrorCodes<TCode>]` on a `static partial class` and the exception → code lookup is written for you, ordered most-derived-first from the registered types' inheritance depth. There is no order to maintain, and misuse is a compile-time diagnostic rather than a lookup that quietly returns the wrong code.
- **Zero dependencies.** BCL only.

## Installation

```bash
dotnet add package SsalKit.Guard
```

The package contains both the runtime types (`Guard`, `ErrorCodedException`, the three attributes) and the source generator — no separate analyzer package to install, and no `PackageReference` of its own.

**Prerequisites:** .NET 10+. Codes are declared with generic attributes (`[ErrorCode<GameStatusCode>(...)]`), which require C# 11 or later.

## Guard clauses

Five clauses, each one a domain invariant rather than an argument check. Each takes a trailing `[CallerArgumentExpression]` parameter that the compiler fills in, so the source text of the checked expression appears in the message without ever being typed out:

```csharp
using SsalKit.Guard;

Guard.That(order.Status == OrderStatus.Open);
// GuardViolationException: Guard.That (order.Status == OrderStatus.Open) failed.

var owner = Guard.NotNull(world.FindPlayer(id));
// GuardViolationException: Guard.NotNull (world.FindPlayer(id)) failed: value was null.

string name = Guard.NotNullOrWhiteSpace(player.Name);
// GuardViolationException: Guard.NotNullOrWhiteSpace (player.Name) failed: value was null, empty, or white-space.

int level = Guard.InRange(player.Level, 10, 60);
// GuardViolationException: Guard.InRange (player.Level) failed: value 3 was outside the inclusive range [10, 60].
```

| Clause | Fails when | Returns | Failure message |
|---|---|---|---|
| `Guard.That(condition)` | `condition` is `false` | `void` | `Guard.That ({expression}) failed.` |
| `Guard.NotNull(value)` | `value` is `null` (reference types and `Nullable<T>`) | the non-nullable value | `Guard.NotNull ({expression}) failed: value was null.` |
| `Guard.NotNullOrEmpty(value)` | the string is `null` or empty | `string` | `Guard.NotNullOrEmpty ({expression}) failed: value was null or empty.` |
| `Guard.NotNullOrWhiteSpace(value)` | the string is `null`, empty, or all white-space | `string` | `Guard.NotNullOrWhiteSpace ({expression}) failed: value was null, empty, or white-space.` |
| `Guard.InRange(value, min, max)` | `value` is outside the inclusive `[min, max]` | `T` | `Guard.InRange ({expression}) failed: value {value} was outside the inclusive range [{min}, {max}].` |

The message contract is `Guard.{Clause} ({expression}) failed.` for `That` and `Guard.{Clause} ({expression}) failed: {detail}` for the rest. `InRange` renders the value and both bounds with the invariant culture, so a failure reads the same everywhere. When the expression text is unavailable — which only happens if a caller explicitly passes `null` or the empty string, or the call comes from a language that does not honour `[CallerArgumentExpression]` — the placeholder `<expression unavailable>` is used instead.

Every clause but `That` returns its value, so a guard reads as part of the expression it protects rather than as a statement standing next to it:

```csharp
string teamName = Guard.NotNull(player.Team).Name;
```

### Throwing your own exception

`That` and the reference-type `NotNull` also take an exception factory, invoked only when the check fails:

```csharp
Guard.That(balance >= amount, () => new InsufficientFundsException(balance, amount));

Team team = Guard.NotNull(player.Team, () => GameErrors.InvalidTeam($"player {player.Id} is on no team"));
```

The success path allocates nothing: the factory is never invoked, and messages are composed only after a check has already failed. If a factory hands back `null`, a `GuardViolationException` naming the clause is thrown rather than a bare `NullReferenceException` whose stack trace says nothing about the guard that failed.

### This is not argument validation

The BCL already covers parameter contracts — `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`, the `ArgumentOutOfRangeException.ThrowIf*` family — and it throws the exception types that callers and analyzers expect from an argument check. `Guard` deliberately does not duplicate them.

Use the BCL for "you passed me a bad argument". Use `Guard` for "this aggregate is no longer in a state that allows this operation" — a domain failure that maps to an error code, not an `ArgumentException`.

## Error codes

### Declaring them

```csharp
using SsalKit.Guard;

public enum GameStatusCode
{
    Unspecified = 0,
    NotFound = 1000,
    UserNotFound = 1001,
    InvalidTeam = 1002,
    ServerBusy = 2001,
    GuardViolation = 9001,
}

// A code lives on the exception type, declared once.
[ErrorCode<GameStatusCode>(GameStatusCode.NotFound)]
public class NotFoundException : ErrorCodedException
{
    public NotFoundException(string? message = null) : base(message) { }
}

// Derives from the type above and carries a different code — see the ordering guarantee below.
[ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
public sealed class UserNotFoundException : NotFoundException
{
    public UserNotFoundException(string? message = null) : base(message) { }
}

[ErrorCode<GameStatusCode>(GameStatusCode.InvalidTeam)]
public sealed class InvalidTeamException : ErrorCodedException
{
    public InvalidTeamException(string? message = null, Exception? innerException = null)
        : base(message, innerException) { }
}

// The mapping container. These four lines are the entire declaration.
[ErrorCodes<GameStatusCode>]
[ExternalErrorCode<GameStatusCode>(typeof(TimeoutException), GameStatusCode.ServerBusy)]
[ExternalErrorCode<GameStatusCode>(typeof(GuardViolationException), GameStatusCode.GuardViolation)]
public static partial class GameErrors;
```

`[ExternalErrorCode]` is where exceptions you do not own get their codes: BCL types, a cache client's timeout, a cluster library's failure, a token validation error. In a real boundary those tend to be half the table, and they cannot carry `[ErrorCode]` themselves, so the container declares them instead.

### What gets generated

Into the other half of `GameErrors`:

```csharp
// The lookup, ordered most-derived first.
if (GameErrors.TryMap(exception, out GameStatusCode code)) { /* ... */ }

// Same order; the two differ only in how "no registration matched" is reported.
GameStatusCode mapped = GameErrors.MapOrDefault(exception, GameStatusCode.Unspecified);

// One factory and one [DoesNotReturn] throw helper per [ErrorCode] exception,
// each mirroring that exception's own constructor.
throw GameErrors.UserNotFound("player p-42 no longer exists");
GameErrors.ThrowInvalidTeam("a team needs at least two members", new TimeoutException("roster lookup"));
```

Helper names drop the `Exception` suffix (`UserNotFoundException` → `UserNotFound` and `ThrowUserNotFound`); if two registered types would collide that way, the whole type name is used instead, and a fully qualified fallback after that.

The generated lookup is a plain `is` chain, so the order it guarantees is right there to read:

```csharp
public static bool TryMap(global::System.Exception exception, out global::Game.GameStatusCode code)
{
    if (exception is global::Game.UserNotFoundException)
    {
        code = global::Game.GameStatusCode.UserNotFound;
        return true;
    }

    if (exception is global::Game.NotFoundException)
    {
        code = global::Game.GameStatusCode.NotFound;
        return true;
    }

    code = default(global::Game.GameStatusCode);
    return false;
}
```

### Derived before base, automatically

`UserNotFoundException` derives from `NotFoundException` and each carries its own code, so the base must never be tested first — and above, it isn't. That order is generated from the registered types' inheritance depth (deepest first, ties broken by fully qualified name so the output is deterministic). Which means:

- **Nothing to keep in order.** A new exception dropped into the middle of an existing hierarchy lands in the right place on the next build, with no comment to maintain and no review checklist.
- **The match is on the runtime type.** An instance held in a variable of its base type — which is exactly how it arrives at a `catch` clause — still yields the derived code.
- **Ambiguity is refused, not resolved.** Registering the same exception type twice in one container is `SSALG003`, an error. Two candidate codes decided by declaration order would be a silent precedence rule, which is the thing this replaces.
- **An unregistered exception matches nothing.** `TryMap` returns `false` — including for a null reference — so "unmapped" stays distinguishable from every real code.

### Factories mirror the exception's constructor

v1 recognises three public constructor shapes and mirrors the widest one the exception declares, nullability included:

| Constructor on the exception | Generated factory (throw helper is the same, prefixed `Throw`) |
|---|---|
| `()` | `Empty()` |
| `(string? message = null)` | `MessageOnly(string? message = null)` |
| `(string message)` | `Required(string message)` — non-nullable, so the parameter stays required |
| `(string? message, Exception? innerException)` | `Full(string? message = null, Exception? innerException = null)` |

An exception declaring none of them still takes part in the mapping table; it simply gets no helpers, and `SSALG006` says so rather than leaving you to wonder. Externally registered types never get helpers either — this library cannot vouch for the constructor contract of a type it does not own.

### Several containers, several code enums

A container collects only exceptions whose `[ErrorCode<TCode>]` names its own enum, so unrelated domains stay apart:

```csharp
[ErrorCodes<GameStatusCode>]
public static partial class GameErrors;

[ErrorCodes<BillingStatusCode>]
public static partial class BillingErrors;
```

Each gets its own `TryMap`, `MapOrDefault`, and helpers, and nothing crosses over.

### Accessibility

The generated part re-declares the container (and every type containing it) with its own accessibility, and each generated member is clamped so the result compiles: `TryMap`/`MapOrDefault` are `public` unless the code enum is not, and each factory and throw helper is `public` unless its exception type is not. An `internal` enum in an `internal` container therefore yields `internal` members, with no accessibility mismatch to fix by hand.

An exception the generated file could not name at all — `private`, `protected`, `private protected`, or `file`-local — is `SSALG009`, an error. Including it would produce a generated file that does not compile, pointing at code you never wrote.

## At the boundary

Catch, map, respond — one function for the whole surface:

```csharp
public Response Handle(Func<Response> operation)
{
    try
    {
        return operation();
    }
    catch (Exception exception) when (GameErrors.TryMap(exception, out GameStatusCode code))
    {
        // Observability lives here, where the request context exists and the decision to
        // handle has already been made. The exception did nothing on its way up.
        Activity.Current?.SetTag("error.code", (int)code);
        logger.LogWarning(exception, "request failed with {ErrorCode}", code);

        return Response.Failure(ToTransportStatus(code), (int)code, exception.Message);
    }
}
```

Three things worth pointing out:

- **Tagging and logging are the consuming side's job.** This is a deliberate change from the prototype this library was extracted from, where the exception's own constructor tagged `Activity.Current`. Doing it here means an exception that is caught, wrapped, and rethrown is recorded once at the moment it is actually handled, and a unit test that constructs one touches no telemetry at all.
- **`TryMap` in the `when` filter leaves unmapped exceptions alone.** They keep unwinding instead of being swallowed by a handler that has nothing useful to say about them, which is usually what you want at a boundary. `MapOrDefault(exception, GameStatusCode.Unspecified)` is the shorter form for when a fallback code is genuinely fine.
- **The mapping stops at your enum.** Turning `GameStatusCode` into an HTTP status, a gRPC code, or a wire integer is your transport's business — which is exactly why the generated lookup returns `TCode` and nothing here is transport-shaped.

## Diagnostics

| ID | Severity | Reported when |
|---|---|---|
| `SSALG001` | Error | `[ErrorCode]` is applied to a type that does not derive from `ErrorCodedException`. |
| `SSALG002` | Error | An `[ErrorCodes]` container is not a `static partial class`. |
| `SSALG003` | Error | The same exception type is registered more than once in one container. |
| `SSALG004` | Error | `[ExternalErrorCode]` names a type that is not an exception, or an unbound generic type. |
| `SSALG005` | Error | An `[ErrorCode]` exception is abstract, generic, or nested inside a generic type. |
| `SSALG006` | Warning | An `[ErrorCode]` exception declares none of the recognised constructors, so no factory or throw helper is generated for it. It still maps. |
| `SSALG007` | Error | An `[ErrorCodes]` container is generic, or nested inside a generic type. |
| `SSALG008` | Warning | An `[ErrorCode<TCode>]` exception exists, but the compilation has no `[ErrorCodes<TCode>]` container for that enum, so nothing is generated for it anywhere. |
| `SSALG009` | Error | An `[ErrorCode]` exception is not accessible from the generated file (`private`, `protected`, `private protected`, or `file`-local). |

A rule about a single registration (`SSALG001`, `SSALG004`, `SSALG005`, `SSALG009`) drops that registration and leaves the rest of the container intact — one mis-declared exception should not take the whole mapping table down with it. A rule about the container (`SSALG002`, `SSALG007`), or an ambiguity the generator refuses to resolve on your behalf (`SSALG003`), suppresses that container's generated file entirely.

## Things to know

- **A code is always a declared type.** There is no `throw new SomeException(4001, "…")` here: `ErrorCodedException` carries no code field, and throwing an arbitrary code from an otherwise anonymous exception is deliberately unsupported. One small class per code buys the thing you `catch`, the thing you document, and the thing the compiler can check — a code that exists only as an integer at one throw site is invisible to all three. Migrating from that shape means writing those classes, and that is the trade this library makes on purpose.
- **Only three constructor shapes are mirrored.** `()`, `(string?)`, and `(string?, Exception?)`. An exception with domain-specific parameters — `InsufficientFundsException(decimal balance, decimal amount)` — still maps perfectly well, gets `SSALG006` to say why it has no helpers, and is constructed the ordinary way, including inside a guard's exception-factory overload.
- **Give `GuardViolationException` a code.** It derives from `ErrorCodedException` like any other domain failure, but it is declared in this package, so it is registered on your container rather than on the type: `[ExternalErrorCode<GameStatusCode>(typeof(GuardViolationException), GameStatusCode.GuardViolation)]`. Without that line every guard failure falls through your mapping as unmapped; with it, an internal invariant violation becomes a first-class code in your own enum.
- **`ErrorCodedException` is also a `catch` target.** A single `catch (ErrorCodedException)` separates domain failures from everything else, which is useful at a boundary that wants to treat the two differently before any mapping happens.
- **One container per class, if you have any doubt.** A class may carry `[ErrorCodes<A>]` and `[ErrorCodes<B>]` at once — they are different attribute types — and each enum's generated file is kept separate. The case that is not handled is an exception declaring a code in *both* enums: its helpers would be generated into both halves of the same class under the same name.

## License

MIT — see [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE).

---

**AI disclosure:** This project was built with AI assistance (Claude).
