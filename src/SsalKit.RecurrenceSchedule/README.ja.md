[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ja.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.RecurrenceSchedule/README.md) | [한국어](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.RecurrenceSchedule/README.ko.md) | **日本語**

# SsalKit.RecurrenceSchedule

タイムゾーンを意識した繰り返しリセット境界（日次 / 週次 / 月次）と、半開区間の時間ウィンドウ演算を、呼び出し側が渡した時刻に対する純粋関数として提供するライブラリです。恒久的に固定された夏時間（DST）の契約と、すでに時計を保持しているコードのための `TimeProvider` オーバーロードを備えています。依存関係はありません。
[![NuGet](https://img.shields.io/nuget/v/SsalKit.RecurrenceSchedule.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.RecurrenceSchedule)

## なぜ SsalKit.RecurrenceSchedule なのか

「前回確認したときから日次リセットは過ぎたか？」という問いは、日次クォータ、ログインボーナス、課金サイクル、レポートウィンドウを持つコードベースなら必ず現れます。見た目は `DateTime` 演算 2 行なので、共有される代わりに呼び出し箇所ごとに書き直され、その複製同士が食い違った答えを返し始めます。

このライブラリの原型となったコードベースには、そうした実装が 2 つ並んで存在していました。

- **1 つは深夜 0 時 UTC と月曜日がハードコード**されており、メソッド内部で直接 `DateTime.UtcNow` を読んでいました。その上に載るコードは、マシンの時計を動かさない限りテストできません。しかも境界の包含ルールがメソッドごとに違い、両端を含むものもあれば、カレンダー日付だけを比較するものもありました。
- **もう 1 つはリセット時刻を設定可能でしたが**、「リセットは過ぎたか」を `from.Hour >= resetHour` で判定していました。そのため 04:15 が 04:30 のリセットを過ぎたものとして扱われました。`4 >= 4` だからです。設定したスケジュールの分と秒は黙って捨てられ、このバグは 4 時 15 分に日次報酬がもう一度配られるという形でしか表面化しませんでした。

1 つのコードベースの中に、同じ問いに対する 2 つの異なる答え。その間には 25 か所を超える呼び出し箇所と、永続化された「最後のリセット」フィールドがありました。

.NET 8 で `TimeProvider` が追加され、「時計を誰が所有するか」という半分は解決しました。しかし、繰り返しウィンドウそのものを表す型は依然としてありません。BCL には「この時刻が属するリセット期間」という概念がないのです。NodaTime はカレンダーとタイムゾーンを深くモデル化しますがリセットウィンドウの概念はなく、Cronos は cron 式を解析して次の発生時刻を返すだけで、ウィンドウ所属の判定や通過回数は扱いません。

SsalKit.RecurrenceSchedule はその空白を埋めます。

- **`RecurrenceSchedule`** はカレンダーに整列した繰り返しを定義し（毎日 04:30 ソウル時間、毎週月曜 09:00 UTC、毎月 31 日）、それについて問う価値のある 4 つの質問に答えます。`PreviousBoundary`、`NextBoundary`、`CurrentWindow`、そして「しばらく離れていたユーザー」のための `HasCrossed` / `CountBoundaries` です。
- **包含ルールはどこでも 1 つだけ。** `TimeWindow` は半開区間 `[Start, End)` であり、閉区間の亜種はありません。そのため連続するウィンドウが、二重カウントも隙間もなくタイムラインを正確に敷き詰めます。
- **DST の契約は型の寿命にわたって固定。** 境界は永続化されるため、存在しない、あるいは 2 度現れる壁時計時刻をどう解決するかは実装の詳細ではなく、バージョンをまたぐ約束です。
- **すべてが `(スケジュール, 時刻)` の純粋関数。** 周囲の時計を読む API はひとつもありません。`TimeProvider` オーバーロードはその上に載る糖衣であって、その逆ではありません。
- **依存関係ゼロ。** `PackageReference` なし、BCL のみです。

## インストール

```bash
dotnet add package SsalKit.RecurrenceSchedule
```

## クイックスタート

```csharp
using SsalKit.RecurrenceSchedule;

var seoul = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
var dailyReset = RecurrenceSchedule.Daily(new TimeOnly(4, 30), seoul);

// 前回確認したときから 04:30 のリセットは過ぎたか？
if (dailyReset.HasCrossed(player.LastQuotaReset, now))
{
    player.Quota = DailyQuota;
    player.LastQuotaReset = dailyReset.PreviousBoundary(now);
}

// 戻ってきたプレイヤーが取り逃した日次報酬は何回分か？（(lastSeen, now] 内の境界の数。）
int missedRewards = dailyReset.CountBoundaries(player.LastLogin, now);

// 今はどのリセット期間で、あとどれだけ残っているか？
TimeWindow today = dailyReset.CurrentWindow(now);
TimeSpan remaining = dailyReset.UntilNext(now);   // 常に正
```

`WindowAt` はオフセットで隣の期間を取り出します。`0` が今日、`-1` がその 1 つ前なので、「前日比」の数値を出すときに向いています。`EnumerateBoundaries` は区間内の境界を昇順に遅延列挙します。

```csharp
TimeWindow yesterday = dailyReset.WindowAt(now, -1);   // O(1)。-30 でもコストは同じ

foreach (var boundary in dailyReset.EnumerateBoundaries(player.LastLogin, now))
{
    // (lastSeen, now] 内の境界が昇順で、ちょうど CountBoundaries(player.LastLogin, now) 個
}
```

週次・月次の周期も同じ形で、短い月の末日を超える月次スケジュールはその月の末日にクランプされます。

```csharp
var weekly = RecurrenceSchedule.Weekly(DayOfWeek.Monday, new TimeOnly(9, 0));   // 既定は UTC
var monthly = RecurrenceSchedule.Monthly(31, new TimeOnly(0, 0));

monthly.NextBoundary(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
// 2026-02-28T00:00:00+00:00 -- 2 月はクランプされるが、境界はやはりちょうど 1 つ
```

DST を含めて以上をすべて実行できるサンプルは [samples/SsalKit.RecurrenceSchedule.Sample](https://github.com/ssalkit/ssalkit/tree/main/samples/SsalKit.RecurrenceSchedule.Sample) にあります。

## API 概要

### `RecurrenceSchedule`

| メンバー | 用途 |
|---|---|
| `Daily(TimeOnly atTimeOfDay, TimeZoneInfo? timeZone = null)` | 暦日ごとに 1 回、指定した壁時計時刻に。`timeZone` の既定値は UTC。 |
| `Weekly(DayOfWeek dayOfWeek, TimeOnly atTimeOfDay, TimeZoneInfo? timeZone = null)` | 暦週ごとに 1 回、指定した曜日に。 |
| `Monthly(int dayOfMonth, TimeOnly atTimeOfDay, TimeZoneInfo? timeZone = null)` | 暦月ごとに 1 回、`1`〜`31` 日に。短い月は末日にクランプ。 |
| `PreviousBoundary(DateTimeOffset asOf)` | `b <= asOf` を満たす最大の境界。`asOf` 自体が境界ならそのまま返す。 |
| `NextBoundary(DateTimeOffset asOf)` | `b > asOf` を満たす最小の境界。厳密な不等号なので、境界を渡すと次の境界が返る。 |
| `UntilNext(DateTimeOffset asOf)` | `NextBoundary(asOf) - asOf`。**常に正**。境界を渡すと 0 ではなくウィンドウ 1 つ分が返る。 |
| `CurrentWindow(DateTimeOffset asOf)` | `[PreviousBoundary(asOf), NextBoundary(asOf))` — `asOf` が属するリセット期間。 |
| `WindowAt(DateTimeOffset asOf, int offset)` | `offset` 個ずれたウィンドウ。`0` は `CurrentWindow(asOf)`、`-1` はその 1 つ前。O(1)。 |
| `HasCrossed(DateTimeOffset lastSeen, DateTimeOffset now)` | `lastSeen < b <= now` を満たす境界 `b` が存在するか。 |
| `CountBoundaries(DateTimeOffset lastSeen, DateTimeOffset now)` | その `b` の個数。`now <= lastSeen` なら `0`。 |
| `EnumerateBoundaries(DateTimeOffset from, DateTimeOffset to)` | その境界そのものを昇順に遅延列挙。個数は常に `CountBoundaries(from, to)` と一致。 |
| `ToString()` | 診断用の表記。`Daily 04:30 @ UTC`、`Weekly Monday 09:00 @ Asia/Seoul`、`Monthly day 31 00:00 @ America/New_York`。 |

境界は常に **その日付におけるスケジュールのタイムゾーンの UTC オフセット** を伴って返ります。ソウルのスケジュールなら `+09:00`、ニューヨークなら `-05:00` または `-04:00`、UTC なら `+00:00` です。比較には影響しません（`DateTimeOffset` は絶対時刻を比較します）が、境界を書式化すると、そのスケジュールが定義された現地の壁時計時刻がそのまま表示されます。

利便性のために追加されたメンバーについて、3 点だけ補足します。

- **`CountBoundaries` は O(1)、`EnumerateBoundaries` は O(境界の個数)です。** 両者は同じ半開区間 `(from, to]` を扱い、列挙結果の個数は常に `CountBoundaries` と一致しますが、個数のほうは閉形式の暦演算であるのに対し、列挙は境界を 1 つずつ実際に解決します。個数だけが必要なら個数を尋ねてください。列挙は遅延評価で（検証すべき引数がないので、先に検査されるものもありません）、`Take` で打ち切ったり `to` を広く取ったりできます。
- **`WindowAt` は決してラップしません。** `DateTime` の範囲外へ移動させる `offset` は、無関係な世紀のウィンドウを黙って返す代わりに `ArgumentOutOfRangeException` を投げます。演算は 64 ビットで行い、結果を範囲検査します。
- **`ToString()` はログ用であって、パース対象ではありません。** 後述の DST 規則とは違い、この書式には互換性の約束がなく、どのリリースでも改善される可能性があります。表記には invariant culture とタイムゾーンの `TimeZoneInfo.Id` を用い、時刻部分はスケジュールがその精度を持つときにだけ `HH:mm:ss`（あるいはティック精度）まで伸びます。

### `TimeWindow`

半開区間 `[Start, End)` を表す `readonly record struct` です。

| メンバー | 用途 |
|---|---|
| `new TimeWindow(DateTimeOffset start, DateTimeOffset end)` | `start == end` の空ウィンドウは合法。`start > end` は `ArgumentException`。 |
| `Start` / `End` / `Duration` | 含まれる開始、含まれない終了、そして `End - Start`（負にはならない）。 |
| `Contains(DateTimeOffset instant)` | `Start <= instant < End`。空ウィンドウでは常に `false`。 |
| `Overlaps(TimeWindow other)` | 交差が空でないか。接しているだけのウィンドウは重なって **いない**。 |
| `Intersect(TimeWindow other)` | 共有区間、なければ `null`。対称。 |
| `Clamp(DateTimeOffset instant)` | *閉* 区間 `[Start, End]` に丸めるので、終了を超えた時刻は `End` になる。 |

### `TimeProvider` 拡張

`RecurrenceScheduleTimeProviderExtensions` は、引数の全体が「今」であるメンバー 6 つについて `TimeProvider.GetUtcNow()` を転送するオーバーロードを提供します。1 回の呼び出しでちょうど 1 回だけ読むので、動いている時計に対して値が食い違うことはありません。

| 拡張 | 等価な呼び出し |
|---|---|
| `schedule.PreviousBoundary(timeProvider)` | `schedule.PreviousBoundary(timeProvider.GetUtcNow())` |
| `schedule.NextBoundary(timeProvider)` | `schedule.NextBoundary(timeProvider.GetUtcNow())` |
| `schedule.UntilNext(timeProvider)` | `schedule.UntilNext(timeProvider.GetUtcNow())` |
| `schedule.CurrentWindow(timeProvider)` | `schedule.CurrentWindow(timeProvider.GetUtcNow())` |
| `schedule.HasCrossed(lastSeen, timeProvider)` | `schedule.HasCrossed(lastSeen, timeProvider.GetUtcNow())` |
| `schedule.CountBoundaries(lastSeen, timeProvider)` | `schedule.CountBoundaries(lastSeen, timeProvider.GetUtcNow())` |

`WindowAt` と `EnumerateBoundaries` にはプロバイダー版がありません。基準となる時刻 *に加えて* 明示的な範囲を取る API なので、`timeProvider.GetUtcNow()` を自分で渡すのと比べて得るものがないからです。

`TimeProvider` は .NET 8 以降 BCL の一部なので、これらを使ってもパッケージ依存は増えません。

## 境界のセマンティクス

すべては 1 つのルールから導かれます。**境界の時刻は、それが閉じるウィンドウではなく、それが開くウィンドウに属する。**

```csharp
schedule.CurrentWindow(b).Start == b   // 任意の境界 b について
```

ここから残りが自然に決まります。

- `PreviousBoundary` は含む（`b <= asOf`）、`NextBoundary` は厳密（`b > asOf`）、`CurrentWindow` はその間の半開区間です。したがって連続するウィンドウがタイムラインを正確に敷き詰めます。2 つのウィンドウに同時に属する時刻も、どのウィンドウにも属さない時刻もありません。
- `HasCrossed(lastSeen, now)` は **半開区間 `(lastSeen, now]`** の中に境界があるかを問います。`lastSeen` 自体が境界なら、そのウィンドウはすでに見ているので何も通過していません。`now` がちょうど境界なら、たった今通過したところです。
- `CountBoundaries` は同じ `(lastSeen, now]` の境界を数えます。`HasCrossed` はちょうど `CountBoundaries(...) > 0` と等価で、最初の 1 つで打ち切る分だけ安いだけです。反転した区間（`now < lastSeen`）は負ではなく `0` を返します。

すべての比較がカレンダーのフィールドではなく時刻同士で行われるため、原型の hour 比較バグは修正されたというより **表現できません**。

```csharp
var reset = RecurrenceSchedule.Daily(new TimeOnly(4, 30));

reset.HasCrossed(At(4, 00), At(4, 15));   // false -- hour フィールド比較なら true と答えていた
reset.HasCrossed(At(4, 00), At(4, 30));   // true
```

## 夏時間（DST）の契約

スケジュール時刻はそのタイムゾーンの **壁時計** 時刻であり、壁時計が壊れる形はちょうど 2 通りです。どちらの解決もここに固定されます。

1. **存在しないスケジュール時刻** — 02:30 のスケジュールに対して時計が 02:00 → 03:00 と飛ぶ場合 — は **ギャップ直後の最初の有効な時刻** に移ります。03:30 ではなく、遷移そのものである 03:00 です。境界が失われないので、日次スケジュールはその日もちょうど 1 つの境界を持ち、`CountBoundaries` は経過日数と一致したままです。
2. **2 度現れるスケジュール時刻** — 01:30 のスケジュールに対して時計が 02:00 → 01:00 と戻る場合 — は **最初の発生**、つまり遷移前の（大きいほうの）UTC オフセット側に解決されます。その日にスケジュールが 2 度発火することはありません。
3. **それ以外のすべての壁時計時刻** はその日付におけるゾーンのオフセットを使います。したがって境界は季節とともに流されることなく、意図した現地時刻にとどまります。

`America/New_York` の 2026 年の遷移で確かめると、次のようになります。

```csharp
// 春の遷移: 2026-03-08、02:00 EST が 03:00 EDT になるので 02:30 は存在しない。
var spring = RecurrenceSchedule.Daily(new TimeOnly(2, 30), newYork);

spring.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 1, 0, 0, Est));   // 2026-03-07T02:30:00-05:00
spring.PreviousBoundary(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt));  // 2026-03-08T03:00:00-04:00  <- 遷移そのもの
spring.NextBoundary(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt));      // 2026-03-09T02:30:00-04:00
spring.CurrentWindow(new DateTimeOffset(2026, 3, 8, 12, 0, 0, Edt)).Duration;  // 23:30 -- ウィンドウが短くなるだけで境界は残る

// 秋の遷移: 2026-11-01、02:00 EDT が 01:00 EST になるので 01:30 が 2 度現れる。
var autumn = RecurrenceSchedule.Daily(new TimeOnly(1, 30), newYork);
var first  = new DateTimeOffset(2026, 11, 1, 1, 30, 0, Edt);   // 05:30Z
var second = new DateTimeOffset(2026, 11, 1, 1, 30, 0, Est);   // 06:30Z

autumn.PreviousBoundary(new DateTimeOffset(2026, 11, 1, 12, 0, 0, Est));  // == first
autumn.CountBoundaries(first, second);                                    // 0 -- 2 度は発火しない
autumn.CurrentWindow(second).Duration;                                    // 25:00 -- ウィンドウが長くなるだけ
```

これらのルールは **バージョンをまたぐ契約であり、パッチやマイナーリリースで変更されることは決してありません。** 境界は永続化されます（「このプレイヤーが最後に見たリセット」）。保存された時刻と再計算した時刻を比べる操作は、計算が決して動かないときにだけ成り立ちます。シード付き PRNG のアルゴリズム契約とまったく同じで、別の解決ポリシーが必要になった場合は、この型の挙動を変えるのではなく新しい型として提供されます。

これらのルールは周期にも依存しません。遷移日に境界が来る週次・月次スケジュールも同じように解決され、シフト幅が 1 時間でないゾーンでも成り立ちます（`Australia/Lord_Howe` は 30 分ずれ、そこでの 02:15 のスケジュールは 02:45 ではなく遷移時刻である 02:30 に解決されます）。

タイムゾーン識別子は `TimeZoneInfo` 自身の解決に従います。.NET 6 以降では、ICU が利用可能であれば `TimeZoneInfo.FindSystemTimeZoneById` は Unix だけでなく Windows でも `America/New_York` のような IANA 識別子を受け付けます。注意すべきなのは globalization-invariant モードで動作する Windows 環境です。

## `TimeWindow`: 包含ルールは 1 つだけ

半開区間 `[Start, End)` がこの型の提供する唯一のルールで、閉区間の亜種は意図的にありません。両者を混ぜるコードベースでは、あるメソッドの組では共有された端点が二重にカウントされ、別の組では同じ端点に穴が空きます。原型が食い違う 2 つの答えを抱えることになった経緯が、まさにこれです。

```csharp
var yesterday = dailyReset.WindowAt(now, -1);

yesterday.End == today.Start;          // true  -- ちょうど接する
yesterday.Overlaps(today);             // false -- 接することは重なることではない
yesterday.Contains(today.Start);       // false -- 共有された時刻は今日のウィンドウのもの
today.Contains(today.Start);           // true

today.Intersect(maintenanceWindow);    // 共有区間、なければ null
today.Clamp(overrunInstant);           // == today.End: 「このウィンドウのどこまで進んだか」
```

**オフセットは表示のためだけのものです。** `DateTimeOffset` はタイムライン上の 1 点を指し、`TimeWindow` の演算も値の等価性もその点を比較します。`2026-07-25T04:30:00+09:00` と `2026-07-24T19:30:00+00:00` は同じ瞬間なので、どちらの表記で作ったウィンドウも `==` であり同じ挙動をします。オフセットが `Start`、`End`、`ToString()` に保たれるのは、スケジュールが生成したウィンドウがそのゾーンの現地時刻をそのまま表示できるようにするためだけです。

## テスト

コア API が時刻を引数で受け取るので、ほとんどのテストには時計がまったく必要ありません。検証したい時刻をそのまま渡すだけです。テスト対象のクラスが注入された `TimeProvider` を保持している場合は、フェイクを渡してください。

```csharp
// Microsoft.Extensions.TimeProvider.Testing でも、自前の数行でも構いません。
sealed class FixedTimeProvider(DateTimeOffset instant) : TimeProvider
{
    private readonly DateTimeOffset _utcNow = instant.ToUniversalTime();

    public override DateTimeOffset GetUtcNow() => _utcNow;
}

var clock = new FixedTimeProvider(new DateTimeOffset(2026, 7, 25, 9, 15, 0, TimeSpan.FromHours(9)));

Assert.True(dailyReset.HasCrossed(lastReset, clock));
Assert.Equal(5, dailyReset.CountBoundaries(lastLogin, clock));
```

拡張メソッドは `GetUtcNow()` しか呼ばないので、他にフェイクにするものはありません。

## パフォーマンス

`CountBoundaries` は **ループではなく閉じた形のカレンダー演算** です。10 年のギャップが 1 日のギャップと同じコストなので、2020 年から休眠しているアカウントの取り逃した報酬を数える処理が 3,653 回の反復にはなりません。

.NET 10、AMD Ryzen 9 3950X、Windows 11 での測定値です。

| 呼び出し | 平均 |
|---|---:|
| `America/New_York` で 10 年分の `CountBoundaries`（境界 3,653 個） | 約 0.9 μs |
| `America/New_York` で 1 日分の `CountBoundaries`（境界 1 個） | 約 0.5 μs |
| UTC で 10 年分の `CountBoundaries` | 約 0.05 μs |

残るコストは区間の幅ではなく `TimeZoneInfo` の変換です。UTC の行が同じ 10 年区間の DST ゾーンより 20 倍速いのも、そのゾーンの 10 年の行と 1 日の行が 2 倍以内に収まっているのも、そのためです。このライブラリにベンチマークプロジェクトは同梱していません。ホットパスではないスケジュール計算 API の、あくまで目安の数値です。

## このライブラリの位置づけ

- **スケジューラーではありません。** このライブラリは時刻を計算するだけで、何も実行しません。実行は依然として Quartz.NET、Hangfire、あるいはホステッドサービスの仕事です。`RecurrenceSchedule` には *いつ* を尋ね、*実行* はそちらに任せてください。
- **NodaTime とは補完関係です。** NodaTime はカレンダー体系、期間、ゾーン演算を BCL よりはるかに徹底してモデル化します。ただし「リセットウィンドウ」の概念はなく、このライブラリにも NodaTime を置き換える意図はありません。すでにカレンダー処理で NodaTime を使っていても、境界通過に関する問いには、BCL の型の上で、調整すべき依存関係なしにこのライブラリが答えます。
- **Cronos とも補完関係です。** Cronos は cron 式を解析して次の発生時刻を返します。このライブラリは cron を解析せず、3 つの固定されたカレンダー周期だけを提供しますが、cron パーサーが答えないことに答えます。ある時刻がどのウィンドウに属するか、そして 2 つの時刻の間に発生が何回あったかです。

v1 で意図的に対象外としたのは、cron 式、RFC 5545 の繰り返し規則、営業日・祝日カレンダー、開区間、そしてアンカー規則が別の設計問題となる固定間隔（「6 時間ごと」）の繰り返しです。

## 例外とエッジケース

| 条件 | 挙動 |
|---|---|
| 定義されていない `DayOfWeek` での `Weekly` | `ArgumentOutOfRangeException` |
| `dayOfMonth` が 1〜31 の外の `Monthly` | `ArgumentOutOfRangeException` |
| `end` が `start` より前の `new TimeWindow(start, end)` | `ArgumentException` |
| `new TimeWindow(start, start)` | 合法。空ウィンドウは何も含まず、何とも重ならない |
| `to <= from` での `CountBoundaries` / `HasCrossed` / `EnumerateBoundaries` | `0` / `false` / 空のシーケンス — 負にはならない |
| 表現可能な範囲を外れる `offset` での `WindowAt` | `ArgumentOutOfRangeException` — 黙ってラップしたウィンドウは返らない |
| 2 月における `Monthly(31, ...)` | 28 日または 29 日にクランプ。その月もちょうど 1 つの境界を持つ |
| `null` のスケジュールまたはプロバイダーでの `TimeProvider` 拡張の呼び出し | `ArgumentNullException` |

**範囲の両端についてひとつ注意があります。** 境界は `DateTime` の範囲内で計算されます。`DateTimeOffset.MinValue` や `MaxValue` から境界 1 つ分の距離に入る `asOf` は表現不可能な境界を要求することになり、内部の日付演算が `ArgumentOutOfRangeException` を投げます。したがって「一度も見ていない」を表すセンチネルとして `DateTimeOffset.MinValue` を使うのは避けるのが賢明です。永続化された `lastSeen` が `MinValue` だと、西暦 1 年以降のすべての境界を報告する代わりに例外になります。実際の時刻を保存するか、チェック可能な `null` を使ってください。

## ライセンス

MIT — 詳細は [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE) を参照してください。

---

**AI に関する開示:** 本プロジェクトは AI（Claude）を活用して制作されました。
