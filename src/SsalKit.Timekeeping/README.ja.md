[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ja.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Timekeeping/README.md) | [한국어](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Timekeeping/README.ko.md) | **日本語**

# SsalKit.Timekeeping

以前は `SsalKit.RecurrenceSchedule` として公開されていました（非推奨）。型と契約は同一で、変更されたのはパッケージ id と名前空間のみです。

SsalKit.Timekeeping は、時計そのものを一切読まずに決定的で永続化可能な時間状態を計算します。すべてのメンバーは `(状態, 時刻)` の純粋関数であり、すべての状態型は不変でシリアライズ可能な `record struct` であり、時刻は常に呼び出し側が渡す引数です — 直接渡すか、すでに時計を保持しているコードのための `TimeProvider` オーバーロードを通じて渡します。依存関係はありません。
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Timekeeping.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Timekeeping)

| コンポーネント | 答える問い | 状態 |
|---|---|---|
| [`RecurrenceSchedule`](#クイックスタート-recurrenceschedule) + [`TimeWindow`](#timewindow-包含ルールは-1-つだけ) | カレンダーの壁時計境界 — 日次/週次/月次のリセット、恒久的に固定された DST 契約 | 既存 |
| [`Cooldown`](#クイックスタート-cooldowns) + [`RechargePool`](#cooldowns) | 経過時間の状態 — 単一のクールダウン、または容量制限付きの充電プール | 新規 |

### 境界の所在

| 境界の種類 | 使うべき型 |
|---|---|
| カレンダーの壁時計（日次/週次/月次リセット、DST） | `RecurrenceSchedule` |
| イベントからの経過時間（アビリティのクールダウン、スタミナ/チャージプール） | `Cooldown` / `RechargePool` |
| プロセス内リソースのスロットリング（同時リクエスト数制限、トークンバケット） | このパッケージの対象外 — [`System.Threading.RateLimiting`](https://learn.microsoft.com/dotnet/api/system.threading.ratelimiting) を参照 |

## なぜ SsalKit.Timekeeping なのか

「前回確認したときから日次リセットは過ぎたか？」という問いは、日次クォータ、ログインボーナス、課金サイクル、レポートウィンドウを持つコードベースなら必ず現れます。見た目は `DateTime` 演算 2 行なので、共有される代わりに呼び出し箇所ごとに書き直され、その複製同士が食い違った答えを返し始めます。

このライブラリの原型となったコードベースには、そうした実装が 2 つ並んで存在していました。

- **1 つは深夜 0 時 UTC と月曜日がハードコード**されており、メソッド内部で直接 `DateTime.UtcNow` を読んでいました。その上に載るコードは、マシンの時計を動かさない限りテストできません。しかも境界の包含ルールがメソッドごとに違い、両端を含むものもあれば、カレンダー日付だけを比較するものもありました。
- **もう 1 つはリセット時刻を設定可能でしたが**、「リセットは過ぎたか」を `from.Hour >= resetHour` で判定していました。そのため 04:15 が 04:30 のリセットを過ぎたものとして扱われました。`4 >= 4` だからです。設定したスケジュールの分と秒は黙って捨てられ、このバグは 4 時 15 分に日次報酬がもう一度配られるという形でしか表面化しませんでした。

1 つのコードベースの中に、同じ問いに対する 2 つの異なる答え。その間には 25 か所を超える呼び出し箇所と、永続化された「最後のリセット」フィールドがありました。

.NET 8 で `TimeProvider` が追加され、「時計を誰が所有するか」という半分は解決しました。しかし、繰り返しウィンドウそのものを表す型は依然としてありません。BCL には「この時刻が属するリセット期間」という概念がないのです。NodaTime はカレンダーとタイムゾーンを深くモデル化しますがリセットウィンドウの概念はなく、Cronos は cron 式を解析して次の発生時刻を返すだけで、ウィンドウ所属の判定や通過回数は扱いません。

SsalKit.Timekeeping はその空白を埋めます。

- **`RecurrenceSchedule`** はカレンダーに整列した繰り返しを定義し（毎日 04:30 ソウル時間、毎週月曜 09:00 UTC、毎月 31 日）、それについて問う価値のある 4 つの質問に答えます。`PreviousBoundary`、`NextBoundary`、`CurrentWindow`、そして「しばらく離れていたユーザー」のための `HasCrossed` / `CountBoundaries` です。
- **包含ルールはどこでも 1 つだけ。** `TimeWindow` は半開区間 `[Start, End)` であり、閉区間の亜種はありません。そのため連続するウィンドウが、二重カウントも隙間もなくタイムラインを正確に敷き詰めます。
- **DST の契約は型の寿命にわたって固定。** 境界は永続化されるため、存在しない・2 度現れる・ついに到達しない壁時計時刻をどう解決するかは実装の詳細ではなく、バージョンをまたぐ約束です。
- **すべてが `(スケジュール, 時刻)` の純粋関数。** 周囲の時計を読む API はひとつもありません。`TimeProvider` オーバーロードはその上に載る糖衣であって、その逆ではありません。
- **依存関係ゼロ。** `PackageReference` なし、BCL のみです。

## インストール

```bash
dotnet add package SsalKit.Timekeeping
```

## クイックスタート: RecurrenceSchedule

```csharp
using SsalKit.Timekeeping;

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

DST を含めて以上をすべて実行できるサンプルは [samples/SsalKit.Timekeeping.Sample](https://github.com/ssalkit/ssalkit/tree/main/samples/SsalKit.Timekeeping.Sample) にあります。

## API 概要: RecurrenceSchedule

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

## 境界のセマンティクス: RecurrenceSchedule

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

スケジュール時刻はそのタイムゾーンの **壁時計** 時刻であり、壁時計が壊れる形はちょうど 3 通りです。いずれの解決もここに固定されます。

1. **存在しないスケジュール時刻** — 02:30 のスケジュールに対して時計が 02:00 → 03:00 と飛ぶ場合 — は **ギャップ直後の最初の有効な時刻** に移ります。03:30 ではなく、遷移そのものである 03:00 です。境界が失われないので、日次スケジュールはその日もちょうど 1 つの境界を持ち、`CountBoundaries` は経過日数と一致したままです。
2. **2 度現れるスケジュール時刻** — 01:30 のスケジュールに対して時計が 02:00 → 01:00 と戻る場合 — は **最初の発生**、つまり遷移前の（大きいほうの）UTC オフセット側に解決されます。その日にスケジュールが 2 度発火することはありません。
3. **壁時計がついに到達しないスケジュール時刻** — 季節ではなくゾーンの *基準* オフセットが恒久的に変わる場合で、2012 年初のリビア、2007 年のベネズエラ、2011 年 12 月 30 日がまるごと消えたサモア、2015 年の北朝鮮、基準オフセットが引き直された複数のロシアのゾーンが該当します — は **ゾーンの壁時計がスケジュール時刻に到達する最初の瞬間** に解決されます。これは規則 1 の原理を言い切ったもので、規則 1 は「ゾーン自身がその穴をギャップと呼ぶ」特殊ケースにすぎません。こうした継ぎ目では `TimeZoneInfo.IsInvalidTime` は何も報告せず、その現地時刻に対してゾーンが返すオフセットは、その組み合わせが指す瞬間に実際に効いているオフセットではありません。「到達する最初の瞬間」という解決は、継ぎ目が生む状況 — 壁時計が 1 時間だけ後ろに戻ってからまた前へ飛ぶ — でも well-defined です。
4. **それ以外のすべての壁時計時刻** はその日付におけるゾーンのオフセットを使います。したがって境界は季節とともに流されることなく、意図した現地時刻にとどまります。

どのゾーンが継ぎ目を持つかは、このライブラリではなくプラットフォームのタイムゾーンデータの性質です。上に挙げた履歴は Windows が提供するデータでは継ぎ目として現れ、tzdata ベースのビルドでは普通の遷移として記録されます。規則 3 は特定のゾーンの話ではなく、どちらであっても正しく振る舞うという話です。

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

継ぎ目の例として、Windows が提供する `Africa/Tripoli` のデータで、基準オフセットが 2012 年へ変わる時点に +02:00 から +01:00 へ下がる場合を見ると次のようになります。

```csharp
// 2011-12-31T21:00Z は 23:00、22:00Z は再び 23:00（基準オフセットが一時的に下がる）、23:00Z は
// 01:00 を指す。つまり壁時計は 2012-01-01 00:00 を一度も指さない。
var midnight = RecurrenceSchedule.Daily(new TimeOnly(0, 0), tripoli);

midnight.PreviousBoundary(new DateTimeOffset(2012, 1, 1, 12, 0, 0, TimeSpan.FromHours(2)));
// 2012-01-01T01:00:00+02:00、すなわち 2011-12-31T23:00Z -- スケジュール時刻に到達する最初の瞬間
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

## Cooldowns

`Cooldown` と `RechargePool` は `RecurrenceSchedule` とは異なる問いに答えます。「カレンダーの境界が過ぎたか」ではなく、「次のチャージが使えるようになるまでどれだけ経過時間が必要か」です。どちらも状態全体が `DateTimeOffset` / `TimeSpan` フィールド 1〜2 個だけの `readonly record struct` なので、`RecurrenceSchedule` の永続化された境界とまったく同じ形で、プロセス再起動やオフラインの空白期間を乗り越えられます — 構造体を保存しておき、あとはその構造体と問い合わせる時刻から残りをすべて再計算するだけです。

### クイックスタート: Cooldowns

```csharp
using SsalKit.Timekeeping;

// 30 秒のクールダウンを持つ単一のアビリティ。
var cooldown = Cooldown.Create(TimeSpan.FromSeconds(30), now);

if (cooldown.TryUse(now, out var updated))
{
    player.AbilityCooldown = updated;   // ストレージに保存し直す
}

TimeSpan left = cooldown.Remaining(now);
bool ready = cooldown.IsReady(now);

// 20 分ごとに 1 つ充電される、5 個のスタミナチャージ。
var pool = RechargePool.Create(capacity: 5, rechargeEvery: TimeSpan.FromMinutes(20), asOf: now);

if (pool.TryConsume(now, amount: 1, out var updatedPool))
{
    player.Stamina = updatedPool;       // こちらも保存し直す
}

int available = pool.AvailableAt(now);
TimeSpan? untilNext = pool.UntilNextCharge(now);
```

どちらの型も `(状態, 時刻)` の純粋関数です — 周囲の時計を読む箇所はありません — そのため `player.AbilityCooldown` と `player.Stamina` はストレージ（JSON、DB のカラム、何であれ）をそのまま往復します。次の `IsReady` / `AvailableAt` 呼び出しは、保存された構造体とそのとき渡した時刻だけからすべてを再計算し、別途「最後に保存した時刻」を管理する必要はありません。

### API 概要: Cooldown と RechargePool

#### `Cooldown`

| メンバー | 用途 |
|---|---|
| `static Cooldown Create(TimeSpan duration, DateTimeOffset asOf)` | 即座に使用可能なクールダウン。`duration` は今後の `TryUse` が突入させる待ち時間の長さ。`duration < 0` は `ArgumentOutOfRangeException`。`TimeSpan.Zero` は合法で、常に使用可能なクールダウンを作る。 |
| `IsReady(DateTimeOffset asOf)` | `asOf >= ReadyAt`。 |
| `Remaining(DateTimeOffset asOf)` | `max(0, ReadyAt - asOf)`。負になることはない。 |
| `TryUse(DateTimeOffset asOf, out Cooldown updated)` | 成功時、新しい `Duration` 分の待ちを開始（`ReadyAt = asOf + Duration`）。失敗時、`updated` はこのインスタンスのまま変わらない — そのまま代入し直しても常に安全。 |
| `Reset(DateTimeOffset asOf)` | 残りの待ちを破棄し、`asOf` に即座に使用可能にする。 |
| `Duration` / `ReadyAt` | 設定された待ち時間の長さと、クールダウンが次に使用可能になる時刻。 |

#### `RechargePool`

| メンバー | 用途 |
|---|---|
| `static RechargePool Create(int capacity, TimeSpan rechargeEvery, DateTimeOffset asOf, int initialCharges = -1)` | `capacity >= 1`、`rechargeEvery > 0` — でなければ `ArgumentOutOfRangeException`。`initialCharges` の既定値は `-1`（満タンを意味する）、それ以外は `[0, capacity]` の範囲でなければならない。 |
| `AvailableAt(DateTimeOffset asOf)` | `0..Capacity` の値。 |
| `TryConsume(DateTimeOffset asOf, int amount, out RechargePool updated)` | `amount` は `1..Capacity` でなければならない（`amount > Capacity` は例外 — このプールでは決して満たせない要求）。現在利用可能な量が `amount` 未満なら `false` を返し、`updated` は変わらない。 |
| `UntilNextCharge(DateTimeOffset asOf)` | 満タンなら `null`。そうでなければ、長くても `RechargeEvery` の長さの期間。 |
| `UntilFull(DateTimeOffset asOf)` | 満タンなら `null`。そうでなければ、ちょうど `FullAt - asOf`。 |
| `Grant(int amount, DateTimeOffset asOf)` | `Capacity` でクランプしつつ単位を加える。`TryConsume` と異なり `amount` に上限はない — 過剰付与は単に満タンで飽和する。 |
| `Refill(DateTimeOffset asOf)` | 次の単位に向けた部分的な進捗を破棄し、`asOf` に完全に満タンにする。 |
| `Capacity` / `RechargeEvery` / `FullAt` | 設定された容量と充電間隔、そしてプールが完全に満タンになる単一の時刻 — [`FullAt` モデル](#fullat-モデル) を参照。 |

#### `CooldownTimeProviderExtensions`

`RecurrenceScheduleTimeProviderExtensions` と同じパターンです。上記メンバーのうち「今」に相当する引数が `asOf` だけのものごとに（`Cooldown` の `IsReady`、`Remaining`、`TryUse`、`Reset`。`RechargePool` の `AvailableAt`、`TryConsume`、`UntilNextCharge`、`UntilFull`、`Grant`、`Refill`）オーバーロードがあり、それぞれ `TimeProvider.GetUtcNow()` をちょうど 1 回転送します。`null` プロバイダーは `ArgumentNullException`。

### 境界のセマンティクス: Cooldowns

ルールは 1 つだけで、`RecurrenceSchedule` の「境界はそれが開くウィンドウに属する」と同じ地位の、恒久的でバージョンをまたぐ契約です。

> **クールダウンや 1 単位のチャージは、完了するちょうどその瞬間から使用可能であり、その後だけ使用可能なのではない。**

```csharp
cooldown.IsReady(cooldown.ReadyAt);       // true
cooldown.Remaining(cooldown.ReadyAt);     // TimeSpan.Zero
pool.AvailableAt(pool.FullAt);            // Capacity。Capacity - 1 ではない
```

クールダウンとプールの状態は `RecurrenceSchedule` の境界と同じ形で永続化されます（「このアビリティを最後に使った時刻」「このプールが満タンになる時刻」）。そのため、この比較は保存された `ReadyAt` や `FullAt` を壊さない限り、リリースをまたいで意味が変わることはありません。

### `FullAt` モデル

`RechargePool` の状態全体は `FullAt` — プールが完全に満タンになる単一の時刻 — です。それ以外のすべての量はこれと `RechargeEvery` から導かれます。

```
available(t)  = Capacity - clamp(ceil((FullAt - t) / RechargeEvery), 0, Capacity)
consume(k, t) : FullAt' = max(FullAt, t) + k * RechargeEvery
grant(k, t)   : FullAt' = max(t, FullAt - k * RechargeEvery)
refill(t)     : FullAt' = t
```

これにより、プールがどれだけ長くオフラインだったか、どれだけの単位が不足しているかに関わらず、すべてのメンバーが **O(1)** になります。また、頼りにできる 3 つの性質も得られます。

- **次の単位に向けた部分的な進捗が正確に保存されます。** 1 単位消費すると、`FullAt` と消費時刻のうち遅い方を基準に `RechargeEvery` 1 つ分だけ前に押し出されるだけで、すでに進んでいたチャージの進捗はリセットされません。同じ時刻に同じ量を `Grant` したときにこの移動が元の `FullAt` に正確に戻るかどうかは、消費時点で実際にチャージが進行中だったかによります。進行中だった場合（消費時刻に `FullAt` がその時刻以降）、往復は損失なく復元され、その時刻より前の観測についても同様です。逆にプールがすでに満タンだった場合（消費時刻に `FullAt` がその時刻以前）、往復は実際に満タンになった元の時刻ではなく、消費/付与の時刻そのものに `FullAt` を戻します — その時刻以降の観測（両方の状態ともその区間ずっと満タンと報告する）は一致し続けますが、その時刻より前の照会や、2 つの `RechargePool` 値の等価性比較では違いが分かります。
- **オフラインの空白は 1 分でも 10 年でも同じコストです。** どんな空白の後でも `AvailableAt` は引き算 1 回と割り算 1 回であり、逃したチャージを走査するループではありません。
- **時刻の逆行は例外ではなく、正常に処理されます。** 違反されうる「最後に観測した時刻」という保存値がそもそも存在しません。以前に使われた時刻より早い `asOf` は（上の式の `clamp` 項を通じて）単に少ない利用可能数を報告するだけで、例外も状態破損もありません。

### 例外

| 条件 | 挙動 |
|---|---|
| `Create`: `capacity < 1` / `rechargeEvery <= 0` / `duration < 0` / `initialCharges` が合法範囲外 | `ArgumentOutOfRangeException` |
| `TryConsume` / `Grant`: `amount < 1` | `ArgumentOutOfRangeException` |
| `TryConsume`: `amount > Capacity` | `ArgumentOutOfRangeException` — このサイズのプールでは何度充電しても決して満たせない要求なので、永遠に `false` を返す代わりに呼び出し側のバグとして拒否 |
| `TryConsume`: `amount` は有効だが、現在それだけ利用可能ではない | `false`、`updated` は変わらない — 元の値の上にそのまま代入し直しても常に安全 |
| `default(Cooldown)`（破損または切り詰められたデシリアライズ結果を含む） | 合法 — `Cooldown.Create(TimeSpan.Zero, DateTimeOffset.MinValue)` とまったく同じに振る舞う、すなわち常に使用可能 |
| `default(RechargePool)`（破損または切り詰められたデシリアライズ結果を含む） | すべてのメンバーが `InvalidOperationException` を投げる — `Cooldown` と異なり、容量 `0` で決して充電されないプールは使用可能な退化状態ではない |
| 時刻の逆行 | 例外なし — 上記「時刻の逆行」を参照 |
| `DateTimeOffset` の範囲を超える演算、または `RechargePool` のティック乗算のオーバーフロー | 下層の checked 演算による `ArgumentOutOfRangeException`（または `OverflowException`） |

`Cooldown` と `RechargePool` は意図的に既定値の扱いが異なります。`Cooldown.Duration = TimeSpan.Zero` はすでに「クールダウン未設定」を表す合法な退化ケースなので、`default(Cooldown)` は単にそのケースがあらかじめ組み立てられているだけです。`RechargePool.Create` はすべて 0 の既定値を意味あるものにしようとすると 0 の `RechargeEvery` で割ることになってしまうため、代わりにすべてのメンバーが明示的にこれを防御し、例外を投げます。

### シリアライズとスレッド安全性

どちらの型も public な `get`/`init` プロパティのみを持つ `record struct` なので、System.Text.Json（あるいは MessagePack、その他何でも）はカスタムコンバーターなしに往復させられます。構造体そのものが状態です。`Cooldown` はフィールド 2 つ（`Duration`、`ReadyAt`）を、`RechargePool` はフィールド 3 つ（`Capacity`、`RechargeEvery`、`FullAt`）を保持します。破損したペイロードのデシリアライズによってコンストラクタが迂回された場合、それをキャッチするのはデシリアライズの時点ではなく、上の例外表のガードがいずれかのメソッド呼び出し時点で捕まえます。

不変な値と純粋関数の組み合わせにより、どちらの型も読み取り目的なら複数スレッド間で安全に共有できます。ただし、read-modify-write の一連の操作を原子的にするわけではありません。`if (pool.TryConsume(now, 1, out var updated)) player.Stamina = updated;` は、2 つのスレッドが同じ保存済みの値に対して同時に実行すればやはり競合します。これは楽観的並行性制御による更新まわりで呼び出し側がすでに負っている責任と同じであり、このパッケージはその上にロックを追加しません。

### `RecurrenceSchedule` との組み合わせ

この 2 つの系列は直交しています — どちらの型も互いを知りません — そのため「このプールを毎日 04:30 にリセットしつつ、それ以外の時間は普段どおり充電させる」ことは、どちらかの型が提供すべき機能ではなく、ただの普通の呼び出し側コードになります。

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

拡張メソッドは `GetUtcNow()` しか呼ばないので、他にフェイクにするものはありません。`Cooldown` と `RechargePool` も同じ方法でテストできます — 時刻を直接渡すか、同じフェイクの `TimeProvider` を拡張メソッドに渡してください。

## パフォーマンス

`CountBoundaries`、`PreviousBoundary`、`NextBoundary`、`CurrentWindow`、`WindowAt` はいずれも **O(1)** です。ループではなく閉じた形のカレンダー演算なので、10 年のギャップが 1 日のギャップと同じコストになります。2020 年から休眠しているアカウントの取り逃した報酬を数える処理が 3,653 回の反復にはならず、`WindowAt(now, -1000)` は `WindowAt(now, -1)` と同じコストです。いずれも **マイクロ秒のオーダー** に収まり、その時間を費やす先は区間の幅ではなく `TimeZoneInfo` の変換です。変換がまったく不要な UTC スケジュールは、DST のあるゾーンでの同じ呼び出しより一桁安く済みます。

O(1) ではなく、そのつもりもないものが 2 つあります。

- `EnumerateBoundaries` は境界の個数に比例し、境界ごとにタイムゾーン解決が 1 回ずつかかります。個数だけが必要なら `CountBoundaries` を使ってください。
- DST のギャップや基準オフセットの継ぎ目に落ちる境界（規則 1 と規則 3）の解決は、壁時計がスケジュール時刻に到達する瞬間を探索するため、通常の解決の百倍ほどかかります。ゾーンごとに年 1〜2 日の話であり、通常の経路には一切触れません。

`Cooldown` と `RechargePool` の各メンバーも同様に O(1) です — 上記の [`FullAt` モデル](#fullat-モデル) こそが、プールがどれだけ長くオフラインだったかに関わらずこれを成り立たせている源泉です。

**このライブラリにベンチマークプロジェクトは意図的に同梱していません。** `SsalKit.Randomness` と違ってこちらはホットパスではなくスケジュール計算 API であり、絶対値を特定のマシンに固定することは、その維持コストに見合いません。ライブラリが約束するのは上の計算量であり、それは実行時間の予算ではなくテストスイートの構造的な表明によって担保されます。

## このライブラリの位置づけ

- **スケジューラーではありません。** このライブラリは時刻を計算するだけで、何も実行しません。実行は依然として Quartz.NET、Hangfire、あるいはホステッドサービスの仕事です。`RecurrenceSchedule` には *いつ* を尋ね、*実行* はそちらに任せてください。
- **リソースリミッターではありません。** `Cooldown` と `RechargePool` は、永続化され特定の時刻と比較される状態をモデル化します — プレイヤーのアビリティのクールダウン、ログイン報酬のプールなどです。`System.Threading.RateLimiting`（`TokenBucketRateLimiter`、`ConcurrencyLimiter` など）は別の問題を解決します。再起動後も生き残る必要がなく、プロセス間で比較される必要もない、同時実行のプロセス内作業をスロットリングする問題です。API のスロットリングには `RateLimiter` を、状態そのものを保存・検査・復元する必要があるときはこれらの型を使ってください。
- **NodaTime とは補完関係です。** NodaTime はカレンダー体系、期間、ゾーン演算を BCL よりはるかに徹底してモデル化します。ただし「リセットウィンドウ」の概念はなく、このライブラリにも NodaTime を置き換える意図はありません。すでにカレンダー処理で NodaTime を使っていても、境界通過に関する問いには、BCL の型の上で、調整すべき依存関係なしにこのライブラリが答えます。
- **Cronos とも補完関係です。** Cronos は cron 式を解析して次の発生時刻を返します。このライブラリは cron を解析せず、3 つの固定されたカレンダー周期だけを提供しますが、cron パーサーが答えないことに答えます。ある時刻がどのウィンドウに属するか、そして 2 つの時刻の間に発生が何回あったかです。

v1 で意図的に対象外としたのは、cron 式、RFC 5545 の繰り返し規則、営業日・祝日カレンダー、開区間、そしてアンカー規則が別の設計問題となる固定間隔（「6 時間ごと」）の繰り返しです。

## 例外とエッジケース: RecurrenceSchedule と TimeWindow

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

**範囲の両端についてひとつ注意があります。** 境界は `DateTime` の範囲内で計算されます。`DateTimeOffset.MinValue` や `MaxValue` から境界 1 つ分の距離に入る `asOf` は表現不可能な境界を要求することになり、内部の日付演算が `ArgumentOutOfRangeException` を投げます。したがって「一度も見ていない」を表すセンチネルとして `DateTimeOffset.MinValue` を使うのは避けるのが賢明です。永続化された `lastSeen` が `MinValue` だと、西暦 1 年以降のすべての境界を報告する代わりに例外になります。実際の時刻を保存するか、チェック可能な `null` を使ってください。`Cooldown` と `RechargePool` には [Cooldowns](#cooldowns) の下に別の例外表があります — とりわけ `Cooldown` は `default` / `MinValue` 由来の状態を例外ではなく合法として扱っており、ここでの `RecurrenceSchedule` の注意点とは正反対です。

## ライセンス

MIT — 詳細は [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE) を参照してください。

---

**AI に関する開示:** 本プロジェクトは AI（Claude）を活用して制作されました。
