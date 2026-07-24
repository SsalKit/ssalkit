[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ko.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.md) | **한국어** | [日本語](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.ja.md)

# SsalKit.Randomness

결정적(deterministic)이고 상태를 직렬화할 수 있는 PRNG(xoshiro256** + SplitMix64)와, 통일된 난수 소스 추상화, 가중치 랜덤 추첨을 제공하는 라이브러리입니다. 의존성이 없습니다.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Randomness.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Randomness)

## 왜 SsalKit.Randomness인가

게임 로직, 시뮬레이션, 절차적 콘텐츠 생성은 결국 같은 요구사항에 부딪힙니다. 같은 시드 혹은 같은 저장된 상태가 주어지면, "무작위" 결과의 정확히 같은 수열이 다시 나와야 한다는 것입니다 — 리플레이, 결정적 락스텝 멀티플레이어, 한 판의 실행을 비트 단위로 재현하는 세이브 파일을 위해서요.

`System.Random`은 이 요구를 온전히 채워주지 못합니다.

- **시드를 지정한 `Random`은 레거시 알고리즘을 사용합니다.** `int` 시드를 받는 생성자는 호환성을 위해 출력을 안정적으로 유지하지만, 그 안정성은 애초에 1급 설계 목표가 아니었고, `Random`의 모든 생성 경로에 걸쳐 보장되는 것도 아닙니다.
- **상태 export를 지원하지 않습니다.** `System.Random`은 내부 상태를 꺼내 저장했다가 나중에 복원하는 공식적인 방법을 제공하지 않습니다. 실행 내내 같은 `Random` 인스턴스를 살려두거나, 아니면 재현성을 포기해야 합니다.
- **스트림 분기가 없습니다.** 하나의 부모 시드에서 독립적이고 재현 가능한 자식 생성기를 파생시키는 내장 방법이 없습니다 (엔티티별·서브시스템별 난수가 하나의 루트 시드로 추적되어야 할 때 유용합니다).

SsalKit.Randomness는 접근 방식 자체가 다릅니다.

- **`DeterministicRandom`**은 `System.Random`과 비슷한 모양의 sealed PRNG(xoshiro256**)로, 256bit 전체 상태를 export하여 어디든(세이브 파일, DB 로우, 네트워크 패킷) 저장했다가 복원하면, 어떤 플랫폼에서든 영원히 정확히 같은 수열을 이어갈 수 있습니다.
- **`IRandomSource`**는 결정적/공유(`Random.Shared`)/암호학적 난수를 하나의 인터페이스로 통일하므로, 범위 생성·셔플·추첨 코드를 한 번만 작성해 셋 중 무엇에도 적용할 수 있습니다.
- **가중치 랜덤 추첨**(`PickWeighted`, `PickManyWeighted(Distinct)`, `WeightedSampler<T>`)이 라이브러리에 함께 제공되며, 명확한 예외 계약과 반복 추첨용 `O(1)` alias method 샘플러를 갖추고 있습니다.
- **의존성 0.** `PackageReference` 없이 BCL만 사용합니다.

## 설치

```bash
dotnet add package SsalKit.Randomness
```

## 빠른 시작

```csharp
using SsalKit.Randomness;

// 결정적 생성기를 시드로 생성합니다.
var rng = new DeterministicRandom(seed: 42);

int roll = rng.Next(1, 7);          // [1, 7)
double chance = rng.NextDouble();   // [0, 1)
bool coinFlip = rng.NextBoolean();

// 상태를 export하여(예: 세이브 파일에) 저장한 뒤, 나중에 정확히 같은 수열을 이어갑니다.
RandomState saved = rng.ExportState();
DeterministicRandom resumed = DeterministicRandom.FromState(saved);

// 부모 시드로부터 독립적인 자식 스트림을 파생시킵니다(예: 게임 엔티티마다 하나씩).
DeterministicRandom child = rng.Fork();

// 가중치 추첨, 단발.
string[] items = ["common", "rare", "legendary"];
long[] weights = [80, 18, 2];
string drop = rng.PickWeighted(items.AsSpan(), weights.AsSpan());

// 가중치 추첨, 반복: 한 번 빌드하고 추첨마다 O(1).
WeightedSampler<string> sampler = WeightedSampler<string>.Create(items, weights.AsSpan());
string anotherDrop = sampler.Pick(rng);
string[] tenDrops = sampler.PickMany(rng, count: 10);
```

## API 개요

| 타입 | 역할 |
|---|---|
| `IRandomSource` | 모든 소스가 공유하는 최소 계약(`NextUInt64()` + `NextBytes(Span<byte>)`). 그 위의 모든 상위 연산은 이 두 멤버로부터 확장 메서드로 파생됩니다. |
| `DeterministicRandom` | 시드 지정·상태 export·fork가 가능한 PRNG. `System.Random`과 비슷한 인스턴스 API(`Next`, `NextInt64`, `NextDouble`, `NextSingle`, `NextBoolean`, `NextBytes`)에 더해 `ExportState()`/`FromState(...)`/`Fork()`를 제공합니다. |
| `RandomState` | 256bit 상태(`S0`..`S3`)를 담는 `readonly record struct`. 값 동등성과 손쉬운 JSON 직렬화를 제공하며, `ulong[4]` 상호운용을 위한 `ToArray()`/`FromSpan(...)`/`CopyTo(...)`가 있습니다. |
| `CryptoRandomSource` | `RandomNumberGenerator` 기반 `IRandomSource`. 예측 불가능하며 스레드 안전하고, `CryptoRandomSource.Instance`로 제공됩니다. |
| `SharedRandomSource` | `Random.Shared` 기반 `IRandomSource`. 스레드 안전하며, `SharedRandomSource.Instance`로 제공됩니다. |
| `SystemRandomSource` | 임의의 `Random` 인스턴스를 감싸는 `IRandomSource` 어댑터로, interop과 테스트용입니다. |
| `RandomSourceExtensions` | `IRandomSource`용 균등 확장 메서드: `Next`/`NextInt64`/`NextDouble`/`NextSingle`/`NextBoolean`, `Shuffle`, `Pick`. `DeterministicRandom`의 인스턴스 메서드와 알고리즘·출력이 완전히 동일합니다. |
| `WeightedRandomExtensions` | `PickWeighted`(단발, `long` 또는 `double` 가중치, 리스트 또는 span 형태), `PickManyWeighted`(복원 추출), `PickManyWeightedDistinct`(비복원 추출). |
| `WeightedSampler<T>` | 고정된 `long` 가중치 항목 집합에서 반복 추첨할 때 쓰는, 불변·스레드 안전한 사전 빌드 alias method 샘플러. 빌드는 `O(n)`, `Pick`/`PickMany`는 호출당 `O(1)`. |

## 알고리즘 및 상태 계약 (v1)

`DeterministicRandom`의 출력 수열은 **xoshiro256\*\***이며, 시드 확장(단일 `ulong` 시드에서 256bit 내부 상태로)은 **SplitMix64**입니다. 상태는 정확히 4개의 `ulong` 워드로 구성되며 `RandomState`로 노출됩니다.

이 계약은 이 타입에 대해 영구적으로 고정됩니다.

- **같은 시드 또는 같은 방식으로 복원된 상태는 어떤 플랫폼·어떤 프로세스에서든, 영원히 항상 같은 수열을 만들어냅니다.**
- `RandomState`가 세이브 데이터로 저장될 수 있으므로, 출력 수열을 바꾸면 모든 소비자의 세이브 데이터가 손상되는 것과 같습니다. 이런 변경은 패치/마이너 릴리스에서 **절대** 일어나지 않습니다.
- 알고리즘을 언젠가 진화시켜야 한다면, `DeterministicRandom` 자체의 동작을 바꾸는 대신 **새로운 타입**(예: 가상의 `DeterministicRandomV2`)으로만 출시됩니다.
- all-zero 상태는 무효한 상태입니다(xoshiro256**는 한 번 그 상태에 들어가면 절대 빠져나오지 못합니다). `FromState(...)`/`RandomState.FromSpan(...)`은 이를 `ArgumentException`으로 거부합니다.

파생 보장 사항:

- `Next(maxValue)` / `NextInt64(maxValue)`와 그 범위 오버로드는 **Lemire의 곱셈-시프트-리젝션 알고리즘**을 사용합니다 — `%` 기반 범위 축소와 달리 모듈로 편향이 없습니다.
- `NextDouble()`은 53bit 정밀도의 `[0, 1)`을, `NextSingle()`은 24bit 정밀도의 `[0, 1)`을 반환합니다. `1.0`/`1.0f`는 절대 반환되지 않습니다.
- `Fork()`의 계약은 정확히 `Fork() == new DeterministicRandom(this.NextUInt64())`입니다. 부모가 `ulong` 하나를 뽑고(다른 `NextUInt64()` 호출과 마찬가지로 부모 상태가 정확히 1스텝 전진합니다), 이를 SplitMix64로 자식의 상태로 확장합니다. 자식의 시드가 64bit이므로, 독립적으로 fork된 자식들 사이의 생일 충돌 확률은 `2^32`회 fork 규모에서야 유의미해집니다 — 어떤 게임·시뮬레이션 워크로드보다도 훨씬 큰 규모입니다.

## 스레드 안전성

| 타입 | 스레드 안전 | 비고 |
|---|---|---|
| `DeterministicRandom` | **아니오** | 동시 접근 시 내부 상태가 파손되고 수열 재현성이 깨집니다. 스레드마다 별도 인스턴스를 사용하거나 외부에서 동기화하세요. |
| `CryptoRandomSource` | 예 | `RandomNumberGenerator.Fill`은 static이며 스레드 안전합니다 → 싱글턴으로 제공됩니다. |
| `SharedRandomSource` | 예 | `Random.Shared` 자체가 스레드 안전합니다. |
| `SystemRandomSource` | 감싼 인스턴스에 따름 | 일반 `new Random(seed)`는 스레드 안전하지 않고, `Random.Shared`는 안전합니다(이 경우 `SharedRandomSource`를 사용하는 것이 좋습니다). |
| `WeightedSampler<T>` | 예 (불변) | 테이블은 `Create(...)`에서 한 번만 빌드되며, `Pick`/`PickMany` 호출은 그 테이블과 호출자가 넘긴 `IRandomSource`만 읽습니다. |

## 보안

**`DeterministicRandom`은 예측 가능합니다.** 연속된 출력 몇 개만 있으면 내부 상태를 복원해 이후의 모든 출력을 예측할 수 있습니다. 토큰, 자격 증명, 비밀이 유지되어야 하는 셔플 등 보안이 필요한 목적으로는 **절대** 사용하지 마세요.

이런 경우에는 `CryptoRandomSource`를 사용하세요. `DeterministicRandom`의 재현성은 필요하지만 시드는 예측 불가능해야 한다면, 암호학적 RNG로 시드를 뽑는 `DeterministicRandom.CreateRandomlySeeded()`를 사용하세요 — 시드만 예측 불가능할 뿐, 생성된 생성기 자체는 여전히 예측 가능한 `DeterministicRandom`입니다.

## 예외

다음 계약은 `RandomState`, 범위 생성 멤버, 그리고 모든 가중치 추첨 API(`WeightedRandomExtensions`, `WeightedSampler<T>`)에 일관되게 적용됩니다.

| 조건 | 예외 |
|---|---|
| `items`가 비어 있음 | `ArgumentException` |
| 음수 가중치 존재 | `ArgumentException` (문제 인덱스 포함) |
| `double` 가중치가 NaN/Infinity | `ArgumentException` (문제 인덱스 포함) |
| 가중치 총합이 0 | `ArgumentException` |
| `long` 가중치 총합 오버플로 | `OverflowException` (checked 합산) |
| `count <= 0` | `ArgumentOutOfRangeException` |
| `PickManyWeightedDistinct`에서 `count`가 양수 가중치 항목 수를 초과 | `ArgumentOutOfRangeException` |
| `RandomState.FromState(...)` / `RandomState.FromSpan(...)`에 all-zero 상태를 전달 | `ArgumentException` |
| 범위 지정 `Next`/`NextInt64` 오버로드에서 `minValue > maxValue` | `ArgumentOutOfRangeException` |

가중치 0인 항목은 **허용**되며 단지 절대 선택되지 않을 뿐입니다(총합만 양수면 됩니다). `PickManyWeightedDistinct`에서 `count`의 상한은 `items.Count`가 아니라 *양수* 가중치 항목의 수입니다 — 가중치 0인 항목은 절대 뽑힐 수 없으므로, 그 이상을 요구하면 무한 탐색이 되거나 가중치 0 항목을 잘못 반환하게 됩니다.

## 라이센스

MIT — 자세한 내용은 [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE)를 참고하세요.

---

**AI 고지:** 이 프로젝트는 AI(Claude)를 활용하여 제작되었습니다.
