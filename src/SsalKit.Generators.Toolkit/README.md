[← SsalKit](https://github.com/ssalkit/ssalkit)

**English** | [한국어](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Generators.Toolkit/README.ko.md) | [日本語](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Generators.Toolkit/README.ja.md)

# SsalKit.Generators.Toolkit

A source-only toolkit for authoring Roslyn source generators: equatable arrays, an indented code writer, C# naming helpers, hint-name sanitization, and a diagnostic descriptor factory — embedded directly into your compilation, with no runtime assembly to ship.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Generators.Toolkit.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Generators.Toolkit)

## Why SsalKit.Generators.Toolkit?

Every non-trivial Roslyn source generator ends up reimplementing the same handful of utilities: a wrapper that gives `ImmutableArray<T>` structural equality so the incremental pipeline caches correctly, a small code writer that tracks indentation while emitting generated source, helpers that turn arbitrary symbol names into valid C# identifiers, a sanitizer for `AddSource`'s `hintName` argument, and a factory that cuts down the boilerplate of declaring `DiagnosticDescriptor`s.

Distributing that as an ordinary NuGet package creates a real problem: a source generator is packaged as an `analyzer`, and any library it depends on has to be packaged *alongside* it in the same `analyzers/dotnet/cs` folder — there's no ordinary dependency resolution for analyzer-time DLLs. That means every consumer of a helper library would need custom packaging just to carry it along for the ride.

SsalKit.Generators.Toolkit takes a different approach:

- **Source-only, not a runtime assembly.** The package ships plain `.cs` files as [`contentFiles`](https://learn.microsoft.com/nuget/reference/nuspec#including-content-files), and those files are compiled directly into *your* generator project. There's no DLL to package alongside your analyzer, because there's no DLL at all.
- **Zero package dependencies.** The embedded sources only need the Roslyn APIs your generator project already references — nothing new to resolve, nothing to conflict with your own `Microsoft.CodeAnalysis.*` version pin.
- **Invisible to your consumers.** Because the helpers are compiled as `internal` types directly into your generator assembly, nothing about this package leaks into the public surface of the generator you ship.
- **Five small, focused components**, not a framework: `EquatableArray<T>`, `IndentedCodeWriter`, `CSharpNaming`, `HintNameSanitizer`, and `DiagnosticDescriptorFactory`. Take what you need; unused `internal` types simply sit there unreferenced.

## Installation

```bash
dotnet add package SsalKit.Generators.Toolkit
```

The package sets `DevelopmentDependency=true`, so a plain `dotnet add package` (or a `<PackageReference>` without extra attributes) already gets `PrivateAssets="all"` applied by NuGet automatically — the reference won't flow to anything that depends on your generator. Making that explicit is still recommended, since it documents the intent for anyone reading the `.csproj` and keeps behavior stable if the implicit default ever changes:

```xml
<ItemGroup>
  <PackageReference Include="SsalKit.Generators.Toolkit" Version="0.1.0" PrivateAssets="all" />
</ItemGroup>
```

## Prerequisites

- Your project is a **Roslyn component** (a source generator and/or analyzer) — this package has no use outside that context.
- Your project targets **`netstandard2.0`** (or is otherwise compatible with it), the standard TFM for Roslyn components.
- Your project's `LangVersion` is **C# 10 or higher**. The embedded sources themselves only use C# 10 syntax (see [Embedded source contract](#embedded-source-contract) below), but the package doesn't attempt to raise or lower your project's language version.
- Your project already references **`Microsoft.CodeAnalysis`** (or `Microsoft.CodeAnalysis.CSharp`). This is a hard requirement of `DiagnosticDescriptorFactory`, which uses `Microsoft.CodeAnalysis.DiagnosticDescriptor` directly — but since every Roslyn component project references it anyway, SsalKit.Generators.Toolkit deliberately does **not** declare it as a package dependency (doing so would force a minimum Roslyn version on you and interfere with your own back-compat version pin).

## Components

### `EquatableArray<T>`

Wraps an `ImmutableArray<T>` so it compares by content instead of by reference. Incremental generator pipelines rely on `EqualityComparer<T>.Default` to decide whether a stage's output changed since the last run — a plain `ImmutableArray<T>` breaks that check (it's reference-equal only), silently defeating the pipeline's caching. `EquatableArray<T>` fixes that for any `T : IEquatable<T>`.

```csharp
using System.Collections.Immutable;
using SsalKit.Generators.Toolkit;

// Pipeline model held across incremental generator runs.
internal readonly struct ServiceModel : IEquatable<ServiceModel>
{
    public ServiceModel(string typeName, ImmutableArray<string> interfaceNames)
    {
        TypeName = typeName;
        InterfaceNames = interfaceNames.ToEquatableArray(); // or EquatableArray.Create(interfaceNames)
    }

    public string TypeName { get; }
    public EquatableArray<string> InterfaceNames { get; }

    public bool Equals(ServiceModel other) =>
        TypeName == other.TypeName && InterfaceNames.Equals(other.InterfaceNames);

    // ... GetHashCode(), object.Equals(), etc.
}
```

### `IndentedCodeWriter`

A small, allocation-light writer that tracks indentation while you build up generated source text, so you don't hand-manage indent strings yourself. Line breaks are always `"\n"` (deterministic across build machines) and blank lines never carry trailing indentation whitespace (stable diffs).

```csharp
using SsalKit.Generators.Toolkit;

var writer = new IndentedCodeWriter();
writer.WriteAutoGeneratedHeader(); // "// <auto-generated/>" + "#nullable enable" + blank line
writer.WriteLine("namespace MyGenerator.Generated;");
writer.WriteLine();

using (writer.Block("internal static class MyAppWebServiceRegistration"))
{
    using (writer.Block("public static void Register(IServiceCollection services)"))
    {
        writer.WriteLine("services.AddSingleton<ICacheService, CacheService>();");
    }
}

string source = writer.ToString();
context.AddSource("MyAppWebServiceRegistration.g.cs", source);
```

`Block(header)` writes `header`, an opening `{` on its own line, indents, and writes a closing `}` when the `using` scope ends. `Block(header, closer)` lets you supply a different closing token (e.g. `"};"` for an object initializer), and `Indent()` gives you a bare indentation scope without any braces at all.

### `CSharpNaming`

Turns arbitrary text (assembly names, symbol names, anything with dots or other separators) into valid C# identifier fragments, and escapes reserved keywords.

```csharp
using SsalKit.Generators.Toolkit;

string methodName = CSharpNaming.ToPascalCaseIdentifier(assemblyName, fallback: "Assembly");
// "MyApp.Web" -> "MyAppWeb"

string paramName = CSharpNaming.ToCamelCaseIdentifier(typeSymbol.Name);
// "IOService" -> "ioService", "UserRepository" -> "userRepository"

string safeParamName = CSharpNaming.EscapeKeyword(paramName);
// "class" -> "@class"; anything else is returned unchanged
```

`ToPascalCaseIdentifier`/`ToCamelCaseIdentifier` return `fallback` whenever the input is `null`, empty, or has no letters or digits at all, and prepend `_` if the result would otherwise start with a digit. `EscapeKeyword` only escapes *reserved* keywords (`class`, `namespace`, `return`, ...) — contextual keywords like `var` or `nameof` are left alone, since they're always valid identifiers.

### `HintNameSanitizer`

Turns a candidate string — typically a type's fully qualified or metadata name — into a value safe to pass as the `hintName` argument of `AddSource`. Generic arity markers (`` Foo`1 ``) and nested-type separators (`Outer+Inner`) are the most common source of `AddSource` failures when a raw FQN is used directly.

```csharp
using SsalKit.Generators.Toolkit;

string hintName = HintNameSanitizer.Sanitize(typeSymbol.ToDisplayString());
// "Namespace.Outer<Inner>" -> "Namespace.Outer_Inner_.g.cs" (unsafe characters replaced, suffix appended)

context.AddSource(hintName, sourceText);
```

`Sanitize` guarantees the result ends with `suffix` (default `".g.cs"`, not duplicated if already present), replaces every character outside Roslyn's accepted hint-name set with `_`, and caps the overall length at 200 characters (trimming from the front, so the more distinguishing tail — and the suffix — always survive). It does not guarantee uniqueness across multiple calls; callers are responsible for passing distinguishable input.

### `DiagnosticDescriptorFactory`

Cuts down the repetitive `DiagnosticDescriptor` constructor call (id, title, message format, category, severity, `isEnabledByDefault`, description, ...) that every generator's diagnostics table repeats for each entry.

```csharp
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;

internal static class DiagnosticDescriptors
{
    private static readonly DiagnosticDescriptorFactory Factory = new("SSAL", "SsalKit.Guard");

    public static readonly DiagnosticDescriptor DuplicateErrorCode = Factory.Error(
        id: 1,
        title: "Duplicate error code",
        messageFormat: "Error code '{0}' is already assigned to '{1}'",
        description: "Each member decorated with [ErrorCodes] must declare a unique error code.");
    // -> id "SSAL001"

    public static readonly DiagnosticDescriptor UnusedErrorCode = Factory.Warning(
        id: 2,
        title: "Unused error code",
        messageFormat: "Error code '{0}' is never thrown",
        description: "Consider removing the unused error code or using it in a Guard call.");
    // -> id "SSAL002"
}
```

Every descriptor produced by a given factory instance shares the same id prefix/category, is formatted as `{idPrefix}{id:D3}` (e.g. `"SSAL001"`), and has `isEnabledByDefault: true`. Both `Error(...)` and `Warning(...)` accept an optional `params string[] customTags` for additional descriptor tags.

## Embedded source contract

Every `.cs` file this package ships starts with the same three lines:

```csharp
// <auto-generated/>
#pragma warning disable
#nullable enable
```

- `// <auto-generated/>` tells your own analyzers (and any consumer-facing tooling) to treat the file as generated code, skipping style/quality rules that would otherwise apply.
- `#pragma warning disable` unconditionally clears every warning in the file, so it compiles clean under your project's exact warning configuration — including `TreatWarningsAsErrors`.
- `#nullable enable` fixes the file's own nullable contract regardless of your project's nullable setting.

On top of the header, every type across the five components is `internal`, and every file lives in the fixed `SsalKit.Generators.Toolkit` namespace — since the types are `internal`, two different generator assemblies that each embed this package never collide with each other. The sources deliberately avoid `record` types and `init`-only properties: on `netstandard2.0` that syntax needs the `IsExternalInit` compiler polyfill, and if your own project (or another embedded package) already defines that polyfill type, embedding a second one would cause a `CS0101` duplicate-definition error. Avoiding the syntax entirely sidesteps the problem rather than trying to coordinate around it. The language surface used elsewhere is capped at C# 10 (file-scoped namespaces are fine; primary constructors and collection expressions are not), since your project's `LangVersion` can't be assumed to be any newer.

## Known limitation

If your generator project grants a test project access to its `internal` types via `[InternalsVisibleTo]`, **don't also let that test project reference SsalKit.Generators.Toolkit directly**. Both paths would bring the same `internal` types (same namespace, same names) into the test project's compilation — once via the generator assembly (through `InternalsVisibleTo`) and once via the package's own embedded sources — which the compiler sees as an ambiguous duplicate and rejects.

If your tests need these helpers, reach them the same way the rest of your test project reaches the generator's other `internal` types: through `[InternalsVisibleTo]` on the generator project, not through a second, direct package reference.

## License

MIT — see [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE).

---

**AI disclosure:** This project was built with AI assistance (Claude).
