[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ja.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.md) | [한국어](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.ko.md) | **日本語**

# SsalKit.Randomness

決定的（deterministic）で状態をシリアライズ可能な PRNG（xoshiro256** + SplitMix64）と、統一された乱数ソース抽象化、重み付きランダム抽選を提供するライブラリです。依存関係はありません。
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Randomness.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Randomness)

## なぜ SsalKit.Randomness なのか

ゲームロジック、シミュレーション、手続き型コンテンツ生成は、いずれ同じ要件に行き着きます。同じシード、あるいは同じ保存済み状態が与えられたとき、「ランダム」な結果の正確に同じ数列が再び得られなければならない、というものです — リプレイのため、決定的なロックステップ・マルチプレイヤーのため、プレイの経過をビット単位で再現するセーブデータのために。

`System.Random` はこの要求を十分に満たしてくれません。

- **シード指定の `Random` はレガシーなアルゴリズムを使います。** `int` シードを受け取るコンストラクターは互換性のために出力を安定させていますが、その安定性はそもそも第一級の設計目標ではなく、`Random` のすべての生成経路にわたって保証されているわけでもありません。
- **状態の export をサポートしません。** `System.Random` には内部状態を取り出して保存し、後で復元するための公式な方法がありません。実行中ずっと同じ `Random` インスタンスを生かし続けるか、再現性をあきらめるかのどちらかです。
- **ストリーム分岐がありません。** 1 つの親シードから独立した再現可能な子ジェネレーターを派生させる組み込みの方法がありません（エンティティごと・サブシステムごとの乱数を 1 つのルートシードにたどれるようにしたい場合に便利です）。

SsalKit.Randomness はアプローチそのものが異なります。

- **`DeterministicRandom`** は `System.Random` に似た形の sealed な PRNG（xoshiro256**）で、256 ビットの全状態を export し、どこにでも（セーブファイル、DB の行、ネットワークパケット）永続化してから復元すれば、どのプラットフォームでも永遠に正確に同じ数列を続けられます。
- **`IRandomSource`** は決定的・共有（`Random.Shared`）・暗号学的な乱数を 1 つのインターフェースに統一するため、範囲生成・シャッフル・抽選のコードを一度書くだけでそのどれに対しても使えます。
- **重み付きランダム抽選**（`PickWeighted`、`PickManyWeighted(Distinct)`、`WeightedSampler<T>`）がライブラリに同梱されており、明確な例外契約と、反復抽選向けの `O(1)` alias method サンプラーを備えています。
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
| `WeightedRandomExtensions` | `PickWeighted`（単発、`long` または `double` の重み、リストまたは span 形式）、`PickManyWeighted`（復元抽出）、`PickManyWeightedDistinct`（非復元抽出）。 |
| `WeightedSampler<T>` | 固定された `long` 重み付き項目集合から繰り返し抽選する際に使う、不変でスレッドセーフな事前ビルド alias method サンプラー。ビルドは `O(n)`、`Pick`/`PickMany` は呼び出しごとに `O(1)`。 |

## アルゴリズムと状態の契約（v1）

`DeterministicRandom` の出力数列は **xoshiro256\*\*** であり、シード拡張（単一の `ulong` シードから 256 ビットの内部状態への拡張）は **SplitMix64** です。状態は正確に 4 つの `ulong` ワードで構成され、`RandomState` として公開されます。

この契約はこの型に対して恒久的に固定されています。

- **同じシード、または同じ方法で復元された状態は、どのプラットフォームでも、どのプロセスでも、永遠に常に同じ数列を生成します。**
- `RandomState` はセーブデータとして永続化されうるため、出力数列を変更することはすべての利用者のセーブデータを破損させることと同義です。このような変更はパッチやマイナーリリースで **決して** 行われません。
- アルゴリズムをいつか進化させる必要が生じた場合、`DeterministicRandom` 自体の挙動を変更するのではなく、**新しい型**（例えば仮の `DeterministicRandomV2`）として出荷されます。
- all-zero 状態は無効な状態です（xoshiro256** は一度その状態に入ると二度と抜け出せません）。`FromState(...)`/`RandomState.FromSpan(...)` はこれを `ArgumentException` として拒否します。

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
