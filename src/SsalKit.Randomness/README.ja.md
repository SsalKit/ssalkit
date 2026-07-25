[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ja.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.md) | [한국어](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.ko.md) | **日本語**

# SsalKit.Randomness

決定的（deterministic）で状態をシリアライズ可能な PRNG（`xoshiro256**` + SplitMix64）と、統一された乱数ソース抽象化、重み付きランダム抽選を提供するライブラリです。`[RandomWeight]` 属性からセレクター不要の抽選拡張をコンパイル時に生成する機能も含みます。依存関係はありません。
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Randomness.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Randomness)

## なぜ SsalKit.Randomness なのか

ゲームロジック、シミュレーション、手続き型コンテンツ生成は、いずれ同じ要件に行き着きます。同じシード、あるいは同じ保存済み状態が与えられたとき、「ランダム」な結果の正確に同じ数列が再び得られなければならない、というものです — リプレイのため、決定的なロックステップ・マルチプレイヤーのため、プレイの経過をビット単位で再現するセーブデータのために。

`System.Random` はこの要求を十分に満たしてくれません。

- **シード指定の `Random` はレガシーなアルゴリズムを使います。** `int` シードを受け取るコンストラクターは互換性のために出力を安定させていますが、その安定性はそもそも第一級の設計目標ではなく、`Random` のすべての生成経路にわたって保証されているわけでもありません。
- **状態の export をサポートしません。** `System.Random` には内部状態を取り出して保存し、後で復元するための公式な方法がありません。実行中ずっと同じ `Random` インスタンスを生かし続けるか、再現性をあきらめるかのどちらかです。
- **ストリーム分岐がありません。** 1 つの親シードから独立した再現可能な子ジェネレーターを派生させる組み込みの方法がありません（エンティティごと・サブシステムごとの乱数を 1 つのルートシードにたどれるようにしたい場合に便利です）。

SsalKit.Randomness はアプローチそのものが異なります。

- **`DeterministicRandom`** は `System.Random` に似た形の sealed な PRNG（`xoshiro256**`）で、256 ビットの全状態を export し、どこにでも（セーブファイル、DB の行、ネットワークパケット）永続化してから復元すれば、どのプラットフォームでも永遠に正確に同じ数列を続けられます。
- **`IRandomSource`** は決定的・共有（`Random.Shared`）・暗号学的な乱数を 1 つのインターフェースに統一するため、範囲生成・シャッフル・抽選のコードを一度書くだけでそのどれに対しても使えます。
- **重み付きランダム抽選**（`PickWeighted`、`PickManyWeighted(Distinct)`、`WeightedSampler<T>`）がライブラリに同梱されており、明確な例外契約と、反復抽選向けの `O(1)` alias method サンプラーを備えています。
- **`[RandomWeight]`** でモデル型の重みメンバーに印を付けると、パッケージに同梱されたソースジェネレーターがセレクターを代わりに書いてくれます。`random.PickWeighted(lootTable, static x => (long)x.Weight)` の代わりに `lootTable.PickWeighted(random)` と書けます。純粋なコンパイル時コード生成なので、リフレクションはなく、AOT・トリミングにも安全です。
- **依存関係ゼロ。** `PackageReference` なし、BCL のみを使用します。

## インストール

```bash
dotnet add package SsalKit.Randomness
```

## クイックスタート

```csharp
using SsalKit.Randomness;

// 決定的なジェネレーターをシードで生成します。
var rng = new DeterministicRandom(seed: 42);

int roll = rng.Next(1, 7);          // [1, 7)
double chance = rng.NextDouble();   // [0, 1)
bool coinFlip = rng.NextBoolean();

// 状態を export して（例: セーブファイルへ）保存し、後で正確に同じ数列を再開します。
RandomState saved = rng.ExportState();
DeterministicRandom resumed = DeterministicRandom.FromState(saved);

// 親シードから独立した子ストリームを派生させます（例: ゲームエンティティごとに 1 つ）。
DeterministicRandom child = rng.Fork();

// 重み付き抽選、単発。
string[] items = ["common", "rare", "legendary"];
long[] weights = [80, 18, 2];
string drop = rng.PickWeighted(items.AsSpan(), weights.AsSpan());

// 重み付き抽選、反復: 一度ビルドして、抽選ごとに O(1)。
WeightedSampler<string> sampler = WeightedSampler<string>.Create(items, weights.AsSpan());
string anotherDrop = sampler.Pick(rng);
string[] tenDrops = sampler.PickMany(rng, count: 10);

// 項目自身が重みを持つ場合: 重みセレクターからビルドでき、項目の型は推論されます。
(string Name, long Weight)[] loot = [("common", 80), ("rare", 18), ("legendary", 2)];
var lootSampler = loot.ToWeightedSampler(entry => entry.Weight);
```

## API 概要

| 型 | 役割 |
|---|---|
| `IRandomSource` | すべてのソースが共有する最小限の契約（`NextUInt64()` + `NextBytes(Span<byte>)`）。それより上位の演算はすべて、この 2 つのメンバーから拡張メソッドとして派生します。 |
| `DeterministicRandom` | シード指定・状態 export・fork が可能な PRNG。`System.Random` に似たインスタンス API（`Next`、`NextInt64`、`NextDouble`、`NextSingle`、`NextBoolean`、`NextBytes`）に加え、`ExportState()`/`FromState(...)`/`Fork()` を提供します。 |
| `RandomState` | 256 ビットの状態（`S0`..`S3`）を保持する `readonly record struct`。値の等価性と容易な JSON シリアライズを備え、`ulong[4]` との相互運用のために `ToArray()`/`FromSpan(...)`/`CopyTo(...)` があります。 |
| `CryptoRandomSource` | `RandomNumberGenerator` を用いた `IRandomSource`。予測不可能でスレッドセーフ、`CryptoRandomSource.Instance` として提供されます。 |
| `SharedRandomSource` | `Random.Shared` を用いた `IRandomSource`。スレッドセーフで、`SharedRandomSource.Instance` として提供されます。 |
| `SystemRandomSource` | 任意の `Random` インスタンスをラップする `IRandomSource` アダプターで、相互運用とテスト用です。 |
| `RandomSourceExtensions` | `IRandomSource` 向けの均等分布拡張メソッド: `Next`/`NextInt64`/`NextDouble`/`NextSingle`/`NextBoolean`、`Shuffle`、`Pick`。`DeterministicRandom` のインスタンスメソッドとアルゴリズム・出力が完全に一致します。 |
| `WeightedRandomExtensions` | `PickWeighted`（単発、`long` または `double` の重み、リストまたは span 形式）、`PickManyWeighted`（復元抽出）、`PickManyWeightedDistinct`（非復元抽出）、および `ToWeightedSampler` — `items.ToWeightedSampler(x => x.Weight)` の形でリストから直接サンプラーをビルドでき、`WeightedSampler<T>.Create` のように型引数を書かなくても項目の型が推論されます。 |
| `WeightedSampler<T>` | 固定された `long` 重み付き項目集合から繰り返し抽選する際に使う、不変でスレッドセーフな事前ビルド alias method サンプラー。ビルドは `O(n)`、`Pick`/`PickMany` は呼び出しごとに `O(1)`。 |
| `RandomWeightAttribute` | モデル型の重みプロパティまたはフィールドに付ける属性。パッケージに同梱されたソースジェネレーターが、その型の `IReadOnlyList<T>` に対するセレクター不要の `PickWeighted`/`PickManyWeighted`/`PickManyWeightedDistinct`/`ToWeightedSampler` 拡張をコンパイル時に生成します。 |

## `[RandomWeight]` によるセレクター不要の抽選

ここまでの重み付き API はすべてセレクターを受け取ります。`random.PickWeighted(lootTable, static x => (long)x.Weight)` のように。モデル型に重みメンバーが 1 つしかないのに、呼び出しのたびにこのセレクターを書き続けるのは無駄です。代わりにメンバーへ印を付けましょう。

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

この属性 1 行がオプトインのすべてです。ジェネレーターはコンパイル時に `LootEntryRandomWeightExtensions` クラスを `LootEntry` と同じ名前空間へ生成するため、その型を使える場所ならすでに拡張メソッドもスコープに入っています。

```csharp
IReadOnlyList<LootEntry> lootTable = [ /* ... */ ];
var rng = new DeterministicRandom(seed: 42);

LootEntry drop      = lootTable.PickWeighted(rng);                        // 単発
LootEntry[] drops   = lootTable.PickManyWeighted(rng, count: 4);          // 復元抽出
LootEntry[] distinct = lootTable.PickManyWeightedDistinct(rng, count: 3); // 非復元抽出

// alias テーブルを一度だけビルドし、抽選は 1 回あたり O(1)。
WeightedSampler<LootEntry> sampler = lootTable.ToWeightedSampler();
LootEntry sampled = sampler.Pick(rng);
```

レシーバーはコレクションで、乱数ソースは明示的な引数のままです — どのソースから引くかは呼び出し側で見えるべき判断なので、引数なしの `lootTable.PickWeighted()` は型が自ら要求しない限り生成されません（[共有ソースのオーバーロード](#共有ソースのオーバーロード)を参照）。

これを属性として提供する理由は 3 つあります。

- **リフレクションもランタイムディスパッチもありません。** 生成されるメソッドはコンパイル時に書き出される普通の C# コードなので、AOT・トリミングに安全で、コストは手書きのセレクターとまったく同じです。
- **追加でインストールするものはありません。** ジェネレーターは `SsalKit.Randomness` パッケージ内に analyzer として同梱されています。パッケージを追加すれば属性とジェネレーターの両方が手に入り、依存関係リストは空のままです。
- **自分でセレクターを書いた場合と挙動が同一です。** 生成された各メソッドは対応するランタイムのオーバーロードへそのまま委譲するため、後述の例外セクションに記載された例外契約がそのまま適用され、同じシードなら同じ抽選結果になります。

### 何が生成されるか

| 重みメンバーの型 | 生成される拡張 |
|---|---|
| `sbyte`、`byte`、`short`、`ushort`、`int`、`uint`、`long` | `PickWeighted(source)`、`PickManyWeighted(source, count)`、`PickManyWeightedDistinct(source, count)`、`ToWeightedSampler()` |
| `float`、`double` | `PickWeighted(source)` のみ — バッチ抽選と alias テーブルによるサンプリングを `long` の重みに対してのみ提供するランタイムの表面をそのまま反映しています |
| それ以外（`ulong`、`decimal`、enum、null 許容の数値型、数値以外の型） | 生成せず、`SSALR001` を報告 |

`ulong` は意図的に除外しています。`long` へ変換する際にオーバーフローしうるためです。生成される拡張のレシーバーはいずれも `IReadOnlyList<T>` で、`List<T>`・配列・`ImmutableArray<T>` がこれに該当します。遅延シーケンスの場合は先に `.ToList()` を明示的に呼んでください — 重み付き抽選にはインデックスアクセスが必要であり、このライブラリはそのコストを隠しません。

### 可視性

生成されるクラスは既定で `public` ですが、対象の型の実効アクセシビリティが上限になります — したがって `internal` なモデル型なら拡張も自動的に `internal` になり、アクセシビリティの不一致は起きません。public なアセンブリの公開 API 表面にこれらのヘルパーを出したくない場合は、明示的に指定してください。

```csharp
[RandomWeight(InternalExtensions = true)]
public long Weight { get; init; }
```

### 共有ソースのオーバーロード

呼び出しごとにソースを渡すのが正しい既定値ですが、抽選を再現する必要がまったくないモデルにとっては形式的な手間でしかありません。`SharedSourceOverloads = true` を指定すると、`SharedRandomSource.Instance` から引く引数なしのオーバーロードが追加で生成されます。

```csharp
public sealed class GachaEntry
{
    public required string CharacterId { get; init; }

    [RandomWeight(SharedSourceOverloads = true)]
    public long Weight { get; init; }
}

IReadOnlyList<GachaEntry> banner = [ /* ... */ ];

GachaEntry pull       = banner.PickWeighted();                        // 共有ソース
GachaEntry[] tenPull  = banner.PickManyWeighted(count: 10);           // 共有ソース
GachaEntry[] distinct = banner.PickManyWeightedDistinct(count: 3);    // 共有ソース

GachaEntry replayable = banner.PickWeighted(new DeterministicRandom(seed: 42)); // これまで通り使えます
```

オーバーロードは置き換えではなく追加です。ソースを受け取る形はまったく変わらず、引数なしのメソッドはそれぞれ対応するオーバーロードへ 1 行で委譲するだけなので、検証・例外・抽選の意味は完全に同一です。`ToWeightedSampler()` はもともとソースを受け取らないため変化しません。重みの型のマトリクスも変わらず、`float`/`double` のメンバーは 2 つの形の `PickWeighted` だけを得ます。

既定でオフにしているのは、`SharedRandomSource` がシードを取れず数列を再現できないからであり、引数なしの呼び出しはまさにその事実が呼び出し側から見えない形だからです。オフのままにしておけば、シードによる再現性の上に築いたコードベースに非決定的な抽選が静かに紛れ込むことはありません。オンにする行為は「この型の抽選は再現する必要がない」という型単位の宣言になります — ガチャのバナー、見た目アイテムのドロップテーブル、フレーバーテキストの抽選などです。迷ったらオフのままにして、ソースを渡し続けてください。

### 診断

| ID | 報告される条件 |
|---|---|
| `SSALR001` | 重みメンバーの型がサポート対象外（上の表を参照）。 |
| `SSALR002` | 1 つの型に `[RandomWeight]` メンバーが 2 つ以上宣言されている。 |
| `SSALR003` | メンバーが `static`、書き込み専用プロパティ、またはインデクサー — 読み取り可能なインスタンスメンバーである必要があります。 |
| `SSALR004` | メンバー・宣言側の型・外側の型のいずれかが生成されたクラスからアクセスできない（`private`、`protected`、file-local）。 |
| `SSALR005` | 宣言側の型がジェネリック、またはジェネリック型の中にネストされている。 |
| `SSALR006` | 宣言側の型が `ref struct` であり、ジェネリック型引数として使えない。 |

6 つはいずれもエラーであり、ある型で 1 つでも発生するとその型については何も生成されません — 部分的な生成はありません。

### 知っておくべきこと

- **重みは通常のプロパティかフィールドとして宣言してください。** ターゲットをリダイレクトする属性表記 — positional record のパラメーターに付ける `[property: RandomWeight]` や、自動プロパティの `[field: RandomWeight]` — はジェネレーターから認識されず、診断も生成コードもないまま静かに無視されます。代わりに `public long Weight { get; init; }`（または通常のフィールド）と書いてください。
- **継承はたどりません。** base 型に `[RandomWeight]` を付けると、その base 型用の拡張だけが生成されます。`IReadOnlyList<out T>` の共変性のおかげで `List<Derived>` からも呼び出せますが、返される静的な型は base 型なので、`Derived` に戻すにはキャストが必要です。
- **サンプラーは一度だけビルドしてください。** `ToWeightedSampler()` は `O(n)` で、`O(1)` なのは抽選だけです。抽選ループの中で呼ぶと反復のたびに alias テーブルを作り直すことになり、サンプラーを使う意味そのものがなくなります。重みテーブルごとに 1 つビルドして保持し（不変でスレッドセーフです）、そこから繰り返し引いてください。1 回だけ引くなら `PickWeighted` を使います。

## パフォーマンス

SsalKit.Randomness が最適化しているのは、生のスループットとは違うゴールです。決定性・状態のシリアライズ・無割り当てを、性能上の代償なしに得ること — 実際、`DeterministicRandom` のスカラー演算は BCL のあらゆる汎用的な代替手段よりもむしろ高速です。

BenchmarkDotNet v0.15.8、.NET 10.0.10、AMD Ryzen 9 3950X、Windows 11 の環境で計測しました（SsalKit.Randomness 0.1.0 時点）。数値はハードウェアによって変わります。[ベンチマークプロジェクト](https://github.com/ssalkit/ssalkit/tree/main/benchmarks/SsalKit.Randomness.Benchmarks) で自分の環境でも再現できます。

### 均等生成

| 演算 | DeterministicRandom | `new Random(seed)` | `Random.Shared` | CryptoRandomSource |
|---|---:|---:|---:|---:|
| NextUInt64 相当 | 1.8 ns / 0 B | 25.1 ns / 0 B | 3.7 ns / 0 B | 70.6 ns / 0 B |
| Next(1000) | 2.4 ns / 0 B | 4.7 ns / 0 B | 3.5 ns / 0 B | 68.1 ns / 0 B |
| NextInt64（範囲指定） | 1.9 ns / 0 B | 15.7 ns / 0 B | 3.6 ns / 0 B | 71.0 ns / 0 B |
| NextDouble | 2.0 ns / 0 B | 3.8 ns / 0 B | 4.0 ns / 0 B | 71.7 ns / 0 B |

`DeterministicRandom` は計測したすべてのスカラー演算で最速です（1.8〜2.6 ns、上表にない `NextRange` も同じく 2.6 ns）。シード指定のレガシー `Random` に対して最大で約 14 倍、`Random.Shared` に対して約 1.5〜2 倍高速です。4 つのソースすべてで、スカラー生成の割り当ては 0 バイトです。

注記:
- `Random.Shared` はスレッドセーフなラッパーであるため、上記のシングルスレッド向けソースとの比較は厳密には同一条件ではありません。
- 唯一の例外は 64 バイトバッファへの `NextBytes` で、この場合は `Random.Shared`（16.5 ns）が `DeterministicRandom`（19.2 ns）よりわずかに高速です。
- `CryptoRandomSource` は全体的に `DeterministicRandom` より約 25〜40 倍遅くなります — `RandomNumberGenerator` を基盤としており、他のソースにはない暗号学的な予測不可能性の対価であるため、当然の結果です。

### ディスパッチコスト

| 呼び出し方式 | Mean |
|---|---:|
| `DeterministicRandom` インスタンスの直接呼び出し | 2.25 ns |
| `IRandomSource` 拡張メソッド経由 | 2.69 ns |

`IRandomSource` 抽象化を経由するコストは約 0.4 ns — 仮想呼び出し 1 回分です。サブナノ秒のオーバーヘッドなので、柔軟性（決定的・共有・暗号学的ソースの差し替え）のために `IRandomSource` を対象にコードを書いても、実質的にコストはかかりません。ホットループで常に単一の具象型しか使わない場合は、`DeterministicRandom` を直接呼び出せばこの分すら省けます。

### 重み付き抽選

| メソッド | N=10 | N=100 | N=1000 |
|---|---:|---:|---:|
| `PickWeighted`（リスト/デリゲート） | 43.1 ns / 104 B | 206.9 ns / 824 B | 1,590.5 ns / 8,024 B |
| `PickWeighted`（span） | 36.7 ns / 0 B | 142.8 ns / 0 B | 1,363.5 ns / 8,024 B |
| `WeightedSampler<T>.Pick` | 11.1 ns / 0 B | 10.6 ns / 0 B | 11.4 ns / 0 B |

`WeightedSampler<T>.Pick` は `N` によらず ~11 ns で横ばいです — alias method テーブルにより、各抽選が実際に `O(1)` であることの実証です。span ベースの `PickWeighted` オーバーロードは項目数 256 個までは割り当てなしで、それを超えるとヒープバッファへフォールバックします（上表 N=1000 の 8KB はこの文書化されたフォールバックであり、リークではありません）。

`WeightedSampler<T>` のビルドコストは無料ではありません — `Create(...)` は N=10 で 237 ns、N=100 で 1.7 μs、N=1000 で 14.9 μs かかります。ただしこれは一度きりのコストです。N=1000 の場合、反復的な単発 `PickWeighted`（span）呼び出しと比較すると、サンプラーのビルドコストは約 **11 回の抽選** で回収できます — 同じテーブルから何度も引く予定があるなら、サンプラーを使う方が得です。

## アルゴリズムと状態の契約（v1）

`DeterministicRandom` の出力数列は `xoshiro256**` であり、シード拡張（単一の `ulong` シードから 256 ビットの内部状態への拡張）は **SplitMix64** です。状態は正確に 4 つの `ulong` ワードで構成され、`RandomState` として公開されます。

この契約はこの型に対して恒久的に固定されています。

- **同じシード、または同じ方法で復元された状態は、どのプラットフォームでも、どのプロセスでも、永遠に常に同じ数列を生成します。**
- `RandomState` はセーブデータとして永続化されうるため、出力数列を変更することはすべての利用者のセーブデータを破損させることと同義です。このような変更はパッチやマイナーリリースで **決して** 行われません。
- アルゴリズムをいつか進化させる必要が生じた場合、`DeterministicRandom` 自体の挙動を変更するのではなく、**新しい型**（例えば仮の `DeterministicRandomV2`）として出荷されます。
- all-zero 状態は無効な状態です（`xoshiro256**` は一度その状態に入ると二度と抜け出せません）。`FromState(...)`/`RandomState.FromSpan(...)` はこれを `ArgumentException` として拒否します。

派生する保証事項:

- `Next(maxValue)` / `NextInt64(maxValue)` とその範囲指定オーバーロードは **Lemire の乗算シフトリジェクトアルゴリズム** を使用します — `%` ベースの範囲縮小と異なり、モジュロバイアスがありません。
- `NextDouble()` は 53 ビット精度の `[0, 1)` を、`NextSingle()` は 24 ビット精度の `[0, 1)` を返します。`1.0`/`1.0f` が返されることはありません。
- `Fork()` の契約は正確に `Fork() == new DeterministicRandom(this.NextUInt64())` です。親が `ulong` を 1 つ引き（他の `NextUInt64()` 呼び出しと同様に親の状態が正確に 1 ステップ進みます）、それを SplitMix64 で子の状態へ拡張します。子のシードが 64 ビットであるため、独立に fork された子同士の誕生日衝突確率が意味を持つのは `2^32` 回の fork という規模になってからです — どのゲーム・シミュレーションのワークロードよりもはるかに大きな規模です。

## スレッドセーフティ

| 型 | スレッドセーフ | 備考 |
|---|---|---|
| `DeterministicRandom` | **いいえ** | 同時アクセスは内部状態を破損させ、数列の再現性を壊します。スレッドごとに個別のインスタンスを使うか、外部で同期してください。 |
| `CryptoRandomSource` | はい | `RandomNumberGenerator.Fill` は static でスレッドセーフです → シングルトンとして提供されます。 |
| `SharedRandomSource` | はい | `Random.Shared` 自体がスレッドセーフです。 |
| `SystemRandomSource` | ラップしたインスタンスに依存 | 通常の `new Random(seed)` はスレッドセーフではなく、`Random.Shared` はスレッドセーフです（その場合は `SharedRandomSource` を使うことを推奨します）。 |
| `WeightedSampler<T>` | はい（不変） | テーブルは `Create(...)` で一度だけビルドされ、`Pick`/`PickMany` 呼び出しはそのテーブルと呼び出し元が渡した `IRandomSource` だけを読み取ります。 |

## セキュリティ

**`DeterministicRandom` は予測可能です。** 連続した出力がいくつかあれば内部状態を復元でき、それ以降のすべての出力を予測できます。トークン、認証情報、秘密が守られるべきシャッフルなど、セキュリティが必要な用途には **決して** 使用しないでください。

そのような用途には `CryptoRandomSource` を使用してください。`DeterministicRandom` の再現性は必要だがシードは予測不可能であってほしい場合は、暗号学的 RNG からシードを取得する `DeterministicRandom.CreateRandomlySeeded()` を使用してください — 予測不可能なのはシードだけであり、生成されたジェネレーター自体は依然として予測可能な `DeterministicRandom` です。

## 例外

次の契約は `RandomState`、範囲生成メンバー、そしてすべての重み付き抽選 API（`WeightedRandomExtensions`、`WeightedSampler<T>`）に一貫して適用されます。

| 条件 | 例外 |
|---|---|
| `items` が空 | `ArgumentException` |
| 負の重みが存在する | `ArgumentException`（該当インデックスを含む） |
| `double` の重みが NaN/Infinity | `ArgumentException`（該当インデックスを含む） |
| 重みの合計が 0 | `ArgumentException` |
| `long` の重みの合計がオーバーフロー | `OverflowException`（checked 加算） |
| `count <= 0` | `ArgumentOutOfRangeException` |
| `PickManyWeightedDistinct` で `count` が正の重みを持つ項目数を超える | `ArgumentOutOfRangeException` |
| `RandomState.FromState(...)` / `RandomState.FromSpan(...)` に all-zero 状態を渡した | `ArgumentException` |
| 範囲指定の `Next`/`NextInt64` オーバーロードで `minValue > maxValue` | `ArgumentOutOfRangeException` |

重みが `0` の項目は **許可** されており、単に決して選ばれないだけです（合計さえ正であれば構いません）。`PickManyWeightedDistinct` における `count` の上限は `items.Count` ではなく、*正の* 重みを持つ項目の数です — 重み 0 の項目は決して選ばれないため、それ以上を要求すると無限探索になるか、重み 0 の項目を誤って返すことになります。

## ライセンス

MIT — 詳細は [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE) を参照してください。

---

**AI に関する開示:** 本プロジェクトは AI（Claude）を活用して制作されました。
