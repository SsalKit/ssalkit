using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit.Testing;
using SsalKit.Randomness.Generator.Tests.TestSupport;

namespace SsalKit.Randomness.Generator.Tests;

/// <summary>
/// One test per <c>SSALR</c> rule (and per distinct trigger within a rule): the id and severity
/// that get reported, the location they point at, and the fact that a type with any diagnostic
/// gets no extension class at all -- there is no partial generation.
/// </summary>
public class DiagnosticTests
{
    [Theory]
    [InlineData("public ulong Weight { get; init; }", "ulong")]
    [InlineData("public decimal Weight { get; init; }", "decimal")]
    [InlineData("public string Weight { get; init; } = \"\";", "string")]
    [InlineData("public bool Weight { get; init; }", "bool")]
    [InlineData("public long? Weight { get; init; }", "long?")]
    [InlineData("public System.DayOfWeek Weight { get; init; }", "System.DayOfWeek")]
    public void SSALR001_UnsupportedWeightType(string memberDeclaration, string expectedTypeInMessage)
    {
        var source = WrapInClass("[RandomWeight]\n    " + memberDeclaration);

        var diagnostic = AssertSingleDiagnostic(source, "SSALR001");

        Assert.Contains(expectedTypeInMessage, diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ulong", "can overflow")]
    [InlineData("decimal", "no weighted-picking overload accepts it")]
    public void SSALR001_SpellsOutWhyTheDeliberatelyExcludedTypesAreMissing(string weightType, string expectedNote)
    {
        var source = WrapInClass($"[RandomWeight]\n    public {weightType} Weight {{ get; init; }}");

        var diagnostic = AssertSingleDiagnostic(source, "SSALR001");

        Assert.Contains(expectedNote, diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void SSALR002_MoreThanOneWeightMember_IsReportedOnEveryDecoratedMember()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                [RandomWeight]
                public long Weight { get; init; }

                [RandomWeight]
                public long Bonus { get; init; }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        Assert.Equal(2, result.Diagnostics.Length);

        foreach (var diagnostic in result.Diagnostics)
        {
            Assert.Equal("SSALR002", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Contains("'Bonus', 'Weight'", diagnostic.GetMessage(), StringComparison.Ordinal);
            AssertReportedOnAnAttribute(diagnostic, source);
        }

        // Both offending declarations are highlighted, not just the second one.
        Assert.Equal(2, result.Diagnostics.Select(d => d.Location.SourceSpan.Start).Distinct().Count());
    }

    [Fact]
    public void SSALR002_CountsMembersAcrossPartialDeclarations()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed partial class LootEntry
            {
                [RandomWeight]
                public long Weight { get; init; }
            }

            public sealed partial class LootEntry
            {
                [RandomWeight]
                public long Bonus { get; init; }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        Assert.Equal(2, result.Diagnostics.Length);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal("SSALR002", diagnostic.Id));
    }

    [Fact]
    public void SSALR002_AlsoCountsAMemberThatIsInvalidOnItsOwn()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry
            {
                [RandomWeight]
                public long Weight { get; init; }

                [RandomWeight]
                public string Label { get; init; } = "";
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        Assert.Equal(
            new[] { "SSALR001", "SSALR002", "SSALR002" },
            result.Diagnostics.Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray());
    }

    [Theory]
    [InlineData("public static long Weight { get; set; }", "static")]
    [InlineData("public static long Weight;", "static")]
    [InlineData("public const long Weight = 1;", "static")]
    [InlineData("public long Weight { set { } }", "a write-only property")]
    [InlineData("public long this[int index] => 0;", "an indexer")]
    public void SSALR003_InvalidMemberKind(string memberDeclaration, string expectedReason)
    {
        var source = WrapInClass("[RandomWeight]\n    " + memberDeclaration);

        var diagnostic = AssertSingleDiagnostic(source, "SSALR003");

        Assert.Contains(expectedReason, diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("private long Weight { get; init; }")]
    [InlineData("protected long Weight { get; init; }")]
    [InlineData("private protected long Weight { get; init; }")]
    [InlineData("private long Weight;")]
    [InlineData("public long Weight { private get; init; }")]
    public void SSALR004_InaccessibleMember(string memberDeclaration)
    {
        var source = WrapInClass("[RandomWeight]\n    " + memberDeclaration, sealedClass: false);

        AssertSingleDiagnostic(source, "SSALR004");
    }

    [Fact]
    public void SSALR004_PrivateNestedDeclaringType()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public static class Tables
            {
                private sealed class Entry
                {
                    [RandomWeight]
                    public long Weight { get; init; }
                }
            }
            """;

        var diagnostic = AssertSingleDiagnostic(source, "SSALR004");

        Assert.Contains("its declaring type", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void SSALR004_FileLocalDeclaringType()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            file sealed class Entry
            {
                [RandomWeight]
                public long Weight { get; init; }
            }
            """;

        var diagnostic = AssertSingleDiagnostic(source, "SSALR004");

        Assert.Contains("file-local", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void SSALR004_ProtectedInternalMemberIsAccepted()
    {
        // The generated class benefits from the "internal" half of protected internal, unlike
        // private protected, which requires a derived class the generated code never is.
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public class LootEntry
            {
                [RandomWeight]
                protected internal long Weight { get; init; }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedSources);
        Assert.Empty(result.GetCompilationErrors());
    }

    [Fact]
    public void SSALR005_GenericDeclaringType()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public sealed class LootEntry<T>
            {
                [RandomWeight]
                public long Weight { get; init; }
            }
            """;

        AssertSingleDiagnostic(source, "SSALR005");
    }

    [Fact]
    public void SSALR005_TypeNestedInsideAGenericType()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public static class Tables<T>
            {
                public sealed class Entry
                {
                    [RandomWeight]
                    public long Weight { get; init; }
                }
            }
            """;

        AssertSingleDiagnostic(source, "SSALR005");
    }

    [Fact]
    public void SSALR006_RefStructDeclaringType()
    {
        const string source = """
            using SsalKit.Randomness;

            namespace Game.Loot;

            public ref struct LootEntry
            {
                [RandomWeight]
                public long Weight { get; set; }
            }
            """;

        AssertSingleDiagnostic(source, "SSALR006");
    }

    [Fact]
    public void EveryDiagnosticUsesTheSsalKitRandomnessCategory()
    {
        var source = WrapInClass("[RandomWeight]\n    public decimal Weight { get; init; }");

        var diagnostic = AssertSingleDiagnostic(source, "SSALR001");

        Assert.Equal("SsalKit.Randomness", diagnostic.Descriptor.Category);
    }

    private static string WrapInClass(string memberDeclaration, bool sealedClass = true) => $$"""
        using SsalKit.Randomness;

        namespace Game.Loot;

        public {{(sealedClass ? "sealed " : "")}}class LootEntry
        {
            {{memberDeclaration}}
        }
        """;

    /// <summary>
    /// Asserts exactly one SSALR diagnostic with the expected id and <c>Error</c> severity was
    /// reported, that it points at the attribute application, and that nothing was generated for
    /// the offending type.
    /// </summary>
    private static Diagnostic AssertSingleDiagnostic(string source, string expectedId)
    {
        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, expectedId, DiagnosticSeverity.Error, exclusive: true);
        AssertReportedOnAnAttribute(diagnostic, source);

        return diagnostic;
    }

    /// <summary>
    /// The reported span must cover the <c>[RandomWeight]</c> application the user wrote (the
    /// attribute syntax itself, i.e. without the enclosing brackets), so the squiggle lands on the
    /// token they can delete.
    /// </summary>
    private static void AssertReportedOnAnAttribute(Diagnostic diagnostic, string source)
    {
        var span = diagnostic.Location.SourceSpan;
        var reportedText = source.Substring(span.Start, span.Length);

        Assert.StartsWith("RandomWeight", reportedText, StringComparison.Ordinal);
    }
}
