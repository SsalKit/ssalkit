[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ko.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Guard/README.md) | **한국어** | [日本語](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Guard/README.ja.md)

# SsalKit.Guard

에러 코드 기반 도메인 예외 라이브러리입니다. 부작용 없는 `ErrorCodedException` 베이스, 호출자가 쓴 식 텍스트를 그대로 캡처하는 정적 가드 절, 그리고 파생 타입이 항상 기반 타입보다 먼저 매칭되도록 정렬된 예외→코드 매핑 테이블을 컴파일 타임에 생성합니다. 의존성이 없습니다.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Guard.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Guard)

## 왜 SsalKit.Guard인가

바깥 세계에 응답하는 서비스는 결국 같은 계층을 갖게 됩니다. 도메인이 던진 예외를 받아 호출자가 이해할 수 있는 코드로 바꾸는 경계 말입니다. 이 계층을 손으로 쓰다 보면 대개 세 가지 문제가 따라옵니다.

- **생성될 때 일을 하는 예외.** 예외 생성자 안에서 `Activity.Current`에 태그를 달거나 로그를 쓰거나 카운터를 올리는 것은 딱 한 번만 편리합니다. 예외를 생성하는 순간은 예외를 처리하는 순간이 아니고, 잡힌 예외는 한참 뒤에 다시 던져지거나 감싸질 수 있어 태그가 엉뚱한 시점을 가리키게 되며, 예외를 생성하기만 하는 테스트가 원치도 않은 앰비언트 텔레메트리를 오염시킵니다.
- **IntelliSense를 점령하는 가드 헬퍼와 손으로 적는 실패 맥락.** `value.ThrowIfNull(...)` 같은 `this T` 확장은 검증과 아무 상관이 없는 호출부에서도 모든 참조 타입의 IntelliSense에 나타납니다. 게다가 검사를 실패시킨 값들은 손으로 관리하는 `(string Name, object Value)[]` 튜플에서 메시지로 덧붙는데, 이 목록은 자신이 설명해야 할 조건식과 금세 어긋납니다.
- **정확성이 주석에 의존하는 매핑 switch.** 예외→코드 `switch`는 파생 타입을 기반 타입보다 먼저 써야 하고, 모두가 그 사실을 계속 기억하는 동안에만 올바릅니다. 실제로는 "이 타입은 아래 타입의 하위 타입이므로 반드시 먼저 매칭해야 함" 같은 주석으로 방어하게 되죠. 새 예외를 추가하고 등록을 잊어도 컴파일러는 아무 말도 해주지 않습니다.

SsalKit.Guard는 이 세 가지를 각각 분해합니다.

- **`ErrorCodedException`은 순수 데이터입니다.** 이 라이브러리의 어떤 생성자도 `Activity`에 태그를 달거나 로그를 쓰거나 메트릭을 내보내지 않습니다. 관측은 주변 요청 컨텍스트를 아는 유일한 지점인 경계의 몫이며, 그 모습은 이 문서에서 보여드립니다.
- **`Guard.`는 정적 진입점이고, 실패 맥락은 컴파일러가 캡처합니다.** 모든 절이 마지막 인자로 `[CallerArgumentExpression]` 매개변수를 받으므로, 검사한 식의 소스 텍스트가 공짜로 메시지에 들어갑니다. `Guard.That (order.Status == OrderStatus.Open) failed.` 처럼요.
- **매핑 테이블은 생성됩니다.** `static partial class`에 `[ErrorCodes<TCode>]`를 붙이면 예외→코드 조회가 대신 작성되며, 등록된 타입들의 상속 깊이로부터 파생 타입 우선 순서가 만들어집니다. 유지해야 할 순서가 없고, 잘못된 사용은 조용히 틀린 코드를 반환하는 대신 컴파일 타임 진단으로 드러납니다.
- **의존성 0.** BCL만 사용합니다.

## 설치

```bash
dotnet add package SsalKit.Guard
```

패키지에는 런타임 타입(`Guard`, `ErrorCodedException`, 특성 3종)과 소스 생성기가 함께 들어 있습니다. 따로 설치할 analyzer 패키지가 없고, 이 패키지 자신의 `PackageReference`도 없습니다.

**사전 요구 사항:** .NET 10+. 코드는 제네릭 특성(`[ErrorCode<GameStatusCode>(...)]`)으로 선언하므로 C# 11 이상이 필요합니다.

## 가드 절

다섯 가지 절이 있으며, 각각은 인자 검사가 아니라 도메인 불변식을 표현합니다. 모든 절은 마지막에 컴파일러가 채워 주는 `[CallerArgumentExpression]` 매개변수를 받으므로, 검사한 식의 소스 텍스트가 손으로 적지 않아도 메시지에 나타납니다.

```csharp
using SsalKit.Guard;

Guard.That(order.Status == OrderStatus.Open);
// GuardViolationException: Guard.That (order.Status == OrderStatus.Open) failed.

var owner = Guard.NotNull(world.FindPlayer(id));
// GuardViolationException: Guard.NotNull (world.FindPlayer(id)) failed: value was null.

string name = Guard.NotNullOrWhiteSpace(player.Name);
// GuardViolationException: Guard.NotNullOrWhiteSpace (player.Name) failed: value was null, empty, or white-space.

int level = Guard.InRange(player.Level, 10, 60);
// GuardViolationException: Guard.InRange (player.Level) failed: value 3 was outside the inclusive range [10, 60].
```

| 절 | 실패 조건 | 반환 | 실패 메시지 |
|---|---|---|---|
| `Guard.That(condition)` | `condition`이 `false` | `void` | `Guard.That ({expression}) failed.` |
| `Guard.NotNull(value)` | `value`가 `null`(참조 타입 및 `Nullable<T>`) | non-nullable 값 | `Guard.NotNull ({expression}) failed: value was null.` |
| `Guard.NotNullOrEmpty(value)` | 문자열이 `null`이거나 빈 문자열 | `string` | `Guard.NotNullOrEmpty ({expression}) failed: value was null or empty.` |
| `Guard.NotNullOrWhiteSpace(value)` | 문자열이 `null`, 빈 문자열, 또는 전부 공백 | `string` | `Guard.NotNullOrWhiteSpace ({expression}) failed: value was null, empty, or white-space.` |
| `Guard.InRange(value, min, max)` | `value`가 닫힌 구간 `[min, max]` 밖 | `T` | `Guard.InRange ({expression}) failed: value {value} was outside the inclusive range [{min}, {max}].` |

메시지 계약은 `That`의 경우 `Guard.{Clause} ({expression}) failed.`, 나머지는 `Guard.{Clause} ({expression}) failed: {detail}` 입니다. `InRange`는 값과 양쪽 경계를 invariant culture로 렌더링하므로 어디서 읽어도 같은 문자열이 나옵니다. 식 텍스트를 얻을 수 없을 때 — 호출자가 명시적으로 `null`이나 빈 문자열을 넘긴 경우, 또는 `[CallerArgumentExpression]`을 지원하지 않는 언어에서 호출한 경우뿐입니다 — 는 `<expression unavailable>` 자리표시자가 대신 들어갑니다.

`That`을 제외한 모든 절은 검사한 값을 반환하므로, 가드가 옆에 서 있는 별도의 문장이 아니라 보호 대상 식의 일부처럼 읽힙니다.

```csharp
string teamName = Guard.NotNull(player.Team).Name;
```

### 직접 정의한 예외 던지기

`That`과 참조 타입 `NotNull`은 예외 팩터리를 받는 오버로드도 제공하며, 팩터리는 검사가 실패했을 때만 호출됩니다.

```csharp
Guard.That(balance >= amount, () => new InsufficientFundsException(balance, amount));

Team team = Guard.NotNull(player.Team, () => GameErrors.InvalidTeam($"player {player.Id} is on no team"));
```

성공 경로에서는 할당이 전혀 없습니다. 팩터리는 호출되지 않고, 메시지도 검사가 실패한 뒤에야 조립됩니다. 팩터리가 `null`을 반환하면, 실패한 가드에 대해 아무것도 알려주지 않는 맨 `NullReferenceException` 대신 어느 절이었는지를 담은 `GuardViolationException`이 던져집니다.

### 인자 검증용이 아닙니다

매개변수 계약은 BCL이 이미 다룹니다. `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentOutOfRangeException.ThrowIf*` 계열이 있고, 이들은 호출자와 분석기가 인자 검사에서 기대하는 예외 타입을 던집니다. `Guard`는 이를 의도적으로 중복 제공하지 않습니다.

"잘못된 인자를 넘겼다"는 BCL로, "이 애그리게이트는 더 이상 이 연산을 허용하는 상태가 아니다"는 `Guard`로 처리하세요. 후자의 실패는 `ArgumentException`이 아니라 에러 코드로 매핑되는 도메인 오류입니다.

## 에러 코드

### 선언하기

```csharp
using SsalKit.Guard;

public enum GameStatusCode
{
    Unspecified = 0,
    NotFound = 1000,
    UserNotFound = 1001,
    InvalidTeam = 1002,
    ServerBusy = 2001,
    GuardViolation = 9001,
}

// 코드는 예외 타입에, 한 번만 선언합니다.
[ErrorCode<GameStatusCode>(GameStatusCode.NotFound)]
public class NotFoundException : ErrorCodedException
{
    public NotFoundException(string? message = null) : base(message) { }
}

// 위 타입을 상속하면서 다른 코드를 갖습니다 — 아래의 정렬 보장을 참고하세요.
[ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
public sealed class UserNotFoundException : NotFoundException
{
    public UserNotFoundException(string? message = null) : base(message) { }
}

[ErrorCode<GameStatusCode>(GameStatusCode.InvalidTeam)]
public sealed class InvalidTeamException : ErrorCodedException
{
    public InvalidTeamException(string? message = null, Exception? innerException = null)
        : base(message, innerException) { }
}

// 매핑 컨테이너. 이 네 줄이 선언의 전부입니다.
[ErrorCodes<GameStatusCode>]
[ExternalErrorCode<GameStatusCode>(typeof(TimeoutException), GameStatusCode.ServerBusy)]
[ExternalErrorCode<GameStatusCode>(typeof(GuardViolationException), GameStatusCode.GuardViolation)]
public static partial class GameErrors;
```

`[ExternalErrorCode]`는 내가 소유하지 않은 예외에 코드를 부여하는 자리입니다. BCL 타입, 캐시 클라이언트의 타임아웃, 클러스터 라이브러리의 실패, 토큰 검증 오류 같은 것들이죠. 실제 경계에서는 이런 예외가 테이블의 절반을 차지하는 일이 흔한데, 이들에는 `[ErrorCode]`를 붙일 수 없으므로 컨테이너 쪽에서 선언합니다.

### 무엇이 생성되는가

`GameErrors`의 나머지 절반에 다음이 생성됩니다.

```csharp
// 조회. 파생 타입이 먼저 검사됩니다.
if (GameErrors.TryMap(exception, out GameStatusCode code)) { /* ... */ }

// 순서는 동일하고, "매칭된 등록이 없음"을 어떻게 알리는지만 다릅니다.
GameStatusCode mapped = GameErrors.MapOrDefault(exception, GameStatusCode.Unspecified);

// [ErrorCode] 예외 하나당 팩터리 하나와 [DoesNotReturn] throw 헬퍼 하나.
// 각각 그 예외 자신의 생성자를 미러링합니다.
throw GameErrors.UserNotFound("player p-42 no longer exists");
GameErrors.ThrowInvalidTeam("a team needs at least two members", new TimeoutException("roster lookup"));
```

헬퍼 이름은 `Exception` 접미사를 뗀 형태입니다(`UserNotFoundException` → `UserNotFound`, `ThrowUserNotFound`). 그렇게 했을 때 등록된 두 타입의 이름이 충돌하면 전체 타입명을 쓰고, 그래도 충돌하면 FQN을 평탄화한 이름으로 물러납니다.

생성된 조회는 평범한 `is` 체인이라, 보장되는 순서가 코드에 그대로 보입니다.

```csharp
public static bool TryMap(global::System.Exception exception, out global::Game.GameStatusCode code)
{
    if (exception is global::Game.UserNotFoundException)
    {
        code = global::Game.GameStatusCode.UserNotFound;
        return true;
    }

    if (exception is global::Game.NotFoundException)
    {
        code = global::Game.GameStatusCode.NotFound;
        return true;
    }

    code = default(global::Game.GameStatusCode);
    return false;
}
```

### 파생 타입이 항상 먼저, 자동으로

`UserNotFoundException`은 `NotFoundException`을 상속하면서 각자 다른 코드를 갖고 있으므로 기반 타입이 먼저 검사되어서는 안 되는데, 위 코드에서는 실제로 그렇지 않습니다. 이 순서는 등록된 타입들의 상속 깊이(깊은 것부터, 동률이면 FQN 순서로 출력이 결정적)로부터 생성됩니다. 따라서

- **유지해야 할 순서가 없습니다.** 기존 상속 계층 중간에 새 예외를 끼워 넣어도 다음 빌드에서 알아서 제자리를 찾습니다. 관리할 주석도, 리뷰 체크리스트도 필요 없습니다.
- **매칭은 런타임 타입 기준입니다.** 인스턴스를 기반 타입 변수에 담아도 — `catch` 절에 도달하는 방식이 바로 그것입니다 — 여전히 파생 타입의 코드가 나옵니다.
- **모호함은 해결하지 않고 거부합니다.** 한 컨테이너에 같은 예외 타입을 두 번 등록하면 오류(`SSALG003`)입니다. 후보 코드가 둘인데 선언 순서로 승자를 정하는 것은 조용한 우선순위 규칙이고, 이 라이브러리가 없애려는 것이 바로 그것입니다.
- **등록되지 않은 예외는 아무것도 매칭하지 않습니다.** `TryMap`은 `false`를 반환합니다(null 참조도 마찬가지). 덕분에 "매핑 없음"이 실제 코드들과 계속 구분됩니다.

### 팩토리는 예외의 생성자를 미러링합니다

v1은 세 가지 public 생성자 형태를 인식하며, 예외가 선언한 것 중 가장 넓은 것을 nullability까지 그대로 미러링합니다.

| 예외의 생성자 | 생성되는 팩토리 (throw 헬퍼는 앞에 `Throw`가 붙은 같은 형태) |
|---|---|
| `()` | `Empty()` |
| `(string? message = null)` | `MessageOnly(string? message = null)` |
| `(string message)` | `Required(string message)` — non-nullable이므로 매개변수가 필수로 유지됨 |
| `(string? message, Exception? innerException)` | `Full(string? message = null, Exception? innerException = null)` |

예외 하나가 얻는 것은 팩토리 하나와 throw 헬퍼 하나뿐입니다 — 생성자마다 오버로드가 하나씩 생기는 것이 아니라, 선언된 것 중 가장 넓은 형태 하나만 미러링됩니다. 더 좁은 형태는 그 하나의 기본값으로 도달할 수 있으니까요.

셋 중 아무것도 선언하지 않은 예외도 매핑 테이블에는 정상적으로 참여하고, 헬퍼만 생성되지 않습니다. 이때는 사용자가 이유를 추측하도록 두지 않고 `SSALG006`으로 알려 줍니다. 외부 등록 타입 역시 헬퍼를 얻지 않습니다 — 이 라이브러리는 자신이 소유하지 않은 타입의 생성자 계약을 보증할 수 없기 때문입니다.

### 컨테이너 여러 개, 코드 enum 여러 개

컨테이너는 자기 enum을 지정한 `[ErrorCode<TCode>]` 예외만 모으므로, 서로 무관한 도메인을 분리해 둘 수 있습니다.

```csharp
[ErrorCodes<GameStatusCode>]
public static partial class GameErrors;

[ErrorCodes<BillingStatusCode>]
public static partial class BillingErrors;
```

각자 자기 `TryMap`·`MapOrDefault`·헬퍼를 갖고, 서로 섞이지 않습니다.

### 접근성

생성된 부분은 컨테이너(및 컨테이너를 감싸는 모든 타입)를 원래 접근성 그대로 다시 선언하고, 각 생성 멤버의 접근성은 결과가 컴파일되도록 클램프됩니다. `TryMap`/`MapOrDefault`는 코드 enum이 public이 아니면 그에 맞춰 낮아지고, 각 팩토리·throw 헬퍼도 대상 예외 타입이 public이 아니면 그에 맞춰 낮아집니다. 따라서 `internal` 컨테이너 안의 `internal` enum이면 멤버도 `internal`로 생성되어, 손으로 고칠 접근성 불일치가 생기지 않습니다.

생성 파일에서 아예 이름을 쓸 수 없는 예외 — `private`, `protected`, `private protected`, `file`-local — 는 오류(`SSALG009`)입니다. 그대로 포함하면 사용자가 작성한 적 없는 코드를 가리키는, 컴파일되지 않는 생성 파일이 나오기 때문입니다.

## 경계에서

잡고, 매핑하고, 응답합니다. 전체 표면을 함수 하나가 담당합니다.

```csharp
public Response Handle(Func<Response> operation)
{
    try
    {
        return operation();
    }
    catch (Exception exception) when (GameErrors.TryMap(exception, out GameStatusCode code))
    {
        // 관측은 여기에 있습니다. 요청 컨텍스트가 존재하고, 처리하기로 하는 결정이
        // 이미 내려진 지점이니까요. 예외 자신은 여기까지 오는 동안 아무 일도 하지 않았습니다.
        Activity.Current?.SetTag("error.code", (int)code);
        logger.LogWarning(exception, "request failed with {ErrorCode}", code);

        return Response.Failure(ToTransportStatus(code), (int)code, exception.Message);
    }
}
```

짚어 둘 점이 셋 있습니다.

- **태깅과 로깅은 소비 측의 몫입니다.** 이는 이 라이브러리의 원형(예외 생성자가 직접 `Activity.Current`에 태그를 달던 코드)에서 의도적으로 바꾼 부분입니다. 여기서 처리하면, 잡혔다가 감싸져 다시 던져지는 예외도 실제로 처리되는 그 순간에 한 번만 기록되고, 예외를 생성하기만 하는 단위 테스트는 텔레메트리를 전혀 건드리지 않습니다.
- **`when` 필터의 `TryMap`은 매핑되지 않은 예외를 그냥 흘려보냅니다.** 해줄 말도 없는 핸들러가 삼켜 버리는 대신 계속 위로 전파되며, 경계에서는 보통 그쪽이 원하는 동작입니다. 폴백 코드로 충분한 상황이라면 `MapOrDefault(exception, GameStatusCode.Unspecified)`가 더 짧은 형태입니다.
- **매핑은 여러분의 enum에서 멈춥니다.** `GameStatusCode`를 HTTP 상태 코드나 gRPC 코드, 와이어 정수로 바꾸는 것은 전송 계층의 일입니다. 생성된 조회가 `TCode`를 반환하고 이 라이브러리에 전송 계층 냄새가 전혀 없는 이유가 바로 그것입니다.

## 판정 (Judgement)

여기까지는 전부 던지는 이야기였습니다. 도메인이 예외를 올리면 경계가 그것을 잡아 코드로 바꿉니다. 하지만 던질 수 없는 자리가 있습니다. 액터의 메시지 루프 안, 거절하는 것 자체가 일인 규칙, 첫 실패에서 멈추지 않고 항목마다 결과를 알려야 하는 일괄 처리 같은 곳이죠. **판정(judgement)** 은 같은 계약의 나머지 반쪽입니다. 예외가 실어 날랐을 바로 그 코드를, 같은 `TCode` enum에서 꺼내, 던지지 않고 돌려줍니다.

```csharp
// 던지기: 경계가 예외를 코드로 바꿉니다.
throw GameErrors.UserNotFound($"player {id} no longer exists");

// 판정하기: 규칙이 코드를 그대로 돌려주고, 무엇을 할지는 호출자가 정합니다.
return Judgement.Reject(GameStatusCode.UserNotFound, $"player {id} no longer exists");
```

**이것은 `Result<T>`가 아닙니다.** `Match`, `Map`, `Bind`, `Select`, `OrElse`도, LINQ도, `T`로부터의 암시적 변환도 없습니다. 판정은 `if` 하나로 읽고 나면 끝입니다. 레일웨이 지향 합성은 다른 라이브러리의 몫이고, 이쪽은 에러 코드 계약의 던지지 않는 반쪽이며 거기서 멈춥니다.

### 두 가지 형태

| 타입 | 알려 주는 것 | payload |
|---|---|---|
| `Judgement<TCode>` | 통과, 또는 코드 하나와 메시지 하나 | 없음 |
| `Judgement<T, TCode>` | 새 상태, 또는 코드 하나와 메시지 하나 | `T? Granted` — 거절일 때만 정확히 `null` |

`TCode`가 마지막에 오는 것은 `Func<T, TResult>` 관례를 따른 것이고, 제약도 특성 3종과 같은 `where TCode : struct, Enum`입니다. 실제로는 매핑 테이블이 반환하는 그 enum을 그대로 쓰게 됩니다. `T`가 참조 타입인 것은 제약이 아니라 강제 장치입니다 — 아래 **한계**를 참고하세요.

두 형태 모두, 호출부에 타입 인자가 하나도 나오지 않습니다.

```csharp
// payload 있는 쪽: 새 상태, 아니면 거절한 단 하나의 이유.
public static Judgement<Enlistment, GameStatusCode> Enlist(Roster roster, Player player)
{
    if (player.Level < roster.LevelFloor)
    {
        return Judgement.Reject(GameStatusCode.LevelTooLow, $"player {player.Id} is level {player.Level}");
    }

    if (roster.Enlisted >= roster.Capacity)
    {
        return Judgement.Reject(GameStatusCode.RosterFull, $"{roster.MatchId} is full");
    }

    return Judgement.Grant(new Enlistment(roster with { Enlisted = roster.Enlisted + 1 }, Slot: roster.Enlisted + 1));
}

// payload 없는 쪽 — 삼항 연산자의 양쪽 가지도 대상 타입으로 변환되므로, 규칙 하나가 식 하나에 들어갑니다.
public static Judgement<GameStatusCode> CanTrade(Player player) =>
    player.IsBanned
        ? Judgement.Reject(GameStatusCode.Banned, "trading is suspended for this account")
        : Judgement.Grant();
```

`Judgement.Grant`와 `Judgement.Reject`가 반환하는 것은 판정이 아닙니다. 작고 불투명한 **캐리어**를 반환하고, 암시 변환이 그것을 대상 타입이 요구하는 판정으로 바꿉니다. 거절에는 payload가 없으므로 그 캐리어는 *두 형태 모두*로 변환됩니다 — `Reject`가 `T`를 명시할 일이 없는 이유가 바로 이것이고, 실제로 통과 쪽보다 훨씬 자주 쓰이는 가지가 그쪽입니다. 대상 타입이 존재하는 자리(`return` 문, 타입을 적은 변수로의 대입, 삼항의 양쪽 가지)에 팩터리를 쓰면 타입 인자는 아예 등장하지 않습니다.

payload가 `null`이거나 메시지가 `null`이면 `ArgumentNullException`입니다. 빈 문자열은 합법적인 메시지입니다.

### 읽는 법

`TryGetRejection` 한 번이면 양쪽 흐름이 함께 좁혀집니다.

```csharp
Judgement<Enlistment, GameStatusCode> judgement = Enlist(roster, player);

if (judgement.TryGetRejection(out GameStatusCode code, out string message))
{
    // 여기서 `code`는 GameStatusCode? 가 아니라 GameStatusCode입니다 — 언제나 존재하는 코드에 `?? Unspecified`를 붙일 일이 없습니다.
    Sender.Tell(new RequestRejected(code, message));
    return;
}

// `!`도, 두 번째 null 검사도 없습니다. 거절을 걸러낸 것이 곧 Granted를 non-null로 좁힌 것이니까요.
Enlistment enlistment = judgement.Granted;
```

양쪽 모두 `[MemberNotNullWhen]` 덕분입니다. `false`를 반환했다는 것은 `Granted`가 null이 아니라는 뜻입니다. 거절 상세가 필요 없을 때를 위해 `IsGranted`에도 반대 방향의 같은 어노테이션이 붙어 있습니다.

멤버를 직접 패턴 매칭해도 똑같이 합법이며, payload 없는 형태에서는 그쪽이 유일한 방법입니다.

```csharp
if (judgement.Granted is not { } enlistment)
{
    // 여기서 RejectedWith는 GameStatusCode? 입니다 — TryGetRejection이 덜어 주는 것이 바로 이 한 칸입니다.
    logger.LogWarning("refused with {Code}: {Reason}", judgement.RejectedWith, judgement.RejectionMessage);
    return;
}

// payload 없는 쪽: 열어 볼 것이 없으므로 nullable 코드를 매칭합니다.
if (verdict.RejectedWith is { } code)
{
    Reply(code, verdict.RejectionMessage);
    return;
}
```

`ToString()`은 짧고 안정적입니다 — `Granted`, `Granted(state)`, `Rejected(Code): message`. payload 전체를 로그 줄에 쏟아붓는 record 기본 구현을 일부러 쓰지 않았습니다.

### 한 도메인의 거절이 언제나 같은 코드일 때

캐리어가 public이므로, 거절 코드가 늘 하나뿐인 규칙 묶음은 세 줄이면 되고 라이브러리에서 더 받아올 것이 없습니다.

```csharp
internal static class TitleJudgements
{
    public static RejectedJudgement<GameStatusCode> NotEarned(string message) =>
        Judgement.Reject(GameStatusCode.TitleNotEarned, message);
}

// Judgement.Reject와 똑같이 어느 반환 타입에나 꽂힙니다.
return TitleJudgements.NotEarned($"take part in defeating {target} to earn this title");
```

### 한계

`Judgement<T, TCode>`는 거절 검사를 빠뜨리면 빌드를 멈추게 합니다. 새 상태에는 `T?`를 통해서만 닿을 수 있으므로, 거절을 먼저 걸러내지 않고 쓰면 그것은 null 역참조이기 때문입니다. 다만 이는 보장이 아니라 올바른 방향으로의 보조이며, 어디까지가 그 범위인지는 분명히 해 둘 만합니다.

- **소비하는 프로젝트의 설정에 달려 있습니다.** 검사 누락이 빌드 오류가 되는 것은 `Nullable`이 켜져 있고 *동시에* 경고가 오류인 경우입니다. 앞의 조건만 참이면 경고로 끝나고, 둘 다 아니면 아무 일도 일어나지 않습니다.
- **`Judgement<TCode>`에는 강제력이 없습니다.** 열어 볼 payload가 없으므로, 판정을 무시한 호출자는 그냥 진행합니다. 검사 누락이 반드시 빌드를 멈춰야 하는 규칙이라면, 새 상태를 반환하도록 설계해 payload 있는 형태를 쓰세요.
- **`TryGetRejection`의 반환값을 버리는 것도 잡히지 않습니다.** out 매개변수를 non-nullable로 둔 것은 거절 갈래에서 `??`가 필요 없게 하기 위해서이고, 그 대가로 반환값을 읽기 전에는 두 값에 의미가 없습니다. 반환값을 무시한 채 out만 읽는 것은 계약 밖이며, v1은 이를 잡는 진단을 제공하지 않습니다.
- **payload가 참조 타입인 것은 의도입니다.** `where T : class`는 제약이 아니라 장치입니다. null 검사가 강제의 전부이므로, null이 될 수 없는 payload는 이 타입의 존재 이유를 지워 버립니다. 새 상태가 값 타입을 포함해 여러 값이라면 `sealed record`로 묶어 `T`로 쓰세요. 그렇게 하면 필드마다 nullable을 두었을 때 생기는 불법적인 반쪽 상태도 함께 사라집니다.

형태에서 따라 나오는 작은 계약이 셋 더 있습니다.

- **캐리어는 반환값이지 보관할 값이 아닙니다.** `var pending = Judgement.Grant(state);`는 컴파일되지만 캐리어에는 public 멤버가 없어 그 결과로 할 수 있는 일이 없습니다. 판정을 만드는 자리에서 바로 변환하세요.
- **`default` 캐리어는 예외를 던집니다.** `default(GrantedJudgement<T>)`와 `default(RejectedJudgement<TCode>)`는 팩터리를 거치지 않아 계약이 요구하는 상태가 비어 있으므로, 변환하면 조용히 거짓말하는 판정이 아니라 `InvalidOperationException`이 나옵니다. payload 없는 `GrantedJudgement`는 어차피 담는 것이 없으므로 `default`도 합법입니다.
- **판정은 프로세스 밖으로 나가지 않습니다.** private 생성자와 get 전용 프로퍼티, 직렬화 계약 없음 — 프로세스 내부의 값입니다. 프로세스 경계를 넘는 것은 코드이지, 그 코드를 담은 판정이 아닙니다.

## 진단

| ID | 심각도 | 보고 조건 |
|---|---|---|
| `SSALG001` | 오류 | `[ErrorCode]`가 `ErrorCodedException` 파생이 아닌 타입에 붙음. |
| `SSALG002` | 오류 | `[ErrorCodes]` 컨테이너가 생성 파일이 두 번째 부분을 붙일 수 있는 `static partial class`가 아님 — 클래스가 아니거나, `static`이 아니거나, `partial`이 아니거나, `file`-local이거나, `partial`이 아닌 타입 안에 중첩됨. |
| `SSALG003` | 오류 | 한 컨테이너에 같은 예외 타입이 두 번 이상 등록됨. |
| `SSALG004` | 오류 | `[ExternalErrorCode]`가 예외가 아닌 타입, 또는 언바운드 제네릭 타입을 지정함. |
| `SSALG005` | 오류 | `[ErrorCode]` 예외가 abstract이거나 제네릭이거나 제네릭 타입 안에 중첩됨. |
| `SSALG006` | 경고 | `[ErrorCode]` 예외가 인식 가능한 생성자를 하나도 선언하지 않아 팩토리·throw 헬퍼가 생성되지 않음(매핑에는 정상 참여). |
| `SSALG007` | 오류 | `[ErrorCodes]` 컨테이너가 제네릭이거나 제네릭 타입 안에 중첩됨. 또는 코드 enum이 제네릭 타입 안에 중첩됨. |
| `SSALG008` | 경고 | `[ErrorCode<TCode>]` 예외는 있는데 그 enum에 대한 `[ErrorCodes<TCode>]` 컨테이너가 컴파일 단위에 없어, 아무 곳에도 아무것도 생성되지 않음. |
| `SSALG009` | 오류 | `[ErrorCode]` 예외가 생성 파일에서 접근 불가(`private`, `protected`, `private protected`, `file`-local). |
| `SSALG010` | 경고 | `[ExternalErrorCode<TCode>]`가 컨테이너의 `[ErrorCodes<TCode>]`와 다른 코드 enum을 지정해, 어느 컨테이너에도 속하지 못하고 버려짐. |
| `SSALG011` | 경고 | 컨테이너의 코드 enum이 다른 어셈블리에 선언되어 있고 이 컴파일 단위에서 등록되는 것이 하나도 없어, 생성된 매핑이 비어 있음. |

등록 하나에 대한 규칙(`SSALG001`, `SSALG004`, `SSALG005`, `SSALG009`, `SSALG010`)은 해당 등록만 버리고 컨테이너의 나머지는 그대로 둡니다 — 잘못 선언된 예외 하나가 매핑 테이블 전체를 무너뜨려서는 안 되니까요. 컨테이너 자체에 대한 규칙(`SSALG002`, `SSALG007`)이나 생성기가 사용자 대신 해결하기를 거부하는 모호함(`SSALG003`)은 그 컨테이너의 생성 파일을 통째로 억제합니다.

## 알아 둘 점

- **코드는 언제나 선언된 타입입니다.** 이 라이브러리에는 `throw new SomeException(4001, "…")`이 없습니다. `ErrorCodedException`은 코드 필드를 갖지 않으며, 이름 없는 예외에 임의의 코드를 실어 던지는 사용은 의도적으로 지원하지 않습니다. 코드마다 작은 클래스 하나를 쓰면 `catch` 대상, 문서, 컴파일러가 검사할 수 있는 대상을 한꺼번에 얻습니다. 한 throw 지점의 정수로만 존재하는 코드는 이 셋 모두에게 보이지 않습니다. 그런 형태에서 이전해 오려면 그 클래스들을 작성해야 하는데, 이는 이 라이브러리가 일부러 택한 트레이드오프입니다.
- **미러링되는 생성자는 세 형태뿐입니다.** `()`, `(string?)`, `(string?, Exception?)`. `InsufficientFundsException(decimal balance, decimal amount)`처럼 도메인 고유 매개변수를 받는 예외도 매핑은 아무 문제 없이 되고, 헬퍼가 없는 이유는 `SSALG006`이 알려 주며, 생성은 평범하게 `new`로 하면 됩니다. 가드의 예외 팩터리 오버로드 안에서도 마찬가지입니다.
- **`GuardViolationException`에 코드를 주세요.** 이 타입도 다른 도메인 실패와 똑같이 `ErrorCodedException`을 상속하지만 이 패키지 안에 선언되어 있으므로, 타입이 아니라 컨테이너 쪽에서 등록합니다. `[ExternalErrorCode<GameStatusCode>(typeof(GuardViolationException), GameStatusCode.GuardViolation)]` 한 줄입니다. 이 줄이 없으면 모든 가드 실패가 매핑되지 않은 채 빠져나가고, 있으면 내부 불변식 위반이 여러분의 enum 안에서 1급 코드가 됩니다.
- **`ErrorCodedException`은 `catch` 대상이기도 합니다.** `catch (ErrorCodedException)` 한 줄이면 도메인 실패를 나머지 전부와 분리할 수 있어, 매핑 이전 단계에서 둘을 다르게 다루고 싶은 경계에 유용합니다.
- **클래스 하나에 컨테이너 하나 — 언어가 그렇게 정합니다.** 한 클래스에 `[ErrorCodes<A>]`와 `[ErrorCodes<B>]`를 같이 붙이면 "특성이 중복되었습니다"(`CS0579`) 오류입니다. 제네릭 특성의 `AllowMultiple = false`는 구성된 각 형태가 아니라 특성 *정의*를 기준으로 적용되기 때문입니다. 한 예외에 `[ErrorCode<A>]`와 `[ErrorCode<B>]`를 함께 붙이는 것도 마찬가지입니다. 코드 enum이 하나 더 필요하면 컨테이너 클래스를 하나 더 만들면 되고, 어차피 여기의 모든 설계가 그 형태를 전제로 합니다.
- **생성기는 컴파일 단위 하나만 봅니다.** 컨테이너는 자기가 컴파일되는 어셈블리의 `[ErrorCode]` 예외만 모읍니다. 두 프로젝트가 코드 enum을 공유하더라도, 참조된 프로젝트에 있는 예외는 컨테이너에게 보이지 않습니다. 컨테이너는 그 예외들과 같은 프로젝트에 두세요. 다른 곳의 타입(참조 프로젝트의 예외나 서드파티 타입)을 매핑하려면 `[ExternalErrorCode]`로 명시적으로 등록하면 되고, 이쪽은 어셈블리 경계를 넘습니다. 다른 어셈블리의 코드 enum을 맡은 컨테이너가 결국 텅 비게 되면, 조용히 빈 테이블이 나오는 대신 경고(`SSALG011`)가 나옵니다.

## 라이센스

MIT — 자세한 내용은 [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE)를 참고하세요.

---

**AI 고지:** 이 프로젝트는 AI(Claude)를 활용하여 제작되었습니다.
