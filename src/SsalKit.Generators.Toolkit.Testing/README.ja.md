[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ja.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Generators.Toolkit.Testing/README.md) | [한국어](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Generators.Toolkit.Testing/README.ko.md) | **日本語**

# SsalKit.Generators.Toolkit.Testing

インクリメンタルソースジェネレーターと analyzer をテストするための、薄くテストフレームワークに依存しないハーネスです。インメモリのソースに対してジェネレーターを実行し、生成されたコードが再コンパイルできることを証明し、診断を id・severity・位置で検証します — そして、スナップショットでは決して捕まえられないただ一つのこと、つまりあなたのパイプラインが今もキャッシュされ続けているかどうかを検証します。
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Generators.Toolkit.Testing.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Generators.Toolkit.Testing)

## なぜ SsalKit.Generators.Toolkit.Testing なのか

すべてのソースジェネレーターテストプロジェクトは、同じ40行を書くことから始まります。文字列から `CSharpCompilation` を構築する。生成されたコードが、テストが宣言したスタブではなく本物の BCL に対して型チェックされるよう、どの参照アセンブリを渡すべきかを調べる。`CSharpGeneratorDriver` を作成する。実行する。生成ソースを `GetRunResult().Results.SelectMany(...)` から取り出す。そして*出力*コンパイルの診断も忘れずに実行する — そうしないと、構文的には正しいが型チェックを通らないコードを出すジェネレーターが、書いたすべてのアサーションを素通りしてしまうからです。どれも面白みのある作業ではなく、どのプロジェクトでも同じであり、それぞれのプロジェクトがブログ記事から同じものを再導出しています。

それは退屈な半分です。もう半分こそが、このパッケージが存在する理由です。

**あなたのジェネレーターが今もインクリメンタルであることを検証するものは何もありません。** インクリメンタルジェネレーターのパイプラインは値の等価性でキャッシュします。`ISymbol`、`Location`、生の `ImmutableArray<T>`、あるいは参照等価性を持つ何かをパイプラインモデルに入れてしまうと、キャッシュは静かに機能しなくなります — すべてのステージがキー入力のたびに再計算され、テストでは 3ms で終わっていたジェネレーターが、利用者が IDE で1文字打つたびにパイプライン全体を再実行するようになります。スナップショットテストはそれでも通り続けます。*出力*は変わらず正しいからです。変わったのはコストだけです。

Roslyn 自身はその証拠を公開しています — `GeneratorDriverOptions(trackIncrementalGeneratorSteps: true)` は各出力についてステップごとの理由(`Cached`、`Unchanged`、`Modified`、`New`)を記録します — しかし `TrackedSteps` を正しく読み解くには、同じドライバーで2回実行し、その間にコンパイルを変更し、どの理由が何を証明するのかを理解している必要があります。実際にそこまでやる人はほとんどおらず、それをアサーションとして提供するパッケージ化されたハーネスもありません。

本パッケージはそれを提供します。

- **`IncrementalAssert.AllCachedOrUnchanged`** — パイプラインのモデルが観測しない編集の後は、何も再計算されてはいけません。これは、モデルが値の等価性を失った瞬間に失敗するアサーションです。
- **`IncrementalAssert.SomeOutputRecomputed`** — モデルが*実際に*観測する編集の後は、何かが再計算されなければなりません。これは、エミッターが実際に使っているフィールドをモデルが取りこぼしたときに失敗するアサーションで、そうでなければ古い出力を永遠に返し続けることになります。

両者を合わせると両面契約になります。多くを取り込みすぎたモデルは前者で失敗し、取り込みが足りないモデルは後者で失敗します。ステップトラッキングは常に有効なので、事前のオプトインなしにどの実行でも両方を利用できます。

パッケージのそれ以外の部分はすべて、あなたがもう書かなくてよくなる定型コードです。

- **既定で本物の参照アセンブリを使用します。** テスト対象のコンパイルは、テストホスト自身が信頼するすべての参照アセンブリに対して構築されるため、`AssertCompilesCleanly()` は生成コードを本物の BCL に対して型チェックします。`AdditionalAssemblies = [typeof(MyAttribute).Assembly]` を使えば、出荷するランタイムパッケージをそこに追加できるので、生成された呼び出しは、テストソースに貼り付けたコピーではなく、実際に出荷している API に対して検証されます。
- **意図がそのまま読めるアサーション。** `GetSingleSource()`、`GetSource("...ServiceCollectionExtensions.g.cs")`、`AssertNoGeneratedSources()`、`DiagnosticAssert.Single(..., exclusive: true)`、`DiagnosticAssert.LocatedOn(diagnostic, "[Marker]")` — 失敗メッセージも `Expected 1, got 0` ではなく、*実際に*何が生成されたか、あるいは*実際に*何が報告されたかを列挙します。
- **analyzer にも対応します。** 同じコンパイル設定でパッケージ全体の analyzer をまとめて実行できるため、テストソースがどの構文を使っていても他の analyzer が沈黙し続けることを証明できます。

## インストール

```bash
dotnet add package SsalKit.Generators.Toolkit.Testing
```

これは**テストプロジェクト**用のパッケージです。出荷するジェネレータープロジェクトではなく、テストプロジェクトから参照してください。

```xml
<ItemGroup>
  <PackageReference Include="SsalKit.Generators.Toolkit.Testing" Version="0.1.0" />
</ItemGroup>
```

## 動作要件

- テストプロジェクトが **`net10.0`** 以降をターゲットにしていること。
- パッケージは **`Microsoft.CodeAnalysis.CSharp`** を同梱します — これが唯一の依存関係であり、意図的にどのテストフレームワークにも依存しません。

## クイックスタート

### ジェネレーターを実行して生成結果を確認する

```csharp
using SsalKit.Generators.Toolkit.Testing;

[Fact]
public void EmitsAGreeterForEachMarkedType()
{
    var result = GeneratorTest.Run<GreeterGenerator>(
        """
        namespace Demo;

        [Mine.Marker("hello")]
        public sealed class Widget;
        """);

    string generated = result.AssertCompilesCleanly().GetSingleSource();

    Assert.Contains("public static class WidgetGreeter", generated);
}
```

`AssertCompilesCleanly()` は生成ソースを含んだ状態でコンパイルを再チェックし、その結果を返すので、そのまま `GetSingleSource()` へ連結できます。これは「出力されたテキストが正しく見える」ことと「出力されたテキストが呼び出し先の API に正しくバインドする有効な C# である」ことを分けるアサーションであり、自前で組んだハーネスの多くが見落としているものです。

1回の実行が複数のファイルを生成する場合、`GetSource("...Extensions.g.cs")` は hint name の接尾辞でその1つを取り出せます。また `ToSnapshotText()` はすべてを(それぞれ `// ==== <hint name>` 行を前置して)1つの文字列にレンダリングし、スナップショットライブラリへ渡せるようにします。

```csharp
[Fact]
public Task WholeRun_MatchesSnapshot()
{
    var result = GeneratorTest.Run<GreeterGenerator>(Source, Options);

    return Verify(result.AssertCompilesCleanly().ToSnapshotText());
}
```

### コンパイル設定を共有する

テストプロジェクトごとに1つの `static readonly` なオプションインスタンスを持つようにすれば、生成コードが何に対して型チェックされるかを決める場所が1か所に定まります。

```csharp
internal static class GeneratorTestSupport
{
    public static readonly GeneratorTestOptions Options = new()
    {
        // このパッケージ自身の診断だけがアサーションに届くため、意図的に不正な
        // テストソースが出す付随的なコンパイラーエラーを手作業でフィルタする必要がない。
        DiagnosticIdPrefix = "MINE",

        // 複数ファイルのスナップショットが、生成順序の変化だけで揺れることはない。
        SortGeneratedSourcesByHintName = true,

        // 生成コードは出荷するランタイムパッケージに対して検証される。
        AdditionalAssemblies = [typeof(Mine.MarkerAttribute).Assembly],
    };
}
```

どのエントリーポイントもオプションを受け取り(または `GeneratorTestOptions.Default` を意味する `null`)、これはイミュータブルな record なので、一時的なバリエーションは `with` 式で作れます — `Options with { AllowUnsafe = true }`。

### キャッシュ契約を検証する

```csharp
using Microsoft.CodeAnalysis.CSharp;

[Fact]
public void AnUnrelatedEditRecomputesNothing()
{
    var (_, second) = GeneratorTest.RunTwiceWithCompilationChange<GreeterGenerator>(
        Source,
        static compilation => compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText("// nothing to do with the generator")));

    IncrementalAssert.AllCachedOrUnchanged(
        second, TrackingNames.Models, TrackingNames.Collected);
}

[Fact]
public void AnEditTheModelCapturesFlowsThroughToTheOutput()
{
    var (first, second) = GeneratorTest.RunTwice<GreeterGenerator>(
        Source, static source => source.Replace("\"hello\"", "\"goodbye\""));

    Assert.Contains("hello", first.GetSingleSource());
    Assert.Contains("goodbye", second.GetSingleSource());

    IncrementalAssert.SomeOutputRecomputed(second, TrackingNames.Models);
}
```

どちらも、1つのドライバーを共有する2回の実行のうち**2回目**を受け取ります — `RunTwice` はソースファイルを丸ごと差し替え、`RunTwiceWithCompilationChange` はコンパイルをそのまま渡してくれるので構文木を追加したり差し替えたりできます。トラッキング名は、あなたのパイプラインが `WithTrackingName` に渡したものそのものです。記録されなかった名前を指定した場合、失敗メッセージはその実行が*実際に*記録した名前の一覧を示すので、たいていはそれだけでタイプミスに気づけます。

`RunTwice` に何が表現できて何ができないかを押さえておきましょう。このメソッドはソースファイル全体を差し替えるため、2回目のソースを変形すると構文駆動のすべてのステージが構造上無効化されます。変形を渡さずに呼ぶと同一のテキストを再パースすることになり、これはもっとも厳しいキャッシュテストです — パイプラインが観測するものは何ひとつ変わっていないので、何も再計算されてはなりません。ただし、*コンパイルの別の場所*での編集が何も変えないこと — 現実的な IDE のシナリオです — を検証したい場合は、上記のように `RunTwiceWithCompilationChange` を使って無関係な構文木を追加してください。

アサーションが失敗すると、ステップごとのキャッシュ状態が出力されます。これが「キャッシュが壊れた」を「`Models[0] -> Modified`」という具体的な情報に変えてくれます。

```
Expected every output of step 'Models' to be Cached or Unchanged after the second run,
but 1 of them was recomputed.

Cache state of the requested steps:
  Models[0] -> Modified

Tracking names recorded by this run:
  - Collected
  - Models
```

### 診断を検証する

```csharp
[Fact]
public void AMarkerOnAStructIsRejected()
{
    const string source = """
        namespace Demo;

        [Mine.Marker("hello")]
        public struct Widget;
        """;

    var result = GeneratorTest.Run<GreeterGenerator>(source, Options);

    var diagnostic = DiagnosticAssert.Single(
        result.Diagnostics, "MINE001", DiagnosticSeverity.Error, exclusive: true);

    Assert.Contains("Widget", diagnostic.GetMessage());
    DiagnosticAssert.LocatedOn(diagnostic, """[Mine.Marker("hello")]""", source);
}
```

- `Single` は、指定した id を持つ診断がちょうど1つであることを検証し、任意で severity と位置もチェックしたうえで、その診断を返すのでメッセージも併せて検証できます。`exclusive: true` を指定すると、報告された診断が*それだけ*であることも追加で検証します — テストソースがちょうど1つのことだけを引き起こすはずの場合に使えば、期待した診断の陰に想定外の2つ目の診断が紛れ込むのを防げます。
- `LocatedOn` は行・列ではなく**ソースの断片**で位置を指定するため、アサーションが読みやすいまま保たれ、その上のソースが編集されても崩れません。指定した断片はソース中にちょうど1回だけ現れなければならず、診断のスパンはその中に収まっている必要があります。キャッシュ安全な位置レコードから復元されたジェネレーター診断は構文木を持たないため、そちらは(上記のように)ソースを渡す必要があります。analyzer の診断は不要です。
- `None(diagnostics, "MINE")` は、その接頭辞を持つ診断が*1つも*報告されなかったことを検証します — テストで名前を付け忘れた診断も、これで捕まえられます。

analyzer も同じ設定を通して、まとめて実行できます。

```csharp
var diagnostics = await GeneratorTest.RunAnalyzersAsync(
    source, [new MarkerAnalyzer(), new NamingAnalyzer()], Options);

DiagnosticAssert.None(diagnostics, "MINE");
```

## API 概要

| 型 | 内容 |
|------|--------------|
| `GeneratorTest` | すべてのエントリーポイントです。単発の実行には `Run<TGenerator>`、インクリメンタルアサーションが消費する2回実行のペアには `RunTwice<TGenerator>`/`RunTwiceWithCompilationChange<TGenerator>`、analyzer には `RunAnalyzerAsync<TAnalyzer>`/`RunAnalyzersAsync`、何も実行せずコンパイルだけを構築するには `CreateCompilation`、参照用に別の独立したアセンブリをコンパイルするには `CompileToReference` を使います。 |
| `GeneratorTestOptions` | 共有される設定項目を持つイミュータブルな record です。`AssemblyName`、`LanguageVersion`、`NullableContextOptions`、`OutputKind`、`AllowUnsafe`、`AdditionalReferences`、`AdditionalAssemblies`、`DiagnosticIdPrefix`、`SortGeneratedSourcesByHintName` を持ちます。`null` が意味するのは `GeneratorTestOptions.Default` です。 |
| `GeneratorTestResult` | 1回の実行が生成したものです。データ: `GeneratedSources`、`Diagnostics`、`OutputCompilation`、`RawResult`、`TrackedSteps`。検索: `GetSingleSource()`、`GetSource(hintNameSuffix)`、`GetCompilationErrors()`、`ToSnapshotText()`。アサーション: `AssertCompilesCleanly()`、`AssertNoGeneratedSources()`。 |
| `GeneratedSource` | 生成されたファイル1つ分です。`HintName` と `Text` を持つ `readonly record struct` です。 |
| `IncrementalAssert` | キャッシュ契約です。`AllCachedOrUnchanged(secondRun, ...names)` と `SomeOutputRecomputed(secondRun, ...names)` を持ちます。 |
| `DiagnosticAssert` | `Single(diagnostics, id, severity?, locatedOnSnippet?, source?, exclusive?)`、`None(diagnostics, idPrefix)`、`LocatedOn(diagnostic, snippet, source?)` を持ちます。 |
| `GeneratorAssertionException` | すべてのアサーションが投げる失敗シグナルで、診断内容をメッセージに含みます。 |

## テストフレームワーク非依存

このパッケージはどのテストフレームワークも参照しません。失敗したアサーションは `GeneratorAssertionException` を投げます。どのフレームワークもテストからの未処理例外を失敗として扱うため、同じハーネスが xunit、NUnit、MSTest、TUnit のもとで変更なく動作します — 2つのフレームワークを併用しているリポジトリでも、ハーネスが2種類に分かれることはありません。

トレードオフとして、失敗はあなたのフレームワークが持つネイティブなアサーション型ではなく例外として表面化するため、診断の中身をメッセージ自体が担わなければなりません。そこにこそ意図的に力を注ぎました。失敗メッセージは、単なる期待値対実際値ではなく、*実際に*生成されたすべてのファイルの hint name、再生成されたコンパイルのコンパイラーエラー、*実際に*報告されたすべての診断の id・severity・位置・メッセージ、あるいはステップごとのキャッシュ状態を列挙します。

あなたのフレームワークが得意とする部分(生成テキストに対する `Assert.Contains`、`[Theory]` のデータなど)にそのフレームワークのアサーションを混ぜて使うことも、何ら妨げられません。このハーネスが担うのは、ジェネレーターに関するアサーションだけです。

## `Microsoft.CodeAnalysis.Testing` との関係

これは `Microsoft.CodeAnalysis.Testing` の代替ではありませんし、そうなろうともしていません。両者は異なる問題を解決します。

| | `Microsoft.CodeAnalysis.Testing` | 本パッケージ |
|---|---|---|
| 対象 | analyzer と**コードフィックス** | インクリメンタルな**ソースジェネレーター** |
| 形 | `TestState`/`FixedState`、マークアップ構文、フレームワークごとのパッケージを持つ設定可能な `*Test` フィクスチャー | 結果オブジェクトを返す普通の静的メソッド |
| 診断 | 期待される状態として宣言し、フィクスチャーが検証する | 実行後に明示的にアサートする |
| コードフィックス/リファクタリングの検証 | 可能 — これを使う理由そのもの | 不可 |
| マルチプロジェクト、追加ファイル、`.editorconfig`、analyzer 設定 | 手厚くサポート | 対象外 |
| インクリメンタルキャッシュ(`trackIncrementalGeneratorSteps`) | 不可 | **可能** — これを使う理由そのもの |

*コードフィックスを伴う* analyzer をテストしているなら `Microsoft.CodeAnalysis.Testing` を使ってください。ここには `VerifyCodeFixAsync` を置き換えるものは何もありません。インクリメンタルジェネレーター — その出力コード、診断、そして今もキャッシュされているかどうか — をテストしているなら、本パッケージは学ぶべきことがずっと少なく、しかも相手にはない一点をカバーします。1つのリポジトリで両方を使っても問題ありません。状態も設定も共有しません。

## 注意点

### `CompileToReference` は `AssemblyName` を上書きする

`CompileToReference(source, assemblyName, options)` は、アセンブリをまたぐ規則(`internal` アクセシビリティ、`extern alias`、`protected internal`、`[InternalsVisibleTo]`)が必要なテストのために、もう1つの独立したアセンブリをコンパイルします。`options` の内容 — 参照、言語バージョン、nullable コンテキスト — はすべて再利用されますが、**`AssemblyName` だけは例外**で、常に `assemblyName` パラメーターが上書きします。

```csharp
// Options.AssemblyName は "MyApp.Sample" だが、参照アセンブリの名前は "Contracts" になる。
var contracts = GeneratorTest.CompileToReference(ContractsSource, "Contracts", Options);

var result = GeneratorTest.Run<GreeterGenerator>(
    source, Options with { AdditionalReferences = [contracts] });
```

これは望ましい挙動です(1つのコンパイル内で2つのアセンブリが同じ名前を共有することはできません)が、その裏返しとして、ジェネレーターがアセンブリ名をキーにする(たとえば出力するファイル名や拡張クラス名を決めるなど)テストは、ここで渡したオプションがその名前を決めたと思い込んではいけません。参照アセンブリの名前はこのパラメーターを通して与え、そこから読み取ってください。

### `SortGeneratedSourcesByHintName` とスナップショット

`GeneratedSources` は既定では生成順、つまりあなたのパイプラインがファイルを出力した順序になります。その順序は実際には安定していますが、どんな契約の一部でもないため、複数ファイルをまとめてカバーするスナップショットでは `SortGeneratedSourcesByHintName = true` を設定し、その順序に依存するのをやめるべきです。ファイル単位のルックアップ(`GetSource`、`GetSingleSource`)はどちらでも構いません。

### `DiagnosticIdPrefix` はフィルタするが `RawResult` はしない

`GeneratorTestResult.Diagnostics` と analyzer 用のエントリーポイントは `DiagnosticIdPrefix` を尊重します。これによって、意図的に不正なテストソースを使っても、結果として出る `CS****` をすべてのアサーションでいちいちフィルタする必要がなくなります。フィルタされていないジェネレーター診断は `RawResult.Diagnostics` にそのまま残っており、`OutputCompilation`/`GetCompilationErrors()` もこの設定の影響を受けません。

## SsalKit.Generators.Toolkit との関係

[SsalKit.Generators.Toolkit](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Generators.Toolkit/README.ja.md) は同じ仕事のもう半分です。あちらはジェネレーターを書く*ための*道具(equatable な配列、インデント付きコードライター、C# 命名ヘルパー、hint-name サニタイズ、キャッシュ安全な診断表現)であり、こちらはそのジェネレーターをテストする*ための*道具です。両者は自然に組み合わさります — `EquatableArray<T>` と `DiagnosticInfo` はパイプラインモデルが値で比較されるようにするために存在し、`IncrementalAssert` はそれが実際にそうなっていることを証明する手段です — しかしどちらも相手に依存せず、それぞれ単独でも機能します。Toolkit は `netstandard2.0` のジェネレータープロジェクトに埋め込まれる source-only パッケージであり、こちらはテストプロジェクトから参照される普通の `net10.0` アセンブリです。

## ライセンス

MIT — 詳細は [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE) を参照してください。

---

**AI に関する開示:** 本プロジェクトは AI(Claude)を活用して制作されました。
