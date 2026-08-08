[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ko.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Timekeeping/README.md) | **한국어** | [日本語](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Timekeeping/README.ja.md)

# SsalKit.Timekeeping

과거 `SsalKit.RecurrenceSchedule`이라는 이름으로 배포되었습니다 (해당 이름은 지원 중단됨). 타입과 계약은 동일하며, 패키지 id와 네임스페이스만 변경되었습니다.

SsalKit.Timekeeping은 시계를 직접 읽지 않고 결정적이고 저장 가능한 시간 상태를 계산합니다. 모든 멤버는 `(상태, 시각)`의 순수 함수이고, 모든 상태 타입은 불변이며 직렬화 가능한 `record struct`이며, 시각은 언제나 호출자가 넘기는 인자입니다 — 직접 넘기거나, 이미 시계를 들고 있는 코드를 위한 `TimeProvider` 오버로드를 통해서든. 의존성이 없습니다.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Timekeeping.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Timekeeping)

| 구성 요소 | 답하는 질문 | 상태 |
|---|---|---|
| [`RecurrenceSchedule`](#빠른-시작-recurrenceschedule) + [`TimeWindow`](#timewindow-포함-규칙은-하나) | 달력 벽시계 경계 — 일간/주간/월간 리셋, 영구히 고정된 DST 계약 | 기존 |
| [`Cooldown`](#빠른-시작-cooldowns) + [`RechargePool`](#cooldowns) | 경과 시간 상태 — 단일 쿨다운, 또는 용량 제한이 있는 충전 풀 | 신규 |
| [`TickSchedule`](#빠른-시작-tickschedule) | 논리적 틱 상태 — 시뮬레이션 틱 번호에 예정된 이벤트를 담는 결정적이고 직렬화 가능한 큐 | 신규 |
| [`TickCooldown`](#빠른-시작-tickcooldown) | 논리적 틱 상태 — 벽시계 경과 시간이 아니라 시뮬레이션 틱으로 재는 단일 쿨다운 | 신규 |

### 경계의 소재

| 경계 종류 | 사용할 타입 |
|---|---|
| 달력 벽시계 (일간/주간/월간 리셋, DST) | `RecurrenceSchedule` |
| 이벤트로부터 경과한 시간 (능력 쿨다운, 스태미나/충전 풀) | `Cooldown` / `RechargePool` |
| 논리적 시뮬레이션 틱 (시계가 아니라 틱 번호를 기준으로 한 결정적 이벤트 디스패치) | `TickSchedule` |
| 행동 이후 경과한 논리적 틱 (같은 능력 쿨다운을 틱으로 셀 때) | `TickCooldown` |
| 프로세스 내 자원 스로틀링 (동시 요청 제한, 토큰 버킷) | 이 패키지 담당 아님 — [`System.Threading.RateLimiting`](https://learn.microsoft.com/dotnet/api/system.threading.ratelimiting) 참고 |

## 왜 SsalKit.Timekeeping인가

"마지막으로 확인한 이후로 일일 리셋이 지나갔는가?"는 일일 쿼터, 출석 보상, 결제 주기, 리포트 윈도우가 있는 코드베이스라면 어디에나 등장합니다. 겉보기에 `DateTime` 산술 두 줄이라서, 공유되는 대신 호출부마다 다시 작성되고, 그렇게 복제된 구현들이 서로 다른 답을 내놓기 시작합니다.

이 라이브러리를 뽑아낸 원형 코드베이스에는 그런 구현이 나란히 두 개 있었습니다.

- **하나는 자정 UTC와 월요일이 하드코딩**되어 있었고, 메서드 안에서 직접 `DateTime.UtcNow`를 읽었습니다. 그 위에 얹힌 어떤 코드도 머신 시계를 옮기지 않고는 테스트할 수 없었죠. 게다가 포함 규칙이 메서드마다 달랐습니다. 어떤 것은 양 끝을 모두 포함했고, 어떤 것은 날짜만 비교했습니다.
- **다른 하나는 리셋 시각을 설정할 수 있었지만**, "리셋이 지났는가"를 `from.Hour >= resetHour`로 판정했습니다. 그래서 04:15가 04:30 리셋을 지난 것으로 취급됐습니다. `4 >= 4`니까요. 설정된 스케줄의 분·초가 조용히 버려졌고, 이 버그는 4시 15분에 일일 보상이 한 번 더 지급되는 형태로만 드러났습니다.

한 코드베이스 안에, 같은 질문에 대한 서로 다른 두 개의 답. 그 사이에 25곳이 넘는 호출부와 영속화된 "마지막 리셋" 필드들이 있었습니다.

.NET 8이 `TimeProvider`를 추가하면서 "시계는 누가 소유하는가" 쪽 절반은 정리됐습니다. 하지만 반복되는 창 자체를 표현하는 타입은 여전히 없습니다. BCL에는 "이 시각이 속한 리셋 기간"이라는 개념이 없습니다. NodaTime은 달력과 타임존을 깊이 있게 모델링하지만 리셋 창 개념은 없고, Cronos는 cron 식을 파싱해 다음 발생 시각을 알려줄 뿐 창 소속 판정이나 통과 횟수는 다루지 않습니다.

SsalKit.Timekeeping은 그 빈자리를 채웁니다.

- `RecurrenceSchedule`은 달력에 정렬된 반복을 정의하고 — 매일 04:30 서울 시각, 매주 월요일 09:00 UTC, 매월 31일 — 그에 대해 물을 가치가 있는 네 가지 질문에 답합니다. `PreviousBoundary`, `NextBoundary`, `CurrentWindow`, 그리고 "자리를 비웠던 사용자"를 위한 `HasCrossed` / `CountBoundaries`입니다.
- **포함 규칙은 어디서나 하나뿐입니다.** `TimeWindow`는 반개구간 `[Start, End)`이고 포함 구간 변형이 없습니다. 그래서 연속된 창들이 타임라인을 이중 계산도 구멍도 없이 정확히 타일링합니다.
- **DST 계약은 타입의 수명 동안 고정입니다.** 경계는 영속화되므로, 존재하지 않거나 두 번 나타나거나 끝내 도달하지 않는 벽시계 시각을 어떻게 해석하는지는 구현 세부사항이 아니라 버전 계약입니다.
- **모든 것이 `(스케줄, 시각)`의 순수 함수입니다.** 어떤 API도 주변 시계를 읽지 않습니다. `TimeProvider` 오버로드는 그 위에 얹은 당의정이지, 그 반대가 아닙니다.
- **의존성이 없습니다.** `PackageReference` 없이 BCL만 사용합니다.

## 설치

```bash
dotnet add package SsalKit.Timekeeping
```

## 빠른 시작: RecurrenceSchedule

```csharp
using SsalKit.Timekeeping;

var seoul = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
var dailyReset = RecurrenceSchedule.Daily(new TimeOnly(4, 30), seoul);

// 마지막으로 확인한 이후 04:30 리셋이 지나갔는가?
if (dailyReset.HasCrossed(player.LastQuotaReset, now))
{
    player.Quota = DailyQuota;
    player.LastQuotaReset = dailyReset.PreviousBoundary(now);
}

// 돌아온 플레이어가 놓친 일일 보상은 몇 회분인가? ((lastSeen, now] 안의 경계 개수.)
int missedRewards = dailyReset.CountBoundaries(player.LastLogin, now);

// 지금은 어느 리셋 기간이고, 얼마나 남았는가?
TimeWindow today = dailyReset.CurrentWindow(now);
TimeSpan remaining = dailyReset.UntilNext(now);   // 항상 0보다 큼
```

`WindowAt`은 오프셋으로 이웃 기간을 집어냅니다. `0`이 오늘, `-1`이 그 직전 기간이라 "전일 대비" 수치를 낼 때 쓰기 좋습니다. `EnumerateBoundaries`는 구간 안의 경계를 오름차순으로 지연 열거합니다.

```csharp
TimeWindow yesterday = dailyReset.WindowAt(now, -1);   // O(1). -30이어도 비용은 같음

foreach (var boundary in dailyReset.EnumerateBoundaries(player.LastLogin, now))
{
    // (lastSeen, now] 안의 경계가 오름차순으로, 정확히 CountBoundaries(player.LastLogin, now)개
}
```

주간·월간 주기도 같은 방식이며, 짧은 달의 마지막 날을 넘어가는 월간 스케줄은 그 달의 말일로 클램프됩니다.

```csharp
var weekly = RecurrenceSchedule.Weekly(DayOfWeek.Monday, new TimeOnly(9, 0));   // 기본값은 UTC
var monthly = RecurrenceSchedule.Monthly(31, new TimeOnly(0, 0));

monthly.NextBoundary(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
// 2026-02-28T00:00:00+00:00 -- 2월은 클램프되지만, 경계는 여전히 정확히 한 개
```

DST를 포함해 위 내용을 전부 실행해 볼 수 있는 예제는 [samples/SsalKit.Timekeeping.Sample](https://github.com/ssalkit/ssalkit/tree/main/samples/SsalKit.Timekeeping.Sample)에 있습니다.

## API 개요: RecurrenceSchedule

### `RecurrenceSchedule`

| 멤버 | 용도 |
|---|---|
| `Daily(TimeOnly atTimeOfDay, TimeZoneInfo? timeZone = null)` | 매 달력일 1회, 지정한 벽시계 시각에. `timeZone` 기본값은 UTC. |
| `Weekly(DayOfWeek dayOfWeek, TimeOnly atTimeOfDay, TimeZoneInfo? timeZone = null)` | 매 달력주 1회, 지정한 요일에. |
| `Monthly(int dayOfMonth, TimeOnly atTimeOfDay, TimeZoneInfo? timeZone = null)` | 매 달력월 1회, `1`~`31`일에. 짧은 달은 말일로 클램프. |
| `PreviousBoundary(DateTimeOffset asOf)` | `b <= asOf`인 가장 큰 경계. `asOf` 자체가 경계면 그대로 반환. |
| `NextBoundary(DateTimeOffset asOf)` | `b > asOf`인 가장 작은 경계. 엄격한 부등호이므로 경계를 넣으면 그 다음 경계가 나옴. |
| `UntilNext(DateTimeOffset asOf)` | `NextBoundary(asOf) - asOf`. **항상 0보다 큼**. 경계를 넣으면 0이 아니라 창 하나 전체가 나옴. |
| `CurrentWindow(DateTimeOffset asOf)` | `[PreviousBoundary(asOf), NextBoundary(asOf))` — `asOf`가 속한 리셋 기간. |
| `WindowAt(DateTimeOffset asOf, int offset)` | `offset`칸 떨어진 창. `0`은 `CurrentWindow(asOf)`, `-1`은 그 직전 창. O(1). |
| `HasCrossed(DateTimeOffset lastSeen, DateTimeOffset now)` | `lastSeen < b <= now`를 만족하는 경계 `b`의 존재 여부. |
| `CountBoundaries(DateTimeOffset lastSeen, DateTimeOffset now)` | 그런 `b`의 개수. `now <= lastSeen`이면 `0`. |
| `EnumerateBoundaries(DateTimeOffset from, DateTimeOffset to)` | 바로 그 경계들 자체를 오름차순으로 지연 열거. 개수는 언제나 `CountBoundaries(from, to)`와 같음. |
| `ToString()` | 진단용 표기. `Daily 04:30 @ UTC`, `Weekly Monday 09:00 @ Asia/Seoul`, `Monthly day 31 00:00 @ America/New_York`. |

경계는 항상 **해당 날짜에 대한 스케줄 타임존의 UTC 오프셋**을 달고 돌아옵니다. 서울 스케줄이면 `+09:00`, 뉴욕 스케줄이면 `-05:00` 또는 `-04:00`, UTC 스케줄이면 `+00:00`입니다. 비교에는 영향이 없지만(`DateTimeOffset`은 절대 시각을 비교합니다), 경계를 포맷하면 그 스케줄이 정의된 현지 벽시계 시각이 그대로 보입니다.

편의를 위해 추가된 멤버에 대해 세 가지만 짚어 둡니다.

- **`CountBoundaries`는 O(1)이고 `EnumerateBoundaries`는 O(경계 개수)입니다.** 둘은 같은 반개구간 `(from, to]`를 다루고 열거 결과의 개수는 언제나 `CountBoundaries`와 일치하지만, 개수는 닫힌 형태의 달력 산술인 반면 열거는 경계를 하나씩 실제로 계산합니다. 개수만 필요하면 개수를 물으세요. 열거는 지연 평가이며(검증할 인자가 없으므로 미리 확인되는 것도 없습니다), `Take`로 끊거나 `to`를 넓게 잡아 쓸 수 있습니다.
- **`WindowAt`은 절대 오버플로로 감기지 않습니다.** `DateTime` 범위를 벗어나게 하는 `offset`은 엉뚱한 세기의 창을 조용히 돌려주는 대신 `ArgumentOutOfRangeException`을 던집니다. 산술은 64비트로 하고 결과를 범위 검사합니다.
- **`ToString()`은 로그용이지 파싱 대상이 아닙니다.** 아래의 DST 규칙과 달리 이 포맷에는 호환성 약속이 없으며 어느 릴리스에서든 개선될 수 있습니다. 표기는 invariant culture와 타임존의 `TimeZoneInfo.Id`를 사용하고, 시각 부분은 스케줄이 그만큼 정밀할 때만 `HH:mm:ss`(또는 틱 정밀도)까지 늘어납니다.

### `TimeWindow`

반개구간 `[Start, End)`를 표현하는 `readonly record struct`입니다.

| 멤버 | 용도 |
|---|---|
| `new TimeWindow(DateTimeOffset start, DateTimeOffset end)` | `start == end`인 빈 창은 합법. `start > end`면 `ArgumentException`. |
| `Start` / `End` / `Duration` | 포함되는 시작, 포함되지 않는 끝, 그리고 `End - Start`(음수가 될 수 없음). |
| `Contains(DateTimeOffset instant)` | `Start <= instant < End`. 빈 창에서는 항상 `false`. |
| `Overlaps(TimeWindow other)` | 교집합이 비어 있지 않은지 여부. 맞닿기만 한 창은 겹치는 것이 **아님**. |
| `Intersect(TimeWindow other)` | 공유 구간, 없으면 `null`. 대칭적. |
| `Clamp(DateTimeOffset instant)` | *닫힌* 범위 `[Start, End]`으로 자르므로, 끝을 넘긴 시각은 `End`가 됨. |

### `TimeProvider` 확장

`RecurrenceScheduleTimeProviderExtensions`는 인자 전체가 "지금"인 여섯 개 멤버에 대해 `TimeProvider.GetUtcNow()`를 전달하는 오버로드를 제공합니다. 호출당 정확히 한 번만 읽으므로, 움직이는 시계에서 값이 찢어질 일이 없습니다.

| 확장 | 동등한 호출 |
|---|---|
| `schedule.PreviousBoundary(timeProvider)` | `schedule.PreviousBoundary(timeProvider.GetUtcNow())` |
| `schedule.NextBoundary(timeProvider)` | `schedule.NextBoundary(timeProvider.GetUtcNow())` |
| `schedule.UntilNext(timeProvider)` | `schedule.UntilNext(timeProvider.GetUtcNow())` |
| `schedule.CurrentWindow(timeProvider)` | `schedule.CurrentWindow(timeProvider.GetUtcNow())` |
| `schedule.HasCrossed(lastSeen, timeProvider)` | `schedule.HasCrossed(lastSeen, timeProvider.GetUtcNow())` |
| `schedule.CountBoundaries(lastSeen, timeProvider)` | `schedule.CountBoundaries(lastSeen, timeProvider.GetUtcNow())` |

`WindowAt`과 `EnumerateBoundaries`에는 프로바이더 오버로드가 없습니다. 기준 시각 *과 함께* 명시적인 범위를 받는 API라, `timeProvider.GetUtcNow()`를 직접 넘기는 것과 비교해 얻는 것이 없기 때문입니다.

`TimeProvider`는 .NET 8부터 BCL에 포함되므로, 이 오버로드를 써도 패키지 의존성은 늘지 않습니다.

## 경계 의미 계약: RecurrenceSchedule

모든 것이 하나의 규칙에서 따라 나옵니다. **경계 시각은 자신이 닫는 창이 아니라, 자신이 여는 창에 속한다.**

```csharp
schedule.CurrentWindow(b).Start == b   // 임의의 경계 b에 대해
```

여기서 나머지가 도출됩니다.

- `PreviousBoundary`는 포함(`b <= asOf`), `NextBoundary`는 엄격(`b > asOf`)이고, `CurrentWindow`는 그 둘 사이의 반개구간입니다. 따라서 연속된 창들이 타임라인을 정확히 타일링합니다. 두 창에 동시에 속하는 시각도, 어느 창에도 속하지 않는 시각도 없습니다.
- `HasCrossed(lastSeen, now)`는 **반개구간 `(lastSeen, now]`** 안에 경계가 있는지를 묻습니다. `lastSeen` 자체가 경계라면 그 창은 이미 본 것이므로 통과한 것이 없습니다. `now`가 정확히 경계라면 방금 통과한 것입니다.
- `CountBoundaries`는 같은 `(lastSeen, now]` 안의 경계를 셉니다. `HasCrossed`는 정확히 `CountBoundaries(...) > 0`과 동치이며, 첫 경계에서 멈추므로 더 쌀 뿐입니다. 역전된 구간(`now < lastSeen`)은 음수가 아니라 `0`을 반환합니다.

모든 비교가 달력 필드가 아니라 시각끼리 이뤄지므로, 원형의 hour 비교 버그는 고쳐진 정도가 아니라 **표현 자체가 불가능**합니다.

```csharp
var reset = RecurrenceSchedule.Daily(new TimeOnly(4, 30));

reset.HasCrossed(At(4, 00), At(4, 15));   // false -- hour 필드 비교라면 true라고 답했을 것
reset.HasCrossed(At(4, 00), At(4, 30));   // true
```

## DST 계약

스케줄 시각은 해당 타임존의 **벽시계** 시각이고, 벽시계가 오작동하는 방식은 정확히 세 가지입니다. 세 해석 모두 여기에 고정됩니다.

1. **존재하지 않는 스케줄 시각** — 02:30 스케줄에 대해 02:00 → 03:00으로 시계가 앞으로 뛰는 경우 — 은 **갭 직후의 첫 유효 시각**으로 이동합니다. 전이 시각 자체인 03:00이지, 03:30이 아닙니다. 경계가 사라지지 않으므로 일간 스케줄은 그날에도 정확히 하나의 경계를 갖고, `CountBoundaries`는 여전히 경과한 일수와 같습니다.
2. **두 번 나타나는 스케줄 시각** — 01:30 스케줄에 대해 02:00 → 01:00으로 시계가 되돌아가는 경우 — 는 **첫 번째 발생**, 즉 전이 이전의 (더 큰) UTC 오프셋 쪽으로 해석됩니다. 그날 스케줄이 두 번 발동하지 않습니다.
3. **벽시계가 끝내 도달하지 못하는 스케줄 시각** — 계절이 아니라 존의 *기준* 오프셋이 영구히 바뀌는 경우로, 2012년 초의 리비아, 2007년의 베네수엘라, 2011년 12월 30일이 통째로 사라진 사모아, 2015년의 북한, 기준 오프셋이 재조정된 여러 러시아 존이 여기에 해당합니다 — 은 **존의 벽시계가 스케줄 시각에 도달하는 첫 순간**으로 해석됩니다. 이는 규칙 1의 원리를 온전히 서술한 것이고, 규칙 1은 존 스스로 그 구멍을 갭이라고 부르는 특수한 경우입니다. 이런 이음새에서 `TimeZoneInfo.IsInvalidTime`은 아무것도 보고하지 않으며, 존이 그 현지 시각에 대해 알려주는 오프셋은 그 짝이 가리키는 순간에 실제로 적용되는 오프셋이 아닙니다. "도달하는 첫 순간"이라는 해석은 이음새가 만들어내는 상황 — 벽시계가 한 시간 뒤로 물러났다가 다시 앞으로 뛰는 — 에서도 잘 정의됩니다.
4. **그 밖의 모든 벽시계 시각**은 해당 날짜의 존 오프셋을 사용합니다. 따라서 경계는 계절에 따라 떠내려가지 않고 의도한 현지 시각에 그대로 머뭅니다.

어떤 존이 이음새를 갖는지는 이 라이브러리가 아니라 플랫폼의 타임존 데이터에 달린 문제입니다. 위 이력들은 Windows가 제공하는 데이터에서는 이음새로 나타나고, tzdata 기반 빌드에서는 평범한 전이로 기록됩니다. 규칙 3은 특정 존에 대한 이야기가 아니라, 어느 쪽이든 올바르게 동작한다는 이야기입니다.

`America/New_York`의 2026년 전이로 확인해 보면 이렇습니다.

```csharp
// 봄 전이: 2026-03-08, 02:00 EST가 03:00 EDT가 되므로 02:30은 존재하지 않는다.
var spring = RecurrenceSchedule.Daily(new TimeOnly(2, 30), newYork);

spring.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 1, 0, 0, Est));   // 2026-03-07T02:30:00-05:00
spring.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt));  // 2026-03-08T03:00:00-04:00  <- 전이 시각
spring.NextBoundary(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt));      // 2026-03-09T02:30:00-04:00
spring.CurrentWindow(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt)).Duration;  // 23:30 -- 창이 짧아질 뿐 경계는 살아남는다

// 가을 전이: 2026-11-01, 02:00 EDT가 01:00 EST가 되므로 01:30이 두 번 나타난다.
var autumn = RecurrenceSchedule.Daily(new TimeOnly(1, 30), newYork);
var first  = new DateTimeOffset(2026, 11, 1, 1, 30, 0, Edt);   // 05:30Z
var second = new DateTimeOffset(2026, 11, 1, 1, 30, 0, Est);   // 06:30Z

autumn.PreviousBoundary(new DateTimeOffset(2026, 11, 1, 12, 0, 0, Est));  // == first
autumn.CountBoundaries(first, second);                                    // 0 -- 두 번 발동하지 않는다
autumn.CurrentWindow(second).Duration;                                    // 25:00 -- 창이 길어질 뿐이다
```

이음새의 예로, Windows가 제공하는 `Africa/Tripoli` 데이터에서 기준 오프셋이 2012년으로 넘어가는 시점에 +02:00에서 +01:00으로 떨어지는 경우를 보면 이렇습니다.

```csharp
// 2011-12-31T21:00Z는 23:00, 22:00Z는 다시 23:00(기준 오프셋이 잠깐 내려간다), 23:00Z는 01:00을
// 가리킨다. 그래서 벽시계는 2012-01-01 00:00을 끝내 한 번도 가리키지 않는다.
var midnight = RecurrenceSchedule.Daily(new TimeOnly(0, 0), tripoli);

midnight.PreviousBoundary(new DateTimeOffset(2012, 1, 1, 12, 0, 0, TimeSpan.FromHours(2)));
// 2012-01-01T01:00:00+02:00, 즉 2011-12-31T23:00Z -- 스케줄 시각에 도달하는 첫 순간
```

이 규칙들은 **버전 계약이며, 패치나 마이너 릴리스에서 절대 변경되지 않습니다.** 경계는 영속화됩니다 — "이 플레이어가 마지막으로 본 리셋" — 그리고 저장된 시각과 재계산한 시각을 비교하는 일은 계산이 절대 움직이지 않을 때만 성립합니다. 시드가 있는 PRNG의 알고리즘 계약과 정확히 같습니다. 다른 해석 정책이 필요해진다면 이 타입의 동작을 바꾸는 대신 새 타입으로 출시됩니다.

이 규칙들은 주기와도 무관합니다. 전이가 일어나는 날에 경계가 놓이는 주간·월간 스케줄도 동일하게 해석되며, 전이 폭이 한 시간이 아닌 존에서도 성립합니다(`Australia/Lord_Howe`는 30분 이동하며, 그곳의 02:15 스케줄은 02:45가 아니라 전이 시각인 02:30으로 해석됩니다).

타임존 식별자는 `TimeZoneInfo`의 해석을 그대로 따릅니다. .NET 6 이상에서는 ICU만 사용 가능하다면 `TimeZoneInfo.FindSystemTimeZoneById`가 Windows에서도 `America/New_York` 같은 IANA 식별자를 받습니다. 주의할 경우는 globalization-invariant 모드로 동작하는 Windows 환경입니다.

## `TimeWindow`: 포함 규칙은 하나

반개구간 `[Start, End)`가 이 타입이 제공하는 유일한 규칙이고, 포함 구간 변형은 의도적으로 없습니다. 두 규칙을 섞는 코드베이스는 어떤 메서드 쌍에서는 공유 끝점이 이중 계산되고, 다른 쌍에서는 같은 끝점에 구멍이 생깁니다. 원형이 서로 다른 두 답을 갖게 된 경위가 정확히 이것입니다.

```csharp
var yesterday = dailyReset.WindowAt(now, -1);

yesterday.End == today.Start;          // true  -- 정확히 맞닿는다
yesterday.Overlaps(today);             // false -- 맞닿는 것은 겹치는 것이 아니다
yesterday.Contains(today.Start);       // false -- 공유된 시각은 오늘 창의 것
today.Contains(today.Start);           // true

today.Intersect(maintenanceWindow);    // 공유 구간, 없으면 null
today.Clamp(overrunInstant);           // == today.End: "이 창 안에서 어디까지 왔는가"
```

**오프셋은 표시용일 뿐입니다.** `DateTimeOffset`은 타임라인 위의 한 점을 가리키고, `TimeWindow`의 연산도 값 동등성도 모두 그 점을 비교합니다. `2026-07-25T04:30:00+09:00`과 `2026-07-24T19:30:00+00:00`은 같은 순간이므로, 어느 쪽 표기로 만든 창이든 `==`이고 동일하게 동작합니다. 오프셋이 `Start`, `End`, `ToString()`에 그대로 보존되는 것은 순전히, 스케줄이 만들어 낸 창이 자기 타임존의 현지 시각을 그대로 보여 주도록 하기 위해서입니다.

## Cooldowns

`Cooldown`과 `RechargePool`은 `RecurrenceSchedule`과는 다른 질문에 답합니다. "달력 경계가 지났는가"가 아니라, "다음 충전이 사용 가능해지기까지 얼마나 남았는가"입니다. 둘 다 상태 전체가 `DateTimeOffset`/`TimeSpan` 필드 한두 개뿐인 `readonly record struct`이므로, `RecurrenceSchedule`의 영속화된 경계와 똑같은 방식으로 프로세스 재시작이나 오프라인 공백을 견뎌 냅니다 — 구조체를 저장해 두고, 나중에 나머지는 그 구조체와 물어보는 시각으로부터 다시 계산하면 됩니다.

### 빠른 시작: Cooldowns

```csharp
using SsalKit.Timekeeping;

// 30초 쿨다운을 가진 단일 능력.
var cooldown = Cooldown.Create(TimeSpan.FromSeconds(30), now);

if (cooldown.TryUse(now, out var updated))
{
    player.AbilityCooldown = updated;   // 저장소에 다시 저장
}

TimeSpan left = cooldown.Remaining(now);
bool ready = cooldown.IsReady(now);

// 20분마다 하나씩 충전되는 스태미나 충전 5개.
var pool = RechargePool.Create(capacity: 5, rechargeEvery: TimeSpan.FromMinutes(20), asOf: now);

if (pool.TryConsume(now, amount: 1, out var updatedPool))
{
    player.Stamina = updatedPool;       // 이것도 다시 저장
}

int available = pool.AvailableAt(now);
TimeSpan? untilNext = pool.UntilNextCharge(now);
```

두 타입 모두 `(상태, 시각)`의 순수 함수입니다 — 여기서 주변 시계를 읽는 곳은 없습니다 — 그래서 `player.AbilityCooldown`과 `player.Stamina`는 저장소(JSON, DB 컬럼, 무엇이든)를 그대로 왕복합니다. 다음 `IsReady` / `AvailableAt` 호출은 저장된 구조체와 그때 넘긴 시각만으로 모든 것을 다시 계산하며, 별도의 "마지막 저장 시각" 부기가 필요 없습니다.

### API 개요: Cooldown과 RechargePool

#### `Cooldown`

| 멤버 | 용도 |
|---|---|
| `static Cooldown Create(TimeSpan duration, DateTimeOffset asOf)` | 즉시 사용 가능한 쿨다운. `duration`은 이후 `TryUse`가 진입시킬 대기 길이. `duration < 0`이면 `ArgumentOutOfRangeException`. `TimeSpan.Zero`는 합법이며 항상 사용 가능한 쿨다운을 만듦. |
| `IsReady(DateTimeOffset asOf)` | `asOf >= ReadyAt`. |
| `Remaining(DateTimeOffset asOf)` | `max(0, ReadyAt - asOf)`. 절대 음수가 되지 않음. |
| `TryUse(DateTimeOffset asOf, out Cooldown updated)` | 성공하면 새 `Duration` 길이의 대기를 시작(`ReadyAt = asOf + Duration`). 실패하면 `updated`는 이 인스턴스 그대로 — 그대로 대입해도 항상 안전. |
| `Reset(DateTimeOffset asOf)` | 남은 대기를 버리고 `asOf`에 즉시 사용 가능하게 만듦. |
| `Duration` / `ReadyAt` | 설정된 대기 길이, 그리고 쿨다운이 다음으로 사용 가능해지는 시각. |

#### `RechargePool`

| 멤버 | 용도 |
|---|---|
| `static RechargePool Create(int capacity, TimeSpan rechargeEvery, DateTimeOffset asOf, int initialCharges = -1)` | `capacity >= 1`, `rechargeEvery > 0` — 아니면 `ArgumentOutOfRangeException`. `initialCharges`는 기본값 `-1`(가득 참)이고, 다른 값은 `[0, capacity]` 범위여야 함. |
| `AvailableAt(DateTimeOffset asOf)` | `0..Capacity` 사이 값. |
| `TryConsume(DateTimeOffset asOf, int amount, out RechargePool updated)` | `amount`는 `1..Capacity` 여야 함(`amount > Capacity`면 예외 — 이 풀로는 절대 채울 수 없는 요청). 현재 사용 가능한 양이 `amount`보다 적으면 `false`를 반환하고 `updated`는 그대로. |
| `UntilNextCharge(DateTimeOffset asOf)` | 가득 찼으면 `null`. 그 외에는, `asOf`가 풀의 모델 충전 구간(`FullAt - (Capacity - 1) * RechargeEvery`부터 `FullAt`까지) 안이면 최대 `RechargeEvery` 길이. `asOf`가 그 구간보다 이전이면 가장 이른 모델 충전이 완료되기까지의 전체 시간이며, `RechargeEvery`를 초과할 수 있음. |
| `UntilFull(DateTimeOffset asOf)` | 가득 찼으면 `null`. 아니면 정확히 `FullAt - asOf`. |
| `Grant(int amount, DateTimeOffset asOf)` | `Capacity`로 클램프하며 충전량을 더함. `TryConsume`과 달리 `amount`에 상한이 없음 — 초과 지급은 그냥 가득 참에서 포화됨. |
| `Refill(DateTimeOffset asOf)` | 다음 충전을 향한 부분 진행률을 버리고 `asOf`에 완전히 가득 참. |
| `Capacity` / `RechargeEvery` / `FullAt` | 설정된 용량과 충전 간격, 그리고 풀이 완전히 가득 차는 단일 시각 — [`FullAt` 모델](#fullat-모델) 참고. |

#### `CooldownTimeProviderExtensions`

`RecurrenceScheduleTimeProviderExtensions`와 같은 패턴입니다. 위 멤버 중 "지금"에 해당하는 인자가 `asOf`뿐인 멤버마다(하나의 `Cooldown`의 `IsReady`, `Remaining`, `TryUse`, `Reset`; `RechargePool`의 `AvailableAt`, `TryConsume`, `UntilNextCharge`, `UntilFull`, `Grant`, `Refill`) 오버로드가 있고, 각각 `TimeProvider.GetUtcNow()`를 정확히 한 번 전달합니다. `null` 프로바이더는 `ArgumentNullException`.

### 경계 의미 계약: Cooldowns

규칙은 하나뿐이며, `RecurrenceSchedule`의 "경계는 자신이 여는 창에 속한다"와 같은 지위의 영구 버전 계약입니다.

> **쿨다운이나 충전 한 개는 완료되는 바로 그 순간부터 사용 가능하며, 그 이후에만 사용 가능한 것이 아니다.**

```csharp
cooldown.IsReady(cooldown.ReadyAt);       // true
cooldown.Remaining(cooldown.ReadyAt);     // TimeSpan.Zero
pool.AvailableAt(pool.FullAt);            // Capacity, Capacity - 1이 아님
```

쿨다운과 풀 상태는 `RecurrenceSchedule`의 경계와 같은 방식으로 영속화됩니다 — "이 능력을 마지막으로 사용한 시각", "이 풀이 가득 차게 될 시각" — 그래서 이 비교는 저장된 `ReadyAt`이나 `FullAt`을 깨뜨리지 않는 한 릴리스 사이에 절대 의미가 바뀌지 않습니다.

### `FullAt` 모델

`RechargePool`의 상태 전체는 `FullAt` — 풀이 완전히 가득 차는 단일 시각 — 입니다. 다른 모든 값은 이것과 `RechargeEvery`로부터 유도됩니다.

```
available(t)  = Capacity - clamp(ceil((FullAt - t) / RechargeEvery), 0, Capacity)
consume(k, t) : FullAt' = max(FullAt, t) + k * RechargeEvery
grant(k, t)   : FullAt' = max(t, FullAt - k * RechargeEvery)
refill(t)     : FullAt' = t
```

이것이 풀이 얼마나 오래 오프라인이었는지, 얼마나 많은 충전이 부족한지와 무관하게 모든 멤버를 **O(1)**로 만드는 원천이며, 의지할 만한 세 가지 성질을 부여합니다.

- **다음 충전을 향한 부분 진행률이 정확히 보존됩니다.** 한 개를 소비하면 `FullAt`과 소비 시각 중 더 늦은 쪽을 기준으로 `RechargeEvery` 하나만큼 앞으로 밀릴 뿐, 이미 진행 중이던 충전의 진행률을 초기화하지 않습니다. 같은 시각에 같은 양을 `Grant`했을 때 이 이동이 원래의 `FullAt`으로 정확히 되돌아가는지는 소비 시점에 진행 중인 충전이 실제로 있었는지에 달려 있습니다. 있었다면(소비 시각에 `FullAt`이 그 시각과 같거나 이후) 왕복은 손실 없이 복원되며, 그 시각 이전 시점의 관측에도 동일하게 적용됩니다. 반대로 풀이 이미 가득 찬 상태였다면(소비 시각에 `FullAt`이 그 시각과 같거나 이전) 왕복은 풀이 실제로 가득 찼던 더 이른 시각이 아니라 소비/부여 시각 자체로 `FullAt`을 되돌립니다 — 그 시각 이후의 관측(두 상태 모두 그 구간 내내 가득 참을 보고)은 여전히 일치하지만, 그 시각 이전의 조회나 두 `RechargePool` 값의 동등성 비교로는 차이를 구분할 수 있습니다.
- **오프라인 공백은 1분이든 10년이든 비용이 같습니다.** 어떤 공백 후에도 `AvailableAt`은 뺄셈 한 번과 나눗셈 한 번일 뿐, 놓친 충전을 순회하는 루프가 아닙니다.
- **시각 역행은 예외가 아니라 정상 처리됩니다.** 위반될 "마지막으로 관측한 시각"이라는 저장 값 자체가 없습니다. 이전에 쓰인 시각보다 이른 `asOf`는 (위 공식의 `clamp` 항을 통해) 그저 더 적은 사용 가능 개수를 보고할 뿐, 예외도 상태 손상도 없습니다.

### 예외

| 조건 | 동작 |
|---|---|
| `Create`: `capacity < 1` / `rechargeEvery <= 0` / `duration < 0` / `initialCharges`가 합법 범위 밖 | `ArgumentOutOfRangeException` |
| `TryConsume` / `Grant`: `amount < 1` | `ArgumentOutOfRangeException` |
| `TryConsume`: `amount > Capacity` | `ArgumentOutOfRangeException` — 이 크기의 풀로는 영원히 충족할 수 없는 요청이므로, 영원히 `false`를 반환하는 대신 호출자 버그로 거부 |
| `TryConsume`: `amount`는 유효하지만 현재 그만큼 사용 가능하지 않음 | `false`, `updated`는 그대로 — 원본 위에 그대로 대입해도 항상 안전 |
| `default(Cooldown)` (손상되거나 잘린 역직렬화 페이로드 포함) | 합법 — `Cooldown.Create(TimeSpan.Zero, DateTimeOffset.MinValue)`와 정확히 동일하게 동작, 즉 항상 사용 가능 |
| `default(RechargePool)` (손상되거나 잘린 역직렬화 페이로드 포함) | 모든 멤버가 `InvalidOperationException`을 던짐 — `Cooldown`과 달리, 용량 `0`에 절대 충전되지 않는 풀은 사용 가능한 퇴화 상태가 아님 |
| 시각 역행 | 예외 없음 — 위 "시각 역행" 항목 참고 |
| `DateTimeOffset` 범위를 벗어나는 산술, 또는 `RechargePool`의 틱 곱셈 오버플로 | 하부 checked 산술에서 나오는 `ArgumentOutOfRangeException`(또는 `OverflowException`) |

`Cooldown`과 `RechargePool`은 의도적으로 default 값의 취급이 다릅니다. `Cooldown.Duration = TimeSpan.Zero`는 이미 "쿨다운이 설정되지 않음"을 뜻하는 합법적인 퇴화 케이스이므로, `default(Cooldown)`은 그 케이스가 미리 만들어져 있는 것일 뿐입니다. `RechargePool.Create`는 전부 0인 default를 의미 있게 다루려면 0인 `RechargeEvery`로 나누어야 하므로, 대신 모든 멤버가 명시적으로 이를 방어하고 예외를 던집니다.

### 직렬화와 스레드 안전성

두 타입 모두 public `get`/`init` 속성만 가진 `record struct`이므로, System.Text.Json(또는 MessagePack, 그 밖의 무엇이든)이 커스텀 컨버터 없이 왕복시킵니다. 구조체 자체가 곧 상태입니다. `Cooldown`은 필드 2개(`Duration`, `ReadyAt`)를, `RechargePool`은 필드 3개(`Capacity`, `RechargeEvery`, `FullAt`)를 저장합니다. 손상된 페이로드를 역직렬화해 생성자를 우회한 경우는, 역직렬화 시점이 아니라 어떤 메서드가 호출되는 시점에 위 예외 표의 가드가 잡아냅니다.

불변 값과 순수 함수의 조합은 두 타입을 읽기용으로는 여러 스레드에서 안전하게 공유할 수 있게 합니다. 하지만 read-modify-write 시퀀스를 원자적으로 만들어 주지는 않습니다. `if (pool.TryConsume(now, 1, out var updated)) player.Stamina = updated;`는 두 스레드가 같은 저장된 값에 대해 동시에 실행하면 여전히 경합합니다. 이는 낙관적 동시성 갱신 주변에서 호출자가 이미 지고 있는 것과 같은 책임이며, 이 패키지는 그 위에 락을 추가하지 않습니다.

### `RecurrenceSchedule`과의 조합

두 계열은 서로 직교합니다 — 어느 쪽도 다른 쪽을 알지 못합니다 — 그래서 "이 풀을 매일 04:30에 리셋하되, 그 외에는 평소대로 충전되게 한다"는 두 타입 중 하나가 제공해야 할 기능이 아니라 그냥 평범한 호출부 코드가 됩니다.

```csharp
using SsalKit.Timekeeping;

var dailyReset = RecurrenceSchedule.Daily(new TimeOnly(4, 30));

if (dailyReset.HasCrossed(player.LastStaminaReset, now))
{
    var boundary = dailyReset.PreviousBoundary(now);
    player.Stamina = player.Stamina.Refill(boundary);
    player.LastStaminaReset = boundary;
}
```

## TickSchedule

`TickSchedule<TEvent>`는 세 번째 종류의 질문에 답합니다 — "달력 경계가 지났는가"(`RecurrenceSchedule`)도, "다음 충전까지 얼마나 남았는가"(`Cooldown` / `RechargePool`)도 아니라, "이 논리적 시뮬레이션 틱에 예정된 이벤트 중 어느 것이 지금 도달했는가"입니다. 틱은 벽시계가 아닙니다 — 시뮬레이션 루프가 이미 세고 있는 `long`일 뿐입니다 — 그리고 스케줄은 넘겨받은 이벤트 *값*만 저장할 뿐 delegate는 저장하지 않으므로, 저장하고 복원한 뒤 같은 `Add`/`PopDue` 호출을 어디서 재생하든 정확히 같은 디스패치 순서가 재현됩니다.

### 빠른 시작: TickSchedule

```csharp
using SsalKit.Timekeeping;

var schedule = TickSchedule<string>.Empty
    .Add("boss-respawn", dueTick: 1800)
    .Add("wave-2", dueTick: 1800);

// ... 시뮬레이션 루프가 한 틱씩 진행한다 ...
var due = schedule.PopDue(currentTick: 1800, out schedule);

foreach (var entry in due)
{
    Dispatch(entry.Event);   // "boss-respawn"이 무엇을 뜻하는지, 그리고 실행은 호출자의 코드가 결정
}
// due에는 두 엔트리가 모두 담기고, 같은 틱이므로 삽입 순서대로 "boss-respawn" 다음 "wave-2"이다.
```

`TickSchedule`은 그 자체로 아무것도 실행하지 않습니다 — `RecurrenceSchedule`과 마찬가지로 오직 *언제*에만 답합니다. 이벤트가 무엇을 뜻하는지 결정하고 실행하는 것은 전적으로 호출자의 몫입니다. 이벤트 값은 enum, id, record 등 직렬화기가 왕복시킬 수 있는 무엇이든 될 수 있지만, delegate만은 안 됩니다. 콜백은 값과 달리 세이브 파일을 살아남지 못하기 때문입니다.

DST를 포함해 위 내용을 전부 실행해 볼 수 있는 예제는 — 전투 타임라인, 공백 이후 캐치업, 반복되는 웨이브 스포너, 세이브/복원 왕복 — [samples/SsalKit.Timekeeping.Sample](https://github.com/ssalkit/ssalkit/tree/main/samples/SsalKit.Timekeeping.Sample)의 `tickschedule` 그룹에 있습니다.

### API 개요: TickSchedule

#### `TickSchedule<TEvent>`

| 멤버 | 용도 |
|---|---|
| `static TickSchedule<TEvent> Empty` | 빈 스케줄. `default(TickSchedule<TEvent>)`와 동일 — 아래 [결정성](#결정성-tickschedule) 참고. |
| `Entries` | 저장(삽입 후 제거) 순서의 엔트리들 — `NextSequence`와 함께 직렬화 표면을 이룸. 그 자체로는 아무 의미도 없음(결정성 참고). |
| `NextSequence` | 다음 `Add`가 배정할 `Sequence`. 빈 스케줄에서는 `0`으로 시작. |
| `Count` / `IsEmpty` | 대기 중인 엔트리 개수, 그리고 하나도 없는지 여부. |
| `NextDueTick` | 대기 중인 엔트리 중 가장 작은 `DueTick`, 비어 있으면 `null` — 다음 `PopDue`가 뭔가를 반환하기 전까지 루프가 얼마나 빨리감기할 수 있는지. |
| `Add(TEvent event, long dueTick)` | `dueTick`에 예정된 새 엔트리를 추가(임의의 `long` 가능, "지금" 이전 값 포함 — 다음 `PopDue`에서 즉시 도달함). |
| `PopDue(long currentTick, out TickSchedule<TEvent> updated)` | `DueTick <= currentTick`인 엔트리 전부를 `(DueTick, Sequence)` 오름차순으로 제거하고 반환 — 경계는 포함. 도달한 것이 없으면 빈 배열과 `updated == this`; 이 메서드는 절대 실패하지 않음. |
| `RemoveAll(TEvent event)` | `event`와 일치하는(`EqualityComparer<TEvent>.Default`) 엔트리 전부 제거 — 예약된 이벤트를 값으로 취소. |

#### `TickScheduleEntry<TEvent>`

| 멤버 | 용도 |
|---|---|
| `DueTick` | 이 엔트리가 도달하는 논리적 틱. |
| `Sequence` | `Add`가 배정한 삽입 순서 — 같은 `DueTick`을 공유하는 엔트리 사이의 FIFO tie-break. |
| `Event` | `Add`에 넘겨진 값. `PopDue`가 실행되지 않은 채 그대로 돌려줌. |

### 결정성: TickSchedule

모든 것이 하나의 규칙에서 나오며, `RecurrenceSchedule`의 DST 규칙 및 `Cooldown`의 경계 포함 규칙과 동일한 지위의 영구 버전 계약입니다.

> **디스패치 순서는 `(DueTick 오름차순, Sequence 오름차순)`이며, 그 밖의 어떤 것도 아니다 — 엔트리가 실제로 어떤 순서로 저장돼 있든 무관하다.**

```csharp
var a = TickSchedule<string>.Empty.Add("x", 10).Add("y", 10);
var b = a; // 같은 엔트리를 어떤 저장 순서로 갖고 있든, 동일하게 팝된다

a.PopDue(10, out _).SequenceEqual(b.PopDue(10, out _));   // 언제나 true
```

- `PopDue`의 경계는 포함입니다. 틱 1800에 예약된 이벤트는 `PopDue(1801, ...)`에서만이 아니라 `PopDue(1800, ...)`부터 도달합니다 — `RecurrenceSchedule`이 달력 경계에 쓰는 "경계는 자신이 여는 것에 속한다"와 같은 관례입니다.
- `Add`는 추가만 할 뿐 `Entries`를 절대 정렬하지 않습니다. 모든 정렬은 필요한 순간, 즉 `PopDue` 내부에서 일어납니다. 유지해야 할 정렬 불변식이 없다는 것은 깨질 불변식도 없다는 뜻입니다 — 역직렬화기가 `Entries`를 임의 순서로(손으로 편집한 세이브, 손상된 페이로드) 배치해도 같은 `PopDue` 결과가 나옵니다. 위 규칙이 애초에 저장 순서를 참조하지 않기 때문입니다.
- 복잡도 트레이드오프는 숨기지 않고 명시합니다 — 아래 [복잡도](#복잡도-tickschedule) 참고.
- `default(TickSchedule<TEvent>)`는 **합법적인 빈 스케줄**입니다 — `default(Cooldown)`과 같은 지위입니다. `Empty`는 정확히 이 값입니다. 모든 멤버가 이를 엔트리 0개로 취급하므로, `EnsureValid` 가드가 거부할 불능 상태가 존재하지 않습니다.
- `NextSequence`는 `Add`마다 checked 산술로 하나씩 증가합니다. `NextSequence`가 `long.MaxValue`에 도달하면 `Add`는 조용히 감싸 중복된 `Sequence`를 만드는 대신 `OverflowException`을 던집니다 — 실질적인 이벤트 발생률로는 도달할 일이 없는 경우입니다.

### 캐치업과 반복 이벤트

`PopDue`는 마지막 호출 이후 몇 틱이 지났든 대기 중인 엔트리 전체를 스캔하므로, 재시작이나 오프라인 공백 이후 "따라잡기"는 특수한 경우가 아니라 단지 더 큰 `currentTick`으로 같은 호출을 하는 것뿐입니다.

```csharp
// 세이브가 마지막으로 저장된 시점은 틱 400, 프로세스는 틱 1000에 재시작한다.
var due = schedule.PopDue(currentTick: 1000, out schedule);
// due에는 DueTick <= 1000인 엔트리가 (DueTick, Sequence) 순서로 전부 담긴다 -- 재생이 필요 없다.
```

v1에는 내장된 반복 스케줄이 없습니다. 반복 이벤트는 한 줄이면 됩니다 — 현재 발생이 팝되는 순간 다음 발생을 위해 같은 이벤트를 다시 `Add`하면 됩니다.

```csharp
foreach (var entry in schedule.PopDue(currentTick, out schedule))
{
    if (entry.Event is "wave")
    {
        schedule = schedule.Add(entry.Event, entry.DueTick + WaveInterval);   // 다음 웨이브를 예약
    }
}
```

### 직렬화: TickSchedule

`Entries`와 `NextSequence`가 직렬화 표면 전체입니다 — 둘 다 public `init` 속성이므로, System.Text.Json(또는 그 밖의 무엇이든)이 커스텀 컨버터 없이 스케줄을 왕복시킵니다. 디스패치 순서가 저장 순서에 의존하지 않으므로(위 결정성 참고), 세이브 파일에서 복원된 스케줄은 역직렬화기가 `Entries`를 어떤 순서로 재구성했든 원본이 팝했을 것과 정확히 같은 순서로 팝합니다.

손상되거나 손으로 편집된 페이로드는 여전히 중복된 `Sequence` 값을 가진 스케줄이나, 기존 엔트리의 `Sequence`보다 뒤로 후퇴한 `NextSequence`를 만들어낼 수 있습니다. 그런 경우에도 `PopDue`는 완전히 결정적입니다. 남은 동점을 각 엔트리의 저장 위치를 구현 전용 세 번째 정렬 키로 삼아 깨기 때문에, *관측 가능한* 계약은 `Add`가 실제로 만들어낼 수 있었던 어떤 페이로드에 대해서도 정확히 `(DueTick, Sequence)` 그대로입니다.

### 복잡도: TickSchedule

| 멤버 | 비용 |
|---|---|
| `Add` | 현재 엔트리 개수에 대해 `O(n)` (`ImmutableArray<T>.Add`가 늘 하는 배킹 배열 복사) |
| `PopDue` | `O(n + k log k)` — due 부분집합을 골라내는 전체 스캔(`n`), 그 부분집합만 정렬(`k`) |
| `RemoveAll`, `NextDueTick` | `O(n)` |
| `Count`, `IsEmpty` | `O(1)` — 정규화된 `Entries`의 `Length`/`IsEmpty`를 바로 읽음 |

`Add`와 `PopDue` 모두 게임·시뮬레이션 규모의 엔트리 개수(수백에서 수천 단위 초반)를 겨냥하고 있어, 이론상 최적인 우선순위 큐와의 차이가 추가 복잡도를 들일 가치가 없습니다 — 그리고 공개 계약이 이 특정 구현이 아니라 저장 순서 비의존성이므로, 이후 릴리스가 호출부를 깨지 않고 내부 표현을 바꿀 수도 있습니다.

### `TickSchedule`을 비교하거나 확장하기 전에 알아둘 두 가지

- **`==`는 저장 순서를 비교하지, 논리적 내용을 비교하지 않습니다.** 같은 엔트리를 다른 `Entries` 순서로 담은 두 스케줄은 `PopDue`가 동일하게 취급하더라도 `==`로는 다르게 나옵니다 — 게다가 `ImmutableArray<T>`의 자체 동등성은 배킹 배열의 *식별자*를 비교하므로, 같은 엔트리를 같은 순서로 각각 `Add`해 만든 두 스케줄조차 다르게 나올 수 있습니다. `TickSchedule` 값을 직접 비교하는 대신 `PopDue` 결과(또는 평범한 리스트로 변환한 `Entries`)를 비교하십시오.
- **`TimeProvider` 오버로드는 없으며, 앞으로도 없을 것입니다.** 논리적 틱은 벽시계 값이 아닙니다. `TimeProvider.GetUtcNow()`를 읽는 당의정을 만들어봐야 애초에 틱 번호가 나오지 않습니다. `currentTick`은 여러분의 시뮬레이션이 이미 틱을 세는 방식 그대로 진행시키십시오.

## TickCooldown

`TickCooldown`은 `Cooldown`을 틱 축으로 옮긴 것입니다. 같은 불변 `record struct`, 같은 `(상태, 틱)`의 순수 함수 형태, 같은 경계 포함 규칙을 — 벽시계 경과 시간이 아니라 시뮬레이션이 이미 세고 있는 `long`으로 잽니다. 네 구성 요소 중 무엇이 필요한지는 한 줄로 갈립니다.

> **벽시계 쿨다운은 `Cooldown`, 틱에 예약된 이벤트는 `TickSchedule`, 틱 쿨다운은 `TickCooldown`.**

"이 대시는 지금부터 300틱 뒤에 다시 쓸 수 있다"를 고정 틱레이트 루프에서 표현하려면 지금까지는 손으로 짜야 했습니다. 경과 시간 가족이 `DateTimeOffset`만 말했기 때문인데, 쿨다운에게 질문 하나 하자고 틱을 시각으로 변환하는 것은 일부러 벽시계를 걷어낸 시뮬레이션에 벽시계를 도로 집어넣는 일입니다.

### 빠른 시작: TickCooldown

```csharp
using SsalKit.Timekeeping;

// 300틱짜리 대시 쿨다운. 생성된 틱에 곧바로 사용 가능하다.
var dash = TickCooldown.Create(durationTicks: 300, asOfTick: currentTick);

if (dash.TryUse(currentTick, out var updated))
{
    player.DashCooldown = updated;   // 이 값을 저장소에 다시 저장
}

long ticksLeft = dash.Remaining(currentTick);
bool ready = dash.IsReady(currentTick);
```

상태는 `(DurationTicks, ReadyAtTick)`뿐이므로 세이브 파일이든 DB 컬럼이든 스냅샷이든 쓴 그대로 왕복하고, 다음 `IsReady` 호출은 저장된 구조체와 그때 넘긴 틱만으로 전부를 다시 유도합니다. 건너뛴 틱 구간을 따라잡는 것도 특수한 경우가 아닙니다 — 더 큰 틱으로 같은 질의를 한 번 하면 되고, 재생할 것이 없습니다.

### API 개요: TickCooldown

| 멤버 | 용도 |
|---|---|
| `static TickCooldown Create(long durationTicks, long asOfTick)` | 즉시 사용 가능한 쿨다운. `durationTicks`는 이후 `TryUse`가 걸 대기 길이. `durationTicks < 0`이면 `ArgumentOutOfRangeException`, `0`은 합법이며 `asOfTick` 이후의 모든 틱에서 준비된 쿨다운이 됨. 산술을 전혀 하지 않으므로 여기서 틱이 오버플로할 수 없음. |
| `IsReady(long asOfTick)` | `asOfTick >= ReadyAtTick`. |
| `Remaining(long asOfTick)` | `ReadyAtTick - asOfTick`을 `[0, long.MaxValue]`로 클램프한 값. 음수가 되지 않고, 절대 던지지 않음 — 아래 [오버플로](#오버플로-tickcooldown) 참고. |
| `TryUse(long asOfTick, out TickCooldown updated)` | 성공하면 `DurationTicks`만큼의 새 대기를 시작(`ReadyAtTick = asOfTick + DurationTicks`). 실패하면 `updated`는 이 인스턴스 그대로 — 되받아 대입해도 언제나 안전. |
| `Reset(long asOfTick)` | 남은 대기를 버리고 `asOfTick`에 다시 즉시 사용 가능하게 함. |
| `DurationTicks` / `ReadyAtTick` | 설정된 대기 길이(틱), 그리고 쿨다운이 다시 사용 가능해지는 틱. |

**이 타입에게 틱은 불투명합니다.** 임의의 `long`이 합법적인 `ReadyAtTick`·`asOfTick`이며 음수도 포함됩니다 — 이 타입은 틱을 비교하고 거기에 `DurationTicks`를 더할 뿐, 의미를 부여하지 않습니다. 틱과 벽시계 사이를 어느 방향으로도 변환하지 않으며, `TickSchedule`과 똑같이 **`TimeProvider` 오버로드가 없고 앞으로도 없습니다** — 논리적 틱은 시계 값이 아니기 때문입니다.

틱이 거꾸로 가는 것도 예외가 아니라 총함수입니다. 앞서 쓴 것보다 이른 `asOfTick`에는 정직하게 답하고, `TryUse`는 던지는 대신 `false`를 돌려줍니다.

### 경계 시맨틱: TickCooldown

패키지의 단일 경계 규칙을 틱 축에 적용한 것으로, `Cooldown`·`RecurrenceSchedule`에서와 같은 영구 버전 계약의 지위를 가집니다. 이유도 같습니다. 이 상태는 저장되므로, 저장된 `ReadyAtTick`을 깨뜨리지 않고서는 릴리스 사이에 비교의 의미가 달라질 수 없습니다.

> **쿨다운은 완료되는 틱에 사용 가능하며, 그 이후부터만 사용 가능한 것이 아니다.**

```csharp
cooldown.IsReady(cooldown.ReadyAtTick);     // true
cooldown.Remaining(cooldown.ReadyAtTick);   // 0
```

### `default(TickCooldown)`과 음수 틱 도메인

`default(TickCooldown)`은 **합법적인** 값이며 `TickCooldown.Create(0, 0)`과 정확히 같습니다 — 호출자가 의도적으로 구성할 수도 있는 값이므로, 어떤 멤버도 이를 가드하지 않습니다. 다만 이 말이 뜻하지 *않는* 것에 주의하십시오.

```csharp
default(TickCooldown) == TickCooldown.Create(0, 0);   // true
default(TickCooldown).IsReady(0);                     // true  -- 틱 0부터(포함) 준비됨
default(TickCooldown).IsReady(-1);                    // false -- 그 이전에는 준비되지 않음
```

`ReadyAt`이 `DateTimeOffset.MinValue`라 전 시간축에서 준비 상태인 `default(Cooldown)`과 달리, 이 타입의 default는 틱 `0`부터만 준비됩니다. `0`은 `long`의 default이지만 최솟값은 아니기 때문입니다. 이는 실수가 아니라 두 도메인의 실제 차이이므로, 감추는 대신 문서화하고 테스트로 고정했습니다 — 그리고 틱이 `0`에서 시작하는 압도적 다수의 시뮬레이션에서는 관측되지 않는 차이입니다.

음수 틱까지 포함해 *전* 틱 도메인에서 준비된 쿨다운이 필요하면 범위의 바닥에서 구성하십시오. 표현 가능한 모든 틱이 `long.MinValue` 이상이므로 비교만으로 그 성질이 나오며, 타입 쪽의 특별한 지원이 필요하지 않습니다.

```csharp
var alwaysReady = TickCooldown.Create(durationTicks, long.MinValue);   // 모든 틱에서 준비됨
```

이를 위한 `AlwaysReady` 정적 프로퍼티는 의도적으로 두지 않았습니다. `TryUse` 한 번이면 `ReadyAtTick`이 사용 틱으로 전진하므로, 그 이름은 자기 수명을 과약속하게 됩니다.

### 오버플로: TickCooldown

`Create`와 `Reset`은 `ReadyAtTick`을 *대입*할 뿐이므로, 산술은 `TryUse`의 성공 경로와 `Remaining`의 미준비 경로 두 곳뿐입니다 — 그리고 범위를 벗어난 결과를 처분하는 방식이 서로 다릅니다. 두 처분 모두 계약입니다.

| 지점 | 산술 | 동작 |
|---|---|---|
| `TryUse`, 성공 시 | `asOfTick + DurationTicks` | **`OverflowException`을 던짐**(checked). 쿨다운을 먼 과거로 감아버리지 않음. `updated`에는 아무것도 대입되지 않으므로 호출자의 값은 그대로 남음. |
| `Remaining`, 미준비 시 | `ReadyAtTick - asOfTick` | **절대 던지지 않음** — 참 차이를 `[0, long.MaxValue]`로 클램프. |

`TryUse`는 자신의 `long` 카운터를 같은 방식으로 지키는 `TickSchedule.Add`와 짝을 이룹니다. 여기는 틱 도메인이고, 그 도메인에서 `long` 산술이 내는 예외가 `OverflowException`입니다 — `Cooldown`이 자기 도메인에서 `DateTimeOffset` 산술이 내는 예외를 그대로 표면화하는 것과 같습니다.

`Remaining`이 대신 클램프하는 이유는, `ReadyAtTick`이 `long.MaxValue`인 상태가 "사실상 영원히 미준비"를 뜻하는 완전히 합법적인 sentinel이고(임의의 `long`이 합법적인 틱이므로), 그것을 음수 틱에서 재면 `long`이 담을 수 없는 폭의 차이를 묻게 되기 때문입니다. 여기서 던지면 합법적인 `(상태, 틱)` 쌍에 대해 총함수성을 잃습니다. 클램프는 "준비됨"으로 오독될 감긴 음수와 달리 방향과 크기가 정직합니다.

```csharp
var neverReady = new TickCooldown { DurationTicks = 0, ReadyAtTick = long.MaxValue };

neverReady.Remaining(1);    // long.MaxValue - 1  -- 정확값
neverReady.Remaining(0);    // long.MaxValue      -- 정확값이자 경계 그 자체
neverReady.Remaining(-1);   // long.MaxValue      -- 감긴 것이 아니라 클램프된 값
```

### 직렬화와 유일한 불법 상태

`DurationTicks`와 `ReadyAtTick`이 직렬화 표면 전체입니다 — public `init` 속성 둘뿐이므로 System.Text.Json(또는 그 밖의 무엇이든)이 커스텀 컨버터 없이 `TickCooldown`을 왕복시킵니다.

**음수 `DurationTicks`** 가 이 타입의 유일한 불법 상태입니다. `Create`는 이를 거부하지만 객체 이니셜라이저나 손상된 페이로드는 여전히 만들어낼 수 있고, 가드하지 않으면 성공한 `TryUse`가 `ReadyAtTick`을 *뒤로* 당겨 쿨다운을 조용히 무력화합니다. 그래서 `DurationTicks`가 음수이면 모든 멤버가 `InvalidOperationException`을 던집니다 — `Cooldown`이 자신의 음수 `Duration`을 가드하는 방식 그대로입니다.

| 조건 | 동작 |
|---|---|
| `durationTicks < 0`으로 `Create` | `ArgumentOutOfRangeException` |
| `durationTicks == 0`으로 `Create` | 합법 — `ReadyAtTick` 이후의 모든 틱에서 준비된 퇴화형 쿨다운 |
| `init`이나 역직렬화로 들어온 음수 `DurationTicks` | 모든 멤버가 `InvalidOperationException`을 던짐 |
| `default(TickCooldown)` (손상되거나 잘린 역직렬화 페이로드 포함) | 합법 — `TickCooldown.Create(0, 0)`과 정확히 같게 동작 |
| 틱이 거꾸로 가는 경우 | 예외 없음 — 정직하게 답하고 `TryUse`는 `false`를 돌려줌 |
| `asOfTick + DurationTicks`가 `long` 범위를 벗어나는 `TryUse` | `OverflowException` |
| 참 차이가 `long.MaxValue`를 넘는 `Remaining` | 예외 없음 — `long.MaxValue`로 클램프 |

### `TickSchedule`과 조합하기

두 틱 축 타입은 서로 직교합니다 — 어느 쪽도 상대를 알지 못하므로, 루프 카운터 하나로 둘을 함께 굴리는 것은 평범한 호출부 코드입니다.

```csharp
using SsalKit.Timekeeping;

for (long tick = world.LastTick + 1; tick <= currentTick; tick++)
{
    foreach (var entry in world.Schedule.PopDue(tick, out world.Schedule))
    {
        Dispatch(entry.Event);   // 스케줄은 *무엇이 도달했는지* 에 답한다
    }

    if (ShouldDash(tick) && player.DashCooldown.TryUse(tick, out var updated))
    {
        player.DashCooldown = updated;   // 쿨다운은 *지금 써도 되는지* 에 답한다
    }
}
```

둘 다 같은 경계 포함 규칙을 쓰므로, 틱 `N`에 팝된 이벤트가 촉발한 `Reset(N)`은 바로 그 틱에 능력을 사용 가능하게 만듭니다. 실행해 볼 수 있는 예제는 [samples/SsalKit.Timekeeping.Sample](https://github.com/ssalkit/ssalkit/tree/main/samples/SsalKit.Timekeeping.Sample)의 `tickcooldown` 그룹에 있습니다.

## 테스트

코어 API가 시각을 인자로 받기 때문에, 대부분의 테스트에는 시계가 아예 필요 없습니다. 검증하고 싶은 시각을 그냥 넘기면 됩니다. 테스트 대상 클래스가 주입된 `TimeProvider`를 들고 있는 경우에는 가짜 시계를 넘기세요.

```csharp
// Microsoft.Extensions.TimeProvider.Testing를 쓰거나, 직접 몇 줄 작성해도 됩니다.
sealed class FixedTimeProvider(DateTimeOffset instant) : TimeProvider
{
    private readonly DateTimeOffset _utcNow = instant.ToUniversalTime();

    public override DateTimeOffset GetUtcNow() => _utcNow;
}

var clock = new FixedTimeProvider(new DateTimeOffset(2026, 7, 25, 9, 15, 0, TimeSpan.FromHours(9)));

Assert.True(dailyReset.HasCrossed(lastReset, clock));
Assert.Equal(5, dailyReset.CountBoundaries(lastLogin, clock));
```

확장 메서드는 `GetUtcNow()`만 호출하므로, 그 밖에 가짜로 만들 것이 없습니다. `Cooldown`과 `RechargePool`도 같은 방식으로 테스트합니다 — 시각을 직접 넘기거나, 같은 가짜 `TimeProvider`를 확장 메서드에 넘기세요. `TickSchedule`과 `TickCooldown`은 가짜가 아예 필요 없습니다 — 시계 값이 아니라 평범한 `long` 틱을 받기 때문입니다.

## 성능

`CountBoundaries`, `PreviousBoundary`, `NextBoundary`, `CurrentWindow`, `WindowAt`은 모두 **O(1)** 입니다. 루프가 아니라 닫힌 형태의 달력 산술이라, 10년 갭이 하루 갭과 같은 비용입니다. 2020년부터 휴면 상태인 계정의 놓친 보상을 세는 일이 3,653회 반복 순회가 되지 않고, `WindowAt(now, -1000)`이 `WindowAt(now, -1)`과 같은 비용입니다. 전부 **마이크로초 단위**에 들어오며, 그 시간을 쓰는 곳은 구간의 폭이 아니라 `TimeZoneInfo` 변환입니다. 변환이 아예 필요 없는 UTC 스케줄은 DST가 있는 존의 같은 호출보다 한 자릿수 더 저렴합니다.

O(1)이 아니고, 그럴 의도도 없는 것이 둘 있습니다.

- `EnumerateBoundaries`는 경계 개수에 비례하고, 경계마다 타임존 해석이 한 번씩 듭니다. 개수만 알면 되는 상황이라면 `CountBoundaries`를 쓰십시오.
- DST 갭이나 기준 오프셋 이음새에 놓인 경계(규칙 1과 규칙 3)를 해석하는 일은 벽시계가 스케줄 시각에 도달하는 순간을 탐색하므로, 평범한 해석의 백 배쯤 듭니다. 존마다 1년에 하루이틀 있는 일이고, 평범한 경로에는 전혀 닿지 않습니다.

`Cooldown`과 `RechargePool`의 모든 멤버도 마찬가지로 O(1)입니다 — 위의 [`FullAt` 모델](#fullat-모델)이 풀이 얼마나 오래 오프라인이었든 이를 보장하는 바로 그 원천입니다. `TickCooldown`의 모든 멤버도 같은 구조적 이유로 O(1)입니다. 상태가 `long` 두 개뿐이라, 만 틱을 건너뛴 경우도 한 틱을 진행한 경우와 똑같은 비교 한 번으로 답이 나옵니다.

`TickSchedule.Add`와 `.PopDue`는 O(1)이 아닙니다 — [복잡도: TickSchedule](#복잡도-tickschedule) 참고 — 하지만 "이 규모에서는 벤치마크할 가치가 없다"는 같은 논리가 적용됩니다. 스케줄이 한 틱만 대기했든 만 틱을 밀려서 따라잡든 비용은 같습니다.

**이 라이브러리에는 벤치마크 프로젝트를 의도적으로 동봉하지 않습니다.** `SsalKit.Randomness`와 달리 이쪽은 핫패스가 아니라 스케줄 계산 API이고, 절대 수치를 특정 기계에 못 박아 두는 일은 그 유지 비용만큼의 값어치도 하지 못하기 때문입니다. 라이브러리가 약속하는 것은 위의 복잡도이며, 이는 실행 시간 예산이 아니라 테스트 스위트의 구조적 단언으로 보장됩니다.

## 이 라이브러리의 자리

- **스케줄러가 아니고, 실행 엔진도 아닙니다.** 이 라이브러리는 시각과 틱 도달 이벤트를 계산할 뿐, 아무것도 실행하지 않습니다. 실행은 여전히 Quartz.NET, Hangfire, 또는 호스팅 서비스의 몫입니다. `RecurrenceSchedule`에는 *언제*를, `TickSchedule`에는 *무엇이 도달했는지*를 묻고, *실행*은 그쪽에 맡기세요.
- **자원 제한기가 아닙니다.** `Cooldown`과 `RechargePool`은 영속화되어 특정 시각과 비교되는 상태를 모델링합니다 — 플레이어의 능력 쿨다운, 출석 보상 풀 같은 것입니다. `System.Threading.RateLimiting`(`TokenBucketRateLimiter`, `ConcurrencyLimiter` 등)은 다른 문제를 풉니다. 재시작 이후에도 유지되거나 프로세스 간에 비교될 필요가 없는, 동시성 있는 프로세스 내 작업을 스로틀링하는 문제입니다. API 스로틀링에는 `RateLimiter`를, 상태 자체를 저장·조회·복원해야 할 때는 이 타입들을 쓰세요.
- **NodaTime과는 보완 관계입니다.** NodaTime은 달력 체계, 기간, 존 산술을 BCL보다 훨씬 철저하게 모델링합니다. 다만 "리셋 창" 개념은 없고, 이 라이브러리도 NodaTime을 대체할 생각이 없습니다. 이미 달력 작업에 NodaTime을 쓰고 있더라도, 경계 통과에 대한 질문은 BCL 타입 위에서 의존성 충돌 없이 이 라이브러리가 답합니다.
- **Cronos와도 보완 관계입니다.** Cronos는 cron 식을 파싱해 다음 발생 시각을 알려줍니다. 이 라이브러리는 cron을 파싱하지 않고 세 가지 고정된 달력 주기만 제공하지만, cron 파서가 답하지 않는 것에 답합니다. 어떤 시각이 어느 창에 속하는지, 그리고 두 시각 사이에 발생이 몇 번 있었는지입니다.

v1에서 의도적으로 제외한 범위는 cron 식, RFC 5545 반복 규칙, 영업일·휴일 달력, 개방 구간, 그리고 앵커 규칙이 별도의 설계 문제인 고정 간격("6시간마다") 반복입니다.

## 예외와 경계 사례: RecurrenceSchedule과 TimeWindow

| 조건 | 동작 |
|---|---|
| 정의되지 않은 `DayOfWeek`로 `Weekly` 호출 | `ArgumentOutOfRangeException` |
| `dayOfMonth`가 1~31 밖인 `Monthly` 호출 | `ArgumentOutOfRangeException` |
| `end`가 `start`보다 이른 `new TimeWindow(start, end)` | `ArgumentException` |
| `new TimeWindow(start, start)` | 합법. 빈 창은 아무것도 포함하지 않고 아무것과도 겹치지 않음 |
| `to <= from`인 `CountBoundaries` / `HasCrossed` / `EnumerateBoundaries` | `0` / `false` / 빈 시퀀스 — 음수가 되지 않음 |
| 표현 가능 범위를 벗어나는 `offset`으로 `WindowAt` 호출 | `ArgumentOutOfRangeException` — 조용히 감긴 창이 나오지 않음 |
| 2월의 `Monthly(31, ...)` | 28일 또는 29일로 클램프. 그 달도 정확히 하나의 경계를 가짐 |
| `null` 스케줄 또는 `null` 프로바이더로 `TimeProvider` 확장 호출 | `ArgumentNullException` |

**범위 양 끝에서 한 가지 주의할 점이 있습니다.** 경계는 `DateTime`의 범위 안에서 계산됩니다. `DateTimeOffset.MinValue`나 `MaxValue`로부터 경계 하나 거리 안쪽에 있는 `asOf`는 표현 불가능한 경계를 요구하게 되고, 내부 날짜 산술이 `ArgumentOutOfRangeException`을 던집니다. 따라서 "한 번도 본 적 없음"을 뜻하는 sentinel로 `DateTimeOffset.MinValue`를 쓰는 것은 피하는 편이 좋습니다. 영속화된 `lastSeen`이 `MinValue`라면 서기 1년 이후의 모든 경계를 보고하는 대신 예외를 던집니다. 실제 시각을 저장하거나, 검사 가능한 `null`을 쓰세요. `Cooldown`과 `RechargePool`은 [Cooldowns](#cooldowns) 아래에 별도의 예외 표를 가지고 있습니다 — 특히 `Cooldown`은 `default`/`MinValue` 유래 상태를 예외 대신 합법으로 취급하는데, 여기서의 `RecurrenceSchedule`의 주의사항과는 정반대입니다.

## 라이센스

MIT — 자세한 내용은 [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE)를 참고하세요.

---

**AI 고지:** 이 프로젝트는 AI(Claude)를 활용하여 제작되었습니다.
