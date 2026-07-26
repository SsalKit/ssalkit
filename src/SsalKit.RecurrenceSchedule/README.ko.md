[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ko.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.RecurrenceSchedule/README.md) | **한국어** | [日本語](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.RecurrenceSchedule/README.ja.md)

# SsalKit.RecurrenceSchedule

타임존을 인지하는 반복 리셋 경계(일간 / 주간 / 월간)와 반개구간 시간 창 산술을, 호출자가 넘긴 시각에 대한 순수 함수로 제공하는 라이브러리입니다. DST 처리 규칙을 영구히 고정한 계약과, 이미 시계를 들고 있는 코드를 위한 `TimeProvider` 오버로드를 함께 제공합니다. 의존성이 없습니다.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.RecurrenceSchedule.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.RecurrenceSchedule)

## 왜 SsalKit.RecurrenceSchedule인가

"마지막으로 확인한 이후로 일일 리셋이 지나갔는가?"는 일일 쿼터, 출석 보상, 결제 주기, 리포트 윈도우가 있는 코드베이스라면 어디에나 등장합니다. 겉보기에 `DateTime` 산술 두 줄이라서, 공유되는 대신 호출부마다 다시 작성되고, 그렇게 복제된 구현들이 서로 다른 답을 내놓기 시작합니다.

이 라이브러리를 뽑아낸 원형 코드베이스에는 그런 구현이 나란히 두 개 있었습니다.

- **하나는 자정 UTC와 월요일이 하드코딩**되어 있었고, 메서드 안에서 직접 `DateTime.UtcNow`를 읽었습니다. 그 위에 얹힌 어떤 코드도 머신 시계를 옮기지 않고는 테스트할 수 없었죠. 게다가 포함 규칙이 메서드마다 달랐습니다. 어떤 것은 양 끝을 모두 포함했고, 어떤 것은 날짜만 비교했습니다.
- **다른 하나는 리셋 시각을 설정할 수 있었지만**, "리셋이 지났는가"를 `from.Hour >= resetHour`로 판정했습니다. 그래서 04:15가 04:30 리셋을 지난 것으로 취급됐습니다. `4 >= 4`니까요. 설정된 스케줄의 분·초가 조용히 버려졌고, 이 버그는 4시 15분에 일일 보상이 한 번 더 지급되는 형태로만 드러났습니다.

한 코드베이스 안에, 같은 질문에 대한 서로 다른 두 개의 답. 그 사이에 25곳이 넘는 호출부와 영속화된 "마지막 리셋" 필드들이 있었습니다.

.NET 8이 `TimeProvider`를 추가하면서 "시계는 누가 소유하는가" 쪽 절반은 정리됐습니다. 하지만 반복되는 창 자체를 표현하는 타입은 여전히 없습니다. BCL에는 "이 시각이 속한 리셋 기간"이라는 개념이 없습니다. NodaTime은 달력과 타임존을 깊이 있게 모델링하지만 리셋 창 개념은 없고, Cronos는 cron 식을 파싱해 다음 발생 시각을 알려줄 뿐 창 소속 판정이나 통과 횟수는 다루지 않습니다.

SsalKit.RecurrenceSchedule은 그 빈자리를 채웁니다.

- `RecurrenceSchedule`은 달력에 정렬된 반복을 정의하고 — 매일 04:30 서울 시각, 매주 월요일 09:00 UTC, 매월 31일 — 그에 대해 물을 가치가 있는 네 가지 질문에 답합니다. `PreviousBoundary`, `NextBoundary`, `CurrentWindow`, 그리고 "자리를 비웠던 사용자"를 위한 `HasCrossed` / `CountBoundaries`입니다.
- **포함 규칙은 어디서나 하나뿐입니다.** `TimeWindow`는 반개구간 `[Start, End)`이고 포함 구간 변형이 없습니다. 그래서 연속된 창들이 타임라인을 이중 계산도 구멍도 없이 정확히 타일링합니다.
- **DST 계약은 타입의 수명 동안 고정입니다.** 경계는 영속화되므로, 존재하지 않거나 두 번 나타나는 벽시계 시각을 어떻게 해석하는지는 구현 세부사항이 아니라 버전 계약입니다.
- **모든 것이 `(스케줄, 시각)`의 순수 함수입니다.** 어떤 API도 주변 시계를 읽지 않습니다. `TimeProvider` 오버로드는 그 위에 얹은 당의정이지, 그 반대가 아닙니다.
- **의존성이 없습니다.** `PackageReference` 없이 BCL만 사용합니다.

## 설치

```bash
dotnet add package SsalKit.RecurrenceSchedule
```

## 빠른 시작

```csharp
using SsalKit.RecurrenceSchedule;

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
TimeSpan remaining = dailyReset.NextBoundary(now) - now;
```

주간·월간 주기도 같은 방식이며, 짧은 달의 마지막 날을 넘어가는 월간 스케줄은 그 달의 말일로 클램프됩니다.

```csharp
var weekly = RecurrenceSchedule.Weekly(DayOfWeek.Monday, new TimeOnly(9, 0));   // 기본값은 UTC
var monthly = RecurrenceSchedule.Monthly(31, new TimeOnly(0, 0));

monthly.NextBoundary(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
// 2026-02-28T00:00:00+00:00 -- 2월은 클램프되지만, 경계는 여전히 정확히 한 개
```

DST를 포함해 위 내용을 전부 실행해 볼 수 있는 예제는 [samples/SsalKit.RecurrenceSchedule.Sample](https://github.com/ssalkit/ssalkit/tree/main/samples/SsalKit.RecurrenceSchedule.Sample)에 있습니다.

## API 개요

### `RecurrenceSchedule`

| 멤버 | 용도 |
|---|---|
| `Daily(TimeOnly atTimeOfDay, TimeZoneInfo? timeZone = null)` | 매 달력일 1회, 지정한 벽시계 시각에. `timeZone` 기본값은 UTC. |
| `Weekly(DayOfWeek dayOfWeek, TimeOnly atTimeOfDay, TimeZoneInfo? timeZone = null)` | 매 달력주 1회, 지정한 요일에. |
| `Monthly(int dayOfMonth, TimeOnly atTimeOfDay, TimeZoneInfo? timeZone = null)` | 매 달력월 1회, `1`~`31`일에. 짧은 달은 말일로 클램프. |
| `PreviousBoundary(DateTimeOffset asOf)` | `b <= asOf`인 가장 큰 경계. `asOf` 자체가 경계면 그대로 반환. |
| `NextBoundary(DateTimeOffset asOf)` | `b > asOf`인 가장 작은 경계. 엄격한 부등호이므로 경계를 넣으면 그 다음 경계가 나옴. |
| `CurrentWindow(DateTimeOffset asOf)` | `[PreviousBoundary(asOf), NextBoundary(asOf))` — `asOf`가 속한 리셋 기간. |
| `HasCrossed(DateTimeOffset lastSeen, DateTimeOffset now)` | `lastSeen < b <= now`를 만족하는 경계 `b`의 존재 여부. |
| `CountBoundaries(DateTimeOffset lastSeen, DateTimeOffset now)` | 그런 `b`의 개수. `now <= lastSeen`이면 `0`. |

경계는 항상 **해당 날짜에 대한 스케줄 타임존의 UTC 오프셋**을 달고 돌아옵니다. 서울 스케줄이면 `+09:00`, 뉴욕 스케줄이면 `-05:00` 또는 `-04:00`, UTC 스케줄이면 `+00:00`입니다. 비교에는 영향이 없지만(`DateTimeOffset`은 절대 시각을 비교합니다), 경계를 포맷하면 그 스케줄이 정의된 현지 벽시계 시각이 그대로 보입니다.

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

`RecurrenceScheduleTimeProviderExtensions`는 `TimeProvider.GetUtcNow()`를 전달하는 네 개의 오버로드를 제공합니다.

| 확장 | 동등한 호출 |
|---|---|
| `schedule.CurrentWindow(timeProvider)` | `schedule.CurrentWindow(timeProvider.GetUtcNow())` |
| `schedule.NextBoundary(timeProvider)` | `schedule.NextBoundary(timeProvider.GetUtcNow())` |
| `schedule.HasCrossed(lastSeen, timeProvider)` | `schedule.HasCrossed(lastSeen, timeProvider.GetUtcNow())` |
| `schedule.CountBoundaries(lastSeen, timeProvider)` | `schedule.CountBoundaries(lastSeen, timeProvider.GetUtcNow())` |

`TimeProvider`는 .NET 8부터 BCL에 포함되므로, 이 오버로드를 써도 패키지 의존성은 늘지 않습니다.

## 경계 의미 계약

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

스케줄 시각은 해당 타임존의 **벽시계** 시각이고, 벽시계가 오작동하는 방식은 정확히 두 가지입니다. 두 해석 모두 여기에 고정됩니다.

1. **존재하지 않는 스케줄 시각** — 02:30 스케줄에 대해 02:00 → 03:00으로 시계가 앞으로 뛰는 경우 — 은 **갭 직후의 첫 유효 시각**으로 이동합니다. 전이 시각 자체인 03:00이지, 03:30이 아닙니다. 경계가 사라지지 않으므로 일간 스케줄은 그날에도 정확히 하나의 경계를 갖고, `CountBoundaries`는 여전히 경과한 일수와 같습니다.
2. **두 번 나타나는 스케줄 시각** — 01:30 스케줄에 대해 02:00 → 01:00으로 시계가 되돌아가는 경우 — 는 **첫 번째 발생**, 즉 전이 이전의 (더 큰) UTC 오프셋 쪽으로 해석됩니다. 그날 스케줄이 두 번 발동하지 않습니다.
3. **그 밖의 모든 벽시계 시각**은 해당 날짜의 존 오프셋을 사용합니다. 따라서 경계는 계절에 따라 떠내려가지 않고 의도한 현지 시각에 그대로 머뭅니다.

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

이 규칙들은 **버전 계약이며, 패치나 마이너 릴리스에서 절대 변경되지 않습니다.** 경계는 영속화됩니다 — "이 플레이어가 마지막으로 본 리셋" — 그리고 저장된 시각과 재계산한 시각을 비교하는 일은 계산이 절대 움직이지 않을 때만 성립합니다. 시드가 있는 PRNG의 알고리즘 계약과 정확히 같습니다. 다른 해석 정책이 필요해진다면 이 타입의 동작을 바꾸는 대신 새 타입으로 출시됩니다.

이 규칙들은 주기와도 무관합니다. 전이가 일어나는 날에 경계가 놓이는 주간·월간 스케줄도 동일하게 해석되며, 전이 폭이 한 시간이 아닌 존에서도 성립합니다(`Australia/Lord_Howe`는 30분 이동하며, 그곳의 02:15 스케줄은 02:45가 아니라 전이 시각인 02:30으로 해석됩니다).

타임존 식별자는 `TimeZoneInfo`의 해석을 그대로 따릅니다. .NET 6 이상에서는 ICU만 사용 가능하다면 `TimeZoneInfo.FindSystemTimeZoneById`가 Windows에서도 `America/New_York` 같은 IANA 식별자를 받습니다. 주의할 경우는 globalization-invariant 모드로 동작하는 Windows 환경입니다.

## `TimeWindow`: 포함 규칙은 하나

반개구간 `[Start, End)`가 이 타입이 제공하는 유일한 규칙이고, 포함 구간 변형은 의도적으로 없습니다. 두 규칙을 섞는 코드베이스는 어떤 메서드 쌍에서는 공유 끝점이 이중 계산되고, 다른 쌍에서는 같은 끝점에 구멍이 생깁니다. 원형이 서로 다른 두 답을 갖게 된 경위가 정확히 이것입니다.

```csharp
var yesterday = dailyReset.CurrentWindow(today.Start.AddTicks(-1));

yesterday.End == today.Start;          // true  -- 정확히 맞닿는다
yesterday.Overlaps(today);             // false -- 맞닿는 것은 겹치는 것이 아니다
yesterday.Contains(today.Start);       // false -- 공유된 시각은 오늘 창의 것
today.Contains(today.Start);           // true

today.Intersect(maintenanceWindow);    // 공유 구간, 없으면 null
today.Clamp(overrunInstant);           // == today.End: "이 창 안에서 어디까지 왔는가"
```

**오프셋은 표시용일 뿐입니다.** `DateTimeOffset`은 타임라인 위의 한 점을 가리키고, `TimeWindow`의 연산도 값 동등성도 모두 그 점을 비교합니다. `2026-07-25T04:30:00+09:00`과 `2026-07-24T19:30:00+00:00`은 같은 순간이므로, 어느 쪽 표기로 만든 창이든 `==`이고 동일하게 동작합니다. 오프셋이 `Start`, `End`, `ToString()`에 그대로 보존되는 것은 순전히, 스케줄이 만들어 낸 창이 자기 타임존의 현지 시각을 그대로 보여 주도록 하기 위해서입니다.

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

확장 메서드는 `GetUtcNow()`만 호출하므로, 그 밖에 가짜로 만들 것이 없습니다.

## 성능

`CountBoundaries`는 **루프가 아니라 닫힌 형태의 달력 산술**입니다. 10년 갭이 하루 갭과 같은 비용이므로, 2020년부터 휴면 상태인 계정의 놓친 보상을 세는 일이 3,653회 반복 순회가 되지 않습니다.

.NET 10, AMD Ryzen 9 3950X, Windows 11에서 측정한 값입니다.

| 호출 | 평균 |
|---|---:|
| `America/New_York`에서 10년치 `CountBoundaries` (경계 3,653개) | ~0.9 μs |
| `America/New_York`에서 하루치 `CountBoundaries` (경계 1개) | ~0.5 μs |
| UTC에서 10년치 `CountBoundaries` | ~0.05 μs |

남는 비용은 구간의 폭이 아니라 `TimeZoneInfo` 변환입니다. UTC 행이 같은 10년 구간의 DST 존보다 20배 빠르고, 그 존의 10년 행과 하루 행이 2배 이내 차이인 이유가 그것입니다. 이 라이브러리에는 벤치마크 프로젝트가 동봉되지 않습니다. 핫패스가 아닌 스케줄 계산 API의 참고용 수치입니다.

## 이 라이브러리의 자리

- **스케줄러가 아닙니다.** 이 라이브러리는 시각을 계산할 뿐, 아무것도 실행하지 않습니다. 실행은 여전히 Quartz.NET, Hangfire, 또는 호스팅 서비스의 몫입니다. `RecurrenceSchedule`에는 *언제*를 묻고, *실행*은 그쪽에 맡기세요.
- **NodaTime과는 보완 관계입니다.** NodaTime은 달력 체계, 기간, 존 산술을 BCL보다 훨씬 철저하게 모델링합니다. 다만 "리셋 창" 개념은 없고, 이 라이브러리도 NodaTime을 대체할 생각이 없습니다. 이미 달력 작업에 NodaTime을 쓰고 있더라도, 경계 통과에 대한 질문은 BCL 타입 위에서 의존성 충돌 없이 이 라이브러리가 답합니다.
- **Cronos와도 보완 관계입니다.** Cronos는 cron 식을 파싱해 다음 발생 시각을 알려줍니다. 이 라이브러리는 cron을 파싱하지 않고 세 가지 고정된 달력 주기만 제공하지만, cron 파서가 답하지 않는 것에 답합니다. 어떤 시각이 어느 창에 속하는지, 그리고 두 시각 사이에 발생이 몇 번 있었는지입니다.

v1에서 의도적으로 제외한 범위는 cron 식, RFC 5545 반복 규칙, 영업일·휴일 달력, 개방 구간, 그리고 앵커 규칙이 별도의 설계 문제인 고정 간격("6시간마다") 반복입니다.

## 예외와 경계 사례

| 조건 | 동작 |
|---|---|
| 정의되지 않은 `DayOfWeek`로 `Weekly` 호출 | `ArgumentOutOfRangeException` |
| `dayOfMonth`가 1~31 밖인 `Monthly` 호출 | `ArgumentOutOfRangeException` |
| `end`가 `start`보다 이른 `new TimeWindow(start, end)` | `ArgumentException` |
| `new TimeWindow(start, start)` | 합법. 빈 창은 아무것도 포함하지 않고 아무것과도 겹치지 않음 |
| `now <= lastSeen`인 `CountBoundaries` / `HasCrossed` | `0` / `false` — 음수가 되지 않음 |
| 2월의 `Monthly(31, ...)` | 28일 또는 29일로 클램프. 그 달도 정확히 하나의 경계를 가짐 |
| `null` 스케줄 또는 `null` 프로바이더로 `TimeProvider` 확장 호출 | `ArgumentNullException` |

**범위 양 끝에서 한 가지 주의할 점이 있습니다.** 경계는 `DateTime`의 범위 안에서 계산됩니다. `DateTimeOffset.MinValue`나 `MaxValue`로부터 경계 하나 거리 안쪽에 있는 `asOf`는 표현 불가능한 경계를 요구하게 되고, 내부 날짜 산술이 `ArgumentOutOfRangeException`을 던집니다. 따라서 "한 번도 본 적 없음"을 뜻하는 sentinel로 `DateTimeOffset.MinValue`를 쓰는 것은 피하는 편이 좋습니다. 영속화된 `lastSeen`이 `MinValue`라면 서기 1년 이후의 모든 경계를 보고하는 대신 예외를 던집니다. 실제 시각을 저장하거나, 검사 가능한 `null`을 쓰세요.

## 라이센스

MIT — 자세한 내용은 [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE)를 참고하세요.

---

**AI 고지:** 이 프로젝트는 AI(Claude)를 활용하여 제작되었습니다.
