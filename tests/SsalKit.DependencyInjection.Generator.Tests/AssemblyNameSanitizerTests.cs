using SsalKit.DependencyInjection.Generator.Emission;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Direct unit tests for <see cref="AssemblyNameSanitizer.ToPascalCaseIdentifier(string?)"/>,
/// covering the null/empty fallback, the all-symbols fallback, the leading-digit prefix rule, and
/// ordinary PascalCase segment conversion.
/// </summary>
public class AssemblyNameSanitizerTests
{
    [Fact]
    public void Null_FallsBackToAssembly()
    {
        var result = AssemblyNameSanitizer.ToPascalCaseIdentifier(null);

        Assert.Equal("Assembly", result);
    }

    [Fact]
    public void EmptyString_FallsBackToAssembly()
    {
        var result = AssemblyNameSanitizer.ToPascalCaseIdentifier(string.Empty);

        Assert.Equal("Assembly", result);
    }

    [Fact]
    public void AllSymbols_FallsBackToAssembly()
    {
        var result = AssemblyNameSanitizer.ToPascalCaseIdentifier("!!!");

        Assert.Equal("Assembly", result);
    }

    [Fact]
    public void LeadingDigitSegment_IsPrefixedWithUnderscore()
    {
        // "123.Sample" -> segments "123" and "Sample" -> concatenated "123Sample", which starts
        // with a digit and therefore is not a valid C# identifier, so an "_" is prepended.
        var result = AssemblyNameSanitizer.ToPascalCaseIdentifier("123.Sample");

        Assert.Equal("_123Sample", result);
    }

    [Fact]
    public void DottedName_ConcatenatesPascalCasedSegments()
    {
        var result = AssemblyNameSanitizer.ToPascalCaseIdentifier("SsalKit.Sample");

        Assert.Equal("SsalKitSample", result);
    }

    [Fact]
    public void LowercaseSingleSegment_IsCapitalized()
    {
        var result = AssemblyNameSanitizer.ToPascalCaseIdentifier("simple");

        Assert.Equal("Simple", result);
    }

    [Fact]
    public void MultipleSeparators_EachStartsANewCapitalizedSegment()
    {
        // "my-app2": '-' splits "my" and "app2"; only the first character of each segment is
        // capitalized, digits mid-segment are copied through as-is.
        var result = AssemblyNameSanitizer.ToPascalCaseIdentifier("my-app2");

        Assert.Equal("MyApp2", result);
    }
}
