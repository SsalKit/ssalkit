using SsalKit.Generators.Toolkit;

namespace SsalKit.Generators.Toolkit.Tests;

/// <summary>
/// Direct unit tests for <see cref="CSharpNaming"/>: PascalCase/camelCase identifier conversion
/// (ported from the original <c>AssemblyNameSanitizer</c> test cases, plus new camelCase and
/// keyword-escaping coverage) and reserved-keyword escaping.
/// </summary>
public class CSharpNamingTests
{
    [Fact]
    public void ToPascalCaseIdentifier_Null_FallsBackToDefault()
    {
        var result = CSharpNaming.ToPascalCaseIdentifier(null);

        Assert.Equal("Identifier", result);
    }

    [Fact]
    public void ToPascalCaseIdentifier_EmptyString_FallsBackToDefault()
    {
        var result = CSharpNaming.ToPascalCaseIdentifier(string.Empty);

        Assert.Equal("Identifier", result);
    }

    [Fact]
    public void ToPascalCaseIdentifier_AllSymbols_FallsBackToDefault()
    {
        var result = CSharpNaming.ToPascalCaseIdentifier("!!!");

        Assert.Equal("Identifier", result);
    }

    [Fact]
    public void ToPascalCaseIdentifier_CustomFallback_IsUsedInsteadOfDefault()
    {
        var result = CSharpNaming.ToPascalCaseIdentifier(null, fallback: "Assembly");

        Assert.Equal("Assembly", result);
    }

    [Fact]
    public void ToPascalCaseIdentifier_LeadingDigitSegment_IsPrefixedWithUnderscore()
    {
        // "123.Sample" -> segments "123" and "Sample" -> concatenated "123Sample", which starts
        // with a digit and therefore is not a valid C# identifier, so an "_" is prepended.
        var result = CSharpNaming.ToPascalCaseIdentifier("123.Sample");

        Assert.Equal("_123Sample", result);
    }

    [Fact]
    public void ToPascalCaseIdentifier_DottedName_ConcatenatesPascalCasedSegments()
    {
        var result = CSharpNaming.ToPascalCaseIdentifier("SsalKit.Sample");

        Assert.Equal("SsalKitSample", result);
    }

    [Fact]
    public void ToPascalCaseIdentifier_LowercaseSingleSegment_IsCapitalized()
    {
        var result = CSharpNaming.ToPascalCaseIdentifier("simple");

        Assert.Equal("Simple", result);
    }

    [Fact]
    public void ToPascalCaseIdentifier_MultipleSeparators_EachStartsANewCapitalizedSegment()
    {
        // "my-app2": '-' splits "my" and "app2"; only the first character of each segment is
        // capitalized, digits mid-segment are copied through as-is.
        var result = CSharpNaming.ToPascalCaseIdentifier("my-app2");

        Assert.Equal("MyApp2", result);
    }

    [Fact]
    public void ToCamelCaseIdentifier_Null_FallsBackToDefault()
    {
        var result = CSharpNaming.ToCamelCaseIdentifier(null);

        Assert.Equal("identifier", result);
    }

    [Fact]
    public void ToCamelCaseIdentifier_EmptyString_FallsBackToDefault()
    {
        var result = CSharpNaming.ToCamelCaseIdentifier(string.Empty);

        Assert.Equal("identifier", result);
    }

    [Fact]
    public void ToCamelCaseIdentifier_AllSymbols_FallsBackToDefault()
    {
        var result = CSharpNaming.ToCamelCaseIdentifier("###");

        Assert.Equal("identifier", result);
    }

    [Fact]
    public void ToCamelCaseIdentifier_CustomFallback_IsUsedInsteadOfDefault()
    {
        var result = CSharpNaming.ToCamelCaseIdentifier(null, fallback: "value");

        Assert.Equal("value", result);
    }

    [Fact]
    public void ToCamelCaseIdentifier_SingleLeadingUpperLetter_IsLowered()
    {
        // Only the leading letter forms the run because the second letter is already lower case.
        var result = CSharpNaming.ToCamelCaseIdentifier("Service");

        Assert.Equal("service", result);
    }

    [Fact]
    public void ToCamelCaseIdentifier_DottedName_LowersOnlyTheLeadingWord()
    {
        var result = CSharpNaming.ToCamelCaseIdentifier("SsalKit.Sample");

        Assert.Equal("ssalKitSample", result);
    }

    [Fact]
    public void ToCamelCaseIdentifier_LeadingAcronym_LowersWholeRunExceptLastLetter()
    {
        // "IOService" -> Pascal segment "IOService"; the "IO" acronym is lowered, but the
        // trailing "S" is preserved because it begins the next word, "Service".
        var result = CSharpNaming.ToCamelCaseIdentifier("IOService");

        Assert.Equal("ioService", result);
    }

    [Fact]
    public void ToCamelCaseIdentifier_AcronymFollowedByAnotherAcronym_LowersUpToTheLastWord()
    {
        // "ABCWord" -> "abcWord": "ABC" is lowered as a run, but the "W" that begins "Word"
        // survives, exercising the false branch of the trailing-word lookahead at "C".
        var result = CSharpNaming.ToCamelCaseIdentifier("ABCWord");

        Assert.Equal("abcWord", result);
    }

    [Fact]
    public void ToCamelCaseIdentifier_ShortAllCapsWord_IsLoweredEntirely()
    {
        // "AB" has no trailing word to preserve a capital for, so it is lowered completely;
        // exercises the "no next character" (hasNext == false) branch at the last letter.
        var result = CSharpNaming.ToCamelCaseIdentifier("AB");

        Assert.Equal("ab", result);
    }

    [Fact]
    public void ToCamelCaseIdentifier_SingleLetter_IsLowered()
    {
        var result = CSharpNaming.ToCamelCaseIdentifier("X");

        Assert.Equal("x", result);
    }

    [Fact]
    public void ToCamelCaseIdentifier_LeadingDigitSegment_IsNotLowered()
    {
        // The Pascal-cased result starts with '_' (not a letter), so there is no leading
        // upper-case run to lower and the value passes through unchanged.
        var result = CSharpNaming.ToCamelCaseIdentifier("123abc");

        Assert.Equal("_123abc", result);
    }

    public static readonly string[] ReservedKeywords =
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
        "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
        "virtual", "void", "volatile", "while",
    };

    [Fact]
    public void ReservedKeywordTable_HasSeventySevenEntries()
    {
        Assert.Equal(77, ReservedKeywords.Length);
    }

    public static IEnumerable<object[]> ReservedKeywordCases() =>
        ReservedKeywords.Select(keyword => new object[] { keyword });

    [Theory]
    [MemberData(nameof(ReservedKeywordCases))]
    public void EscapeKeyword_ReservedKeyword_IsPrefixedWithAt(string keyword)
    {
        var result = CSharpNaming.EscapeKeyword(keyword);

        Assert.Equal("@" + keyword, result);
    }

    [Theory]
    [InlineData("myVariable")]
    [InlineData("Service")]
    [InlineData("value")]
    public void EscapeKeyword_NonKeyword_IsUnchanged(string identifier)
    {
        var result = CSharpNaming.EscapeKeyword(identifier);

        Assert.Equal(identifier, result);
    }

    [Theory]
    [InlineData("var")]
    [InlineData("nameof")]
    [InlineData("async")]
    [InlineData("await")]
    public void EscapeKeyword_ContextualKeyword_IsUnchanged(string contextualKeyword)
    {
        var result = CSharpNaming.EscapeKeyword(contextualKeyword);

        Assert.Equal(contextualKeyword, result);
    }

    [Fact]
    public void EscapeKeyword_IsCaseSensitive()
    {
        var result = CSharpNaming.EscapeKeyword("Class");

        Assert.Equal("Class", result);
    }
}
