[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ko.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Determinism/README.md) | **한국어** | [日本語](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Determinism/README.ja.md)

# SsalKit.Determinism

비결정적 API에 대한 opt-in 컴파일 타임 진단 라이브러리입니다. 타입이나 멤버에 `[Deterministic]`을 붙이면, 패키지에 동봉된 분석기가 그 안에서 **직접** 호출된 ambient 시계, 프로세스 난수, `Guid.NewGuid()`, 랜덤화된 해시, 환경 식별자, 스케줄링 API를 모두 보고합니다 — 그리고 모든 메시지가 구체적인 대체 수단을 지목합니다. 의존성이 없습니다.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Determinism.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Determinism)

## 왜 SsalKit.Determinism인가

어떤 코드는 같은 입력에 대해, 매번, 모든 머신에서 같은 출력을 내야만 합니다. 락스텝 시뮬레이션은 두 클라이언트의 계산이 어긋나는 순간 디싱크됩니다. 리플레이는 그것이 기록된 이유였던 버그를 더 이상 재현하지 못하게 됩니다. 히스토리로부터 재실행되는 워크플로는 두 번째 실행에서 다른 분기를 탑니다. `HashCode.Combine`으로 계산한 캐시 키는 재시작 후 다른 버킷을 가리킵니다. 이 모든 경우에서 결함은 대개 무해해 보이는 한 줄 — `DateTime.UtcNow`, `Random.Shared.Next()`, `Guid.NewGuid()` — 이 애초에 있어서는 안 될 코드 안에 들어가 있는 것이고, 장애는 그 줄에서 한참 떨어진 곳에서, 몇 시간 뒤에, 아무도 재현하지 못하는 divergence로 드러납니다.

컴파일러는 그 줄에 아무 불만이 없습니다. BCL도 그럴 수 없습니다. 똑같은 `DateTime.UtcNow`가 한 파일 옆의 로그 구문에서는 완벽하게 올바르니까요. 잘못된 것은 API가 아니라 **그 스코프 안의** API이고, 스코프야말로 기존 도구들이 표현하지 못하는 부분입니다.

- **`Microsoft.CodeAnalysis.BannedApiAnalyzers`**는 프로젝트 전체에 걸쳐 API 목록을 금지합니다. 실제 프로젝트는 그런 모양이 아닙니다. 결정적 시뮬레이션 코어와 그 로깅, UI, 컴포지션 루트는 보통 한 어셈블리 안에 함께 살고, 프로젝트 전역 금지는 프로젝트를 쪼개거나 정당하게 시계를 읽는 코드에 억제문을 도배하도록 강요합니다. 게다가 "이건 금지됨"이라고만 말할 뿐, 그럼 무엇을 쓰라는 안내는 하지 않습니다.
- **스코프를 가진 결정성 분석기들** — Durable Task, Libplanet — 은 각자의 프레임워크와 각자의 "결정적 영역" 정의에 묶여 있습니다. 직접 작성한 도메인 서비스나 가격 계산, 게임 시뮬레이션에는 쓸 수 없습니다.

SsalKit.Determinism은 두 가지 성질로 이 공백을 채웁니다. 그리고 이 둘이 이 패키지의 전부라는 점을 분명히 해 둘 필요가 있습니다.

- **스코프가 opt-in이고 어휘적(lexical)입니다.** `[Deterministic]`이 붙은 타입이나 멤버 밖에서는 아무것도 보고되지 않습니다. 재현 가능해야 하는 코어에만 표시하면 되고, 그 주변 코드는 억제문도 프로젝트 분리도 없이 이전과 똑같이 분석됩니다. `[AllowNonDeterminism]`은 그 안에서 다시 예외를 파내며, 양방향으로 중첩됩니다.
- **모든 메시지가 구체적인 대체 수단을 지목합니다.** "금지됨"이 아니라 *대신 이걸 쓰라*입니다. `TimeProvider`를 주입하거나 `DateTimeOffset asOf` 인자를 받으라, 명시적 시드의 `DeterministicRandom`을 쓰라, 식별자를 데이터에서 파생시켜라, `HashCode.Combine` 대신 `ComputeStableHash()`를 쓰라. 이 대체 수단들은 SsalKit 패밀리의 나머지이지만, **이 패키지는 그것들을 포함해 아무것에도 의존하지 않습니다**. 해당 타입들은 metadata name으로 조회되므로, 금지 목록의 SsalKit 항목은 그 패키지를 이미 참조하는 컴파일에서만 존재합니다.

런타임 어셈블리는 특성 2종이 전부이고 로직이 없습니다. 나머지는 전부 패키지에 동봉된 분석기가 컴파일 타임에 처리합니다.

## 이 분석기가 잡지 못하는 것 — 무엇보다 먼저 읽으세요

**분석은 의도적으로 얕습니다. 직접 호출만 보며, 그 외에는 아무것도 보지 않습니다. 진단이 0개라는 것은 결정성의 증명이 아닙니다.** 이것은 보장이 아니라 보조 도구이며, 앞으로도 그렇게 유지될 설계입니다 — "얕고 예측 가능함"은 언젠가 걷어낼 한계가 아니라 이 제품 자체입니다.

가장 중요한 귀결은 아래 표의 첫 행입니다. `[Deterministic]` 메서드가 표시되지 않은 헬퍼를 호출하고 그 헬퍼가 `DateTime.Now`를 읽으면, 진단은 하나도 나오지 않습니다.

| 탐지하지 못하는 것 | 이유 |
|---|---|
| **간접 호출** — 표시되지 않은 헬퍼를 경유한 금지 API 도달 | 분석기는 표시한 스코프 밖으로 나가지 않습니다. **헬퍼 타입에도 `[Deterministic]`을 붙이세요** — 이건 우회책이 아니라 의도된 사용 패턴입니다 (빠른 시작 참고). |
| `Dictionary`/`HashSet` 순회 순서 | 의도적 제외입니다. 비순서 컬렉션을 순서에 의존해 소비하는지 아닌지를 구분할 수 없어서, 이 영역의 규칙은 대부분 오탐이 됩니다. |
| 플랫폼 간 부동소수점 차이 (FMA 축약, x87 초과 정밀도, 벡터화) | 정적 분석의 범위 밖입니다 — 같은 IL이 다른 하드웨어에서 다른 결과를 냅니다. |
| 문화권 의존 포매팅·파싱 (`ToString()`, `Parse`, `ToUpper`) | BCL 자체의 `CA1304`/`CA1305`/`CA1310`이 이미, 그리고 더 잘 담당합니다. 이 패키지가 중복 구현하길 기대하지 말고 그 규칙들을 켜세요. |
| 리플렉션으로 디스패치되는 호출 | 대상 심볼이 컴파일 타임에 존재하지 않습니다. |
| `await` 재개 컨텍스트와 스레드 친화성 | 목록에 있는 스케줄링 진입점은 잡지만, await의 결과로 벌어지는 일은 잡지 않습니다. |
| 가변 정적 상태, `static` 캐시, 초기화 순서 | 특정 API가 아니라 프로그램의 구조에 깃든 비결정성입니다. |

같은 원칙에서 두 가지가 더 따라나오며, 둘 다 실수가 아니라 계약입니다.

- **스코프는 표시한 그 자리입니다.** `[Deterministic]`은 `Inherited = false`이고 분석기도 기반 타입을 걷지 않으므로, 표시된 기반 클래스가 파생 클래스를 덮지 않습니다 — 타입마다 표시하세요. 인터페이스는 아예 유효한 타깃이 아닙니다. 붙여도 구현체에 절대 닿지 않기 때문입니다.
- **모든 규칙은 Warning이며 앞으로도 그렇습니다.** 완전할 수 없는 검사 뒤에 빌드를 깨는 error를 두면, 갖고 있지 않은 완전성을 암시하게 됩니다. 심각도를 올리는 것은 프로젝트마다 의도적으로 내리는 결정입니다 — 아래 `.editorconfig` 절 참고.

## 설치

```bash
dotnet add package SsalKit.Determinism
```

패키지에는 특성과 그것을 읽는 분석기가 모두 들어 있습니다 — 별도로 설치할 analyzer 패키지가 없고, 자체 `PackageReference`도 없습니다.

## 빠른 시작

재현 가능해야 하는 코드와, 그것이 기대는 헬퍼에 표시합니다.

```csharp
using SsalKit.Determinism;
using SsalKit.Randomness;

[Deterministic]
public sealed class BattleSimulation
{
    private readonly DeterministicRandom _random;

    public BattleSimulation(ulong seed) => _random = new DeterministicRandom(seed);

    // 시간과 난수 모두 바깥에서 들어옵니다. ambient한 것은 아무것도 읽지 않습니다.
    public int Tick(DateTimeOffset asOf, int armor) => DamageRules.Apply(_random.Next(1, 7), armor);
}

// 분석은 볼 수 있는 것만 다루므로 헬퍼에도 표시합니다 --
// 그러지 않으면 다음 달에 여기 추가되는 DateTime.Now가 조용히 빠져나갑니다.
[Deterministic]
internal static class DamageRules
{
    public static int Apply(int roll, int armor) => Math.Max(0, roll - armor);
}
```

이제 비결정적인 것이 하나라도 들어오면 빌드가 알려줍니다.

```csharp
[Deterministic]
public sealed class BattleSimulation
{
    private readonly Random _random = new();                    // SSALD002
    private long _startedAt = DateTime.UtcNow.Ticks;            // SSALD001
    private readonly Guid _runId = Guid.NewGuid();              // SSALD003

    public int Bucket(string playerId) => HashCode.Combine(playerId) % 100;  // SSALD004
}
```

> warning SSALD001: 'DateTime.UtcNow' is non-deterministic: it reads the ambient clock, so the same code produces a different value on every run. Inject a TimeProvider, or take the instant as an argument (the 'DateTimeOffset asOf' parameter shape SsalKit.Timekeeping uses), so the caller decides what time it is

타입에 붙이면 스코프는 그 타입의 모든 멤버 **그리고 모든 중첩 타입**을 덮으며, 그 안에 쓰인 람다·로컬 함수·필드/프로퍼티 초기화식도 포함합니다. 메서드·생성자·프로퍼티에 붙이면 그 멤버만 덮습니다. `partial` 타입은 어느 한 파트에만 붙여도 됩니다.

## 금지 카탈로그 (v1)

카탈로그는 고정되어 있고 사용자가 확장할 수 없습니다. 프로젝트별로 금지 목록을 늘리는 건 `BannedApiAnalyzers`의 역할이고, 이 패키지가 대신 더하는 것은 스코프와 안내입니다. ID를 카테고리별로 나눈 것은 의도적입니다 — `.editorconfig`에서 ID가 곧 조절 손잡이이기 때문입니다.

| ID | 카테고리 | 금지 멤버 | 대안 |
|---|---|---|---|
| `SSALD001` | ambient 시간 | `DateTime.Now`/`.UtcNow`/`.Today`; `DateTimeOffset.Now`/`.UtcNow`; `TimeProvider.System`; `Stopwatch.StartNew()`/`.GetTimestamp()`/`new Stopwatch()`; `Environment.TickCount`/`.TickCount64` | 주입된 `TimeProvider`(테스트에서는 `FakeTimeProvider`), 또는 명시적 `DateTimeOffset asOf` 파라미터 — [SsalKit.Timekeeping](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Timekeeping/README.ko.md)이 일관되게 쓰는 형태입니다. |
| `SSALD002` | 난수 | `Random.Shared`; `new Random()` **그리고 `new Random(seed)`**; `RandomNumberGenerator.Create`/`Fill`/`GetBytes`/`GetNonZeroBytes`/`GetInt32`/`GetHexString`/`GetString`/`GetItems`/`Shuffle`; `Path.GetRandomFileName()`; 그리고 해당 패키지를 참조할 때만 `SsalKit.Randomness`의 `SharedRandomSource.Instance`, `CryptoRandomSource.Instance`, `DeterministicRandom.CreateRandomlySeeded()` | [SsalKit.Randomness](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.ko.md)의 `DeterministicRandom`(명시적 시드, 상태 내보내기 가능) 또는 주입된 `IRandomSource`. |
| `SSALD003` | 식별자 생성 | `Guid.NewGuid()`; `Guid.CreateVersion7()` (두 오버로드 모두) | 식별자를 데이터에서 파생시키세요: `ComputeStableHash()`, 또는 시드가 고정된 `DeterministicRandom`에서 뽑은 바이트. |
| `SSALD004` | 랜덤화된 해시 | `System.Object`·`System.ValueType`·`System.String`으로 **해석되는** `GetHashCode`; `System.HashCode`의 모든 멤버; `StringComparer.GetHashCode(string)` | [SsalKit.StableHashing](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.StableHashing/README.ko.md)의 `[StableHashContract]` + 생성된 `ComputeStableHash()`. |
| `SSALD005` | 환경 식별자 | `Environment.MachineName`/`.UserName`/`.UserDomainName`/`.ProcessId`/`.CurrentManagedThreadId`/`.ProcessorCount`/`.WorkingSet`/`.CommandLine`/`.CurrentDirectory`/`.GetEnvironmentVariable(…)`/`.GetEnvironmentVariables(…)`; `Process.GetCurrentProcess()`; `Thread.CurrentThread`; `Path.GetTempPath()`/`.GetTempFileName()` | 값을 명시적 구성으로 주입하세요. 결과가 실행된 호스트가 아니라 입력에 의존하게 됩니다. |
| `SSALD006` | 스케줄링·병렬성 | `Task.Run`/`.Delay`/`.WhenAny`/`.Yield`; `TaskFactory.StartNew`(`TaskFactory<T>` 포함); `Thread.Sleep`; `ThreadPool.QueueUserWorkItem`; `Parallel.For`/`.ForEach`/`.Invoke`/`.ForAsync`/`.ForEachAsync`; `ParallelEnumerable.AsParallel`; `new System.Threading.Timer(…)`; `new System.Timers.Timer(…)` | 그대로 갈아 끼울 대안이 없는 유일한 카테고리입니다. 비결정성의 원인이 특정 호출이 아니라 동시성 자체이기 때문입니다. 정말로 순서 무관한 병렬 작업이라면 스코프 밖에 두고 결과만 넘겨받고, 그렇지 않다면 순차 실행으로 바꿔야 합니다. |
| `SSALD007` | 고아 예외 표시 | 자신에게도, 자신을 포함한 어떤 것에도 `[Deterministic]`이 없는데 `[AllowNonDeterminism]`이 붙음 | 특성을 지우거나, 감싸는 타입/멤버에 `[Deterministic]`을 붙이세요. 조용히 아무 일도 하지 않는 표시는 표시가 없는 것보다 나쁩니다. |

`new Random(seed)`가 왜 저 목록에 있는지, 그리고 무엇이 의도적으로 **빠져 있는지**에 대한 보충입니다.

- **`new Random(seed)`는 시드를 줬어도 금지됩니다.** `System.Random`의 알고리즘은 문서화된 계약의 일부가 아니고 이미 런타임 버전 사이에서 바뀐 적이 있습니다. 따라서 고정 시드는 프로세스나 버전을 넘어 시퀀스를 재현해 주지 않습니다 — 한 프로세스 안에서만 재현됩니다. `DeterministicRandom`은 알고리즘(`xoshiro256**`)을 버전 계약으로 고정하며 상태를 내보내고 복원할 수 있습니다.
- **주입된 `TimeProvider`는 금지 대상이 아닙니다** — 그게 권장되는 수정 방법입니다. 금지되는 것은 ambient 싱글턴인 `TimeProvider.System`뿐이며, 넘겨받은 인스턴스에 `timeProvider.GetUtcNow()`를 호출하는 것은 침묵합니다.
- **이미 존재하는 `Random`의 인스턴스 메서드는 금지되지 않습니다.** 금지되는 것은 시퀀스가 *어디서 오는지*, 즉 생성 지점이지 매 추출이 아닙니다.
- **사용자가 작성한 override로 해석되는 `GetHashCode` 호출은 보고되지 않습니다.** 프레임워크 자체의 랜덤화된 구현만 목록에 있습니다. 여러분의 override는 그것이 속한 스코프에서 자기 자신의 자격으로 분석됩니다.
- **`nameof(DateTime.UtcNow)`는 보고되지 않습니다.** 멤버를 읽는 것이 아니라 이름을 부르는 것이기 때문입니다.
- **파일·네트워크 I/O, `Console`, 일반적인 `await`는 v1 카탈로그에 없습니다** — 이는 의도적인 범위 제한이지 괜찮다는 뜻이 아닙니다.

카탈로그는 컴파일마다 한 번 metadata name으로 해석되며, 컴파일이 참조하지 않는 타입은 조용히 건너뜁니다. 위 표의 `SsalKit.Randomness` 항목들이 이 패키지의 의존성 0 계약과 공존할 수 있는 이유가 바로 이것입니다 — 그 패키지를 이미 참조하는 곳에서만 금지 목록에 합류합니다. 그리고 자기 생태계의 비결정 진입점에도 예외는 없습니다. dogfooding은 양쪽으로 작용합니다.

## 의도적으로 쓴 코드를 예외 처리하기

결정적 코어 안에도 정말로 시계가 필요한 코드가 있습니다 — 로그 한 줄, 진단 카운터, 진행 상황 메시지. 그것을 표현하는 방법이 두 가지 있고, 각각 다른 크기의 문제를 위한 것입니다.

**1. 멤버나 중첩 타입에 `[AllowNonDeterminism]` (권장).** 호출 한 곳이 아니라 멤버 전체를 지목하고, 리뷰어가 이미 보고 있는 선언부에 드러나며, `Justification`이 그 이유를 코드 리뷰까지 실어 나릅니다. 런타임에 이 값을 읽는 것은 없고 어떤 진단도 이를 요구하지 않지만, 이유 없는 예외 처리는 다음 독자에게 아무것도 알려주지 못합니다.

```csharp
[Deterministic]
public sealed class ReplayRunner
{
    public void Run(DateTimeOffset asOf) { /* 분석 대상 */ }

    [AllowNonDeterminism(Justification = "wall-clock logging only; never feeds replayed state")]
    private static void LogProgress(int tick) =>
        Console.WriteLine($"{DateTime.UtcNow:O} tick {tick}");
}
```

스코프 판정은 어휘적이고 가장 가까운 표시가 이기므로 예외는 양방향으로 중첩됩니다. `[Deterministic]` 타입 안의 `[AllowNonDeterminism]` 타입은 제외되고, *그 안의* `[Deterministic]` 멤버는 다시 분석됩니다. 모든 `[Deterministic]` 스코프 밖에서는 이 특성이 아무것도 억제하지 않으며, 그것이 `SSALD007`이 보고하는 상황입니다.

**2. 호출 한 곳만 억제하려면 `#pragma warning disable` 또는 `.editorconfig`.**

```csharp
#pragma warning disable SSALD001 // 일회성: 시뮬레이션 상태가 아니라 trace id 시딩용
var traceStartedAt = DateTime.UtcNow;
#pragma warning restore SSALD001
```

카테고리를 잠재우려고 `[Deterministic]` 표시를 지우는 것만은 하지 마세요. 그 스코프의 미래 위반까지 전부 함께 잠재우면서, 그것이 의도적이었다는 기록조차 남기지 않습니다.

## `.editorconfig`에서 심각도 조정하기

모든 규칙은 Warning으로 배포됩니다. 카탈로그가 카테고리별 7개 ID로 나뉘어 있으므로, 한 카테고리를 조이거나 푸는 것은 한 줄입니다.

```ini
# .editorconfig

# 결정적 코어는 빌드 게이트로 만들고...
dotnet_diagnostic.SSALD001.severity = error
dotnet_diagnostic.SSALD002.severity = error
dotnet_diagnostic.SSALD003.severity = error
dotnet_diagnostic.SSALD004.severity = error

# ...이 코드베이스에서 병렬성은 권고 수준으로 남깁니다.
dotnet_diagnostic.SSALD006.severity = suggestion
```

7개 모두 하나의 카테고리를 공유하므로 한꺼번에 옮길 수도 있습니다.

```ini
dotnet_analyzer_diagnostic.category-SsalKit.Determinism.severity = error
```

`.editorconfig`는 경로별로 범위를 지정할 수 있으므로(`[src/Simulation/**.cs]`), 결정적 코어가 한 프로젝트에 모여 있는 솔루션과 잘 맞습니다.

v1에는 code fix provider가 없습니다. 여기서의 수정은 기계적인 편집이 아니라 리팩터링 — `TimeProvider` 파라미터 도입, 생성자를 통한 시드 전달 — 이기 때문입니다.

## 어디에서 값을 하는가

아래 첫 번째·두 번째·다섯 번째·여섯 번째 항목은 각각 [samples/SsalKit.Determinism.Sample](https://github.com/ssalkit/ssalkit/tree/main/samples/SsalKit.Determinism.Sample)의 실행 가능한 섹션에 대응하며, 섹션 이름이 이 목록과 일치합니다.

- **락스텝 시뮬레이션** (`[Simulation]`, `[Desync]`). 같은 입력으로 같은 세계를 계산하는 클라이언트들은 비트 단위로 일치해야 하고, 한 머신의 벽시계 읽기 한 번이 곧 디싱크입니다. 시뮬레이션 코어에 `[Deterministic]`을 붙이면 이 부류의 버그가 런타임의 미스터리에서 빌드 경고로 바뀌고, `TreatWarningsAsErrors`와 함께라면 아예 커밋될 수 없는 것이 됩니다.
- **리플레이와 이벤트 소싱 검증** (`[Replay]`). 기록된 입력 시퀀스는 원본 실행을 정확히 재현해야 하며, 그러지 못하면 버그 리포트로서도 감사 추적으로서도 가치가 없습니다. 재생 경로 전체가 표시해야 할 스코프입니다.
- **워크플로 재실행.** durable execution 엔진(Durable Functions, Temporal 등)은 워크플로 히스토리를 같은 코드로 재생하며 모든 결정이 똑같이 나오기를 요구합니다. 그런 프레임워크는 자기 특성에 대한 자기 분석기를 제공하지만, 프레임워크가 표시하는 영역 밖에 직접 만든 재실행 로직 — 사가 스텝, 재시도 플래너 — 이 있다면 `[Deterministic]`이 같은 안전장치를 제공합니다.
- **합의와 분산 동의.** 독립적인 노드들이 동일한 입력에서 동일한 결론에 도달해야 합니다. 결정 경로 안의 `Guid.NewGuid()`나 `Environment.MachineName`은 합의를 불가능하게 만들며, 그 실패는 명백한 버그가 아니라 드물고 재현되지 않는 분기로 나타납니다.
- **캐시 키·샤딩·A/B 버케팅** (`[Fingerprint]`). `HashCode.Combine`과 `string.GetHashCode()`는 프로세스마다 랜덤화되므로, 오늘 계산한 키나 버킷은 다음 재시작 이후에는 다른 키·버킷입니다. `SSALD004`가 정확히 이걸 잡고, 안정적인 대체 경로를 지목합니다.
- **테스트 가능한 도메인 코어** (`[TestableCore]`, `[OptOut]`). 가장 일상적인 사용처입니다. 시간과 난수를 전부 밖에서 받는 서비스는 테스트에 목 시계도, 재시도도, `Thread.Sleep`도 필요 없는 서비스입니다. `[Deterministic]`은 "시간은 주입하기로 했다"는 코드 리뷰 관례를 컴파일러가 강제하는 규칙으로 바꿔 줍니다.

샘플이 의도적으로 출력하지 *않는* 것이 하나 있습니다. 경고입니다. 이 샘플은 `TreatWarningsAsErrors` 아래에서 컴파일되며, 컴파일된다는 사실 자체가 그 안 어디에서도 `SSALD` 진단이 발생하지 않는다는 증명입니다. 그 반대 시연 — 모든 카테고리에서 하나씩 뽑은 위반과 각각의 대안 주석 — 은 `Violations.cs`에 있고 `#if`로 기본 빌드에서 제외되어 있습니다. 켜는 방법은 `[Showcase]` 그룹이 설명합니다.

## 패밀리의 나머지

진단이 지목하는 대체 수단들은 별도의 선택적 패키지입니다. 이 패키지는 그중 무엇에도 의존하지 않으며, 하나도 설치하지 않아도 동작합니다.

- **[SsalKit.Randomness](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.ko.md)** — `DeterministicRandom`(`xoshiro256**`, 명시적 시드, 상태 내보내기·fork 가능)과 `IRandomSource` 추상화. `SSALD002`용.
- **[SsalKit.Timekeeping](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Timekeeping/README.ko.md)** — 달력 리셋 경계, 쿨다운과 충전 풀, 논리적 틱 이벤트 스케줄. 전부 스스로 시계를 읽지 않고 넘겨받은 시각으로 계산합니다. `SSALD001`용.
- **[SsalKit.StableHashing](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.StableHashing/README.ko.md)** — `[StableHashContract]`와 생성된 `ComputeStableHash()`로 프로세스·머신·.NET 버전을 넘어 살아남는 체크섬. `SSALD003`·`SSALD004`용.

## 라이센스

MIT — 자세한 내용은 [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE)를 참고하세요.

---

**AI 고지:** 이 프로젝트는 AI(Claude)를 활용하여 제작되었습니다.
</content>
