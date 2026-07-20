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
