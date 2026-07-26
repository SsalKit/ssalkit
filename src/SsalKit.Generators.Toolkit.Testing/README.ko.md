[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ko.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Generators.Toolkit.Testing/README.md) | **한국어** | [日本語](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Generators.Toolkit.Testing/README.ja.md)

# SsalKit.Generators.Toolkit.Testing

증분 소스 생성기와 분석기를 테스트하기 위한, 테스트 프레임워크에 종속되지 않는 얇은 하네스입니다: 인메모리 소스에 대해 생성기를 실행하고, 생성된 코드가 다시 컴파일됨을 증명하며, id/심각도/위치로 진단을 검증합니다 — 그리고 스냅샷으로는 결코 잡아낼 수 없는 단 하나, 즉 파이프라인이 여전히 캐싱되고 있는지를 검증합니다.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Generators.Toolkit.Testing.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Generators.Toolkit.Testing)

## 왜 SsalKit.Generators.Toolkit.Testing인가

모든 소스 생성기 테스트 프로젝트는 매번 똑같은 사십여 줄을 작성하며 시작됩니다. 문자열로부터 `CSharpCompilation`을 만들고, 생성된 코드가 테스트가 선언한 스텁이 아니라 실제 BCL을 기준으로 타입 검사되도록 어떤 참조 어셈블리를 넘길지 알아내고, `CSharpGeneratorDriver`를 만들어 실행한 뒤 `GetRunResult().Results.SelectMany(...)`에서 생성된 소스를 파내야 합니다. *출력* 컴파일의 진단도 함께 확인해야 한다는 점도 잊지 말아야 하는데, 그렇지 않으면 구문상으로는 올바르지만 타입 검사를 통과하지 못하는 코드를 내보내는 생성기가 여러분이 작성한 모든 단언을 그대로 통과해 버립니다. 이 중 어느 것도 흥미롭지 않고, 모든 프로젝트에서 똑같으며, 그 프로젝트들은 저마다 이를 어느 블로그 글로부터 다시 도출해 냅니다.

그것은 지루한 절반입니다. 나머지 절반이 바로 이 패키지가 존재하는 이유입니다.

**여러분의 생성기가 여전히 증분(incremental)으로 동작하는지는 아무것도 검증해 주지 않습니다.** 증분 생성기의 파이프라인은 값 동등성으로 캐싱합니다. 파이프라인 모델에 `ISymbol`, `Location`, 원시 `ImmutableArray<T>`, 혹은 참조 동등성을 갖는 그 밖의 무엇이든 담으면 캐시는 조용히 작동을 멈춥니다 — 모든 단계가 키 입력마다 재계산되고, 테스트에서 3ms만에 끝나던 생성기가 이제는 소비자가 IDE에서 글자 하나를 칠 때마다 파이프라인 전체를 다시 실행합니다. 여러분의 스냅샷 테스트는 계속 통과합니다. *출력*은 여전히 올바르니까요. 바뀐 것은 비용뿐입니다.

Roslyn은 이를 확인할 증거를 실제로 노출합니다 — `GeneratorDriverOptions(trackIncrementalGeneratorSteps: true)`는 모든 출력에 대해 단계별 사유(`Cached`, `Unchanged`, `Modified`, `New`)를 기록합니다 — 하지만 `TrackedSteps`를 올바르게 읽으려면 같은 드라이버로 두 번 실행하면서 그 사이에 컴파일을 변경하고, 어떤 사유가 무엇을 증명하는지 알아야 합니다. 이를 실제로 하는 사람은 거의 없고, 이를 단언으로 제공하는 패키지도 없습니다.

이 패키지는 그 일을 합니다:

- **`IncrementalAssert.AllCachedOrUnchanged`** — 파이프라인의 모델이 관측하지 않는 편집 이후에는 아무것도 재계산되어서는 안 됩니다. 모델이 값 동등성을 잃는 순간 실패하는 단언입니다.
- **`IncrementalAssert.SomeOutputRecomputed`** — 모델이 *실제로* 관측하는 편집 이후에는 무언가가 재계산되어야 합니다. 이미터가 실제로 사용하는 필드를 모델이 빠뜨렸을 때 실패하는 단언이며, 그렇지 않으면 오래된 출력을 영원히 계속 내보내게 됩니다.

두 단언은 양면 계약을 이룹니다: 너무 많이 담아내는 모델은 첫 번째 단언에서 실패하고, 너무 적게 담아내는 모델은 두 번째 단언에서 실패합니다. 단계 추적은 항상 켜져 있으므로, 미리 옵트인하지 않아도 모든 실행에서 두 단언 모두 사용할 수 있습니다.

이 패키지의 나머지는 여러분이 더 이상 손으로 쓰지 않아도 되는 보일러플레이트입니다:

- **기본값으로 실제 참조 어셈블리를 사용합니다.** 테스트 대상 컴파일은 테스트 호스트 자신이 신뢰하는 모든 참조 어셈블리를 대상으로 빌드되므로, `AssertCompilesCleanly()`는 생성된 코드를 실제 BCL 기준으로 타입 검사합니다. `AdditionalAssemblies = [typeof(MyAttribute).Assembly]`는 여기에 여러분이 배포하는 런타임 패키지를 추가하므로, 내보낸 호출이 테스트 소스에 붙여 넣은 사본이 아니라 실제로 배포하는 API를 기준으로 검사됩니다.
- **의도가 그대로 읽히는 단언입니다.** `GetSingleSource()`, `GetSource("...ServiceCollectionExtensions.g.cs")`, `AssertNoGeneratedSources()`, `DiagnosticAssert.Single(..., exclusive: true)`, `DiagnosticAssert.LocatedOn(diagnostic, "[Marker]")` — 그리고 `Expected 1, got 0` 대신 *실제로* 무엇이 생성되었는지, 혹은 *실제로* 무엇이 보고되었는지를 나열하는 실패 메시지입니다.
- **분석기도 지원합니다.** 동일한 컴파일 설정으로 패키지 전체의 분석기를 함께 실행할 수 있으며, 이것이 테스트 소스가 사용하는 어떤 구성에 대해서도 다른 분석기들이 침묵을 지킨다는 것을 증명하는 방법입니다.

## 설치

```bash
dotnet add package SsalKit.Generators.Toolkit.Testing
```

이 패키지는 **테스트 프로젝트용**입니다: 여러분이 배포하는 생성기 프로젝트가 아니라 테스트 프로젝트에서 참조하세요.

```xml
<ItemGroup>
  <PackageReference Include="SsalKit.Generators.Toolkit.Testing" Version="0.1.0" />
</ItemGroup>
```

## 사전 요구사항

- 테스트 프로젝트가 **`net10.0`** 이상을 대상으로 해야 합니다.
- 패키지는 `Microsoft.CodeAnalysis.CSharp`를 함께 가져옵니다 — 이것이 유일한 의존성이며, 어떤 테스트 프레임워크에도 의도적으로 의존하지 않습니다.

## 빠른 시작

### 생성기를 실행하고 결과 확인하기

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

    string generated = result.AssertCompilesCleanlyAndGetSource();

    Assert.Contains("public static class WidgetGreeter", generated);
}
```

`AssertCompilesCleanly()`는 생성된 소스가 *포함된* 상태로 컴파일을 다시 검사합니다. "내보낸 텍스트가 맞아 보인다"와 "내보낸 텍스트가 자신이 호출하는 API에 바인딩되는 유효한 C#이다"를 구분해 주는 단언이며, 손으로 만든 대부분의 하네스가 잊어버리는 부분이기도 합니다. 그 결과를 그대로 반환하므로 뒤이어 어떤 조회로든 바로 체이닝되며, `AssertCompilesCleanlyAndGetSource()`는 파일 하나만 내보내는 생성기의 테스트가 거의 항상 시작하는 그 한 쌍에 이름을 붙인 것입니다.

한 번의 실행이 여러 파일을 만들어 낼 때는, `GetSource("...Extensions.g.cs")`가 hint name의 접미사로 그중 하나를 골라내고, `ToSnapshotText()`는 스냅샷 라이브러리에 넘길 수 있도록 전부를 하나의 문자열로 렌더링합니다(각 파일 앞에 `// ==== <hint name>` 줄이 붙습니다):

```csharp
[Fact]
public Task WholeRun_MatchesSnapshot()
{
    var result = GeneratorTest.Run<GreeterGenerator>(Source, Options);

    return Verify(result.AssertCompilesCleanly().ToSnapshotText());
}
```

### 컴파일 설정 공유하기

테스트 프로젝트마다 `static readonly` 옵션 인스턴스를 하나만 유지해서, 생성된 코드가 무엇을 기준으로 타입 검사되는지를 한 곳에서 결정하세요:

```csharp
internal static class GeneratorTestSupport
{
    public static readonly GeneratorTestOptions Options = new()
    {
        // 이 패키지 자신의 진단만 단언에 도달하므로, 의도적으로 잘못 만든 테스트 소스가 일으키는
        // 부수적인 컴파일러 오류를 손으로 걸러낼 필요가 전혀 없습니다.
        DiagnosticIdPrefix = "MINE",

        // 생성 순서가 바뀌었다는 이유로 다중 파일 스냅샷이 흔들릴 수 없습니다.
        SortGeneratedSourcesByHintName = true,

        // 생성된 코드는 배포 중인 런타임 패키지를 기준으로 검사됩니다.
        AdditionalAssemblies = [typeof(Mine.MarkerAttribute).Assembly],
    };
}
```

모든 진입점은 옵션을 받습니다(또는 `GeneratorTestOptions.Default`를 뜻하는 `null`). 옵션은 불변 레코드이므로, 일회성 변형은 `with` 식 하나로 충분합니다: `Options with { AllowUnsafe = true }`.

### 캐싱 계약 검증하기

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

둘 다 하나의 드라이버를 공유하는 두 번의 실행 중 **두 번째** 것을 받습니다 — `RunTwice`는 소스 파일을 교체하고, `RunTwiceWithCompilationChange`는 컴파일 자체를 넘겨주어 신택스 트리를 추가하거나 교체할 수 있게 합니다. 추적 이름은 여러분의 파이프라인이 `WithTrackingName`에 넘긴 그 이름들입니다. 하나가 한 번도 기록되지 않았다면, 실패 메시지는 실행이 *실제로* 기록한 이름들을 나열해 주므로 대개 오탈자를 찾기에 충분합니다.

`RunTwice`가 표현할 수 있는 것과 없는 것을 구분하세요. 이 메서드는 소스 파일 전체를 교체하므로, 두 번째 소스를 변형하면 구문에 의존하는 모든 단계가 구조상 무효화됩니다. 변형 없이 호출하면 동일한 텍스트를 다시 파싱하게 되는데, 이것이야말로 가장 엄격한 캐싱 테스트입니다 — 파이프라인이 관찰하는 것 중 달라진 게 하나도 없으므로 무엇도 재계산되어서는 안 됩니다. 다만 *컴파일의 다른 어딘가에서 일어난* 편집이 아무것도 바꾸지 않는다는, 실제 IDE에서 벌어지는 시나리오를 검증하려면 위 예제처럼 `RunTwiceWithCompilationChange`로 무관한 트리를 추가하세요.

단언이 실패하면 단계별 캐시 상태를 출력하는데, 이것이 "캐시가 망가졌다"를 "`Models[0] -> Modified`"로 바꿔 주는 지점입니다:

```
Expected every output of step 'Models' to be Cached or Unchanged after the second run,
but 1 of them was recomputed.

Cache state of the requested steps:
  Models[0] -> Modified

Tracking names recorded by this run:
  - Collected
  - Models
```

### 진단 검증하기

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

- `Single`은 정확히 하나의 진단만 그 id를 가지고 있음을 단언하고, 선택적으로 심각도와 위치를 확인하며, 메시지도 단언할 수 있도록 그 진단을 반환합니다. `exclusive: true`는 추가로 그것이 보고된 *유일한* 진단이었음을 단언합니다 — 테스트 소스가 정확히 한 가지만 유발하도록 되어 있을 때 사용하면, 예상하지 못한 두 번째 진단이 예상한 진단 옆으로 슬쩍 끼어드는 일을 막을 수 있습니다.
- `LocatedOn`은 줄과 열이 아니라 **소스의 스니펫**으로 위치를 지정하므로, 단언이 읽기 쉽게 유지되고 위쪽 소스가 편집되어도 흔들리지 않습니다. 스니펫은 정확히 한 번만 나타나야 하며, 진단의 스팬은 그 안에 들어와야 합니다. 캐시 안전한 위치 레코드로부터 재구성된 생성기 진단은 신택스 트리를 갖지 않으므로, 이런 경우에는 위 예제처럼 소스를 함께 넘겨야 합니다. 분석기 진단은 그럴 필요가 없습니다.
- `SpanStartsWith(diagnostic, "Mine.Marker", source)`는 유일한 스니펫을 잡을 수 없을 때 쓰는 변형입니다 — 예를 들어 한 타입의 멤버 두 곳에 같은 특성이 붙어 있어서, 그것을 가리키는 스니펫이 무엇이든 소스에 두 번 나타나는 경우입니다. 접두사를 소스에서 찾는 대신 보고된 스팬 자체와 대조하므로, 스팬이 어디서 시작하고 무엇으로 시작하는지는 고정하지만 어디까지 뻗는지는 확인하지 않습니다. 유일한 스니펫이 있다면 범위까지 확인해 주는 `LocatedOn` 쪽을 쓰세요.
- `None(diagnostics, "MINE")`은 그 접두사를 가진 진단이 *아무것도* 보고되지 않았음을 단언합니다 — 이는 테스트에서 누구도 이름 붙일 생각을 하지 못한 진단까지도 잡아냅니다.

분석기도 하나의 집합으로서 동일한 설정을 거쳐 실행됩니다:

```csharp
var diagnostics = await GeneratorTest.RunAnalyzersAsync(
    source, [new MarkerAnalyzer(), new NamingAnalyzer()], Options);

DiagnosticAssert.None(diagnostics, "MINE");
```

## API 개요

| 타입 | 하는 일 |
|------|--------------|
| `GeneratorTest` | 진입점들입니다. 단일 실행에는 `Run<TGenerator>`, 증분 단언이 소비하는 두 번의 실행 쌍에는 `RunTwice<TGenerator>`/`RunTwiceWithCompilationChange<TGenerator>`, 분석기에는 `RunAnalyzerAsync<TAnalyzer>`/`RunAnalyzersAsync`, 아무것도 실행하지 않고 컴파일만 만들려면 `CreateCompilation`, 참조할 두 번째의 별도 어셈블리를 컴파일하려면 `CompileToReference`를 씁니다. |
| `GeneratorTestOptions` | 공유 설정 값들을 담은 불변 레코드입니다: `AssemblyName`, `LanguageVersion`, `NullableContextOptions`, `OutputKind`, `AllowUnsafe`, `AdditionalReferences`, `AdditionalAssemblies`, `DiagnosticIdPrefix`, `SortGeneratedSourcesByHintName`. `null`이 뜻하는 것이 `GeneratorTestOptions.Default`입니다. |
| `GeneratorTestResult` | 한 번의 실행이 만들어 낸 결과입니다. 데이터: `GeneratedSources`, `Diagnostics`, `OutputCompilation`, `RawResult`, `TrackedSteps`. 조회: `GetSingleSource()`, `GetSource(hintNameSuffix)`, `GetCompilationErrors()`, `ToSnapshotText()`. 단언: `AssertCompilesCleanly()`, `AssertCompilesCleanlyAndGetSource()`, `AssertNoGeneratedSources()`. |
| `GeneratedSource` | 생성된 파일 하나입니다: `HintName`과 `Text`로 이루어진 `readonly record struct`입니다. |
| `IncrementalAssert` | 캐싱 계약입니다: `AllCachedOrUnchanged(secondRun, ...names)`와 `SomeOutputRecomputed(secondRun, ...names)`. |
| `DiagnosticAssert` | `Single(diagnostics, id, severity?, locatedOnSnippet?, source?, exclusive?)`, `None(diagnostics, idPrefix)`, `LocatedOn(diagnostic, snippet, source?)`, `SpanStartsWith(diagnostic, prefix, source?)`. |
| `GeneratorAssertionException` | 모든 단언이 실패 신호로 던지는 예외이며, 메시지 안에 진단 내용을 담고 있습니다. |

## 테스트 프레임워크 비의존

이 패키지는 어떤 테스트 프레임워크도 참조하지 않습니다. 실패한 단언은 `GeneratorAssertionException`을 던지며, 모든 프레임워크는 테스트에서 처리되지 않은 예외를 실패로 취급하므로, 동일한 하네스가 xunit, NUnit, MSTest, TUnit에서 그대로 동작합니다 — 두 프레임워크를 함께 쓰는 저장소도 하네스를 두 가지 버전으로 나눠 갖지 않아도 됩니다.

그 대가로 실패가 여러분 프레임워크 고유의 단언 타입이 아니라 예외로 드러나므로, 메시지가 진단 내용을 스스로 담아내야 합니다. 노력을 의도적으로 쏟은 곳이 바로 여기입니다: 실패는 밋밋한 expected-versus-actual 대신, *실제로* 생성된 모든 파일의 hint name, 재생성된 컴파일의 컴파일러 오류, *실제로* 보고된 모든 진단의 id/심각도/위치/메시지, 또는 단계별 캐시 상태를 나열합니다.

여러분 프레임워크가 더 잘하는 부분(생성된 텍스트에 대한 `Assert.Contains`, `[Theory]` 데이터 등)에 그 프레임워크의 단언을 섞어 쓰는 것을 막지 않습니다. 이 하네스가 소유하는 것은 오직 생성기에 관한 단언뿐입니다.

## `Microsoft.CodeAnalysis.Testing`과의 관계

이 패키지는 그것의 대체재가 아니고, 그러려는 의도도 없습니다. 둘은 서로 다른 문제를 풉니다:

| | `Microsoft.CodeAnalysis.Testing` | 이 패키지 |
|---|---|---|
| 대상 | 분석기와 **코드 픽스** | 증분 **소스 생성기** |
| 형태 | `TestState`/`FixedState`, 마크업 문법, 프레임워크별 패키지를 갖춘 설정 가능한 `*Test` 픽스처 | 결과 객체를 반환하는 평범한 정적 메서드 |
| 진단 | 예상 상태로 선언되고 픽스처가 검증 | 실행 후 명시적으로 단언 |
| 코드 픽스/리팩터링 검증 | 있음 — 이것을 쓰는 이유 | 없음 |
| 다중 프로젝트, 추가 파일, `.editorconfig`, 분석기 설정 | 폭넓게 지원 | 다루지 않음 |
| 증분 캐싱(`trackIncrementalGeneratorSteps`) | 없음 | **있음** — 이 패키지를 쓰는 이유 |

*코드 픽스가 딸린* 분석기를 테스트한다면 `Microsoft.CodeAnalysis.Testing`을 쓰세요. 여기 있는 어떤 것도 `VerifyCodeFixAsync`를 대체하지 않습니다. 증분 생성기 — 내보내는 코드, 진단, 그리고 여전히 캐싱되는지 — 를 테스트한다면, 이 패키지는 배우기에 훨씬 작으면서도 저것이 다루지 않는 그 한 가지를 다룹니다. 한 저장소에서 둘 다 쓰는 것도 괜찮습니다. 서로 상태도 설정도 공유하지 않으니까요.

## 주의사항

### `CompileToReference`는 `AssemblyName`을 덮어씁니다

`CompileToReference(source, assemblyName, options)`는 어셈블리 간 규칙(`internal` 접근성, `extern alias`, `protected internal`, `[InternalsVisibleTo]`)이 필요한 테스트를 위해 별도의 두 번째 어셈블리를 컴파일합니다. `options`의 모든 것 — 참조, 언어 버전, nullable 컨텍스트 — 을 그대로 재사용하지만, `AssemblyName`만은 **예외**로 `assemblyName` 매개변수가 항상 덮어씁니다:

```csharp
// Options.AssemblyName은 "MyApp.Sample"이지만, 참조 어셈블리의 이름은 그것이 아니라 "Contracts"입니다.
var contracts = GeneratorTest.CompileToReference(ContractsSource, "Contracts", Options);

var result = GeneratorTest.Run<GreeterGenerator>(
    source, Options with { AdditionalReferences = [contracts] });
```

이것이 원하는 동작입니다(한 컴파일 안의 두 어셈블리는 이름을 공유할 수 없으니까요). 다만 이는 생성기가 어셈블리 이름을 기준으로 무언가를 정하는 테스트 — 예를 들어 내보내는 파일이나 확장 클래스의 이름을 정할 때 — 라면, 여기 넘긴 옵션이 그 이름을 결정했다고 가정해서는 안 된다는 뜻입니다. 참조 어셈블리의 이름은 매개변수를 통해 지정하고, 거기서 읽어 오세요.

### `SortGeneratedSourcesByHintName`과 스냅샷

`GeneratedSources`는 기본적으로 생성 순서, 즉 여러분의 파이프라인이 파일을 내보낸 순서 그대로입니다. 그 순서는 실무에서는 안정적이지만 어떤 계약의 일부도 아니므로, 여러 파일을 한꺼번에 다루는 스냅샷이라면 `SortGeneratedSourcesByHintName = true`를 설정해서 그 순서에 더는 의존하지 않도록 해야 합니다. 파일별 조회(`GetSource`, `GetSingleSource`)는 어느 쪽이든 상관없습니다.

### `DiagnosticIdPrefix`는 필터링하지만 `RawResult`는 아닙니다

`GeneratorTestResult.Diagnostics`와 분석기 진입점들은 `DiagnosticIdPrefix`를 따르며, 이 덕분에 모든 단언이 그 결과로 나오는 `CS****`를 일일이 걸러내지 않아도 테스트 소스를 의도적으로 잘못 만들 수 있습니다. 필터링되지 않은 생성기 진단은 여전히 `RawResult.Diagnostics`에 남아 있고, `OutputCompilation`/`GetCompilationErrors()`는 이 설정의 영향을 받지 않습니다.

## SsalKit.Generators.Toolkit과의 관계

[SsalKit.Generators.Toolkit](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Generators.Toolkit/README.ko.md)은 같은 작업의 나머지 절반입니다: 그것이 생성기를 *작성할 때* 쓰는 것(동등성 배열, 들여쓰기 코드 작성기, C# 명명 헬퍼, hint-name 정리, 캐시 안전한 진단 서술)이라면, 이 패키지는 생성기를 *테스트할 때* 쓰는 것입니다. 둘은 자연스럽게 짝을 이룹니다 — `EquatableArray<T>`와 `DiagnosticInfo`는 파이프라인 모델이 값으로 비교되도록 존재하고, `IncrementalAssert`는 실제로 그렇게 되었는지를 증명하는 방법입니다 — 하지만 어느 쪽도 다른 쪽에 의존하지 않으며, 둘 다 단독으로도 동작합니다. Toolkit은 `netstandard2.0` 생성기 프로젝트에 임베드되는 source-only 패키지이고, 이 패키지는 테스트 프로젝트가 참조하는 평범한 `net10.0` 어셈블리입니다.

## 라이선스

MIT — 자세한 내용은 [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE)를 참고하세요.

---

**AI 고지:** 이 프로젝트는 AI(Claude)를 활용하여 제작되었습니다.
