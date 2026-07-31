**English** | [한국어](README.ko.md) | [日本語](README.ja.md)

# SsalKit

Source-generator-first utility libraries for modern .NET.
[![CI](https://github.com/ssalkit/ssalkit/actions/workflows/ci.yml/badge.svg)](https://github.com/ssalkit/ssalkit/actions/workflows/ci.yml)
[![coverage](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2Fssalkit%2Fssalkit%2Fbadges%2Fcoverage.json)](https://github.com/ssalkit/ssalkit/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Libraries

| Package | NuGet | Description | Docs |
|---------|-------|-------------|------|
| `SsalKit.DependencyInjection` | [![NuGet](https://img.shields.io/nuget/v/SsalKit.DependencyInjection.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.DependencyInjection) | Compile-time DI auto-registration via a Roslyn source generator. | [README](src/SsalKit.DependencyInjection/README.md) |
| `SsalKit.Randomness` | [![NuGet](https://img.shields.io/nuget/v/SsalKit.Randomness.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Randomness) | Deterministic, state-serializable PRNG (`xoshiro256**` + SplitMix64) with weighted-random sampling. | [README](src/SsalKit.Randomness/README.md) |
| `SsalKit.Generators.Toolkit` | [![NuGet](https://img.shields.io/nuget/v/SsalKit.Generators.Toolkit.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Generators.Toolkit) | Source-only toolkit for authoring Roslyn source generators, embedded into the consumer's compilation with no runtime assembly. | [README](src/SsalKit.Generators.Toolkit/README.md) |
| `SsalKit.Guard` | [![NuGet](https://img.shields.io/nuget/v/SsalKit.Guard.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Guard) | Error-code-based domain exceptions, static guard clauses, and a compile-time generated exception-to-code mapping table. | [README](src/SsalKit.Guard/README.md) |
| `SsalKit.Generators.Toolkit.Testing` | [![NuGet](https://img.shields.io/nuget/v/SsalKit.Generators.Toolkit.Testing.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Generators.Toolkit.Testing) | Test-framework-agnostic harness for incremental source generators and analyzers, including incremental caching assertions. | [README](src/SsalKit.Generators.Toolkit.Testing/README.md) |
| `SsalKit.Timekeeping` | [![NuGet](https://img.shields.io/nuget/v/SsalKit.Timekeeping.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Timekeeping) | Deterministic, persistable time state: calendar reset boundaries (daily/weekly/monthly, fixed DST contract), elapsed-time cooldowns and recharging pools, plus a logical-tick event schedule for simulations. | [README](src/SsalKit.Timekeeping/README.md) |
| `SsalKit.StableHashing` | [![NuGet](https://img.shields.io/nuget/v/SsalKit.StableHashing.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.StableHashing) | Platform- and process-independent 64-bit checksums via a version-locked canonical encoding contract, generated from `[StableHashContract]`/`[StableHashMember]`. | [README](src/SsalKit.StableHashing/README.md) |

## Why "SsalKit"?

*Ssal* (쌀) is Korean for rice. The name is a nod to the Korean gaming meme *ssal-meok* (쌀먹) — literally "eating rice off it" — used when someone grinds a game hard enough to actually put food on the table. SsalKit carries that spirit into .NET development: a kit of small, practical libraries you can squeeze every grain of value out of, day after day.

## Philosophy

- **Compile-time first.** Behavior is generated at build time from ordinary C#, not discovered at runtime.
- **Zero reflection.** Generated code is plain, readable, and debuggable — nothing is found dynamically.
- **AOT & trimming friendly.** Since there's no reflection-driven discovery, everything survives trimming and Native AOT without extra annotations.
- **Compile-time diagnostics.** Misuse is caught by the compiler, not by a runtime exception in production.

## Requirements

.NET 10+

## License

MIT — see [LICENSE](LICENSE).

---

**AI disclosure:** This project was built with AI assistance (Claude).
