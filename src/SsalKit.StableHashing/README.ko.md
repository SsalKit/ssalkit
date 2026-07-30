[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ko.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.StableHashing/README.md) | **한국어** | [日本語](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.StableHashing/README.ja.md)

# SsalKit.StableHashing

버전 고정 canonical encoding 계약을 통한, 플랫폼·프로세스 독립적인 64bit 체크섬 라이브러리입니다. `[StableHashContract]`/`[StableHashMember]`를 붙이면 소스 생성기가 `ComputeStableHash()`를 대신 작성해 주며, 해시는 내부 포팅한 XxHash64로 계산합니다. 의존성이 없습니다.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.StableHashing.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.StableHashing)

## 왜 SsalKit.StableHashing인가

`object.GetHashCode()`가 이 역할을 해줄 것처럼 보이지만, BCL은 명시적으로 그렇지 않다고 말합니다. 공식 문서의 계약은 같은 프로그램의 서로 다른 실행 사이에서, 프로세스 사이에서, .NET 버전 사이에서 값이 달라지는 것을 허용하며, 그 값을 어딘가에 영구 저장하거나 한 객체의 생애주기를 벗어나서까지 같은 값이 유지될 것이라 기대하지 말라고 경고합니다. 이는 정확히 "이 객체의 해시"가 정말 필요한 용도들 — DB에 저장하는 체크섬, 네트워크로 보내는 값, 두 머신 사이에서 비교하는 값, 오늘 실행과 다음 달 실행을 비교하는 값 — 을 못 쓰게 막는 셈입니다.

`System.IO.Hashing`(BCL 자체의 `XxHash64`, `XxHash3` 등)도 이 공백을 채워주지 않습니다 — 의도적으로요. 이미 갖고 있는 바이트를 해시할 뿐, C# 객체를 영원히 일관되게 유지되는 방식으로 바이트로 바꾸는 방법에 대해서는 아무 의견도 없습니다. "객체를 바이트로 어떻게 바꿀 것인가" — 필드 순서, 숫자 폭, 문자열 인코딩, `-0.0`이나 `1.0m` vs `1.00m`을 어떻게 처리할 것인가 — 라는 결정이야말로 딱 한 번 정해서 다시는 바꾸면 안 되는 부분입니다. 바꾸는 순간 이전에 계산해 둔 모든 해시가 조용히 무효가 되니까요. 이 라이브러리의 실제 산출물은 바로 이것, **canonical encoding 계약**입니다. 해시 알고리즘이 아니라요. 해시 알고리즘은 상대적으로 교체 가능하지만, 인코딩 규칙은 그렇지 않습니다.

SsalKit.StableHashing은:

- **`[StableHashContract]` / `[StableHashMember(id)]`**로 타입과 그 멤버를 표시합니다. 패키지에 동봉된 소스 생성기가 컴파일 타임에 `ComputeStableHash()` 확장 메서드를 대신 작성해 줍니다 — 리플렉션 없이, AOT·트리밍에 안전하게요.
- **인코딩이 영구 고정된 v1 계약입니다** (바이트 순서, 필드 폭, 문자열 인코딩, 중첩 계약 재귀 방식 등 — 아래 참고). 오늘 계산한 해시와, 이 라이브러리의 미래 패치가 다른 머신·아키텍처에서 계산한 해시가 같은 논리적 입력에 대해 영원히 같은 값입니다.
- **설계 자체로 동등성 일관성을 지킵니다.** `decimal`, `DateTimeOffset`, `float`/`double`은 각각 `==`가 true이지만 내부 비트가 다른 함정이 있는데, 인코딩이 셋 다 정규화하여 `a == b`이면 항상 `encode(a) == encode(b)`가 되도록 보장합니다 (아래 동등성 일관성 불변식 참고).
- **의존성 0.** 해시 알고리즘(XxHash64)을 `System.IO.Hashing`에서 끌어오는 대신 내부에 직접 포팅했으므로, 패키지는 BCL만 사용합니다.

주요 사용처: 두 락스텝/리플레이 시뮬레이션이 디싱크됐는지 감지(전체 상태 대신 틱별 해시 비교), 실제로 변경된 게 없을 때 불필요한 스냅샷 저장을 건너뛰기, 결정적 A/B 버케팅(`hash % 100`), 그리고 재현 가능한 named random stream 파생 — `StableHash64.Value`를 그대로 `new SsalKit.Randomness.DeterministicRandom(hash.Value)`(별도의 선택적 패키지인 [SsalKit.Randomness](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.ko.md))에 넘겨, 해시 가능한 어떤 값이든 시드로 바꿀 수 있습니다. 두 패키지는 서로에 대한 의존성이 없습니다 — 이건 결합이 아니라 문서화된 사용 패턴입니다.

## 설치

```bash
dotnet add package SsalKit.StableHashing
```

패키지에는 런타임 타입(`StableHash64`, `StableHashWriter`, 특성 2종)과 소스 생성기가 모두 들어 있습니다 — 별도로 설치할 analyzer 패키지가 없고, 자체 `PackageReference`도 없습니다.

## 빠른 시작

```csharp
using SsalKit.StableHashing;

[StableHashContract("game.player-snapshot", Version = 1)]
public sealed record PlayerSnapshot
{
    [StableHashMember(1)] public string PlayerId { get; init; } = "";

    [StableHashMember(2)] public int Level { get; init; }

    [StableHashMember(3)] public long Gold { get; init; }
}

var snapshotA = new PlayerSnapshot { PlayerId = "player-42", Level = 17, Gold = 2_450 };
var snapshotB = new PlayerSnapshot { PlayerId = "player-42", Level = 17, Gold = 2_450 };

StableHash64 hashA = snapshotA.ComputeStableHash();
StableHash64 hashB = snapshotB.ComputeStableHash();

hashA == hashB;      // true -- 서로 다른 인스턴스지만 멤버 값이 같음
hashA.ToString();     // "9c3f38517dbc66aa" -- 소문자, 16자리 hex
hashA.Value;          // 원시 ulong 값
```

`[StableHashMember]`는 opt-in입니다. 붙이지 않은 멤버는 그냥 계약에서 제외될 뿐, 진단은 없습니다. 멤버는 선언 순서가 아니라 `Id` 오름차순으로 인코딩되므로, 소스에서 멤버 순서를 바꾸거나 이름을 바꿔도 해시는 절대 변하지 않습니다 — `Id`를 바꾸거나 값의 타입을 바꿀 때만 변합니다.

## API 개요

| 타입 | 역할 |
|---|---|
| `StableHashContractAttribute(string name)` | `class`/`struct`를 계약으로 표시합니다. `Name`은 계약의 영구 식별자(CLR 타입명과 독립적이라 타입은 자유롭게 rename 가능), `Version`(기본값 `1`)은 멤버 집합이나 멤버 타입이 바뀌어 기존에 저장된 해시를 무효화해야 할 때 올립니다. |
| `StableHashMemberAttribute(int id)` | 필드/프로퍼티를 계약의 일부로 표시하며, 값 앞에 인코딩되는 안정적 id(`>= 1`)를 지정합니다. 이 특성이 없는 멤버는 제외됩니다. |
| `StableHash64` | `ulong` 결과를 감싸는 `readonly record struct`. `ToString()`은 소문자, 0으로 채운 16자리 hex를 반환합니다. `.Value`를 `DeterministicRandom`에 넘기면 named seed로 쓸 수 있습니다 (위 참고). |
| `StableHashWriter` | 생성기가 호출하는, 할당 없는 저수준 `ref struct`입니다 — 생성기가 (아직) 다루지 않는 타입에는 직접 사용할 수 있습니다. 정확한 규칙은 아래 인코딩 계약 참고. |
| 생성되는 `ComputeStableHash()` / `AppendStableHash(ref StableHashWriter)` | 계약 타입마다 그 타입의 네임스페이스에 `public static class {Type}StableHashing` 하나가 생성되며, 이 두 확장 메서드를 담습니다. `AppendStableHash`는 중첩 계약 멤버가 호출하는 대상입니다. class 계약의 `ComputeStableHash()`는 receiver가 null이면 `ArgumentNullException`을 던집니다. |

## 인코딩 계약 (v1)

**이 인코딩은 영구적인, 버전 고정 계약입니다.** 아래 규칙 하나하나 — 바이트 순서, 필드 폭, 부동소수점/decimal 정규화 규칙, 선두 포맷 마커 — 가 영원히 고정됩니다. 이 중 무엇이든 바꾸면 이 라이브러리가 지금까지 만들어낸 모든 해시가 조용히 바뀌어, 모든 소비자가 저장해 둔 체크섬이 손상됩니다. 인코딩이 진화해야 한다면 새롭고 별도인 API(예: 가상의 `StableHash128`/`StableHashWriterV2`)로 나가며, 이 API의 동작을 바꾸는 방식으로는 절대 진화하지 않습니다.

모든 출력 스트림은 포맷 마커 1바이트(`0x01`)로 시작한 뒤, 계약 헤더 — 계약 이름(아래처럼 길이 접두 문자열)에 이어 `Version`을 little-endian `int32`로 — 가 옵니다. 멤버 값 앞에는 멤버 id(little-endian `int32`)가 옵니다. 모든 고정폭 정수는 little-endian입니다.

| 타입 | 인코딩 |
|---|---|
| `bool` | 1바이트 (`0x00`/`0x01`) |
| `sbyte`~`ulong`, `Int128`/`UInt128` | 고정폭, little-endian |
| `char` | UTF-16 코드 유닛, little-endian `ushort` |
| `enum` | 기저 타입의 인코딩 (멤버명 변경은 안전, 기저 값 변경은 해시 변경) |
| `float`/`double` | 아래 동등성 일관성 불변식대로 정규화한 뒤 비트 패턴, little-endian |
| `decimal` | 아래 동등성 일관성 불변식대로 정규화한 뒤 sign(1B) + scale(1B) + 96bit mantissa(12B, little-endian) |
| `string` | little-endian `int32` UTF-8 바이트 수 + UTF-8 바이트 (잘못된 UTF-16은 `Encoding.UTF8`의 결정적 대체 문자 처리로 폴백) |
| `Guid` | RFC 4122 big-endian 16바이트 — `Guid.TryWriteBytes(span, bigEndian: true, out _)`, 문자열 표기와 같은 바이트 순서 |
| `DateOnly` | `DayNumber`, little-endian `int32` |
| `TimeOnly` / `TimeSpan` | `Ticks`, little-endian `int64` |
| `DateTimeOffset` | **오직** `UtcTicks`만, little-endian `int64` — 아래 동등성 일관성 불변식 참고 |
| `T?` (`Nullable<T>` / nullable 참조 타입) | 1바이트 마커(`0x00` 없음 / `0x01` + 값); non-nullable 멤버는 마커가 아예 없음 |
| `T[]`, `List<T>`, `IReadOnlyList<T>`, `ImmutableArray<T>` | little-endian `int32` 원소 수 + 각 원소를 순서대로 재귀 인코딩 (원소 타입도 지원 타입이어야 하며, 중첩 허용) |
| 다른 `[StableHashContract]` 타입 | 해당 계약의 전체 인코딩을 헤더까지 포함해 재귀적으로 — 중첩 계약 자체의 버전/이름 변경도 그 계약을 담은 모든 해시에 올바르게 전파됨 |

**v1이 거부하는 것(런타임 사고가 아니라 컴파일 타임 진단):** `DateTime`(`SSALH003` — `DateTimeOffset`이나 `DateOnly` 사용), `Dictionary`/`HashSet`/그 외 비순서·임의 `IEnumerable<T>`(순회 순서가 보장되지 않음), `object`, delegate, pointer, 인터페이스와 추상 타입(컴파일 타임에 런타임 타입을 알 수 없음), `[StableHashContract]`가 없는 사용자 정의 타입, 순환 계약 그래프, 제네릭 계약 타입.

## 동등성 일관성 불변식

> 지원하는 모든 타입에 대해, **`a == b`이면 `encode(a) == encode(b)`.**

BCL의 세 타입은 이 불변식이 조용히 깨질 수 있는 함정을 갖고 있습니다 — `==`는 같다고 하는데 내부 비트는 다른 경우입니다 — 그래서 인코딩은 이 셋을 쓰기 전에 정규화합니다.

| 타입 | 함정 | v1 규칙 |
|---|---|---|
| `decimal` | `1.0m == 1.00m`이지만 내부 scale(따라서 비트)이 다름 | scale이 양수이고 mantissa가 나누어떨어지는 동안 96bit mantissa를 10으로 나누어 정규화(정수 연산만, 최대 28회 반복) — `1.0m`과 `1.00m`이 동일하게 인코딩됨. `0m`, `-0.0m`, `0.00m` 등 모든 0 표현은 하나의 canonical zero 인코딩(sign `0x00`, scale `0`, mantissa `0`)으로 정규화됨 — decimal의 동등성도 0의 부호를 구분하지 않으므로. |
| `DateTimeOffset` | 같은 순간이지만 오프셋이 다르면 `==`가 true (`1pm+01:00 == noon+00:00`) | **오직** `UtcTicks`만 인코딩 — 오프셋은 의도적으로 제외됨. 오프셋 자체가 의미 있다면 별도 멤버로 저장할 것. |
| `float` / `double` | `-0.0 == +0.0`이지만 비트 패턴이 다름; NaN 페이로드 비트는 플랫폼(x86 vs ARM)마다 이식성이 없음 | 음의 0을 양의 0으로, 모든 NaN 비트 패턴을 하나의 canonical quiet NaN(`float`은 `0x7FC00000`, `double`은 `0x7FF8000000000000`)으로 정규화한 뒤 비트 패턴을 씀. |

같은 원칙에서 두 가지 총함수성(totality) 규칙이 더 따라나옵니다.

- **`string`**: 잘못된 UTF-16(짝이 안 맞는 서로게이트)은 `Encoding.UTF8`의 기본 대체 문자(`U+FFFD`) 폴백을 쓰며, 이 자체가 결정적입니다 — 인코딩에 실패하는 입력은 없습니다.
- **`ImmutableArray<T>`**: `default(ImmutableArray<T>)`(미초기화)는 empty로 취급됩니다. 이미 대부분의 코드가 둘을 서로 바꿔 쓰는 방식과 맞고, writer가 이 타입의 모든 값에 대해 총함수로 남게 해 줍니다.

이 불변식은 한쪽 방향으로만 성립합니다: `encode(a) == encode(b)`라고 해서 `a == b`가 성립하는 것은 **아닙니다** — 아래 해시 의미론 참고.

## 진단

| ID | 심각도 | 조건 |
|---|---|---|
| `SSALH001` | Error | 같은 계약 안에서 두 개 이상의 멤버가 같은 `[StableHashMember]` id를 선언 |
| `SSALH002` | Error | 멤버 타입이 v1 지원 타입이 아님 (위의 거부 목록 참고) |
| `SSALH003` | Error | 멤버가 `System.DateTime`임 — 대신 `DateTimeOffset`(순간) 또는 `DateOnly`(달력 날짜) 사용 |
| `SSALH004` | Error | 멤버 타입이 `[StableHashContract]`가 없는 사용자 정의 타입 |
| `SSALH005` | Error | 이 타입의 멤버 타입을 따라가다 보면 결국 이 타입 자체로 순환됨 |
| `SSALH006` | Error | `class` 계약이 `sealed`가 아님 (`static class`도 마찬가지로 거부) |
| `SSALH007` | Error | 생성된 확장 클래스에서 멤버를 읽을 수 없음(`private`/`protected`, `static`, 인덱서, write-only) — 또는 계약 타입 자체나 그 상위 타입이 생성 코드에서 접근 불가 |
| `SSALH008` | Error | `[StableHashMember(id)]`의 id가 1 미만 |
| `SSALH009` | Error | `[StableHashContract]` name이 null/공백이거나 Version이 1 미만 |
| `SSALH010` | Warning | 계약이 `[StableHashMember]` 멤버를 0개 선언 — 모든 인스턴스가 같은 값(헤더뿐)으로 해시됨 |
| `SSALH011` | Warning | 컴파일 단위 내 두 개 이상의 계약 타입이 같은 `[StableHashContract]` 이름을 선언 |
| `SSALH012` | Warning | `[StableHashMember]`가 `[StableHashContract]`가 없는 타입의 멤버에 붙음 — 고아 특성, 아무것도 생성되지 않음 |
| `SSALH013` | Error | `[StableHashContract]`가 제네릭 타입이나 제네릭 타입 안에 중첩된 타입에 붙음 (v1은 오픈 계약 타입 미지원) |

위 error는 모두 해당 계약 타입 전체의 생성을 막습니다 — 부분 생성은 없습니다. warning은 생성을 막지 않습니다.

## 해시 의미론

`StableHash64`는 64bit 지문이며, 고정폭 해시라면 어떤 것이든 대칭이 아니라 비대칭 보장을 제공합니다.

- **두 해시가 다르면, 원본 값은 확실히 다릅니다 (100%).** `encode`/해시가 결정적 함수라는 사실에서 바로 나옵니다: 입력이 같으면 출력이 다를 수 없으니까요.
- **두 해시가 같으면, 원본 값은 *거의 확실히* 같습니다 — 하지만 보장되지는 않습니다.** 64bit 출력 공간에서, 생일 한계(birthday bound)는 충돌이 유의미해지는 지점을 약 2^32(~43억)개의 서로 다른 해시된 값 근처로 잡습니다. 소수의 값 — 두 시뮬레이션 상태, 스냅샷과 그 직전 값, 캐시 키 — 을 비교하는 경우라면 우연한 충돌 확률은 천문학적으로 작습니다. 하지만 0은 아닙니다.

**따라서 `StableHash64`는 충돌이 치명적인 결과를 낳는 곳에서 동일성을 최종 판정하는 용도로는 적합하지 않습니다** — 예를 들어 잘못된 일치로 데이터를 조용히 버리는 중복 제거 같은 경우가 그렇습니다. 이 라이브러리는 사실상 절대 일어나지 않는 사건이 오탐을 일으켜도 감수할 수 있거나, 별도로 검증되는 상황 — 디싱크 탐지, 변경 탐지, 캐시/ETag 재료, 결정적 버케팅 — 에서 저렴하고 빠른 비교용으로 쓰기에 적합한 도구입니다.

## 보안

**`StableHash64`는 암호학적 해시가 아닙니다.** 의도적으로 같은 해시가 나오는 두 개의 다른 입력을 만들어내려는 공격자에 대한 충돌 내성 보장이 없고, 키잉도 없으며, 변조 방지도 없습니다. 악의적인 변조자에 대한 무결성 검사, 메시지 인증, 비밀번호 저장, 디지털 서명, 그 외 어떤 보안 관련 용도로도 사용하지 마세요 — 그런 용도에는 `System.Security.Cryptography.SHA256`이나 유사한 것을 사용하세요.

## 성능

SsalKit.StableHashing은 단 하나의 성능 계약을 중심으로 설계됐습니다: **`ComputeStableHash()` 호출당 할당 0바이트.** `StableHashWriter`는 모든 값을 (작은 인라인 스테이징 버퍼를 거쳐 배치로) 해셔의 상태로 직접 스트리밍하는 `ref struct`입니다 — 중간 `byte[]` 직렬화 버퍼가 어느 경로에도 없으며, writer의 스택 버퍼보다 큰 입력에 대한 문자열 인코딩 폴백(할당 대신 `ArrayPool<byte>`에서 대여)도 마찬가지입니다.

BenchmarkDotNet v0.15.8, .NET 10.0.10, AMD Ryzen 9 3950X, Windows 11 (SsalKit.StableHashing 0.0.4)로 측정했습니다. 수치는 하드웨어에 따라 달라지며, [벤치마크 프로젝트](https://github.com/ssalkit/ssalkit/tree/main/benchmarks/SsalKit.StableHashing.Benchmarks)로 재현할 수 있습니다. 나이브 베이스라인을 제외한 모든 행에서 Allocated는 0 B입니다.

| 시나리오 | 소요 시간 | 할당 |
|---|---:|---:|
| 소형 계약 (스칼라 멤버 4개) | 112.9 ns | 0 B |
| 중형 계약 (문자열 + 중첩 계약 포함 12멤버) | 321.2 ns | 0 B |
| 컬렉션 멤버, 원소 10 / 100 / 1000개 | 142 ns / 787 ns / 7.38 μs | 0 B |
| 문자열 멤버, ASCII / 한글 / 장문(pool 폴백) | 100 ns / 134 ns / 251 ns | 0 B |
| 나이브 베이스라인: 수동 직렬화 후 해시 | 242 ns | 632 B |
| 생성된 `ComputeStableHash()`, 위 베이스라인과 동일한 데이터 | 370 ns | 0 B |

마지막 두 행을 정직하게 함께 읽어야 합니다: 나이브 방식 — 값을 수동으로 `byte[]`로 직렬화한 뒤 그 버퍼를 해시하는 방식 — 이 측정에서 순수 시간으로는(242 ns vs. 370 ns) 생성된 스트리밍 코드보다 *더 빠릅니다*. 대신 호출마다 632바이트를 할당하는데, 생성된 경로는 아무것도 할당하지 않습니다. 이건 실수가 아니라 의도된 트레이드오프입니다: 틱 루프, 저장 경로, 초당 수천 번 호출되는 그 외 핫패스에서는 호출당 GC 압력 0이 ~130ns의 시간 차이보다 더 값집니다 — 나이브 버전의 호출당 632B는 규모가 커지면 실제 컬렉션 정지로 이어지지만, 스트리밍 설계의 비용은 호출 빈도와 무관하게 일정합니다. `ComputeStableHash()`를 드물게(예: HTTP 요청당 한 번) 호출한다면 손수 짠 직렬화의 속도 우위가 덜 중요할 수도 있지만, 그렇더라도 생성된 경로를 쓰면 그 할당 비용을 아예 신경 쓸 필요가 없어집니다.

## 라이센스

MIT — 자세한 내용은 [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE)를 참고하세요.

---

**AI 고지:** 이 프로젝트는 AI(Claude)를 활용하여 제작되었습니다.
