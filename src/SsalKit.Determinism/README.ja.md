[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ja.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Determinism/README.md) | [한국어](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Determinism/README.ko.md) | **日本語**

# SsalKit.Determinism

非決定的な API に対する opt-in なコンパイル時診断ライブラリです。型やメンバーに `[Deterministic]` を付けると、パッケージに同梱されたアナライザーが、その中で**直接**呼ばれた ambient な時計、プロセス乱数、`Guid.NewGuid()`、ランダム化されたハッシュ、環境識別子、スケジューリング API をすべて報告します — そしてすべてのメッセージが具体的な代替手段を名指しします。依存関係はありません。
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Determinism.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Determinism)

## なぜ SsalKit.Determinism なのか

同じ入力に対して、毎回、どのマシンでも同じ出力を出さなければならないコードがあります。ロックステップシミュレーションは 2 つのクライアントの計算が食い違った瞬間に desync します。リプレイは、それが記録された理由であるバグをもはや再現できなくなります。履歴から再実行されるワークフローは、2 回目に別の分岐をたどります。`HashCode.Combine` で計算したキャッシュキーは、再起動後に別のバケットを指します。これらすべてにおいて、欠陥はたいてい無害に見える 1 行 — `DateTime.UtcNow`、`Random.Shared.Next()`、`Guid.NewGuid()` — が、本来あってはならないコードの中に置かれていることであり、障害はその行から遠く離れた場所で、数時間後に、誰も再現できない divergence として現れます。

コンパイラはその行に何の異議も唱えません。BCL にもできません。まったく同じ `DateTime.UtcNow` が、隣のファイルのログ文では完全に正しいのですから。間違っているのは API ではなく、**そのスコープの中の** API であり、スコープこそ既存のツールが表現できていない部分です。

- **`Microsoft.CodeAnalysis.BannedApiAnalyzers`** はプロジェクト全体にわたって API のリストを禁止します。実際のプロジェクトはそういう形をしていません。決定的なシミュレーションコアと、そのロギング、UI、コンポジションルートは通常 1 つのアセンブリに同居しており、プロジェクト全体の禁止はプロジェクトの分割か、正当に時計を読むコードへの抑制コメントの山かを強います。しかも「これは禁止」としか言わず、では何を使えばよいかは教えてくれません。
- **スコープを持つ決定性アナライザー** — Durable Task、Libplanet — は、それぞれのフレームワークと、それぞれの「決定的な領域」の定義に縛られています。自分で書いたドメインサービスや価格計算、ゲームシミュレーションには使えません。

SsalKit.Determinism は 2 つの性質でこの空白を埋めます。そしてこの 2 つがこのパッケージのすべてである、という点は明確にしておく価値があります。

- **スコープは opt-in で、かつレキシカルです。** `[Deterministic]` が付いた型やメンバーの外では何も報告されません。再現可能でなければならないコアだけをマークすればよく、その周辺のコードは抑制もプロジェクト分割もなしに、これまでどおり解析されます。`[AllowNonDeterminism]` はその中から例外を切り出し、両方向にネストします。
- **すべてのメッセージが具体的な代替手段を名指しします。** 「禁止」ではなく*代わりにこれを使え*です。`TimeProvider` を注入するか `DateTimeOffset asOf` 引数を受け取れ、明示的なシードの `DeterministicRandom` を使え、識別子はデータから導出しろ、`HashCode.Combine` の代わりに `ComputeStableHash()` を使え。これらの代替手段は SsalKit ファミリーの残りですが、**このパッケージはそれらを含め何にも依存しません**。該当する型は metadata name で解決されるため、禁止リストの SsalKit 項目は、そのパッケージをすでに参照しているコンパイルにのみ存在します。

ランタイムアセンブリは 2 つの属性だけで、ロジックはありません。それ以外はすべて、パッケージに同梱されたアナライザーがコンパイル時に行います。

## このアナライザーが検出できないもの — 何よりも先に読んでください

**解析は意図的に浅く、直接の呼び出ししか見ません。診断が 0 件であることは決定性の証明ではありません。** これは保証ではなく補助ツールであり、今後もそう在り続ける設計です — 「浅く予測可能」であることは、いずれ取り払われる制約ではなく、この製品そのものです。

最も重要な帰結が、次の表の 1 行目です。`[Deterministic]` なメソッドがマークされていないヘルパーを呼び、そのヘルパーが `DateTime.Now` を読んでも、診断は 1 件も出ません。

| 検出されないもの | 理由 |
|---|---|
| **間接呼び出し** — マークされていないヘルパー経由で禁止 API に到達する場合 | アナライザーはマークしたスコープの外へは出ません。**ヘルパー型にも `[Deterministic]` を付けてください** — これは回避策ではなく、意図された利用パターンです（クイックスタート参照）。 |
| `Dictionary`/`HashSet` の列挙順序 | 意図的に対象外です。非順序コレクションを順序に依存して消費しているかどうかを区別できず、この領域のルールはほとんどが誤検出になります。 |
| プラットフォーム間の浮動小数点の差異（FMA の縮約、x87 の過剰精度、ベクトル化） | 静的解析の範囲外です — 同じ IL が異なるハードウェアで異なる結果を出します。 |
| カルチャ依存の書式化・解析（`ToString()`、`Parse`、`ToUpper`） | BCL 自身の `CA1304`/`CA1305`/`CA1310` がすでに、しかもより適切に担当しています。このパッケージに重複実装を期待せず、それらを有効にしてください。 |
| リフレクション経由でディスパッチされる呼び出し | 対象のシンボルがコンパイル時に存在しません。 |
| `await` の再開コンテキストとスレッドアフィニティ | リストにあるスケジューリングの入口は捕まえますが、await した結果として起きることは捕まえません。 |
| 可変な静的状態、`static` キャッシュ、初期化順序 | 特定の API ではなく、プログラムの構造に宿る非決定性です。 |

同じ原則からさらに 2 つが導かれ、どちらも見落としではなく契約です。

- **スコープは書いたその場所です。** `[Deterministic]` は `Inherited = false` であり、アナライザーも基底型をたどらないため、マークされた基底クラスが派生クラスを覆うことはありません — 型ごとにマークしてください。インターフェースはそもそも有効なターゲットではありません。付けても実装には決して届かないからです。
- **すべてのルールは Warning であり、今後もそうです。** 完全になり得ない検査の後ろにビルドを壊す error を置けば、持っていない完全性を示唆することになります。重大度を上げるのは、プロジェクトごとに意図的に下す判断です — 下の `.editorconfig` の節を参照してください。

## インストール

```bash
dotnet add package SsalKit.Determinism
```

パッケージには属性と、それを読むアナライザーの両方が含まれています — 別途インストールするアナライザーパッケージはなく、独自の `PackageReference` もありません。

## クイックスタート

再現可能でなければならないコードと、それが頼るヘルパーをマークします。

```csharp
using SsalKit.Determinism;
using SsalKit.Randomness;

[Deterministic]
public sealed class BattleSimulation
{
    private readonly DeterministicRandom _random;

    public BattleSimulation(ulong seed) => _random = new DeterministicRandom(seed);

    // 時間も乱数も外から入ってきます。ambient なものは何も読みません。
    public int Tick(DateTimeOffset asOf, int armor) => DamageRules.Apply(_random.Next(1, 7), armor);
}

// 解析は見えるものしか扱わないので、ヘルパーにもマークします --
// そうしないと、来月ここに追加される DateTime.Now が静かにすり抜けます。
[Deterministic]
internal static class DamageRules
{
    public static int Apply(int roll, int armor) => Math.Max(0, roll - armor);
}
```

非決定的なものが 1 つでも入り込めば、ビルドがそう言ってくれます。

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

型に付けると、スコープはその型のすべてのメンバー**およびすべてのネストした型**を覆い、その中に書かれたラムダ、ローカル関数、フィールド／プロパティ初期化子も含みます。メソッド・コンストラクター・プロパティに付けると、そのメンバーだけを覆います。`partial` 型はどれか 1 つのパートに付ければ十分です。

## 禁止カタログ（v1）

カタログは固定で、利用者が拡張することはできません。プロジェクトごとに禁止リストを増やすのは `BannedApiAnalyzers` の役目であり、このパッケージが代わりに加えるのはスコープと助言です。ID をカテゴリごとに分けているのは意図的です — `.editorconfig` では ID こそが調整のつまみだからです。

| ID | カテゴリ | 禁止メンバー | 代わりに使うもの |
|---|---|---|---|
| `SSALD001` | ambient な時間 | `DateTime.Now`/`.UtcNow`/`.Today`; `DateTimeOffset.Now`/`.UtcNow`; `TimeProvider.System`; `Stopwatch.StartNew()`/`.GetTimestamp()`/`new Stopwatch()`; `Environment.TickCount`/`.TickCount64` | 注入された `TimeProvider`（テストでは `FakeTimeProvider`）、または明示的な `DateTimeOffset asOf` パラメーター — [SsalKit.Timekeeping](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Timekeeping/README.ja.md) が一貫して使っている形です。 |
| `SSALD002` | 乱数 | `Random.Shared`; `new Random()` **および `new Random(seed)`**; `RandomNumberGenerator.Create`/`Fill`/`GetBytes`/`GetNonZeroBytes`/`GetInt32`/`GetHexString`/`GetString`/`GetItems`/`Shuffle`; `Path.GetRandomFileName()`; そして、そのパッケージを参照している場合に限り `SsalKit.Randomness` 自身の `SharedRandomSource.Instance`、`CryptoRandomSource.Instance`、`DeterministicRandom.CreateRandomlySeeded()` | [SsalKit.Randomness](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.ja.md) の `DeterministicRandom`（明示的なシード、状態のエクスポート可能）、または注入された `IRandomSource`。 |
| `SSALD003` | 識別子の生成 | `Guid.NewGuid()`; `Guid.CreateVersion7()`（両オーバーロード） | 識別子はデータから導出してください: `ComputeStableHash()`、またはシードを固定した `DeterministicRandom` から取り出したバイト列。 |
| `SSALD004` | ランダム化されたハッシュ | `System.Object`・`System.ValueType`・`System.String` に**解決される** `GetHashCode`; `System.HashCode` の全メンバー; `StringComparer.GetHashCode(string)` | [SsalKit.StableHashing](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.StableHashing/README.ja.md) の `[StableHashContract]` + 生成される `ComputeStableHash()`。 |
| `SSALD005` | 環境の識別子 | `Environment.MachineName`/`.UserName`/`.UserDomainName`/`.ProcessId`/`.CurrentManagedThreadId`/`.ProcessorCount`/`.WorkingSet`/`.CommandLine`/`.CurrentDirectory`/`.GetEnvironmentVariable(…)`/`.GetEnvironmentVariables(…)`; `Process.GetCurrentProcess()`; `Thread.CurrentThread`; `Path.GetTempPath()`/`.GetTempFileName()` | 値を明示的な構成として渡してください。結果が実行されたホストではなく入力に依存するようになります。 |
| `SSALD006` | スケジューリング・並列性 | `Task.Run`/`.Delay`/`.WhenAny`/`.Yield`; `TaskFactory.StartNew`（`TaskFactory<T>` を含む）; `Thread.Sleep`; `ThreadPool.QueueUserWorkItem`; `Parallel.For`/`.ForEach`/`.Invoke`/`.ForAsync`/`.ForEachAsync`; `ParallelEnumerable.AsParallel`; `new System.Threading.Timer(…)`; `new System.Timers.Timer(…)` | そのまま差し替えられる代替がない唯一のカテゴリです。非決定性の原因が特定の呼び出しではなく並行性そのものだからです。本当に順序に依存しない並列処理ならスコープの外に置いて結果だけを渡し、そうでなければ逐次実行に作り替える必要があります。 |
| `SSALD007` | 孤立した例外指定 | 自身にも、自身を含むどこにも `[Deterministic]` がないのに `[AllowNonDeterminism]` が付いている | 属性を削除するか、囲んでいる型／メンバーに `[Deterministic]` を付けてください。静かに何もしないマークは、マークがないことより悪いからです。 |

`new Random(seed)` がなぜこのリストにあるのか、そして何が意図的に**入っていない**のかの補足です。

- **`new Random(seed)` はシードを与えていても禁止です。** `System.Random` のアルゴリズムは文書化された契約の一部ではなく、すでにランタイムのバージョン間で変更されています。したがって固定シードはプロセスやバージョンをまたいでシーケンスを再現してはくれません — 1 つのプロセス内でしか再現しません。`DeterministicRandom` はアルゴリズム（`xoshiro256**`）をバージョン契約として固定し、状態をエクスポート・復元できます。
- **注入された `TimeProvider` は禁止されません** — それが推奨される修正です。禁止されるのは ambient なシングルトンである `TimeProvider.System` だけで、受け取ったインスタンスに `timeProvider.GetUtcNow()` を呼ぶのは無診断です。
- **既存の `Random` のインスタンスメソッドは禁止されません。** 禁止されるのはシーケンスが*どこから来るか*、つまり生成箇所であって、1 回ごとの取り出しではありません。
- **ユーザーが書いた override に解決される `GetHashCode` 呼び出しは報告されません。** リストにあるのはフレームワーク自身のランダム化された実装だけです。あなたの override は、それが属するスコープで、それ自身として解析されます。
- **`nameof(DateTime.UtcNow)` は報告されません。** メンバーを読むのではなく、名前を呼んでいるだけだからです。
- **ファイル／ネットワーク I/O、`Console`、一般的な `await` は v1 のカタログにありません** — これは意図的な範囲の限定であって、問題ないという意味ではありません。

カタログはコンパイルごとに一度 metadata name で解決され、そのコンパイルが参照していない型は静かにスキップされます。上の表の `SsalKit.Randomness` の行が、このパッケージの依存関係ゼロという契約と両立できるのはそのためです — それらはそのパッケージをすでに参照している場所でのみ禁止リストに加わります。そして自分のエコシステムの非決定的な入口にも例外はありません。dogfooding は両方向に効きます。

## 意図して書いたコードを例外にする

決定的なコアの中にも、本当に時計が必要なコードはあります — ログの 1 行、診断カウンター、進捗メッセージ。それを表明する方法は 2 つあり、それぞれ違う大きさの問題のためのものです。

**1. メンバーやネストした型に `[AllowNonDeterminism]`（推奨）。** 1 つの呼び出し箇所ではなくメンバー全体を名指しし、レビュアーがすでに見ている宣言部に現れ、`Justification` がその理由をコードレビューまで運びます。実行時にこれを読むものはなく、どの診断もこれを要求しませんが、理由のない例外指定は次の読み手に何も伝えません。

```csharp
[Deterministic]
public sealed class ReplayRunner
{
    public void Run(DateTimeOffset asOf) { /* 解析対象 */ }

    [AllowNonDeterminism(Justification = "wall-clock logging only; never feeds replayed state")]
    private static void LogProgress(int tick) =>
        Console.WriteLine($"{DateTime.UtcNow:O} tick {tick}");
}
```

スコープの判定はレキシカルで、最も近いマークが勝つため、例外は両方向にネストします。`[Deterministic]` な型の中の `[AllowNonDeterminism]` な型は除外され、*その中の* `[Deterministic]` なメンバーは再び解析されます。すべての `[Deterministic]` スコープの外では、この属性は何も抑制しません。それが `SSALD007` の報告する状況です。

**2. 呼び出し 1 箇所だけなら `#pragma warning disable` または `.editorconfig`。**

```csharp
#pragma warning disable SSALD001 // 一度きり: シミュレーション状態ではなく trace id のシード用
var traceStartedAt = DateTime.UtcNow;
#pragma warning restore SSALD001
```

カテゴリを黙らせるために `[Deterministic]` のマークを消すことだけはしないでください。そのスコープの将来の違反まで一緒に黙らせたうえ、それが意図的だったという記録すら残しません。

## `.editorconfig` での重大度の調整

すべてのルールは Warning として出荷されます。カタログがカテゴリごとの 7 つの ID に分かれているので、1 つのカテゴリを締めるのも緩めるのも 1 行です。

```ini
# .editorconfig

# 決定的なコアはビルドゲートにしつつ...
dotnet_diagnostic.SSALD001.severity = error
dotnet_diagnostic.SSALD002.severity = error
dotnet_diagnostic.SSALD003.severity = error
dotnet_diagnostic.SSALD004.severity = error

# ...このコードベースでは並列性は助言レベルのままにします。
dotnet_diagnostic.SSALD006.severity = suggestion
```

7 つは 1 つのカテゴリを共有しているので、まとめて動かすこともできます。

```ini
dotnet_analyzer_diagnostic.category-SsalKit.Determinism.severity = error
```

`.editorconfig` はパスごとにスコープを指定できる（`[src/Simulation/**.cs]`）ので、決定的なコアが 1 つのプロジェクトにまとまっているソリューションとよく噛み合います。

v1 には code fix provider がありません。ここでの修正は機械的な編集ではなくリファクタリング — `TimeProvider` パラメーターの導入、コンストラクター経由でのシードの受け渡し — だからです。

## どこで効いてくるか

以下の 1 番目・2 番目・5 番目・6 番目の項目は、それぞれ [samples/SsalKit.Determinism.Sample](https://github.com/ssalkit/ssalkit/tree/main/samples/SsalKit.Determinism.Sample) の実行可能なセクションに対応しており、セクション名はこのリストと一致します。

- **ロックステップシミュレーション**（`[Simulation]`、`[Desync]`）。同じ入力から同じ世界を計算するクライアント同士はビット単位で一致していなければならず、1 台のマシンでの壁時計の読み取り 1 回がそのまま desync になります。シミュレーションコアに `[Deterministic]` を付ければ、この種のバグは実行時のミステリーからビルド警告に変わり、`TreatWarningsAsErrors` と組み合わせればそもそもコミットできないものになります。
- **リプレイとイベントソーシングの検証**（`[Replay]`）。記録された入力シーケンスは元の実行を正確に再現しなければならず、そうでなければバグ報告としても監査証跡としても価値がありません。再生パス全体がマークすべきスコープです。
- **ワークフローの再実行。** durable execution エンジン（Durable Functions、Temporal など）はワークフローの履歴を同じコードで再生し、すべての判断が同じ結果になることを要求します。それらのフレームワークは自分の属性のためのアナライザーを自前で提供しますが、フレームワークがマークする領域の外に自作の再実行ロジック — サガのステップ、リトライのプランナー — がある場合、`[Deterministic]` が同じガードレールになります。
- **合意と分散同意。** 独立したノードが同一の入力から同一の結論に到達しなければなりません。判断パスの中の `Guid.NewGuid()` や `Environment.MachineName` は合意を不可能にし、その失敗は明白なバグではなく、まれで再現しない分裂として現れます。
- **キャッシュキー・シャーディング・A/B バケット割り当て**（`[Fingerprint]`）。`HashCode.Combine` と `string.GetHashCode()` はプロセスごとにランダム化されるため、今日計算したキーやバケットは次の再起動後には別のキー・バケットです。`SSALD004` はまさにこれを捕まえ、安定した代替を指し示します。
- **テスト可能なドメインコア**（`[TestableCore]`、`[OptOut]`）。最も日常的なケースです。時間も乱数もすべて外から受け取るサービスは、テストにモックの時計もリトライも `Thread.Sleep` も要らないサービスです。`[Deterministic]` は「時間は注入する約束だ」というコードレビュー上の慣習を、コンパイラが強制する規則に変えます。

このサンプルが意図的に出力*しない*ものが 1 つあります。警告です。サンプルは `TreatWarningsAsErrors` の下でコンパイルされ、コンパイルできること自体が、その中のどこでも `SSALD` 診断が出ていないことの証明になっています。その逆のデモ — 各カテゴリから 1 つずつ取った違反と、それぞれの代替手段のコメント — は `Violations.cs` にあり、`#if` によって既定のビルドからは除外されています。有効にする方法は `[Showcase]` グループが説明します。

## ファミリーの残り

診断が名指しする代替手段は、別パッケージで任意利用です。このパッケージはそのどれにも依存せず、1 つもインストールしなくても動作します。

- **[SsalKit.Randomness](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.ja.md)** — `DeterministicRandom`（`xoshiro256**`、明示的なシード、状態のエクスポートと fork が可能）と `IRandomSource` 抽象。`SSALD002` 用。
- **[SsalKit.Timekeeping](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Timekeeping/README.ja.md)** — カレンダーのリセット境界、クールダウンと充電プール、論理ティックのイベントスケジュール。いずれも自分で時計を読まず、渡された時刻から計算します。`SSALD001` 用。
- **[SsalKit.StableHashing](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.StableHashing/README.ja.md)** — `[StableHashContract]` と生成される `ComputeStableHash()` による、プロセス・マシン・.NET バージョンをまたいで生き残るチェックサム。`SSALD003`・`SSALD004` 用。

## ライセンス

MIT — 詳細は [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE) を参照してください。

---

**AI に関する開示:** 本プロジェクトは AI（Claude）を活用して制作されました。
</content>
