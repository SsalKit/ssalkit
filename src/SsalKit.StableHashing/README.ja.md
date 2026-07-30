[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ja.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.StableHashing/README.md) | [한국어](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.StableHashing/README.ko.md) | **日本語**

# SsalKit.StableHashing

バージョン固定の canonical encoding 契約による、プラットフォーム・プロセス非依存の 64bit チェックサムライブラリです。`[StableHashContract]`/`[StableHashMember]` を付けるとソースジェネレーターが `ComputeStableHash()` を書いてくれます。ハッシュは内部に移植した XxHash64 で計算します。依存関係はありません。
[![NuGet](https://img.shields.io/nuget/v/SsalKit.StableHashing.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.StableHashing)

## なぜ SsalKit.StableHashing なのか

`object.GetHashCode()` は一見この役目を果たしてくれそうですが、BCL は明示的にそうではないと述べています。公式ドキュメントの契約は、同じプログラムの異なる実行間、プロセス間、.NET バージョン間で値が変わることを許容しており、その値をどこかに永続化したり、1 つのオブジェクトのライフタイムを超えて同じ値であり続けることを期待したりしないよう警告しています。これはまさに「このオブジェクトのハッシュ」が本当に必要な用途 — DB に保存するチェックサム、ネットワーク越しに送る値、2 台のマシン間で比較する値、今日の実行と来月の実行を比較する値 — を封じてしまいます。

`System.IO.Hashing`（BCL 自身の `XxHash64`、`XxHash3` など）もこの空白を埋めてはくれません — それは意図的なものです。すでに持っているバイト列をハッシュするだけで、C# のオブジェクトを永遠に一貫性を保つ方法でバイト列に変換する方法については何の見解も持ちません。「オブジェクトをどうバイト列に変換するか」— フィールドの順序、数値の幅、文字列エンコーディング、`-0.0` や `1.0m` vs `1.00m` の扱い — という決定こそが、一度決めたら二度と変えてはならない部分です。変えた瞬間、これまでに計算したすべてのハッシュが静かに無効になるからです。このライブラリの本当の成果物はこれ、**canonical encoding 契約** です。ハッシュアルゴリズムではありません。ハッシュアルゴリズムは相対的に交換可能ですが、エンコーディング規則はそうではありません。

SsalKit.StableHashing は:

- **`[StableHashContract]` / `[StableHashMember(id)]`** で型とそのメンバーをマークします。パッケージに同梱されたソースジェネレーターがコンパイル時に `ComputeStableHash()` 拡張メソッドを書いてくれます — リフレクションなし、AOT・トリミング安全です。
- **エンコーディングは永続的に固定された v1 契約です**（バイト順、フィールド幅、文字列エンコーディング、ネストした契約の再帰方式など — 下記参照）。今日計算したハッシュと、このライブラリの将来のパッチが別のマシン・アーキテクチャで計算したハッシュは、同じ論理的入力に対して永遠に同じ値です。
- **設計自体が等価一貫性を保証します。** `decimal`、`DateTimeOffset`、`float`/`double` にはそれぞれ `==` が true でも内部ビットが異なる罠がありますが、エンコーディングはこの 3 つを正規化し、`a == b` なら常に `encode(a) == encode(b)` となるようにします（下記の等価一貫性不変条件を参照）。
- **依存関係ゼロ。** ハッシュアルゴリズム（XxHash64）は `System.IO.Hashing` から取り込むのではなく内部に直接移植しているため、パッケージは BCL のみに依存します。

主な用途: 2 つのロックステップ／リプレイシミュレーションが desync したかを検出する（状態全体ではなくティックごとのハッシュを比較）、実際に変更がない場合の冗長なスナップショット保存をスキップする、決定的な A/B バケット割り当て（`hash % 100`）、そして再現可能な named random stream の導出 — `StableHash64.Value` をそのまま `new SsalKit.Randomness.DeterministicRandom(hash.Value)`（別パッケージで任意利用の [SsalKit.Randomness](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Randomness/README.ja.md)）に渡すことで、ハッシュ可能などんな値もシードに変えられます。2 つのパッケージは互いに依存しません — これは結合ではなく、ドキュメント化された利用パターンです。

## インストール

```bash
dotnet add package SsalKit.StableHashing
```

パッケージにはランタイム型（`StableHash64`、`StableHashWriter`、2 つの属性）とソースジェネレーターの両方が含まれています — 別途インストールするアナライザーパッケージはなく、独自の `PackageReference` もありません。

## クイックスタート

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

hashA == hashB;      // true -- 別々のインスタンスだがメンバー値が同じ
hashA.ToString();     // "9c3f38517dbc66aa" -- 小文字、16 桁の hex
hashA.Value;          // 生の ulong 値
```

`[StableHashMember]` は opt-in です。付けなかったメンバーは単に契約から除外されるだけで、診断は出ません。メンバーは宣言順ではなく `Id` の昇順でエンコードされるため、ソース上でメンバーの順序を入れ替えたり名前を変えたりしてもハッシュは変わりません — 変わるのは `Id` を変更した時、または値の型を変更した時だけです。

## API 概要

| 型 | 役割 |
|---|---|
| `StableHashContractAttribute(string name)` | `class`/`struct` を契約としてマークします。`Name` は契約の永続的な識別子（CLR の型名から独立しているため型は自由にリネーム可能）、`Version`（デフォルト `1`）はメンバー集合やメンバーの型が変わり、既存の保存済みハッシュを無効化すべき時に上げます。 |
| `StableHashMemberAttribute(int id)` | フィールド／プロパティを契約の一部としてマークし、値の直前にエンコードされる安定 id（`>= 1`）を指定します。この属性がないメンバーは除外されます。 |
| `StableHash64` | `ulong` の結果をラップする `readonly record struct`。`ToString()` は小文字・0 埋めの 16 桁 hex を返します。`.Value` を `DeterministicRandom` に渡せば named seed として使えます（上記参照）。 |
| `StableHashWriter` | ジェネレーターが呼び出す、アロケーションなしの低レベル `ref struct` です — ジェネレーターが（まだ）対応していない型には直接使えます。正確な規則は下記のエンコーディング契約を参照。 |
| 生成される `ComputeStableHash()` / `AppendStableHash(ref StableHashWriter)` | 契約型ごとに、その型自身の名前空間に `public static class {Type}StableHashing` が 1 つ生成され、この 2 つの拡張メソッドを持ちます。`AppendStableHash` はネストした契約メンバーが呼び出す対象です。class 契約の `ComputeStableHash()` はレシーバーが null の場合 `ArgumentNullException` を投げます。 |

## エンコーディング契約（v1）

**このエンコーディングは永続的な、バージョン固定の契約です。** 以下のルール一つひとつ — バイト順、フィールド幅、浮動小数点／decimal の正規化規則、先頭のフォーマットマーカー — が永遠に固定されます。これらのいずれかを変更すると、このライブラリがこれまでに生成したすべてのハッシュが静かに変わり、すべての利用者が保存したチェックサムが壊れます。エンコーディングが進化する必要がある場合は、新しい別の API（例えば仮の `StableHash128`/`StableHashWriterV2`）として提供され、このエンコーディング自体の挙動を変更する形では決して進化しません。

すべての出力ストリームはフォーマットマーカー 1 バイト（`0x01`）で始まり、その後に契約ヘッダー — 契約名（下記の長さプレフィックス付き文字列）に続けて `Version` をリトルエンディアン `int32` で — が続きます。メンバーの値の前にはメンバー id（リトルエンディアン `int32`）が置かれます。固定幅整数はすべてリトルエンディアンです。

| 型 | エンコーディング |
|---|---|
| `bool` | 1 バイト（`0x00`/`0x01`） |
| `sbyte`〜`ulong`、`Int128`/`UInt128` | 固定幅、リトルエンディアン |
| `char` | UTF-16 コードユニット、リトルエンディアン `ushort` |
| `enum` | 基底型のエンコーディング（メンバー名の変更は安全、基底値の変更はハッシュが変わる） |
| `float`/`double` | 下記の等価一貫性不変条件に従い正規化した後、ビットパターン（リトルエンディアン） |
| `decimal` | 下記の等価一貫性不変条件に従い正規化した後、sign（1B）+ scale（1B）+ 96bit mantissa（12B、リトルエンディアン） |
| `string` | リトルエンディアン `int32` の UTF-8 バイト数 + UTF-8 バイト列（不正な UTF-16 は `Encoding.UTF8` の決定的な置換文字フォールバックへ） |
| `Guid` | RFC 4122 ビッグエンディアン 16 バイト — `Guid.TryWriteBytes(span, bigEndian: true, out _)`、文字列表現と同じバイト順 |
| `DateOnly` | `DayNumber`、リトルエンディアン `int32` |
| `TimeOnly` / `TimeSpan` | `Ticks`、リトルエンディアン `int64` |
| `DateTimeOffset` | **`UtcTicks` のみ**、リトルエンディアン `int64` — 下記の等価一貫性不変条件を参照 |
| `T?`（`Nullable<T>` / nullable 参照型） | 1 バイトマーカー（`0x00` なし／`0x01` + 値）; non-nullable なメンバーにはマーカー自体が存在しない |
| `T[]`、`List<T>`、`IReadOnlyList<T>`、`ImmutableArray<T>` | リトルエンディアン `int32` の要素数 + 各要素を順に再帰的にエンコード（要素の型もサポート対象である必要があり、ネストは可能） |
| 別の `[StableHashContract]` 型 | その契約全体のエンコーディングをヘッダーごと再帰的に — ネストした契約自身のバージョン／名前の変更も、それを保持するすべてのハッシュに正しく伝播する |

**v1 が拒否するもの（実行時の驚きではなくコンパイル時診断として）:** `DateTime`（`SSALH003` — `DateTimeOffset` か `DateOnly` を使用）、`Dictionary`/`HashSet`/その他の非順序または任意の `IEnumerable<T>`（列挙順序が保証されない）、`object`、デリゲート、ポインタ、インターフェースと抽象型（コンパイル時にランタイム型がわからない）、`[StableHashContract]` のないユーザー定義型、循環する契約グラフ、ジェネリックな契約型。

## 等価一貫性不変条件

> サポートされるすべての型について、**`a == b` ならば `encode(a) == encode(b)`。**

BCL の 3 つの型には、これが静かに破られかねない罠があります — `==` は等しいと言うのに内部ビットが異なるケースです — そのためエンコーディングは書き込む前にこの 3 つを正規化します。

| 型 | 罠 | v1 規則 |
|---|---|---|
| `decimal` | `1.0m == 1.00m` だが内部の scale（したがってビット）が異なる | scale が正で mantissa が割り切れる間、96bit mantissa を 10 で割って正規化（整数演算のみ、最大 28 回）— `1.0m` と `1.00m` は同一にエンコードされる。`0m`、`-0.0m`、`0.00m` などすべてのゼロ表現は 1 つの canonical zero エンコーディング（sign `0x00`、scale `0`、mantissa `0`）に正規化される — decimal の等価性もゼロの符号を区別しないため。 |
| `DateTimeOffset` | 同じ瞬間でもオフセットが異なると `==` が true になる（`1pm+01:00 == noon+00:00`） | **`UtcTicks` のみ**をエンコード — オフセットは意図的に除外される。オフセット自体に意味がある場合は別メンバーとして保存すること。 |
| `float` / `double` | `-0.0 == +0.0` だがビットパターンが異なる; NaN のペイロードビットはプラットフォーム（x86 vs ARM）間で移植性がない | 負のゼロを正のゼロに、すべての NaN ビットパターンを単一の canonical quiet NaN（`float` は `0x7FC00000`、`double` は `0x7FF8000000000000`）に正規化してからビットパターンを書き込む。 |

同じ原則からさらに 2 つの全域性（totality）規則が導かれます。

- **`string`**: 不正な UTF-16（対になっていないサロゲート）は `Encoding.UTF8` のデフォルトの置換文字（`U+FFFD`）フォールバックを使用し、これ自体が決定的です — エンコードに失敗する入力はありません。
- **`ImmutableArray<T>`**: `default(ImmutableArray<T>)`（未初期化）は空として扱われます。すでに多くのコードがこの 2 つを同じものとして扱っているのと一致し、writer をこの型のすべての値に対する全域関数として保ちます。

この不変条件は片方向のみ成立します: `encode(a) == encode(b)` だからといって `a == b` が成り立つ**わけではありません** — 下記のハッシュの意味論を参照。

## 診断

| ID | 重大度 | 条件 |
|---|---|---|
| `SSALH001` | Error | 同じ契約内で 2 つ以上のメンバーが同じ `[StableHashMember]` id を宣言している |
| `SSALH002` | Error | メンバーの型が v1 サポート対象でない（上記の拒否リストを参照） |
| `SSALH003` | Error | メンバーが `System.DateTime` である — 代わりに `DateTimeOffset`（瞬間）か `DateOnly`（暦日）を使用 |
| `SSALH004` | Error | メンバーの型が `[StableHashContract]` のないユーザー定義型 |
| `SSALH005` | Error | この型のメンバーの型を辿っていくと最終的にこの型自身に循環する |
| `SSALH006` | Error | `class` 契約が `sealed` でない（`static class` も同様に拒否） |
| `SSALH007` | Error | 生成された拡張クラスからメンバーを読み取れない（`private`/`protected`、`static`、インデクサ、write-only）— または契約型自体、もしくはその上位の型が生成コードからアクセスできない |
| `SSALH008` | Error | `[StableHashMember(id)]` の id が 1 未満 |
| `SSALH009` | Error | `[StableHashContract]` の name が null／空白、または Version が 1 未満 |
| `SSALH010` | Warning | 契約が `[StableHashMember]` メンバーを 0 個宣言している — すべてのインスタンスが同じ値（ヘッダーのみ）にハッシュされる |
| `SSALH011` | Warning | コンパイル単位内で 2 つ以上の契約型が同じ `[StableHashContract]` 名を宣言している |
| `SSALH012` | Warning | `[StableHashMember]` が `[StableHashContract]` のない型のメンバーに付いている — 孤立した属性で、何も生成されない |
| `SSALH013` | Error | `[StableHashContract]` がジェネリック型、またはジェネリック型の中にネストされた型に付いている（v1 はオープンな契約型未対応） |

上記の Error はすべて、その契約型に対する生成全体を止めます — 部分的な生成はありません。Warning は生成を止めません。

## ハッシュの意味論

`StableHash64` は 64bit の指紋であり、固定幅ハッシュはどれも対称ではなく非対称な保証しか与えません。

- **2 つのハッシュが異なれば、元の値は確実に異なります（100%）。** `encode`／ハッシュが決定的な関数であることから直接導かれます: 入力が同じなら出力が異なることはあり得ないからです。
- **2 つのハッシュが同じであれば、元の値は*ほぼ確実に*同じです — ただし保証はされません。** 64bit の出力空間では、誕生日限界（birthday bound）により、約 2^32（~43 億）個の異なるハッシュ済み値のあたりで衝突が有意になり始めます。少数の値 — 2 つのシミュレーション状態、スナップショットとその直前の値、キャッシュキー — を比較する場合、偶然の衝突の確率は天文学的に小さいです。しかしゼロではありません。

**したがって `StableHash64` は、衝突が致命的な結果を招く場面での同一性の最終判定には適していません** — 例えば、誤った一致でデータを静かに破棄してしまう重複排除などです。このライブラリは、ほぼ絶対に起きない事象が偽陽性を引き起こしても許容できる、あるいは別途検証されるような状況 — desync 検出、変更検出、キャッシュ／ETag の素材、決定的なバケット割り当て — における、安価で高速な比較のためのツールです。

## セキュリティ

**`StableHash64` は暗号学的ハッシュではありません。** 同じハッシュになる 2 つの異なる入力を意図的に作り出そうとする攻撃者に対する衝突耐性の保証はなく、鍵付けもなく、改ざん防止もありません。悪意ある改ざん者に対する完全性検証、メッセージ認証、パスワード保存、デジタル署名、その他いかなるセキュリティ関連の用途にも使用しないでください — そうした用途には `System.Security.Cryptography.SHA256` などを使用してください。

## パフォーマンス

SsalKit.StableHashing は 1 つのパフォーマンス契約を軸に設計されています: **`ComputeStableHash()` の呼び出しごとにアロケーション 0 バイト。** `StableHashWriter` は、すべての値を（小さなインラインステージングバッファを介してバッチ処理しつつ）ハッシャーの状態へ直接ストリーミングする `ref struct` です — どの経路にも中間の `byte[]` シリアライズバッファは存在せず、writer のスタックバッファを超える入力に対する文字列エンコーディングのフォールバック（アロケーションの代わりに `ArrayPool<byte>` からレンタル）も同様です。

BenchmarkDotNet v0.15.8、.NET 10.0.10、AMD Ryzen 9 3950X、Windows 11（SsalKit.StableHashing 0.0.4）で計測しました。数値はハードウェアによって変動します。[ベンチマークプロジェクト](https://github.com/ssalkit/ssalkit/tree/main/benchmarks/SsalKit.StableHashing.Benchmarks) で再現できます。ナイーブなベースラインを除くすべての行で Allocated は 0 B です。

| シナリオ | 所要時間 | アロケーション |
|---|---:|---:|
| 小型契約（スカラーメンバー 4 個） | 112.9 ns | 0 B |
| 中型契約（文字列 + ネストした契約を含む 12 メンバー） | 321.2 ns | 0 B |
| コレクションメンバー、要素 10 / 100 / 1000 個 | 142 ns / 787 ns / 7.38 μs | 0 B |
| 文字列メンバー、ASCII / 韓国語 / 長文（プールフォールバック） | 100 ns / 134 ns / 251 ns | 0 B |
| ナイーブなベースライン: 手動シリアライズ後にハッシュ | 242 ns | 632 B |
| 生成された `ComputeStableHash()`、上のベースラインと同じデータ | 370 ns | 0 B |

最後の 2 行は正直に一緒に読む必要があります: ナイーブな方法 — 値を手動で `byte[]` にシリアライズしてからそのバッファをハッシュする方法 — は、この計測では純粋な時間としては（242 ns vs. 370 ns）生成されたストリーミングコードより*速い*です。その代償として呼び出しごとに 632 バイトを割り当てますが、生成された経路は何も割り当てません。これは見落としではなく意図的なトレードオフです: ティックループ、保存経路、あるいは 1 秒間に何千回も呼ばれるその他のホットパスでは、呼び出しごとの GC 圧力ゼロが約 130 ns の時間差より価値があります — ナイーブ版の呼び出しごとの 632 B は規模が大きくなると実際のコレクション停止につながりますが、ストリーミング設計のコストは呼び出し頻度に関わらず一定です。`ComputeStableHash()` をまれにしか呼ばない場合（例えば HTTP リクエストごとに 1 回）、手書きシリアライズの生の速度の優位性はそれほど重要でないかもしれませんが、それでも生成された経路を使えばそのアロケーションコストについて考える必要が一切なくなります。

## ライセンス

MIT — 詳細は [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE) を参照してください。

---

**AI に関する開示:** 本プロジェクトは AI（Claude）を活用して制作されました。
