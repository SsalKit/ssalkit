# SsalKit.DependencyInjection — AI contract sheet

Compile-time DI auto-registration for `IServiceCollection` via a Roslyn source generator: `[Service]` on classes, `[assembly: RegisterImplementationsOf]` convention scans, and `[ServiceFactory]` enum-keyed factories. No reflection, no runtime scanning.

- **TFM:** `net10.0`. **Package dependency:** `Microsoft.Extensions.DependencyInjection.Abstractions`.
- **Bundled analyzer:** `SsalKit.DependencyInjection.Generator` (`netstandard2.0`) ships inside the package under `analyzers/dotnet/cs`. No separate package.
- **Namespace:** `SsalKit.DependencyInjection` (attributes). Generated registration extension lands in `Microsoft.Extensions.DependencyInjection`.
- This file is written for AI coding agents. Human-facing docs: [`README.md`](README.md) (also `README.ko.md`, `README.ja.md`).

## 1. API surface

### Pick the right construct

| Requirement | Use |
|---|---|
| Register one class explicitly | `[Service(lifetime, ...)]` on the class |
| Register it under one specific type | `[Service(..., As = typeof(IFoo))]` |
| Several registrations for one class | Repeat `[Service]` (`AllowMultiple = true`) |
| Keyed service (.NET 8+ keyed DI) | `[Service(..., Key = value)]` |
| Construct via a static method rather than a constructor | `[Service(..., Factory = nameof(Create))]` |
| "every implementation of X is Scoped" | `[assembly: RegisterImplementationsOf(typeof(X), ServiceLifetime.Scoped)]` |
| Exclude one class from a convention scan | Give it its own `[Service]` |
| Strongly typed enum→service lookup | `[ServiceFactory]` on a single-method interface |
| Several implementations injected as `IEnumerable<T>` | `Mode = RegistrationMode.TryAddEnumerable` on every one |

### `ServiceAttribute` — `[AttributeUsage(Class, AllowMultiple = true, Inherited = false)]`

| Member | Type | Default | Contract |
|---|---|---|---|
| `ServiceAttribute(ServiceLifetime lifetime = Singleton)` | ctor | `Singleton` | The lifetime the service is registered with. |
| `ServiceLifetime Lifetime { get; }` | `ServiceLifetime` | — | Read-only, set through the constructor. |
| `Type? As { get; set; }` | `Type?` | `null` | Register only as this type. `null` means every implemented interface, or the class itself when it implements none. For an open generic class this must itself be an unbound `typeof(IFoo<>)`. |
| `RegistrationMode Mode { get; set; }` | `RegistrationMode` | `Add` | How the call is applied to the collection. |
| `object? Key { get; set; }` | `object?` | `null` | Keyed registration (`AddKeyed*`). |
| `string? Factory { get; set; }` | `string?` | `null` | Name of a **static** factory method declared **directly** on the decorated class. Use `nameof`. |

`Factory` method requirements: declared directly on the class (not inherited), `static`, non-generic, returns **exactly** the decorated class, parameterless or a single `IServiceProvider` parameter, at least `internal`. When both overloads exist, the `IServiceProvider` one wins deterministically. Not supported on open generics (`SSAL013`).

### `RegistrationMode` — `enum`

| Value | Emits | Note |
|---|---|---|
| `Add` | `services.Add*` | Default for `[Service]`. Unconditional. |
| `TryAdd` | `services.TryAdd*` | Only when the service type is unregistered. |
| `TryAddEnumerable` | `services.TryAddEnumerable(ServiceDescriptor...)` | Default for `RegisterImplementationsOf`. **Does not forward** — each interface gets an independent descriptor, so instances are **not** shared. Cannot combine with `Key` (`SSAL005`) or self-registration (`SSAL006`). |
| `Replace` | `services.Replace(...)` | **Removes every existing descriptor for the service type first.** Destructive. |

### `RegisterImplementationsOfAttribute` — `[AttributeUsage(Assembly, AllowMultiple = true, Inherited = false)]`

| Member | Type | Default | Contract |
|---|---|---|---|
| `RegisterImplementationsOfAttribute(Type contract, ServiceLifetime lifetime = Singleton)` | ctor | `Singleton` | `contract` must be an interface (`SSAL021`): non-generic, closed generic, or unbound `typeof(IHandler<,>)`. |
| `Type Contract { get; }` | `Type` | — | The scanned interface. |
| `ServiceLifetime Lifetime { get; }` | `ServiceLifetime` | `Singleton` | Lifetime of every match. |
| `RegistrationMode Mode { get; set; }` | `RegistrationMode` | **`TryAddEnumerable`** | Differs from `[Service]`'s `Add` default. |

Matching: an unbound contract registers one `(instantiation, class)` pair per implemented instantiation; inherited implementations count; an open generic class matches only under the exact-match rule.

### `ServiceFactoryAttribute` — `[AttributeUsage(Interface, AllowMultiple = false, Inherited = false)]`

No properties. The decorated interface must declare **exactly one** member: an ordinary, non-static, non-generic method with **exactly one by-value `enum` parameter** returning a non-`void`, non-by-ref service type; the interface must be non-generic, not nested in a generic type, and must not inherit an interface with implementable members (a pure marker base is fine). The generated `internal sealed` class forwards to `GetRequiredKeyedService<TReturn>(key)` with the enum value used verbatim as the key.

### Generated output

| Artifact | Shape |
|---|---|
| Registration entry point | `public static class {Assembly}ServiceCollectionExtensions` in namespace `Microsoft.Extensions.DependencyInjection`, with `public static IServiceCollection Add{Assembly}Services(this IServiceCollection services)`. `{Assembly}` is the assembly name with non-identifier characters removed (`MyApp.Web` → `MyAppWeb`). |
| Factory implementation | `internal sealed class {Interface}Implementation` under the **reserved** namespace root `SsalKit.DependencyInjection.Generated` (plus the interface's own namespace segments), registered as `AddSingleton<TInterface, TImplementation>()`. |

## 2. Contracts (versioned / immutable)

- **Emission order is part of the package contract, not an implementation detail.** `Add{Assembly}Services` writes three blocks, always in this order:
  1. every `[Service]` registration, ordinal by fully-qualified implementation type name;
  2. every convention registration, by contract, then implementation type, then service type;
  3. every `[ServiceFactory]` singleton, ordinal by interface type name.
  Combined with Microsoft.Extensions.DependencyInjection's **last-registration-wins**, this is what decides the winner when two blocks bind the same service type. Within a block the order is by **name**, not by source position — renaming a class can change the winner (`SSAL015`, `SSAL027` exist to say so).
- **`[Service]` is a scan opt-out, not a priority rule.** A class carrying at least one `[Service]` is excluded from every convention scan in the assembly. That prevents *that class* being registered twice; it does not stop a contract matching some *other* class of the same service type, and because the convention block is emitted second, the convention wins a single-instance resolution.
- **Convention scans only see the current compilation.** Classes in referenced assemblies are never discovered, even through a project reference. Declare the attribute in that assembly too and call its own `Add{Assembly}Services`.
- **Multi-interface `Singleton`/`Scoped` share one instance via forwarding factories** (`sp => sp.GetRequiredService<TImpl>()`). Consequences: `Dispose` can run once per forwarded registration (keep `Dispose` idempotent), and a later manual re-registration of the concrete type is followed by all forwarded interfaces. `TryAddEnumerable` and open generics cannot forward, so they never share.
- **Open generic exact-match rule.** An open generic class `C<T1..Tn>` may only be registered as itself or as an implemented interface/base whose type arguments are exactly `C`'s own type parameters in declaration order. Closed, reordered, partial, or wrapped arguments are rejected (`SSAL009`). A class nested inside a generic type is never a valid `[Service]` target (`SSAL003`).
- **`SsalKit.DependencyInjection.Generated` is a reserved namespace root.** Consumer code must not declare types under it; the analyzers treat everything there as generator output and skip it.
- **`[ServiceFactory]` has no fallback.** Whatever `GetRequiredKeyedService` throws for an unregistered key (`InvalidOperationException` on the built-in container) is the factory's contract, thrown from the `Create` call. No diagnostic is reported for keys registered in another assembly — that is a supported arrangement.
- **Assembly-name collisions are not worked around.** `Foo.Bar` and `FooBar` produce the same extension class in the same namespace (CS0101 or an ambiguous invocation). The generated names are the user-facing API; rename an assembly or qualify the call.
- **`SSAL015` compares service types as written**, so `IRepo<>` and `IRepo<int>` count as two service types even though a request for `IRepo<int>` matches both at runtime.

## 3. DO NOT

- **DO NOT treat `[Service]` as outranking a convention scan.** It only opts the decorated class out. The convention block is emitted **after** the `[Service]` block, so with last-registration-wins the convention's registration is the one a single-instance resolution returns. Disambiguate with `Key`, narrow the contract, or use `TryAddEnumerable`.
- **DO NOT reach for `Mode = RegistrationMode.Replace` casually**, and especially not on a `RegisterImplementationsOf` contract. `IServiceCollection.Replace` **deletes** every existing descriptor for the service type first, so one assembly-level attribute can silently unregister an explicit `[Service]` elsewhere in the assembly.
- **DO NOT expect a convention scan to find classes in referenced assemblies.** It is a compile-time scan of the current compilation only. `SSAL022` fires when a contract matches nothing.
- **DO NOT combine `Key` with `TryAddEnumerable`** (`SSAL005` — there is no keyed `TryAddEnumerable` API), and **do not register a type as itself under `TryAddEnumerable`** (`SSAL006` — MS DI throws at runtime because it cannot tell the duplicates apart).
- **DO NOT expect `TryAddEnumerable` registrations to share an instance across interfaces.** It cannot use forwarding factories; every interface gets an independent descriptor.
- **DO NOT give a `[ServiceFactory]` interface more than one member, a property, an event, a nested type, a generic method, a non-enum or by-ref parameter, a `void` return, or a base interface with implementable members.** Each is rejected (`SSAL016`–`SSAL019`) and **no** implementation is generated for that interface.
- **DO NOT declare your own types under `SsalKit.DependencyInjection.Generated`.** It is reserved; the analyzers skip everything there, so your types would be invisible to convention scans and could collide with generated names.
- **DO NOT point `Factory` at an inherited, instance, generic, or wrong-return-type method**, or at one narrower than `internal`. It must be declared directly on the decorated class (`SSAL011`, `SSAL012`, `SSAL014`), and it cannot be used on an open generic class (`SSAL013`).
- **DO NOT use a string literal for `Factory`.** Use `nameof(Create)` so a rename is a compile error.
- **DO NOT register a class whose constructors are all non-public** without a `Factory` (`SSAL028`) — the built-in container only considers public constructors and throws at resolution time.
- **DO NOT assume registration order follows source order.** Within each block it is ordinal by type name; renaming a class can flip which implementation a single-instance resolution returns.
- **DO NOT expect an assembly's generated method to register another assembly's services.** One `Add{Assembly}Services()` call per assembly that has registrations.

## 4. Diagnostics

Prefix `SSAL`, category `SsalKit.DependencyInjection`. Reported by three analyzers: `ServiceAttributeAnalyzer` (SSAL001–015, plus 027/028), `ServiceFactoryAnalyzer` (SSAL016–020), `RegisterImplementationsOfAnalyzer` (SSAL021–026).

| ID | Trigger | Fix |
|---|---|---|
| `SSAL001` | `[Service]` on an abstract or static class. | Only concrete, non-static classes can be registered. |
| `SSAL002` | `As` names a type the class does not implement or inherit. | Use an implemented interface, a base class, or the class itself. |
| `SSAL003` | `[Service]` on a class nested inside a generic type. | Move it out; a generic class is only supported when all type parameters are its own. |
| `SSAL004` | (Warning) The same (service type, implementation type, key) is registered by more than one `[Service]`. | Remove the duplicate attribute. |
| `SSAL005` | `Key` combined with `RegistrationMode.TryAddEnumerable`. | Drop the key, or use another mode — MS DI has no keyed `TryAddEnumerable`. |
| `SSAL006` | `TryAddEnumerable` registering a type as its own service type. | Implement an interface or set `As` to a distinct service type. |
| `SSAL007` | The class, a service type, a `typeof(...)` key, or a generic argument (with its containing types) is not at least `internal`, or is file-local. | Widen to `internal`/`public` and drop `file`. |
| `SSAL008` | An undefined `ServiceLifetime`/`RegistrationMode` value on `[Service]` (e.g. `(ServiceLifetime)42`). | Use a defined enum value. |
| `SSAL009` | An open generic class registered against a non-exact-match service type. | Use the class itself, or an interface/base whose type arguments are exactly its own type parameters in order. |
| `SSAL010` | (Warning) An open generic class registered under 2+ service types as `Singleton`/`Scoped`. | Instances are not shared (no forwarding for open generics). Suppress if intended. |
| `SSAL011` | No ordinary method with the `Factory` name is declared on the class (includes an empty-string `Factory`). | Point `Factory` at a real method via `nameof`. |
| `SSAL012` | A method of that name exists but none has a usable signature. | Make it `static`, non-generic, returning exactly the class, parameterless or single `IServiceProvider`. |
| `SSAL013` | `Factory` on an open generic class. | Not supported by MS DI; drop `Factory` or close the generic. |
| `SSAL014` | The chosen factory method is not accessible to generated code. | Make it at least `internal`. |
| `SSAL015` | (Warning) One service type (+ key) registered with 2+ different implementation types. | Use `TryAddEnumerable` on all of them, distinct `Key`s, or suppress if one deliberately overrides. |
| `SSAL016` | `[ServiceFactory]` on a non-interface. | Apply it to an interface (normally pre-empted by CS0592). |
| `SSAL017` | The factory interface does not declare exactly one ordinary non-static method, or inherits an interface with implementable members. | Reduce to one method; make base interfaces pure markers. |
| `SSAL018` | The factory method is generic, does not take exactly one by-value `enum` parameter, returns `void`, or returns by reference. | Match the required shape. |
| `SSAL019` | `[ServiceFactory]` on a generic interface, or one nested inside a generic type. | Make it non-generic and not nested in a generic type. |
| `SSAL020` | The factory interface, its enum key type, or its return type is not nameable from the generated implementation. | Make each at least `internal` and not file-local. |
| `SSAL021` | A `RegisterImplementationsOf` contract is not an interface. | Only interfaces have "implementations" to discover. |
| `SSAL022` | (Warning) A contract matched no class in this assembly. | Check spelling/namespace; referenced assemblies are never scanned; candidates may be abstract/static/inaccessible/nested-in-generic/already `[Service]`-decorated. |
| `SSAL023` | The same contract is declared by two `[assembly: RegisterImplementationsOf]` attributes. | Keep one; use `[Service]` on classes that need to deviate. |
| `SSAL024` | An undefined `ServiceLifetime`/`RegistrationMode` value on `[assembly: RegisterImplementationsOf]`. | Use a defined enum value. |
| `SSAL025` | The contract (or a generic argument) is not accessible from generated code. | Make it at least `internal` and not file-local. |
| `SSAL026` | (Warning) Overlapping contracts register the same implementation under the same service type but disagree on lifetime/mode. | Make them agree (identical ones collapse silently), or narrow one contract. |
| `SSAL027` | (Warning) A `[Service]` registration and a non-`TryAddEnumerable` convention contract bind the same service type. | The convention wins (emitted last); `Replace` deletes the explicit one. Use distinct `Key`s, narrow the contract, or switch to `TryAddEnumerable`. |
| `SSAL028` | (Warning) A registered class declares no public constructor. | Make one public, or use `[Service(Factory = ...)]` (never reported). |

## 5. Canonical snippets

### Attributes and the generated entry point

```csharp
using Microsoft.Extensions.DependencyInjection;
using SsalKit.DependencyInjection;

// Registered under every interface it implements (one shared instance via forwarding).
[Service(ServiceLifetime.Singleton)]
public sealed class CacheService : ICacheService, IDisposable
{
    public void Dispose() { }        // keep Dispose idempotent: forwarding can call it twice
}

// Registered only as IUserRepository.
[Service(ServiceLifetime.Scoped, As = typeof(IUserRepository))]
public sealed class UserRepository : IUserRepository { }

// Keyed.
[Service(ServiceLifetime.Singleton, As = typeof(ICache), Key = "redis")]
public sealed class RedisCache : ICache { }

// Only if IClock is not already registered.
[Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAdd)]
public sealed class DefaultClock : IClock { }
```

```csharp
// Program.cs — the method is named after the assembly (MyApp.Web -> AddMyAppWebServices).
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMyAppWebServices();
var app = builder.Build();
```

### Static factory construction

```csharp
using Microsoft.Extensions.DependencyInjection;
using SsalKit.DependencyInjection;

[Service(ServiceLifetime.Scoped, Factory = nameof(Create))]   // nameof, never a string literal
public sealed class ApiClient : IApiClient
{
    private ApiClient(HttpClient httpClient) => HttpClient = httpClient;

    public HttpClient HttpClient { get; }

    // static, non-generic, returns exactly ApiClient, single IServiceProvider parameter, >= internal
    public static ApiClient Create(IServiceProvider sp) => new(sp.GetRequiredService<HttpClient>());
}
// emits: services.AddScoped<IApiClient, ApiClient>(sp => ApiClient.Create(sp));
```

### Convention scan (current compilation only)

```csharp
using Microsoft.Extensions.DependencyInjection;
using SsalKit.DependencyInjection;

[assembly: RegisterImplementationsOf(typeof(IRequestHandler<,>), ServiceLifetime.Scoped)]
[assembly: RegisterImplementationsOf(typeof(IStartupTask))]   // Mode defaults to TryAddEnumerable

public sealed class PingHandler : IRequestHandler<Ping, Pong> { }
public sealed class MigrateDatabase : IStartupTask { }

// [Service] opts THIS class out of the scan — it does not outrank other scanned classes.
[Service(ServiceLifetime.Transient)]
public sealed class PersistStep : IStartupTask { }
```

### Enum-keyed factory

```csharp
using Microsoft.Extensions.DependencyInjection;
using SsalKit.DependencyInjection;

public enum PaymentMethod { Card, Bank }

[Service(ServiceLifetime.Singleton, As = typeof(IPaymentProcessor), Key = PaymentMethod.Card)]
public sealed class CardPaymentProcessor : IPaymentProcessor { }

[Service(ServiceLifetime.Singleton, As = typeof(IPaymentProcessor), Key = PaymentMethod.Bank)]
public sealed class BankPaymentProcessor : IPaymentProcessor { }

[ServiceFactory]                       // exactly one method, one by-value enum parameter
public interface IPaymentProcessorFactory
{
    IPaymentProcessor Create(PaymentMethod method);
}
// An implementation is generated into SsalKit.DependencyInjection.Generated.* and registered
// as a singleton. An unregistered key throws whatever GetRequiredKeyedService throws.
```
