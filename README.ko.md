[English](README.md) | **한국어** | [日本語](README.ja.md)

# SsalKit

최신 .NET을 위한, 소스 생성기 기반 유틸리티 라이브러리 모음입니다.
[![CI](https://github.com/ssalkit/ssalkit/actions/workflows/ci.yml/badge.svg)](https://github.com/ssalkit/ssalkit/actions/workflows/ci.yml)
[![coverage](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2Fssalkit%2Fssalkit%2Fbadges%2Fcoverage.json)](https://github.com/ssalkit/ssalkit/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Libraries

| 패키지 | NuGet | 설명 | 문서 |
|---------|-------|-------------|------|
| `SsalKit.DependencyInjection` | [![NuGet](https://img.shields.io/nuget/v/SsalKit.DependencyInjection.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.DependencyInjection) | Roslyn 소스 생성기 기반의 컴파일 타임 DI 자동 등록 라이브러리. | [README](src/SsalKit.DependencyInjection/README.ko.md) |
| `SsalKit.Randomness` | [![NuGet](https://img.shields.io/nuget/v/SsalKit.Randomness.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Randomness) | 결정적·상태 직렬화 가능 PRNG(`xoshiro256**` + SplitMix64)와 가중치 랜덤 추첨 라이브러리. | [README](src/SsalKit.Randomness/README.ko.md) |

## 이름의 유래

SsalKit은 '쌀' + 'Kit'입니다. 게임에서 캔 재화로 현실의 쌀을 사 먹는다는 밈 '쌀먹'에서 따왔습니다. 개발에 필요한 라이브러리도 그렇게 야무지게 뽑아 먹자 — 매일 밥값을 해내는 작고 실용적인 라이브러리 모음이 되겠다는 뜻을 담았습니다.

## 철학

- **컴파일 타임 우선.** 동작은 런타임에 발견되는 것이 아니라, 빌드 시점에 평범한 C# 코드로 생성됩니다.
- **리플렉션 제로.** 생성된 코드는 평범하고 읽기 쉬우며 디버깅 가능합니다. 동적으로 탐색되는 것은 없습니다.
- **AOT/트리밍 친화적.** 리플렉션 기반 탐색이 없으므로 추가 어노테이션 없이도 트리밍과 Native AOT를 그대로 통과합니다.
- **컴파일 타임 진단.** 잘못된 사용은 프로덕션 런타임 예외가 아니라 컴파일러가 먼저 잡아줍니다.

## 요구 사항

.NET 10+

## 라이센스

MIT — 자세한 내용은 [LICENSE](LICENSE)를 참고하세요.

---

**AI 고지:** 이 프로젝트는 AI(Claude)를 활용하여 제작되었습니다.
