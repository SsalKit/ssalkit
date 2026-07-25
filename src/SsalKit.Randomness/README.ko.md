[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ko.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.md) | **한국어** | [日本語](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.ja.md)

# SsalKit.Randomness

결정적(deterministic)이고 상태를 직렬화할 수 있는 PRNG(`xoshiro256**` + SplitMix64)와, 통일된 난수 소스 추상화, 가중치 랜덤 추첨을 제공하는 라이브러리입니다. `[RandomWeight]` 특성으로 셀렉터 없는 추첨 확장을 컴파일 타임에 생성하는 기능도 포함합니다. 의존성이 없습니다.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Randomness.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Randomness)

## 왜 SsalKit.Randomness인가

게임 로직, 시뮬레이션, 절차적 콘텐츠 생성은 결국 같은 요구사항에 부딪힙니다. 같은 시드 혹은 같은 저장된 상태가 주어지면, "무작위" 결과의 정확히 같은 수열이 다시 나와야 한다는 것입니다 — 리플레이, 결정적 락스텝 멀티플레이어, 한 판의 실행을 비트 단위로 재현하는 세이브 파일을 위해서요.

`System.Random`은 이 요구를 온전히 채워주지 못합니다.

- **시드를 지정한 `Random`은 레거시 알고리즘을 사용합니다.** `int` 시드를 받는 생성자는 호환성을 위해 출력을 안정적으로 유지하지만, 그 안정성은 애초에 1급 설계 목표가 아니었고, `Random`의 모든 생성 경로에 걸쳐 보장되는 것도 아닙니다.
- **상태 export를 지원하지 않습니다.** `System.Random`은 내부 상태를 꺼내 저장했다가 나중에 복원하는 공식적인 방법을 제공하지 않습니다. 실행 내내 같은 `Random` 인스턴스를 살려두거나, 아니면 재현성을 포기해야 합니다.
- **스트림 분기가 없습니다.** 하나의 부모 시드에서 독립적이고 재현 가능한 자식 생성기를 파생시키는 내장 방법이 없습니다 (엔티티별·서브시스템별 난수가 하나의 루트 시드로 추적되어야 할 때 유용합니다).

SsalKit.Randomness는 접근 방식 자체가 다릅니다.

- **`DeterministicRandom`**은 `System.Random`과 비슷한 모양의 sealed PRNG(`xoshiro256**`)로, 256bit 전체 상태를 export하여 어디든(세이브 파일, DB 로우, 네트워크 패킷) 저장했다가 복원하면, 어떤 플랫폼에서든 영원히 정확히 같은 수열을 이어갈 수 있습니다.
- **`IRandomSource`**는 결정적/공유(`Random.Shared`)/암호학적 난수를 하나의 인터페이스로 통일하므로, 범위 생성·셔플·추첨 코드를 한 번만 작성해 셋 중 무엇에도 적용할 수 있습니다.
- **가중치 랜덤 추첨**(`PickWeighted`, `PickManyWeighted(Distinct)`, `WeightedSampler<T>`)이 라이브러리에 함께 제공되며, 명확한 예외 계약과 반복 추첨용 `O(1)` alias method 샘플러를 갖추고 있습니다.
- **`[RandomWeight]`**로 모델 타입의 가중치 멤버를 표시하면, 패키지에 동봉된 소스 생성기가 셀렉터를 대신 작성해 줍니다. `random.PickWeighted(lootTable, static x => (long)x.Weight)` 대신 `lootTable.PickWeighted(random)`이면 됩니다. 순수하게 컴파일 타임 코드 생성이므로 리플렉션이 없고 AOT·트리밍에 안전합니다.
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

// 항목이 가중치를 직접 들고 있다면: 가중치 셀렉터로 빌드하며, 항목 타입은 추론됩니다.
(string Name, long Weight)[] loot = [("common", 80), ("rare", 18), ("legendary", 2)];
var lootSampler = loot.ToWeightedSampler(entry => entry.Weight);
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
| `WeightedRandomExtensions` | `PickWeighted`(단발, `long` 또는 `double` 가중치, 리스트 또는 span 형태), `PickManyWeighted`(복원 추출), `PickManyWeightedDistinct`(비복원 추출), 그리고 `ToWeightedSampler` — `items.ToWeightedSampler(x => x.Weight)` 형태로 리스트에서 바로 샘플러를 빌드하며, `WeightedSampler<T>.Create`처럼 타입 인자를 적을 필요 없이 항목 타입이 추론됩니다. |
| `WeightedSampler<T>` | 고정된 `long` 가중치 항목 집합에서 반복 추첨할 때 쓰는, 불변·스레드 안전한 사전 빌드 alias method 샘플러. 빌드는 `O(n)`, `Pick`/`PickMany`는 호출당 `O(1)`. |
| `RandomWeightAttribute` | 모델 타입의 가중치 프로퍼티 또는 필드에 붙이는 특성. 패키지에 동봉된 소스 생성기가 해당 타입의 `IReadOnlyList<T>`에 대한 셀렉터 없는 `PickWeighted`/`PickManyWeighted`/`PickManyWeightedDistinct`/`ToWeightedSampler` 확장을 컴파일 타임에 생성합니다. |

## `[RandomWeight]`로 셀렉터 없이 추첨하기

위의 가중치 API는 모두 셀렉터를 받습니다. `random.PickWeighted(lootTable, static x => (long)x.Weight)`처럼요. 모델 타입에 가중치 멤버가 딱 하나뿐인데도 호출할 때마다 이 셀렉터를 반복해서 적는 것은 군더더기입니다. 대신 멤버에 표시만 해 두세요.

```csharp
using SsalKit.Randomness;

namespace Game.Loot;

public sealed class LootEntry
{
    public required string ItemId { get; init; }

    [RandomWeight]
    public long Weight { get; init; }
}
```

이 특성 한 줄이 필요한 전부입니다. 생성기는 컴파일 타임에 `LootEntryRandomWeightExtensions` 클래스를 `LootEntry`와 같은 네임스페이스에 생성하므로, 타입을 쓸 수 있는 곳이면 확장 메서드도 이미 스코프 안에 들어와 있습니다.

```csharp
IReadOnlyList<LootEntry> lootTable = [ /* ... */ ];
var rng = new DeterministicRandom(seed: 42);

LootEntry drop      = lootTable.PickWeighted(rng);                        // 단건
LootEntry[] drops   = lootTable.PickManyWeighted(rng, count: 4);          // 복원 추출
LootEntry[] distinct = lootTable.PickManyWeightedDistinct(rng, count: 3); // 비복원 추출

// alias 테이블을 한 번만 빌드하고, 추첨은 건당 O(1).
WeightedSampler<LootEntry> sampler = lootTable.ToWeightedSampler();
LootEntry sampled = sampler.Pick(rng);
```

리시버는 컬렉션이고, 난수 소스는 명시적인 인자로 남습니다 — 어떤 소스에서 뽑는지는 호출부에서 보여야 하는 결정이므로, 인자 없는 `lootTable.PickWeighted()`는 타입이 직접 요청하지 않는 한 생성되지 않습니다([공유 소스 오버로드](#공유-소스-오버로드) 참고).

이 기능을 특성으로 제공할 만한 이유는 세 가지입니다.

- **리플렉션도, 런타임 디스패치도 없습니다.** 생성된 메서드는 컴파일 타임에 만들어진 평범한 C# 코드라서 AOT·트리밍에 안전하고, 비용도 손으로 셀렉터를 적었을 때와 정확히 같습니다.
- **따로 설치할 것이 없습니다.** 생성기는 `SsalKit.Randomness` 패키지 안에 analyzer로 동봉되어 있습니다. 패키지를 추가하면 특성과 생성기를 함께 받게 되며, 의존성 목록은 여전히 비어 있습니다.
- **직접 셀렉터를 적었을 때와 동작이 동일합니다.** 생성된 메서드는 각각 대응하는 런타임 오버로드로 그대로 위임하므로, 아래 예외 섹션에 문서화된 예외 계약이 그대로 적용되고, 같은 시드에서는 같은 추첨 결과가 나옵니다.

### 무엇이 생성되는가

| 가중치 멤버 타입 | 생성되는 확장 |
|---|---|
| `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long` | `PickWeighted(source)`, `PickManyWeighted(source, count)`, `PickManyWeightedDistinct(source, count)`, `ToWeightedSampler()` |
| `float`, `double` | `PickWeighted(source)`만 — 배치 추첨과 alias 테이블 샘플링을 `long` 가중치에 대해서만 제공하는 런타임 표면을 그대로 반영한 결과입니다 |
| 그 외(`ulong`, `decimal`, enum, nullable 숫자 타입, 숫자가 아닌 타입) | 생성하지 않고 `SSALR001` 진단 |

`ulong`은 의도적으로 제외했습니다. `long`으로 변환할 때 오버플로가 발생할 수 있기 때문입니다. 생성된 확장의 리시버는 모두 `IReadOnlyList<T>`이며, `List<T>`·배열·`ImmutableArray<T>`가 모두 여기에 해당합니다. 지연 시퀀스라면 먼저 `.ToList()`를 명시적으로 호출해야 합니다 — 가중치 추첨에는 인덱스 접근이 필요하고, 이 라이브러리는 그 비용을 숨기지 않습니다.

### 가시성

생성되는 클래스는 기본적으로 `public`이며, 대상 타입의 유효 접근성을 상한으로 합니다 — 따라서 `internal` 모델 타입이면 확장도 자동으로 `internal`이 되어 접근성 불일치가 생기지 않습니다. public 어셈블리의 공개 API 표면에 이 헬퍼들을 노출하고 싶지 않다면 명시적으로 지정하세요.

```csharp
[RandomWeight(InternalExtensions = true)]
public long Weight { get; init; }
```

### 공유 소스 오버로드

호출부마다 소스를 넘기는 것이 옳은 기본값이지만, 추첨을 다시 재현할 일이 전혀 없는 모델에서는 형식적인 절차일 뿐입니다. `SharedSourceOverloads = true`를 지정하면 `SharedRandomSource.Instance`에서 뽑는 인자 없는 오버로드가 추가로 생성됩니다.

```csharp
public sealed class GachaEntry
{
    public required string CharacterId { get; init; }

    [RandomWeight(SharedSourceOverloads = true)]
    public long Weight { get; init; }
}

IReadOnlyList<GachaEntry> banner = [ /* ... */ ];

GachaEntry pull       = banner.PickWeighted();                        // 공유 소스
GachaEntry[] tenPull  = banner.PickManyWeighted(count: 10);           // 공유 소스
GachaEntry[] distinct = banner.PickManyWeightedDistinct(count: 3);    // 공유 소스

GachaEntry replayable = banner.PickWeighted(new DeterministicRandom(seed: 42)); // 그대로 사용 가능
```

오버로드는 대체가 아니라 추가입니다. 소스를 받는 형태는 전혀 달라지지 않고, 인자 없는 메서드는 각각 대응하는 오버로드로 한 줄 위임할 뿐이므로 검증·예외·추첨 의미가 완전히 동일합니다. `ToWeightedSampler()`는 애초에 소스를 받지 않으므로 변화가 없습니다. 가중치 타입 매트릭스도 그대로여서, `float`/`double` 멤버는 두 형태의 `PickWeighted`만 얻습니다.

기본값이 꺼져 있는 이유는 `SharedRandomSource`가 시드를 받을 수 없어 수열을 재현할 수 없는데, 인자 없는 호출은 바로 그 사실이 호출부에서 보이지 않는 형태이기 때문입니다. 꺼진 상태를 기본으로 두면 시드 기반 재현성 위에 세운 코드베이스에 비결정적 추첨이 조용히 섞이지 않고, 켜는 행위는 "이 타입의 추첨은 재현할 일이 없다"는 타입 단위의 선언이 됩니다 — 가챠 배너, 외형 아이템 드롭 테이블, 대사 문구 추첨 같은 경우입니다. 애매하면 끈 채로 두고 소스를 계속 넘기세요.

### 진단

| ID | 보고 조건 |
|---|---|
| `SSALR001` | 가중치 멤버의 타입이 지원 대상이 아님(위 표 참고). |
| `SSALR002` | 한 타입에 `[RandomWeight]` 멤버가 둘 이상 선언됨. |
| `SSALR003` | 멤버가 `static`이거나 쓰기 전용 프로퍼티, 또는 인덱서임 — 읽을 수 있는 인스턴스 멤버여야 함. |
| `SSALR004` | 멤버·선언 타입·바깥 타입 중 하나가 생성된 클래스에서 접근 불가(`private`, `protected`, file-local). |
| `SSALR005` | 선언 타입이 제네릭이거나 제네릭 타입 안에 중첩되어 있음. |
| `SSALR006` | 선언 타입이 `ref struct`라서 제네릭 타입 인자로 쓸 수 없음. |

여섯 가지 모두 오류이며, 어떤 타입에서 하나라도 발생하면 그 타입에 대해서는 아무것도 생성되지 않습니다 — 부분 생성은 없습니다.

### 알아 둘 점

- **가중치는 일반 프로퍼티나 필드로 선언하세요.** 대상을 리다이렉트하는 특성 표기 — positional record 매개변수의 `[property: RandomWeight]`나 자동 프로퍼티의 `[field: RandomWeight]` — 는 생성기가 인식하지 못하며, 진단도 생성 코드도 없이 조용히 무시됩니다. 대신 `public long Weight { get; init; }`(또는 일반 필드)로 선언하세요.
- **상속은 따라가지 않습니다.** base 타입에 `[RandomWeight]`를 붙이면 그 base 타입용 확장만 생성됩니다. `IReadOnlyList<out T>`의 공변성 덕분에 `List<Derived>`에서도 호출할 수 있지만, 반환되는 정적 타입은 base 타입이므로 `Derived`로 되돌리려면 캐스트가 필요합니다.
- **샘플러는 한 번만 빌드하세요.** `ToWeightedSampler()`는 `O(n)`이고 `O(1)`인 것은 추첨뿐입니다. 추첨 루프 안에서 호출하면 매 반복마다 alias 테이블을 다시 만들게 되어 샘플러를 쓰는 이유 자체가 사라집니다. 가중치 테이블당 하나만 빌드해 두고(불변이며 스레드 안전합니다) 반복해서 뽑으세요. 한 번만 뽑을 거라면 `PickWeighted`를 쓰면 됩니다.

## 성능

SsalKit.Randomness는 순수 처리량 극대화가 아닌 다른 목표를 최적화합니다. 결정성·상태 직렬화·무할당을 성능 비용 없이 얻는 것입니다 — 실제로 `DeterministicRandom`의 스칼라 연산은 BCL의 모든 범용 대안보다 오히려 더 빠릅니다.

BenchmarkDotNet v0.15.8, .NET 10.0.10, AMD Ryzen 9 3950X, Windows 11 환경에서 측정했습니다(SsalKit.Randomness 0.1.0 기준). 수치는 하드웨어에 따라 달라질 수 있으며, [벤치마크 프로젝트](https://github.com/ssalkit/ssalkit/tree/main/benchmarks/SsalKit.Randomness.Benchmarks)로 직접 재현할 수 있습니다.

### 균등 생성

| 연산 | DeterministicRandom | `new Random(seed)` | `Random.Shared` | CryptoRandomSource |
|---|---:|---:|---:|---:|
| NextUInt64 상당 | 1.8 ns / 0 B | 25.1 ns / 0 B | 3.7 ns / 0 B | 70.6 ns / 0 B |
| Next(1000) | 2.4 ns / 0 B | 4.7 ns / 0 B | 3.5 ns / 0 B | 68.1 ns / 0 B |
| NextInt64(범위 지정) | 1.9 ns / 0 B | 15.7 ns / 0 B | 3.6 ns / 0 B | 71.0 ns / 0 B |
| NextDouble | 2.0 ns / 0 B | 3.8 ns / 0 B | 4.0 ns / 0 B | 71.7 ns / 0 B |

`DeterministicRandom`은 측정된 모든 스칼라 연산에서 가장 빠릅니다(1.8~2.6 ns, 위 표에 없는 `NextRange`도 동일하게 2.6 ns). 시드를 지정한 레거시 `Random` 대비 최대 약 14배, `Random.Shared` 대비 약 1.5~2배 빠릅니다. 네 소스 모두 스칼라 생성에서 할당이 0바이트입니다.

참고:
- `Random.Shared`는 스레드 안전 래퍼이므로, 위의 단일 스레드 소스들과 완전히 동일한 조건의 비교는 아닙니다.
- 유일한 예외는 64바이트 버퍼에 대한 `NextBytes`로, 이 경우에는 `Random.Shared`(16.5 ns)가 `DeterministicRandom`(19.2 ns)보다 근소하게 더 빠릅니다.
- `CryptoRandomSource`는 전반적으로 `DeterministicRandom`보다 약 25~40배 느립니다 — `RandomNumberGenerator` 기반이라 암호학적 예측 불가능성을 제공하는 대가이며, 다른 소스들은 이 보장을 제공하지 않으므로 당연한 결과입니다.

### 디스패치 비용

| 호출 방식 | Mean |
|---|---:|
| `DeterministicRandom` 인스턴스 직접 호출 | 2.25 ns |
| `IRandomSource` 확장 메서드 경유 | 2.69 ns |

`IRandomSource` 추상화를 거치는 비용은 약 0.4 ns — 가상 호출 1회분입니다. 서브나노초 수준의 오버헤드이므로, 유연성(결정적/공유/암호학적 소스 교체)을 위해 `IRandomSource`를 대상으로 코드를 작성해도 사실상 공짜입니다. 핫루프에서 항상 하나의 구체 타입만 쓴다면, `DeterministicRandom`을 직접 호출해 이 비용마저 없앨 수 있습니다.

### 가중치 추첨

| 메서드 | N=10 | N=100 | N=1000 |
|---|---:|---:|---:|
| `PickWeighted`(리스트/델리게이트) | 43.1 ns / 104 B | 206.9 ns / 824 B | 1,590.5 ns / 8,024 B |
| `PickWeighted`(span) | 36.7 ns / 0 B | 142.8 ns / 0 B | 1,363.5 ns / 8,024 B |
| `WeightedSampler<T>.Pick` | 11.1 ns / 0 B | 10.6 ns / 0 B | 11.4 ns / 0 B |

`WeightedSampler<T>.Pick`은 `N`과 무관하게 ~11 ns로 평탄합니다 — alias method 테이블 덕분에 매 추첨이 실제로 `O(1)`이라는 뜻입니다. span 기반 `PickWeighted` 오버로드는 항목 256개까지는 할당이 없으며, 그 이상에서는 힙 버퍼로 폴백합니다(위 N=1000의 8KB는 이 문서화된 폴백이지 누수가 아닙니다).

`WeightedSampler<T>`를 빌드하는 비용은 공짜가 아닙니다 — `Create(...)`는 N=10에서 237 ns, N=100에서 1.7 μs, N=1000에서 14.9 μs가 걸립니다. 하지만 이는 일회성 비용입니다. N=1000 기준으로, 반복적인 단발 `PickWeighted`(span) 호출과 비교했을 때 샘플러를 빌드하는 비용은 약 **11회 추첨**이면 상환됩니다 — 같은 테이블에서 몇 번 이상 뽑을 계획이라면 샘플러 쪽이 이득입니다.

## 알고리즘 및 상태 계약 (v1)

`DeterministicRandom`의 출력 수열은 `xoshiro256**`이며, 시드 확장(단일 `ulong` 시드에서 256bit 내부 상태로)은 **SplitMix64**입니다. 상태는 정확히 4개의 `ulong` 워드로 구성되며 `RandomState`로 노출됩니다.

이 계약은 이 타입에 대해 영구적으로 고정됩니다.

- **같은 시드 또는 같은 방식으로 복원된 상태는 어떤 플랫폼·어떤 프로세스에서든, 영원히 항상 같은 수열을 만들어냅니다.**
- `RandomState`가 세이브 데이터로 저장될 수 있으므로, 출력 수열을 바꾸면 모든 소비자의 세이브 데이터가 손상되는 것과 같습니다. 이런 변경은 패치/마이너 릴리스에서 **절대** 일어나지 않습니다.
- 알고리즘을 언젠가 진화시켜야 한다면, `DeterministicRandom` 자체의 동작을 바꾸는 대신 **새로운 타입**(예: 가상의 `DeterministicRandomV2`)으로만 출시됩니다.
- all-zero 상태는 무효한 상태입니다(`xoshiro256**`는 한 번 그 상태에 들어가면 절대 빠져나오지 못합니다). `FromState(...)`/`RandomState.FromSpan(...)`은 이를 `ArgumentException`으로 거부합니다.

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
