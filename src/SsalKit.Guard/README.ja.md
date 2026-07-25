[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ja.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Guard/README.md) | [한국어](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Guard/README.ko.md) | **日本語**

# SsalKit.Guard

エラーコードベースのドメイン例外ライブラリです。副作用のない `ErrorCodedException` 基底クラス、呼び出し側が書いた式のテキストをそのまま取り込む静的なガード節、そして派生型が常に基底型より先にマッチするよう整列された例外 → コードのマッピングテーブルをコンパイル時に生成します。依存関係はありません。
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Guard.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Guard)

## なぜ SsalKit.Guard なのか

外の世界に応答するサービスは、いずれ同じ層を持つようになります。ドメインが投げた例外を受け取り、呼び出し側が理解できるコードへ変換する境界です。この層を手で書いていると、たいてい 3 つの問題が付いてきます。

- **生成時に仕事をしてしまう例外。** 例外のコンストラクターの中で `Activity.Current` にタグを付けたり、ログを書いたり、カウンターを進めたりするのは、最初の一度だけ便利です。例外を生成する瞬間は、例外を処理する瞬間ではありません。捕捉された例外はずっと後で再スローされたりラップされたりするため、タグは違う時点を指すことになりますし、例外を生成するだけのテストが、頼んでもいないアンビエントなテレメトリーを汚染します。
- **IntelliSense を占領するガードヘルパーと、手で書く失敗コンテキスト。** `value.ThrowIfNull(...)` のような `this T` 拡張は、検証と何の関係もない呼び出し箇所でも、あらゆる参照型の IntelliSense に現れます。さらに、チェックを失敗させた値は手で保守する `(string Name, object Value)[]` からメッセージへ付け足されますが、この一覧は説明すべき条件式とすぐに食い違っていきます。
- **正しさがコメントに依存するマッピング switch。** 例外 → コードの `switch` は派生型を基底型より先に書かなければならず、全員がそれを覚えている間だけ正しくあり続けます。実際には「この型は下の型のサブタイプなので、必ず先にマッチさせること」といったコメントで守ることになります。新しい例外を追加して登録を忘れても、コンパイラは何も言ってくれません。

SsalKit.Guard は、この 3 つをそれぞれ分解します。

- **`ErrorCodedException` は純粋なデータです。** このライブラリのどのコンストラクターも、`Activity` にタグを付けず、ログを書かず、メトリクスも出しません。観測は、周囲のリクエストコンテキストを知る唯一の場所である境界の仕事であり、その姿は本ドキュメントで示します。
- **`Guard.` は静的なエントリーポイントで、失敗コンテキストはコンパイラが取り込みます。** すべての節が最後の引数として `[CallerArgumentExpression]` パラメーターを受け取るため、チェックした式のソーステキストが無料でメッセージに入ります。`Guard.That (order.Status == OrderStatus.Open) failed.` のように。
- **マッピングテーブルは生成されます。** `static partial class` に `[ErrorCodes<TCode>]` を付ければ、例外 → コードのルックアップが代わりに書き出され、登録された型の継承の深さから「最も派生した型が先」という順序が組み立てられます。保守すべき順序は存在せず、誤用は静かに間違ったコードを返すのではなくコンパイル時の診断として現れます。
- **依存関係ゼロ。** BCL のみを使用します。

## インストール

```bash
dotnet add package SsalKit.Guard
```

パッケージにはランタイム型（`Guard`、`ErrorCodedException`、3 つの属性）とソースジェネレーターの両方が含まれています。別途インストールする analyzer パッケージはなく、このパッケージ自身の `PackageReference` もありません。

**動作要件:** .NET 10+。コードはジェネリック属性（`[ErrorCode<GameStatusCode>(...)]`）で宣言するため、C# 11 以降が必要です。

## ガード節

節は 5 つあり、いずれも引数チェックではなくドメインの不変条件を表します。すべての節は末尾にコンパイラが埋めてくれる `[CallerArgumentExpression]` パラメーターを受け取るため、チェックした式のソーステキストが手で書かなくてもメッセージに現れます。

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

| 節 | 失敗する条件 | 戻り値 | 失敗メッセージ |
|---|---|---|---|
| `Guard.That(condition)` | `condition` が `false` | `void` | `Guard.That ({expression}) failed.` |
| `Guard.NotNull(value)` | `value` が `null`（参照型および `Nullable<T>`） | null 非許容の値 | `Guard.NotNull ({expression}) failed: value was null.` |
| `Guard.NotNullOrEmpty(value)` | 文字列が `null` または空 | `string` | `Guard.NotNullOrEmpty ({expression}) failed: value was null or empty.` |
| `Guard.NotNullOrWhiteSpace(value)` | 文字列が `null`、空、またはすべて空白 | `string` | `Guard.NotNullOrWhiteSpace ({expression}) failed: value was null, empty, or white-space.` |
| `Guard.InRange(value, min, max)` | `value` が閉区間 `[min, max]` の外 | `T` | `Guard.InRange ({expression}) failed: value {value} was outside the inclusive range [{min}, {max}].` |

メッセージの契約は、`That` が `Guard.{Clause} ({expression}) failed.`、それ以外が `Guard.{Clause} ({expression}) failed: {detail}` です。`InRange` は値と両端の境界を invariant culture でレンダリングするため、どこで読んでも同じ文字列になります。式のテキストが得られない場合 — 呼び出し側が明示的に `null` や空文字列を渡した場合か、`[CallerArgumentExpression]` を尊重しない言語から呼び出した場合だけです — はプレースホルダー `<expression unavailable>` が代わりに入ります。

`That` を除くすべての節はチェックした値を返すので、ガードは隣に並ぶ別の文ではなく、守る対象の式の一部として読めます。

```csharp
string teamName = Guard.NotNull(player.Team).Name;
```

### 独自の例外を投げる

`That` と参照型の `NotNull` には例外ファクトリーを受け取るオーバーロードもあり、ファクトリーはチェックが失敗したときにのみ呼び出されます。

```csharp
Guard.That(balance >= amount, () => new InsufficientFundsException(balance, amount));

Team team = Guard.NotNull(player.Team, () => GameErrors.InvalidTeam($"player {player.Id} is on no team"));
```

成功パスでは一切アロケーションが発生しません。ファクトリーは呼ばれず、メッセージもチェックが失敗した後にしか組み立てられません。ファクトリーが `null` を返した場合は、実際に失敗したガードについて何も語らない素の `NullReferenceException` ではなく、どの節だったかを示す `GuardViolationException` が投げられます。

### これは引数の検証ではありません

パラメーターの契約は BCL がすでにカバーしています。`ArgumentNullException.ThrowIfNull`、`ArgumentException.ThrowIfNullOrWhiteSpace`、`ArgumentOutOfRangeException.ThrowIf*` 系があり、これらは呼び出し側やアナライザーが引数チェックに期待する例外型を投げます。`Guard` は意図的にそれらを重複して提供しません。

「不正な引数を渡された」は BCL で、「この集約はもうこの操作を許す状態ではない」は `Guard` で扱ってください。後者の失敗は `ArgumentException` ではなく、エラーコードへマップされるドメインエラーです。

## エラーコード

### 宣言する

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

// コードは例外型に、一度だけ宣言します。
[ErrorCode<GameStatusCode>(GameStatusCode.NotFound)]
public class NotFoundException : ErrorCodedException
{
    public NotFoundException(string? message = null) : base(message) { }
}

// 上の型を継承しつつ別のコードを持ちます — 後述の順序保証を参照してください。
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

// マッピングコンテナー。この 4 行が宣言のすべてです。
[ErrorCodes<GameStatusCode>]
[ExternalErrorCode<GameStatusCode>(typeof(TimeoutException), GameStatusCode.ServerBusy)]
[ExternalErrorCode<GameStatusCode>(typeof(GuardViolationException), GameStatusCode.GuardViolation)]
public static partial class GameErrors;
```

`[ExternalErrorCode]` は、自分が所有していない例外にコードを与える場所です。BCL の型、キャッシュクライアントのタイムアウト、クラスターライブラリの障害、トークン検証エラーなど。実際の境界ではこうした例外がテーブルの半分を占めることも珍しくありませんが、これらに `[ErrorCode]` を付けることはできないため、コンテナー側で宣言します。

### 何が生成されるか

`GameErrors` のもう半分に、次のものが生成されます。

```csharp
// ルックアップ。派生型から先にテストされます。
if (GameErrors.TryMap(exception, out GameStatusCode code)) { /* ... */ }

// 順序は同じで、「マッチする登録がない」ことをどう伝えるかだけが異なります。
GameStatusCode mapped = GameErrors.MapOrDefault(exception, GameStatusCode.Unspecified);

// [ErrorCode] の例外 1 つにつき、ファクトリー 1 つと [DoesNotReturn] な throw ヘルパー 1 つ。
// いずれもその例外自身のコンストラクターをミラーリングします。
throw GameErrors.UserNotFound("player p-42 no longer exists");
GameErrors.ThrowInvalidTeam("a team needs at least two members", new TimeoutException("roster lookup"));
```

ヘルパー名は `Exception` サフィックスを落とした形です（`UserNotFoundException` → `UserNotFound`、`ThrowUserNotFound`）。それで登録済みの 2 つの型の名前が衝突する場合は型名全体を使い、さらに衝突する場合は FQN を平坦化した名前へ後退します。

生成されるルックアップはごく普通の `is` チェーンなので、保証される順序がそのままコードとして読めます。

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

### 派生型が常に先、しかも自動で

`UserNotFoundException` は `NotFoundException` を継承しつつそれぞれ別のコードを持つため、基底型が先にテストされてはいけません。上のコードでは実際にそうなっていません。この順序は登録された型の継承の深さ（深いものから先、同率は FQN 順で出力が決定的）から生成されます。つまり、

- **保守すべき順序がありません。** 既存の継承階層の途中に新しい例外を差し込んでも、次のビルドで正しい位置に収まります。維持するコメントも、レビュー用チェックリストも要りません。
- **マッチはランタイム型で行われます。** インスタンスを基底型の変数に入れていても — `catch` 節に届くのはまさにその形です — 派生型のコードが得られます。
- **曖昧さは解決せず拒否します。** 同じ例外型を 1 つのコンテナーに 2 回登録するとエラー（`SSALG003`）です。候補コードが 2 つあるのに宣言順で勝者を決めるのは静かな優先順位ルールであり、それこそがこのライブラリの置き換え対象です。
- **未登録の例外は何にもマッチしません。** `TryMap` は `false` を返します（null 参照でも同様）。おかげで「マップされていない」が実在するどのコードとも区別され続けます。

### ファクトリーは例外のコンストラクターをミラーリングします

v1 は 3 つの public コンストラクター形状を認識し、例外が宣言しているもののうち最も広いものを、null 許容性まで含めてミラーリングします。

| 例外側のコンストラクター | 生成されるファクトリー（throw ヘルパーは先頭に `Throw` が付いた同じ形） |
|---|---|
| `()` | `Empty()` |
| `(string? message = null)` | `MessageOnly(string? message = null)` |
| `(string message)` | `Required(string message)` — null 非許容なのでパラメーターは必須のまま |
| `(string? message, Exception? innerException)` | `Full(string? message = null, Exception? innerException = null)` |

どれも宣言していない例外も、マッピングテーブルには問題なく参加します。ヘルパーが生成されないだけで、その理由を利用者に推測させず `SSALG006` が知らせます。外部登録した型にもヘルパーは生成されません — このライブラリは、自分が所有していない型のコンストラクター契約を保証できないからです。

### 複数のコンテナー、複数のコード enum

コンテナーは自分の enum を指定した `[ErrorCode<TCode>]` の例外だけを集めるため、無関係なドメインを分けたまま保てます。

```csharp
[ErrorCodes<GameStatusCode>]
public static partial class GameErrors;

[ErrorCodes<BillingStatusCode>]
public static partial class BillingErrors;
```

それぞれが自分の `TryMap`・`MapOrDefault`・ヘルパーを持ち、互いに混ざることはありません。

### アクセシビリティ

生成される部分は、コンテナー（およびそれを包むすべての型）を元のアクセシビリティのまま再宣言し、生成される各メンバーのアクセシビリティは結果がコンパイルできるようにクランプされます。`TryMap`/`MapOrDefault` はコード enum が public でなければそれに合わせて下がり、各ファクトリーと throw ヘルパーも対象の例外型が public でなければ同様に下がります。したがって `internal` なコンテナーの中の `internal` な enum なら、メンバーも `internal` として生成され、手で直すアクセシビリティの不一致は発生しません。

生成ファイルからそもそも名前を書けない例外 — `private`、`protected`、`private protected`、`file`-local — はエラー（`SSALG009`）です。そのまま含めると、利用者が書いた覚えのないコードを指し示す、コンパイルできない生成ファイルができてしまうためです。

## 境界にて

捕まえて、マップして、応答する。表面全体を 1 つの関数が担当します。

```csharp
public Response Handle(Func<Response> operation)
{
    try
    {
        return operation();
    }
    catch (Exception exception) when (GameErrors.TryMap(exception, out GameStatusCode code))
    {
        // 観測はここに置きます。リクエストコンテキストが存在し、処理するという判断が
        // すでに下された場所だからです。例外自身はここへ来るまで何もしていません。
        Activity.Current?.SetTag("error.code", (int)code);
        logger.LogWarning(exception, "request failed with {ErrorCode}", code);

        return Response.Failure(ToTransportStatus(code), (int)code, exception.Message);
    }
}
```

押さえておきたい点が 3 つあります。

- **タグ付けとロギングは利用側の仕事です。** これは、例外のコンストラクター自身が `Activity.Current` にタグを付けていた本ライブラリの原型から、意図的に変更した点です。ここで行えば、捕捉されラップされて再スローされる例外も、実際に処理されるその瞬間に一度だけ記録され、例外を生成するだけの単体テストはテレメトリーにまったく触れません。
- **`when` フィルターの `TryMap` は、マップされない例外をそのまま通します。** 語ることのないハンドラーに飲み込まれるのではなく上へ伝播し続けるので、境界ではたいていそちらが望ましい挙動です。フォールバックのコードで十分な場面では、`MapOrDefault(exception, GameStatusCode.Unspecified)` が短い書き方になります。
- **マッピングはあなたの enum で止まります。** `GameStatusCode` を HTTP ステータスや gRPC コード、ワイヤー上の整数に変換するのはトランスポート層の仕事です。生成されるルックアップが `TCode` を返し、このライブラリにトランスポートの匂いが一切ないのは、まさにそのためです。

## 診断

| ID | 重大度 | 報告される条件 |
|---|---|---|
| `SSALG001` | エラー | `[ErrorCode]` が `ErrorCodedException` 派生ではない型に付けられている。 |
| `SSALG002` | エラー | `[ErrorCodes]` コンテナーが `static partial class` ではない。 |
| `SSALG003` | エラー | 同じ例外型が 1 つのコンテナーに 2 回以上登録されている。 |
| `SSALG004` | エラー | `[ExternalErrorCode]` が例外でない型、または未束縛のジェネリック型を指定している。 |
| `SSALG005` | エラー | `[ErrorCode]` の例外が abstract、ジェネリック、またはジェネリック型の中にネストされている。 |
| `SSALG006` | 警告 | `[ErrorCode]` の例外が認識可能なコンストラクターを 1 つも宣言しておらず、ファクトリーと throw ヘルパーが生成されない（マッピングには参加する）。 |
| `SSALG007` | エラー | `[ErrorCodes]` コンテナーがジェネリック、またはジェネリック型の中にネストされている。 |
| `SSALG008` | 警告 | `[ErrorCode<TCode>]` の例外があるのに、その enum 用の `[ErrorCodes<TCode>]` コンテナーがコンパイル内に存在せず、どこにも何も生成されない。 |
| `SSALG009` | エラー | `[ErrorCode]` の例外が生成ファイルからアクセスできない（`private`、`protected`、`private protected`、`file`-local）。 |

1 つの登録に関するルール（`SSALG001`、`SSALG004`、`SSALG005`、`SSALG009`）は、その登録だけを捨ててコンテナーの残りはそのまま残します — 宣言を誤った例外 1 つがマッピングテーブル全体を道連れにすべきではないからです。コンテナー自体に関するルール（`SSALG002`、`SSALG007`）や、ジェネレーターが利用者に代わって解決することを拒む曖昧さ（`SSALG003`）は、そのコンテナーの生成ファイルを丸ごと抑制します。

## 知っておきたいこと

- **コードは常に宣言された型です。** このライブラリに `throw new SomeException(4001, "…")` はありません。`ErrorCodedException` はコードのフィールドを持たず、匿名同然の例外に任意のコードを載せて投げる使い方は意図的にサポートしていません。コードごとに小さなクラスを 1 つ書けば、`catch` する対象、ドキュメントになる対象、コンパイラが検査できる対象が同時に手に入ります。1 か所の throw に整数としてだけ存在するコードは、その 3 つすべてから見えません。その形からの移行にはこれらのクラスを書く手間がかかりますが、それはこのライブラリが意図して選んだトレードオフです。
- **ミラーリングされるコンストラクターは 3 形状だけです。** `()`、`(string?)`、`(string?, Exception?)`。`InsufficientFundsException(decimal balance, decimal amount)` のようにドメイン固有のパラメーターを取る例外も、マッピングは問題なく行われ、ヘルパーがない理由は `SSALG006` が知らせ、生成は普通に `new` で行えます。ガードの例外ファクトリーのオーバーロードの中でも同じです。
- **`GuardViolationException` にコードを与えてください。** この型も他のドメイン失敗と同じく `ErrorCodedException` を継承していますが、このパッケージ内に宣言されているため、型ではなくコンテナー側で登録します。`[ExternalErrorCode<GameStatusCode>(typeof(GuardViolationException), GameStatusCode.GuardViolation)]` の 1 行です。この行がなければすべてのガード失敗はマップされないまま通り抜け、あれば内部不変条件の違反があなたの enum の中で一級のコードになります。
- **`ErrorCodedException` は `catch` の対象でもあります。** `catch (ErrorCodedException)` の 1 行でドメインの失敗をそれ以外すべてと分離できるので、マッピングの前段階で両者を違うふうに扱いたい境界で役に立ちます。
- **迷うなら 1 クラスに 1 コンテナー。** 1 つのクラスに `[ErrorCodes<A>]` と `[ErrorCodes<B>]` を同時に付けることはでき（互いに別の属性型です）、enum ごとの生成ファイルも分かれます。対応していないのは、1 つの例外が*両方*の enum に対してコードを宣言した場合です。そのとき、同じクラスの両方の半分に同名のヘルパーが生成されます。

## ライセンス

MIT — 詳細は [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE) を参照してください。

---

**AI に関する開示:** 本プロジェクトは AI（Claude）を活用して制作されました。
