[← SsalKit](https://github.com/ssalkit/ssalkit/blob/main/README.ko.md)

[English](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Generators.Toolkit/README.md) | **한국어** | [日本語](https://github.com/ssalkit/ssalkit/blob/main/src/SsalKit.Generators.Toolkit/README.ja.md)

# SsalKit.Generators.Toolkit

Roslyn 소스 생성기 저작을 위한 source-only 툴킷입니다: 구조적 동등성 배열, 들여쓰기 코드 작성기, C# 명명 헬퍼, hint-name 정리, 캐시 안전한 진단 표현, 진단 서술자 팩터리를 여러분의 컴파일에 직접 임베드합니다 — 별도로 배포할 런타임 어셈블리가 없습니다.
[![NuGet](https://img.shields.io/nuget/v/SsalKit.Generators.Toolkit.svg?logo=nuget)](https://www.nuget.org/packages/SsalKit.Generators.Toolkit)

## 왜 SsalKit.Generators.Toolkit인가

웬만큼 규모가 있는 Roslyn 소스 생성기는 결국 같은 유틸리티 몇 가지를 매번 다시 구현하게 됩니다. `ImmutableArray<T>`에 구조적 동등성을 부여해 증분 파이프라인이 제대로 캐싱되도록 하는 래퍼, 생성 소스를 작성하며 들여쓰기를 추적하는 작은 코드 작성기, 임의의 심볼 이름을 유효한 C# 식별자로 바꿔주는 헬퍼, `AddSource`의 `hintName` 인자를 정리하는 sanitizer, 파이프라인에 SyntaxTree를 고정하지 않는 캐시 안전한 `Diagnostic` 대역, 그리고 `DiagnosticDescriptor` 선언의 보일러플레이트를 줄여주는 팩터리 같은 것들입니다.

이걸 평범한 NuGet 패키지로 배포하려 하면 실질적인 문제가 생깁니다. 소스 생성기는 `analyzer`로 패키징되는데, 생성기가 의존하는 라이브러리가 있다면 그 DLL을 같은 `analyzers/dotnet/cs` 폴더에 **함께** 패키징해야 합니다 — analyzer 시점 DLL에는 일반적인 의존성 해석이 적용되지 않기 때문입니다. 즉 헬퍼 라이브러리를 쓰는 모든 소비자가 그저 그 라이브러리를 함께 실어 나르기 위해 커스텀 패키징을 해야 한다는 뜻입니다.

SsalKit.Generators.Toolkit은 접근 방식 자체가 다릅니다.

- **런타임 어셈블리가 아닌 source-only.** 패키지는 평범한 `.cs` 파일을 [`contentFiles`](https://learn.microsoft.com/nuget/reference/nuspec#including-content-files) 형태로 배포하며, 이 파일들은 *여러분의* 생성기 프로젝트에 직접 컴파일됩니다. analyzer 옆에 함께 패키징해야 할 DLL이 아예 없습니다.
- **패키지 의존성 0.** 임베드된 소스는 여러분의 생성기 프로젝트가 이미 참조하고 있는 Roslyn API만 필요로 합니다 — 새로 해석해야 할 것도, 여러분의 `Microsoft.CodeAnalysis.*` 버전 고정과 충돌할 것도 없습니다.
- **소비자에게 보이지 않음.** 헬퍼들은 여러분의 생성기 어셈블리에 `internal` 타입으로 직접 컴파일되므로, 이 패키지의 존재는 여러분이 배포하는 생성기의 공개 표면에 전혀 드러나지 않습니다.
- **프레임워크가 아니라 작고 독립적인 컴포넌트 8종**: `EquatableArray<T>`, `IndentedCodeWriter`, `CSharpNaming`, `HintNameSanitizer`, `DiagnosticInfo`/`LocationInfo`, `DiagnosticDescriptorFactory`, `SymbolFacts`, `AttributeLocations`. 여기에 `netstandard2.0` 생성기가 `record` 모델을 쓰려면 반드시 필요한 `IsExternalInit` 폴리필이 함께 들어 있습니다. 필요한 것만 쓰면 되고, 쓰지 않는 `internal` 타입은 참조되지 않은 채 그냥 남아 있을 뿐입니다.

## 설치

```bash
dotnet add package SsalKit.Generators.Toolkit
```

이 패키지는 `DevelopmentDependency=true`로 설정되어 있어서, 평범한 `dotnet add package`(또는 별다른 속성 없는 `<PackageReference>`)만으로도 NuGet이 자동으로 `PrivateAssets="all"`을 적용합니다 — 이 참조가 여러분의 생성기에 의존하는 그 무엇에도 전이되지 않습니다. 그래도 명시적으로 적어두는 것을 권장합니다. `.csproj`를 읽는 사람에게 의도를 문서화해주고, 나중에 암묵적 기본값이 바뀌더라도 동작이 안정적으로 유지되기 때문입니다.

```xml
<ItemGroup>
  <PackageReference Include="SsalKit.Generators.Toolkit" Version="0.1.0" PrivateAssets="all" />
</ItemGroup>
```

## 전제 조건

- 여러분의 프로젝트가 **Roslyn 컴포넌트**(소스 생성기 및/또는 analyzer)여야 합니다 — 이 패키지는 그 맥락 밖에서는 쓸모가 없습니다.
- 프로젝트가 `netstandard2.0`을 대상으로 하거나 그와 호환되어야 합니다 (Roslyn 컴포넌트의 표준 TFM).
- 프로젝트의 `LangVersion`이 **C# 10 이상**이어야 합니다. 임베드된 소스 자체는 C# 10 문법만 사용하지만(아래 [임베드 소스 규약](#임베드-소스-규약) 참고), 이 패키지가 여러분 프로젝트의 언어 버전을 올리거나 내리려 하지는 않습니다.
- 프로젝트가 이미 **`Microsoft.CodeAnalysis`**(또는 `Microsoft.CodeAnalysis.CSharp`)를, 그것도 **4.4.0 이상**으로 참조하고 있어야 합니다. 참조 자체는 Roslyn 타입을 직접 사용하는 `DiagnosticDescriptorFactory`, `DiagnosticInfo`, `SymbolFacts`, `AttributeLocations`의 하드 요구사항입니다. 모든 Roslyn 컴포넌트 프로젝트가 어차피 이를 참조하고 있으므로 SsalKit.Generators.Toolkit은 의도적으로 이를 패키지 의존성으로 선언하지 **않습니다** — 선언하면 여러분 대신 버전을 고정해 버려 하위 호환을 위한 여러분 자신의 선택을 방해하기 때문입니다. 그래서 이 하한을 *강제하는* 장치도 없습니다. 더 낮은 Roslyn에서는 그냥 빌드가 아래 오류로 실패합니다.

### 왜 4.4.0인가

딱 한 줄 때문입니다. `SymbolFacts.FindGeneratedCodeAccessBlocker`가 `INamedTypeSymbol.IsFileLocal`을 읽는데, 이 API는 Roslyn이 C# 11의 `file` 지역 타입과 함께 **4.4.0**에서 추가한 것입니다. 나머지 컴포넌트는 훨씬 낮은 Roslyn에서도 컴파일됩니다. 4.3.x 이하에서는 이렇게 실패합니다.

```
error CS1061: 'INamedTypeSymbol' does not contain a definition for 'IsFileLocal'
```

버전에 무관한 대체 구현은 낼 만한 것이 없습니다. 남는 방법은 컴파일러가 file 지역 타입에 붙이는 맹글링된 이름(`<Source>F0__Widget`)을 알아보는 것인데, 이는 호환성 약속이 전혀 없는 구현 세부사항이고 판정이 틀리면 생성 코드가 볼 수 없는 타입을 이름으로 부르게 됩니다.

낮은 Roslyn을 의도적으로 고정해야 한다면(예: 오래된 IDE에서도 생성기가 로드되게 하려고) 패키지 전체가 아니라 그 파일 하나만 빼세요. `contentFiles`는 복원 과정에서 `Compile` 항목으로 들어오므로, 제거는 평범한 `ItemGroup`이 아니라 타깃 안에서 해야 합니다.

```xml
<Target Name="DropToolkitSymbolFacts" BeforeTargets="CoreCompile">
  <ItemGroup>
    <Compile Remove="@(Compile)"
             Condition="'%(NuGetPackageId)' == 'SsalKit.Generators.Toolkit' And '%(Filename)' == 'SymbolFacts'" />
  </ItemGroup>
</Target>
```

패키지 안의 다른 어떤 파일도 `SymbolFacts`를 참조하지 않으므로, 제거해도 잃는 것은 그 컴포넌트 하나뿐입니다.

## 컴포넌트

### `EquatableArray<T>`

`ImmutableArray<T>`를 감싸서 참조가 아닌 내용으로 비교되게 합니다. 증분 생성기 파이프라인은 `EqualityComparer<T>.Default`에 의존해 어떤 단계의 출력이 지난 실행 대비 바뀌었는지 판단하는데, 평범한 `ImmutableArray<T>`는 이 검사를 깨뜨립니다(참조 동등성만 지원하기 때문). 결과적으로 파이프라인의 캐싱이 조용히 무력화됩니다. `EquatableArray<T>`는 `T : IEquatable<T>`인 모든 타입에 대해 이 문제를 해결합니다.

```csharp
using System.Collections.Immutable;
using SsalKit.Generators.Toolkit;

// 증분 생성기 실행 사이에 유지되는 파이프라인 모델.
internal readonly struct ServiceModel : IEquatable<ServiceModel>
{
    public ServiceModel(string typeName, ImmutableArray<string> interfaceNames)
    {
        TypeName = typeName;
        InterfaceNames = interfaceNames.ToEquatableArray(); // 또는 EquatableArray.Create(interfaceNames)
    }

    public string TypeName { get; }
    public EquatableArray<string> InterfaceNames { get; }

    public bool Equals(ServiceModel other) =>
        TypeName == other.TypeName && InterfaceNames.Equals(other.InterfaceNames);

    // ... GetHashCode(), object.Equals() 등
}
```

**`default` 인스턴스는 `EquatableArray<T>.Empty`와 같지 않습니다.** 이 래퍼는 `ImmutableArray<T>`가 구분하는 "배열이 아예 없음"과 "원소가 없는 배열"의 차이를 그대로 보존합니다. `Length`, `AsImmutableArray()`, 열거는 둘을 똑같이 취급하지만 동등성과 해시는 그렇지 않습니다. 문제가 되는 자리는 하나뿐이지만 그 하나가 아픕니다. 어떤 단계가 한 번은 `default`를, 다음엔 `Empty`를 내놓으면 둘 다 "없음"을 뜻하는데도 키 입력마다 변경으로 보고됩니다. "비어 있음"의 표기를 하나로 정하세요. 빈 시퀀스에 대한 `ToEquatableArray()`와 `EquatableArray<T>.Empty`는 둘 다 비-default 형태를, 초기화하지 않은 필드는 default 형태를 줍니다.

### `IndentedCodeWriter`

생성 소스 텍스트를 작성하는 동안 들여쓰기를 추적해주는, 가볍고 할당이 적은 작성기입니다. 들여쓰기 문자열을 직접 관리할 필요가 없습니다. 줄바꿈은 항상 `"\n"`이며(빌드 머신에 관계없이 결정적), 빈 줄에는 들여쓰기 공백이 남지 않습니다(안정적인 diff).

```csharp
using SsalKit.Generators.Toolkit;

var writer = new IndentedCodeWriter();
writer.WriteAutoGeneratedHeader(); // "// <auto-generated/>" + "#nullable enable" + 빈 줄
writer.WriteLine("namespace MyGenerator.Generated;");
writer.WriteLine();

using (writer.Block("internal static class MyAppWebServiceRegistration"))
{
    using (writer.Block("public static void Register(IServiceCollection services)"))
    {
        writer.WriteLine("services.AddSingleton<ICacheService, CacheService>();");
    }
}

string source = writer.ToString();
context.AddSource("MyAppWebServiceRegistration.g.cs", source);
```

`Block(header)`는 `header`를 쓰고, 여는 `{`를 그 다음 줄에 쓴 뒤 들여쓰기를 늘리고, `using` 스코프가 끝날 때 닫는 `}`를 씁니다. `Block(header, closer)`는 다른 닫는 토큰(예: 객체 초기화자용 `"};"`)을 지정할 수 있게 해주며, `Indent()`는 중괄호 없이 순수한 들여쓰기 스코프만 제공합니다.

`WriteAutoGeneratedHeader()`는 `// <auto-generated/>`와 `#nullable enable`을 씁니다. `WriteAutoGeneratedHeader(suppressWarnings: true)`는 그 사이에 `#pragma warning disable` 한 줄을 넣어 파일 나머지의 모든 경고를 지웁니다. 생성한 코드가 소비자의 `TreatWarningsAsErrors`와 그쪽이 켜 둔 analyzer 세트를 통과해야 할 때 쓰세요 — 작성자가 손댈 수 없는 파일에서 빌드가 깨지는 건 하루를 날리는 일입니다. 기본값이 off인 이유는, 전부 억제하면 *보고 싶은* 경고(예: 생성 코드가 호출하는 obsolete API)까지 함께 숨기기 때문입니다. 인자 없는 호출은 그대로이므로 기존 생성기의 헤더 출력은 한 바이트도 달라지지 않습니다.

**들여쓰기는 줄 단위가 아니라 쓰기 단위로 적용됩니다.** 줄 첫머리에 무언가를 쓸 때 한 번 들여쓴 뒤로는 텍스트를 통짜로 취급하므로, 이미 줄바꿈을 품은 문자열(메서드 본문 전체를 담은 raw string 리터럴 같은)은 *첫 줄*만 들여쓰기가 붙습니다. 그런 블록은 한 줄씩 나눠 쓰거나, 들여쓰기 0에서 써서 그 안의 레이아웃을 그대로 살리세요.

생성 메서드 하나에 10~20줄씩 들어가기 쉬운 XML 문서 주석을 위해서는 `WriteDocLine(content)`와 `WriteDocLines(contents)`가 `/// ` 접두사를 대신 붙여줍니다.

```csharp
writer.WriteDocLines(
    "<summary>",
    "Picks a single random element of <paramref name=\"items\"/>.",
    "</summary>",
    "<param name=\"items\">The candidate items.</param>");
```

빈 문자열을 넘기면 뒤에 공백 없이 `///`만 씁니다. 생성된 문서 블록에 후행 공백이 남지 않습니다.

`WriteDocLines`는 오버로드가 둘입니다. 위처럼 리터럴을 나열할 때는 `params string[]`을, 조건에 따라 줄을 덧붙여 조립할 때는 `IEnumerable<string>`을 씁니다. 후자를 배열 리터럴로만 처리하면 거의 같은 목록 둘을 `if`/`else`로 나눠 써야 합니다.

```csharp
IEnumerable<string> docs = new[] { "<summary>", summaryText, "</summary>" };
if (isObsolete)
{
    docs = docs.Concat(new[] { "<remarks>Superseded by <c>" + replacement + "</c>.</remarks>" });
}

writer.WriteDocLines(docs);
```

시퀀스는 쓰는 도중에 정확히 한 번만 열거하므로, 지연 쿼리를 미리 배열로 만들 필요가 없습니다. 매개변수 타입은 `string[]`이 더 구체적이므로 기존 호출부(인자 나열, 명시적 `string[]`, 인자 없음)는 전과 똑같이 `params` 오버로드로 바인딩됩니다.

### `CSharpNaming`

임의의 텍스트(어셈블리 이름, 심볼 이름, 점이나 다른 구분자가 섞인 문자열 등)를 유효한 C# 식별자 조각으로 바꾸고, 예약 키워드를 이스케이프합니다.

```csharp
using SsalKit.Generators.Toolkit;

string methodName = CSharpNaming.ToPascalCaseIdentifier(assemblyName, fallback: "Assembly");
// "MyApp.Web" -> "MyAppWeb"

string paramName = CSharpNaming.ToCamelCaseIdentifier(typeSymbol.Name);
// "IOService" -> "ioService", "UserRepository" -> "userRepository"

string safeParamName = CSharpNaming.EscapeKeyword(paramName);
// "class" -> "@class"; 그 외에는 그대로 반환

string flattened = CSharpNaming.JoinIdentifierSegments(new[] { "Outer", "Inner" });
// -> "Outer_Inner" (중첩 타입 이름을 최상위 타입 이름으로 평탄화하는 통상적인 방법)
```

`ToPascalCaseIdentifier`/`ToCamelCaseIdentifier`는 입력이 `null`, 빈 문자열이거나 글자·숫자가 하나도 없을 때 `fallback`을 반환하며, 결과가 숫자로 시작하게 될 경우 `_`를 앞에 붙입니다. `EscapeKeyword`는 *예약* 키워드(`class`, `namespace`, `return` 등)만 이스케이프합니다 — `var`나 `nameof` 같은 문맥 키워드는 항상 유효한 식별자이므로 건드리지 않습니다.

**`ToCamelCaseIdentifier`는 예약 키워드를 반환할 수 있고**(`"Class"`, `"Event"`, `"Params"`는 모두 소문자화하면 키워드가 됩니다) 그것을 대신 이스케이프해 주지 않습니다. 이 책임 분리가 옳은데, 더 긴 식별자에 끼워 넣는 조각(`classFactory`)이 중간에 엉뚱한 `@`를 달고 있으면 안 되기 때문입니다. 결과를 매개변수나 지역 변수 이름으로 단독 방출할 때는 사용 지점에서 두 함수를 조합하세요: `CSharpNaming.EscapeKeyword(CSharpNaming.ToCamelCaseIdentifier(name))`. `ToPascalCaseIdentifier`는 이런 주의가 필요 없습니다 — C# 예약 키워드는 전부 소문자이기 때문입니다.

`JoinIdentifierSegments`는 이어 붙이기만 할 뿐 정리하지 않습니다. 각 세그먼트가 이미 유효한 식별자라고 전제하므로, 그렇지 않다면 먼저 `ToPascalCaseIdentifier`를 통과시키세요. `null`이거나 빈 세그먼트는 이어 붙이지 않고 건너뛰므로, 결과가 구분자로 시작하거나 끝나지 않고 구분자가 연속으로 나오지도 않습니다. 빈 목록은 `string.Empty`를 반환합니다. 구분자 기본값은 `'_'`이며 변경할 수 있습니다.

### `HintNameSanitizer`

후보 문자열(대개 타입의 정규화된 이름 또는 메타데이터 이름)을 `AddSource`의 `hintName` 인자로 안전하게 넘길 수 있는 값으로 바꿉니다. 제네릭 arity 마커(`` Foo`1 ``)와 중첩 타입 구분자(`Outer+Inner`)는 원시 FQN을 그대로 사용할 때 `AddSource`가 실패하는 가장 흔한 원인입니다.

```csharp
using SsalKit.Generators.Toolkit;

string hintName = HintNameSanitizer.Sanitize(typeSymbol.ToDisplayString());
// "Namespace.Outer<Inner>" -> "Namespace.Outer_Inner_.g.cs" (허용되지 않는 문자를 치환하고 접미사를 붙임)

string fromFqn = HintNameSanitizer.Sanitize(
    typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
// "global::Namespace.MyType" -> "Namespace.MyType.g.cs" (별칭 한정자는 제거됨)

// 타입 두 개의 이름을 이어 붙인 파일 이름 — 어느 쪽도 미리 벗겨낼 필요가 없습니다.
string forPair = HintNameSanitizer.Sanitize(containerFqn + "." + codeEnumFqn + ".Mapping");
// "global::My.Container.global::My.Codes.Mapping" -> "My.Container.My.Codes.Mapping.g.cs"

context.AddSource(hintName, sourceText);
```

`Sanitize`는 결과가 `suffix`(기본값 `".g.cs"`, 이미 있으면 중복 추가하지 않음)로 끝남을 보장하고, Roslyn이 허용하는 hint-name 문자 집합 밖의 모든 문자를 `_`로 치환하며, 전체 길이를 200자로 제한합니다(앞쪽부터 잘라내므로 더 식별력 있는 뒷부분과 접미사는 항상 살아남습니다). 여러 번 호출했을 때의 유일성은 보장하지 않습니다 — 구별 가능한 입력을 넘기는 책임은 호출자에게 있습니다.

`SymbolDisplayFormat.FullyQualifiedFormat`이 모든 이름에 붙이는 `global::`은 문자 단위로 치환하지 않고 **제거**합니다. 덕분에 정규화된 이름을 그대로 넘겨도 생성되는 파일 이름에 `global__`이 남지 않습니다. 선행 위치뿐 아니라 나타나는 모든 위치에서 제거하므로, 정규화된 이름 여러 개를 이어 붙여 만든 hint name도 호출자가 직접 벗겨낼 필요가 없습니다. 제거 대상은 정확히 `global::` 한정자뿐이라, 단어만 같은 세그먼트(`global.MyType`)는 그대로 둡니다. 후보 문자열이 `global::` 한정자로만 이루어져 있으면 `null`/빈 문자열/공백과 마찬가지로 `"Generated"`로 대체됩니다.

### `DiagnosticInfo` / `LocationInfo`

캐시 안전한 `Diagnostic` 대역입니다. 실제 `Diagnostic`(또는 `Location`)을 증분 파이프라인에 실어 나르면 그것이 유래한 `SyntaxTree`가, 나아가 `Compilation` 전체가 생성기 캐시에 고정됩니다. 메모리 누수도 문제지만, 동일한 소스에 대한 두 실행이 서로 같지 않은 `Location`을 만들어내므로 캐싱 자체가 무력화됩니다. `DiagnosticInfo`는 서술자, 선택적 `LocationInfo`(파일 경로 + 스팬), 메시지 인자만 담고 이 셋 전부를 값으로 비교합니다.

```csharp
using SsalKit.Generators.Toolkit;

// transform 단계: 심볼/노드를 캐시 안전한 값으로 축약한다.
var info = new DiagnosticInfo(
    DiagnosticDescriptors.UnsupportedWeightType,
    LocationInfo.CreateFrom(memberSymbol.Locations.FirstOrDefault()),
    memberDisplayName,
    memberType.ToDisplayString());

// source-output 단계: 되살려서 보고한다.
context.RegisterSourceOutput(diagnostics, static (spc, reported) =>
{
    foreach (var diagnostic in reported)
    {
        spc.ReportDiagnostic(diagnostic.ToDiagnostic());
    }
});
```

`LocationInfo.CreateFrom`은 `Location`이나 `SyntaxNode`를 받고, 소스상의 위치가 아닌 것(메타데이터 위치, "none" 위치, `null` 인자)에 대해서는 `null`을 반환합니다. 이는 `Diagnostic.Create`가 "위치 없이 보고"로 그대로 받아들이는 값이므로, 이후 단계에서 별도 처리가 필요 없습니다. 두 타입 모두 `IEquatable<T>`와 짝이 맞는 `GetHashCode`를 구현하므로 `EquatableArray<T>`를 비롯한 어떤 파이프라인 모델 안에도 넣을 수 있습니다.

메시지 인자는 `EquatableArray<string>`으로 보관합니다. `Diagnostic.Create`가 받는 `object?[]`가 아니라 **문자열 고정**입니다. 임의의 `object` 인자는 그 타입이 구현한 동등성(대개 참조 동등성)을 그대로 가져오므로, "같은" 진단을 만든 두 실행이 서로 다르다고 비교되어 캐시가 무력화될 수 있습니다. 더 나쁘게는 인자가 심볼이나 구문 노드라면 `Compilation` 전체를 붙잡습니다. 그래서 포맷은 호출부에서 끝냅니다 — `DiagnosticInfo`를 만들기 전에 각 인자를 문자열로 렌더링하고(문화권에 민감한 값은 invariant 포맷으로), `ToDiagnostic()`은 그 문자열을 그대로 넘깁니다. `params string[]` 생성자 오버로드가 배열을 대신 만들어 줍니다.

### `DiagnosticDescriptorFactory`

모든 생성기의 진단 테이블이 항목마다 반복하는 `DiagnosticDescriptor` 생성자 호출(id, title, 메시지 포맷, category, severity, `isEnabledByDefault`, description, ...)의 반복을 줄여줍니다.

```csharp
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;

internal static class DiagnosticDescriptors
{
    private static readonly DiagnosticDescriptorFactory Factory = new("SSAL", "SsalKit.Guard");

    public static readonly DiagnosticDescriptor DuplicateErrorCode = Factory.Error(
        id: 1,
        title: "Duplicate error code",
        messageFormat: "Error code '{0}' is already assigned to '{1}'",
        description: "Each member decorated with [ErrorCodes] must declare a unique error code.");
    // -> id "SSAL001"

    public static readonly DiagnosticDescriptor UnusedErrorCode = Factory.Warning(
        id: 2,
        title: "Unused error code",
        messageFormat: "Error code '{0}' is never thrown",
        description: "Consider removing the unused error code or using it in a Guard call.");
    // -> id "SSAL002"
}
```

같은 팩터리 인스턴스가 만드는 모든 서술자는 동일한 id 접두사/category를 공유하고, `{idPrefix}{id:D3}` 형식(예: `"SSAL001"`)으로 포맷되며, `isEnabledByDefault: true`를 가집니다. `Error(...)`와 `Warning(...)` 모두 추가 서술자 태그를 위한 `params string[] customTags`를 선택적으로 받습니다.

**알아둬야 할 트레이드오프 하나.** id가 `DiagnosticDescriptor` 생성자 호출 지점에 리터럴로 적히지 않고 런타임에 조립되기 때문에, Microsoft.CodeAnalysis.Analyzers의 [릴리스 추적 규칙](https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md)(RS2000-RS2003)이 여러분의 id를 하나도 해석하지 못하며, `AnalyzerReleases.Shipped.md`/`AnalyzerReleases.Unshipped.md`의 모든 항목을 짝이 없는 것으로 보고합니다. 이 파일들을 유지한다면 `RS2002`/`RS2003`을 억제하고 같은 검사를 테스트로 대신하세요 — 두 릴리스 파일을 읽어 `(id, category, severity)` 행을 서술자 테이블과 대조하고, 모든 서술자가 어느 분석기의 `SupportedDiagnostics`에든 들어 있는지 단언하면 됩니다. 작성하기 쉬운 데다, 분석기가 하던 것보다 더 많이 검증합니다.

### `SymbolFacts`

거의 모든 생성기가 결국 묻게 되는 심볼 수준의 질문들이며, 어느 것도 `Compilation`을 필요로 하지 않습니다: 타입을 생성 코드에 어떻게 쓰는지, 생성된 파일이 그 타입을 명명할 수 있는지, 제네릭인지, 그리고 한 번의 실행이 내놓는 진단을 어떤 순서로 정렬하는지.

```csharp
using SsalKit.Generators.Toolkit;

// global:: 한정 이름 -- 생성 코드의 타입 참조는 이 형태로 써야 합니다.
string fqn = SymbolFacts.ToFqn(typeSymbol);          // "global::Game.Loot.LootEntry"

// 네임스페이스 이름, 전역 네임스페이스면 "" -- 네임스페이스 선언을 낼지 말지 판단하는 데 필요한 값입니다.
string ns = SymbolFacts.GetContainingNamespaceName(typeSymbol);

// 생성된 멤버를 inconsistent-accessibility 오류 없이 public으로 선언할 수 있는가?
bool canBePublic = SymbolFacts.IsEffectivelyPublic(typeSymbol);

// 자신의 타입 파라미터가 있거나, 컨테이너 타입에서 물려받았는가.
bool isGeneric = SymbolFacts.IsGenericOrNestedInGeneric(typeSymbol);

// 같은 어셈블리의 별도 생성 파일이 이 타입을 명명할 수 있는가?
bool nameable = SymbolFacts.IsAccessibleFromGeneratedCode(typeSymbol);
```

`IsAccessibleFromGeneratedCode`는 중첩 체인 전체를 따라갑니다. `private` 타입 안에 중첩된 `public` 타입은 private 타입만큼이나 도달 불가능하며, `file`-local 타입은 `Accessibility.Internal`로 보고되므로 따로 물어봐야 합니다. `protected internal`은 통과하고(생성 파일이 `internal` 쪽 절반을 받습니다), `private protected`는 통과하지 못합니다(생성 코드는 무엇도 상속하지 않으므로 `protected` 쪽 절반은 결코 받지 못합니다). `IErrorTypeSymbol`(해석되지 않은 이름)은 거부됩니다 -- 명명할 타입 자체가 없으며, 그것을 참조하는 코드를 내보내면 컴파일 오류 하나가 둘이 됩니다.

접근 불가 타입을 그냥 건너뛰는 대신 *진단으로 보고*하고 싶다면, `FindGeneratedCodeAccessBlocker`가 단순한 `bool` 대신 체인에서 문제가 된 고리를 돌려줍니다. 그래서 메시지가 그 이름을 직접 지목하고, 그것이 타입 자신인지 컨테이너인지까지 말해줄 수 있습니다.

```csharp
var blocker = SymbolFacts.FindGeneratedCodeAccessBlocker(typeSymbol);
if (blocker is not null)
{
    string reason = ReferenceEquals(blocker, typeSymbol)
        ? "it is declared '" + blocker.DeclaredAccessibility + "'"
        : "it is nested inside '" + blocker.ToDisplayString() + "'";
    // ... 보고
}
```

마지막으로 `SortForDiagnosticDeterminism`은 `ImmutableArray<DiagnosticInfo>`를 소스 파일 -> 위치 -> id 순으로 정렬하며, 위치가 없는 진단은 맨 뒤로 보냅니다. 파이프라인 노드는 호스트가 정하는 순서대로 실행되므로, 마지막에 정렬하지 않으면 동일한 빌드 두 번의 진단 순서가 달라질 수 있습니다 -- 스냅샷 테스트가 간헐적으로 깨지고 빌드 로그가 불안정해지는 형태로 드러납니다.

### `AttributeLocations`

메서드 하나. 생성기가 거의 항상 조금씩 틀리게 잡는 그 위치 하나를 위한 것입니다.

```csharp
Location location = AttributeLocations.GetLocation(attributeData, decoratedSymbol);
```

attribute 적용에 대한 규칙을 보고하기 가장 좋은 자리는 데코레이트된 선언 전체가 아니라 attribute 적용 그 자체 -- 사용자가 직접 쓴, 지울 수 있는 토큰 -- 입니다. 그런데 `AttributeData.ApplicationSyntaxReference`는 attribute가 소스에서 오지 않았을 때 항상 `null`이고, 합성된 심볼은 위치를 아예 갖지 않으므로 순진한 한 줄짜리 코드에는 구멍이 둘 있습니다. 이 메서드는 그 둘을 모두 폴백으로 덮습니다: attribute 구문 -> 데코레이트된 심볼의 첫 위치 -> `Location.None`(`Diagnostic.Create`가 받아들이는 값으로, 진단을 버리는 대신 파일 위치 없이 보고합니다). 위치는 `GetSyntax()` 호출 대신 구문 참조가 이미 들고 있는 트리와 span에서 바로 만듭니다. 이미 가진 span을 얻자고 attribute 노드를 실체화(지연 로드된 트리라면 재파싱)할 이유가 없기 때문입니다.

다만 `Location`은 파이프라인을 타면 안 됩니다 — 자신이 나온 구문 트리를, 그리고 그 트리를 통해 `Compilation` 전체를 붙잡기 때문입니다. `GetLocationInfo`는 같은 답을 캐시 안전한 형태로 이미 투영해 돌려주므로, transform 단계가 날것의 `Location`을 부를 이유 자체가 사라집니다.

```csharp
LocationInfo? location = AttributeLocations.GetLocationInfo(attributeData, decoratedSymbol);
```

소스 위치가 아닐 때 돌아오는 `null`은 `Diagnostic.Create`가 "위치 없이 보고"로 받아들이는 값이므로, 하류에서 따로 분기할 필요가 없습니다.

### `IsExternalInit` (컴파일러 폴리필)

`netstandard2.0` 참조 어셈블리에는 `System.Runtime.CompilerServices.IsExternalInit`이 없습니다. C# 컴파일러는 `init` 접근자를 내보내기 전에 이 타입을 요구하며, 따라서 이 타입 없이는 `record` 선언 자체가 불가능합니다. 파이프라인 모델은 `record`를 쓰기 딱 좋은 자리이므로, 결국 모든 생성기 프로젝트가 똑같은 빈 타입을 손수 만들게 됩니다. 그 수고를 덜기 위해 패키지가 함께 배포합니다.

이 파일은 `SsalKit.Generators.Toolkit` 네임스페이스에 들어 있지 않은 유일한 임베드 소스입니다. 컴파일러가 고정된 정규화 이름으로 이 타입을 찾기 때문에 옮길 수 없습니다.

**옵트아웃.** 여러분의 컴파일에 이미 이 타입이 선언되어 있다면(직접 만든 폴리필이든 다른 패키지의 것이든) 정의가 둘이 되어 `CS0101` 중복 정의 오류가 납니다. `SSALKIT_GENERATORS_TOOLKIT_EXCLUDE_ISEXTERNALINIT`를 정의하면 이 사본이 빠집니다.

```xml
<PropertyGroup>
  <DefineConstants>$(DefineConstants);SSALKIT_GENERATORS_TOOLKIT_EXCLUDE_ISEXTERNALINIT</DefineConstants>
</PropertyGroup>
```

옵트아웃은 언제나 안전합니다. 패키지의 다른 어떤 것도 이 파일에 의존하지 않는데, 툴킷 자신의 소스가 이 폴리필이 열어주는 문법을 의도적으로 쓰지 않기 때문입니다(아래 참고).

## 임베드 소스 규약

이 패키지가 배포하는 모든 `.cs` 파일은 동일한 세 줄로 시작합니다.

```csharp
// <auto-generated/>
#pragma warning disable
#nullable enable
```

- `// <auto-generated/>`는 여러분 자신의 analyzer(및 소비자 측 도구)가 이 파일을 생성된 코드로 취급하도록 하여, 그렇지 않으면 적용될 스타일/품질 규칙을 건너뛰게 합니다.
- `#pragma warning disable`은 파일 내 모든 경고를 무조건 해제하여, `TreatWarningsAsErrors`를 포함한 여러분 프로젝트의 정확한 경고 설정 아래에서도 깨끗하게 컴파일되도록 합니다.
- `#nullable enable`은 여러분 프로젝트의 nullable 설정과 무관하게 파일 자체의 nullable 계약을 고정합니다.

이 헤더 외에도, 8개 컴포넌트 전체에서 모든 타입은 `internal`이며, 모든 파일은 고정된 `SsalKit.Generators.Toolkit` 네임스페이스에 있습니다 — 타입이 `internal`이므로, 이 패키지를 각자 임베드하는 서로 다른 두 생성기 어셈블리가 충돌할 일은 없습니다. (`IsExternalInit` 폴리필만은 위에서 설명한 이유로 이 네임스페이스 규칙에서 의도적으로 제외됩니다.)

폴리필을 함께 배포하게 된 지금도, 소스 자신은 의도적으로 `record` 타입과 `init` 전용 속성을 쓰지 않습니다. 그래야 폴리필 옵트아웃이 대가 없는 선택으로 남기 때문입니다 — 툴킷 자신의 코드가 `init`을 필요로 한다면, 폴리필을 빼는 순간 패키지의 나머지까지 함께 깨집니다. 그래서 `DiagnosticInfo`와 `LocationInfo`도 `record`가 아니라 `IEquatable<T>`를 직접 구현한 평범한 클래스입니다. 조건부 컴파일이 허용되는 파일도 `IsExternalInit.cs` 하나뿐이며, 나머지는 소비자의 `DefineConstants`가 무엇이든 동일하게 컴파일됩니다.

문법 상한은 C# 10입니다(파일 스코프 네임스페이스는 괜찮지만, primary constructor와 컬렉션 표현식은 사용하지 않습니다) — 여러분 프로젝트의 `LangVersion`이 그보다 최신이라고 가정할 수 없기 때문입니다. `global using`을 선언하는 파일도 없습니다. 임베드 파일의 `global using`은 *여러분* 컴파일의 모든 파일에 적용되어, 이 패키지가 보지도 못한 소스에서 이름이 바인딩되는 방식을 조용히 바꿔 버립니다.

같은 사실 — 이 소스들이 패키지가 아니라 **여러분**의 옵션으로 컴파일된다는 사실 — 에서 규칙 두 가지가 더 나옵니다.

- **모든 해시 누산은 명시적으로 `unchecked`입니다.** 해시는 넘쳐 돌게 되어 있는데, `<CheckForOverflowUnderflow>true</CheckForOverflowUnderflow>`에서는 감싸지 않은 누산이 `OverflowException`을 던집니다. 그것도 여러분이 손댈 수 없는 파일에서, 파이프라인 모델의 동등성 경로 위에서요. `EquatableArray<T>`, `DiagnosticInfo`, `LocationInfo`가 각자의 누산을 감쌉니다.
- **출력에 `Environment.NewLine`을 쓰지 않습니다.** `IndentedCodeWriter`는 무조건 `"\n"`을 내보내므로, 어느 머신에서 빌드하든 생성 파일이 바이트 단위로 동일합니다.

여기 나열한 모든 것 — 헤더, `internal` 전용 타입, 네임스페이스, C# 10, `global using` 금지, `unchecked` 해싱, `Environment.NewLine` 금지 — 은 관례가 아니라 실제 배포 파일을 읽는 테스트로 강제됩니다.

## 알려진 제약

생성기 프로젝트가 `[InternalsVisibleTo]`로 테스트 프로젝트에 `internal` 접근을 열어주고 있다면, **그 테스트 프로젝트가 SsalKit.Generators.Toolkit을 직접 참조하게 하지 마세요.** 두 경로 모두 동일한 `internal` 타입(같은 네임스페이스, 같은 이름)을 테스트 프로젝트의 컴파일에 들여오게 됩니다 — 한 번은 (`InternalsVisibleTo`를 통해) 생성기 어셈블리 경유로, 또 한 번은 패키지 자신의 임베드 소스 경유로요. 컴파일러는 이를 모호한 중복으로 보고 거부합니다.

테스트에서 이 헬퍼들이 필요하다면, 테스트 프로젝트가 생성기의 다른 `internal` 타입에 접근하는 것과 같은 방식으로 접근하세요 — 즉 생성기 프로젝트에 대한 `[InternalsVisibleTo]`를 통해서이지, 별도의 직접 패키지 참조를 통해서가 아닙니다.

## 라이센스

MIT — 자세한 내용은 [LICENSE](https://github.com/ssalkit/ssalkit/blob/main/LICENSE)를 참고하세요.

---

**AI 고지:** 이 프로젝트는 AI(Claude)를 활용하여 제작되었습니다.
