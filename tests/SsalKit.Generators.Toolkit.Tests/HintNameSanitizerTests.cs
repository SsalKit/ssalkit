using SsalKit.Generators.Toolkit;

namespace SsalKit.Generators.Toolkit.Tests;

/// <summary>
/// Direct unit tests for <see cref="HintNameSanitizer"/>: invalid-character replacement (generic
/// arity, nested types, and Roslyn-disallowed characters), passthrough of the allowed character
/// set, suffix handling, the empty/whitespace fallback, and length capping.
/// </summary>
public class HintNameSanitizerTests
{
    [Fact]
    public void Sanitize_PlainName_AppendsDefaultSuffix()
    {
        var result = HintNameSanitizer.Sanitize("MyNamespace.MyType");

        Assert.Equal("MyNamespace.MyType.g.cs", result);
    }

    [Fact]
    public void Sanitize_GenericArityBacktick_IsReplacedWithUnderscore()
    {
        var result = HintNameSanitizer.Sanitize("Foo`1");

        Assert.Equal("Foo_1.g.cs", result);
    }

    [Fact]
    public void Sanitize_NestedTypePlusSign_IsReplacedWithUnderscore()
    {
        var result = HintNameSanitizer.Sanitize("Outer+Inner");

        Assert.Equal("Outer_Inner.g.cs", result);
    }

    [Fact]
    public void Sanitize_DisallowedCharacters_AreReplacedWithUnderscore()
    {
        var result = HintNameSanitizer.Sanitize("A<B>C:D/E\\F");

        Assert.Equal("A_B_C_D_E_F.g.cs", result);
    }

    [Fact]
    public void Sanitize_AllowedPunctuationAndSpaces_PassThroughUnchanged()
    {
        var result = HintNameSanitizer.Sanitize("My.Type,Name-Foo(Bar)[Baz] Qux_1");

        Assert.Equal("My.Type,Name-Foo(Bar)[Baz] Qux_1.g.cs", result);
    }

    [Fact]
    public void Sanitize_CandidateAlreadyEndingWithSuffix_DoesNotDuplicateSuffix()
    {
        var result = HintNameSanitizer.Sanitize("Already.g.cs");

        Assert.Equal("Already.g.cs", result);
    }

    [Fact]
    public void Sanitize_CustomSuffix_IsAppendedInsteadOfDefault()
    {
        var result = HintNameSanitizer.Sanitize("MyType", suffix: ".designer.cs");

        Assert.Equal("MyType.designer.cs", result);
    }

    [Fact]
    public void Sanitize_CustomSuffixAlreadyPresent_DoesNotDuplicateSuffix()
    {
        var result = HintNameSanitizer.Sanitize("MyType.designer.cs", suffix: ".designer.cs");

        Assert.Equal("MyType.designer.cs", result);
    }

    [Fact]
    public void Sanitize_EmptyString_FallsBackToGenerated()
    {
        var result = HintNameSanitizer.Sanitize(string.Empty);

        Assert.Equal("Generated.g.cs", result);
    }

    [Fact]
    public void Sanitize_WhitespaceOnly_FallsBackToGenerated()
    {
        var result = HintNameSanitizer.Sanitize("   ");

        Assert.Equal("Generated.g.cs", result);
    }

    [Fact]
    public void Sanitize_Null_FallsBackToGeneratedWithoutThrowing()
    {
        var result = HintNameSanitizer.Sanitize(null!);

        Assert.Equal("Generated.g.cs", result);
    }

    [Fact]
    public void Sanitize_LeadingGlobalAlias_IsStrippedNotReplaced()
    {
        var result = HintNameSanitizer.Sanitize("global::MyNamespace.MyType");

        Assert.Equal("MyNamespace.MyType.g.cs", result);
    }

    [Fact]
    public void Sanitize_GlobalAliasNotAtTheStart_IsReplacedLikeAnyOtherColon()
    {
        var result = HintNameSanitizer.Sanitize("MyNamespace.global::MyType");

        Assert.Equal("MyNamespace.global__MyType.g.cs", result);
    }

    [Fact]
    public void Sanitize_RepeatedGlobalAlias_StripsOnlyTheFirst()
    {
        var result = HintNameSanitizer.Sanitize("global::global::MyType");

        Assert.Equal("global__MyType.g.cs", result);
    }

    [Fact]
    public void Sanitize_BareGlobalAlias_FallsBackToGenerated()
    {
        var result = HintNameSanitizer.Sanitize("global::");

        Assert.Equal("Generated.g.cs", result);
    }

    [Fact]
    public void Sanitize_ResultExceedsMaxLength_IsTruncatedFromTheFrontPreservingTail()
    {
        var candidate = new string('A', 250);

        var result = HintNameSanitizer.Sanitize(candidate);

        var expected = new string('A', 195) + ".g.cs";
        Assert.Equal(200, result.Length);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Sanitize_ResultAtExactlyMaxLength_IsNotTruncated()
    {
        // Total length after appending the suffix is exactly 200 (195 'A's + 5-char suffix).
        var candidate = new string('A', 195);

        var result = HintNameSanitizer.Sanitize(candidate);

        Assert.Equal(200, result.Length);
        Assert.Equal(new string('A', 195) + ".g.cs", result);
    }
}
