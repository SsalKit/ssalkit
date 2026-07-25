[← SsalKit](https://github.com/ssalkit/ssalkit)

# SsalKit.Guard

Error-code-based domain exceptions: a side-effect-free `ErrorCodedException` base, static guard clauses that capture the caller's expression text, and a compile-time generated exception-to-code mapping table with derived-before-base ordering. Zero dependencies.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Guard.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Guard)

## Installation

```bash
dotnet add package SsalKit.Guard
```

## Quick start

```csharp
using SsalKit.Guard;

Guard.That(order.Status == OrderStatus.Open);
// GuardViolationException: Guard.That (order.Status == OrderStatus.Open) failed.

var player = Guard.NotNull(world.FindPlayer(id));
// GuardViolationException: Guard.NotNull (world.FindPlayer(id)) failed: value was null.
```

```csharp
public enum GameStatusCode { UserNotFound = 1001, GuardViolation = 9001 }

[ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
public sealed class UserNotFoundException : ErrorCodedException
{
    public UserNotFoundException(string? message = null) : base(message) { }
}

[ErrorCodes<GameStatusCode>]
[ExternalErrorCode<GameStatusCode>(typeof(GuardViolationException), GameStatusCode.GuardViolation)]
public static partial class GameErrors;
```

> **Note:** this document is a placeholder. Full documentation (English / 한국어 / 日本語) lands with the source generator.

## License

MIT — see [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE).
