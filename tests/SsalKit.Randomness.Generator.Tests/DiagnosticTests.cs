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
            DiagnosticAssert.SpanStartsWith(diagnostic, "RandomWeight", source);
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

    /// <summary>
    /// SSALR007 on the two shapes a <c>field:</c> target can be written on. The reason clause differs
    /// per shape, because the nameable member to decorate instead does: an auto-property is itself
    /// that member, while a positional record parameter has the synthesized property behind
    /// <c>property:</c>.
    /// </summary>
    [Theory]
    [InlineData(
        "public sealed record LootEntry(string ItemId, [field: RandomWeight] long Weight);",
        "write '[property: RandomWeight]' instead")]
    [InlineData(
        """
        public sealed class LootEntry
        {
            [field: RandomWeight]
            public long Weight { get; init; }
        }
        """,
        "apply '[RandomWeight]' to the property itself, with no target specifier")]
    public void SSALR007_TargetRedirectedToABackingField(string declaration, string expectedFix)
    {
        var source = Wrap(declaration);

        var diagnostic = AssertSingleDiagnostic(source, "SSALR007");

        Assert.Contains("'Game.Loot.LootEntry.Weight'", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("the compiler-generated backing field", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains(expectedFix, diagnostic.GetMessage(), StringComparison.Ordinal);

        // The member is named as the user wrote it. The symbol the attribute actually landed on is
        // called '<Weight>k__BackingField', which would be both unhelpful here and wrong in
        // SSALR002's member list.
        Assert.DoesNotContain("BackingField", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A <c>field:</c> target on a manually implemented property is discarded by the compiler
    /// (CS0657) -- there is no backing field for it to land on -- so the generator stays silent
    /// rather than adding SSALR007 to an application that exists on no symbol.
    /// </summary>
    [Fact]
    public void FieldTargetOnAManuallyImplementedProperty_IsSilent()
    {
        var source = Wrap("""
            public sealed class LootEntry
            {
                private readonly long _weight;

                [field: RandomWeight]
                public long Weight => _weight;
            }
            """);

        AssertSilence(source);
    }

    /// <summary>
    /// A target specifier that names the declaration's own default target redirects nothing: the
    /// attribute lands on the property or field itself, which the attribute provider reports. The
    /// syntax-driven branch must therefore leave both alone -- claiming either would model the one
    /// application twice and trip SSALR002 on a type that declares exactly one weight member.
    /// </summary>
    [Theory]
    [InlineData("[property: RandomWeight]", "public long Weight { get; init; }")]
    [InlineData("[field: RandomWeight]", "public long Weight;")]
    public void ATargetNamingTheDeclarationsOwnDefault_IsModelledExactlyOnce(string attribute, string member)
    {
        var source = WrapInClass(attribute + "\n    " + member);

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedSources);
        Assert.Empty(result.GetCompilationErrors());
    }

    /// <summary>
    /// A <c>property:</c> target with no synthesized property to attach to: shadowed by a
    /// user-declared member of the same name, or written on a non-record primary constructor. The
    /// compiler reports CS0657 and discards the attribute, which then exists on no symbol at all, so
    /// there is no weight member for the generator to have an opinion about.
    /// </summary>
    [Theory]
    [InlineData("""
        public sealed record LootEntry(string ItemId, [property: RandomWeight] long Weight)
        {
            public long Weight { get; init; }
        }
        """)]
    [InlineData("""
        public sealed class LootEntry(string itemId, [property: RandomWeight] long weight)
        {
            public string ItemId { get; } = itemId;

            public long Weight { get; } = weight;
        }
        """)]
    public void PropertyTargetWithNoSynthesizedProperty_IsSilent(string declaration) =>
        AssertSilence(Wrap(declaration));

    /// <summary>
    /// Both targets on one parameter: two applications, two members, so SSALR007 (for the one on the
    /// backing field) and SSALR002 (for there now being two) are reported together, and nothing is
    /// generated. No special case makes this happen -- it falls out of the same rules that report a
    /// valid member alongside an invalid one.
    /// </summary>
    [Fact]
    public void BothTargetsOnOneParameter_ReportsSSALR007AndSSALR002()
    {
        var source = Wrap(
            "public sealed record LootEntry(string ItemId, [property: RandomWeight][field: RandomWeight] long Weight);");

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        Assert.Equal(
            new[] { "SSALR002", "SSALR002", "SSALR007" },
            result.Diagnostics.Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray());

        // Each application is reported where it was written, so the two attributes on the one
        // parameter are told apart.
        Assert.Equal(2, result.Diagnostics.Select(d => d.Location.SourceSpan.Start).Distinct().Count());
    }

    /// <summary>
    /// SSALR002 counts across both branches. The two providers are collected separately and merged
    /// before grouping precisely so that "one weight member per type" stays a fact about the type
    /// rather than about one provider's view of it.
    /// </summary>
    [Fact]
    public void SSALR002_CountsMembersFromBothBranches()
    {
        var source = Wrap("""
            public sealed record LootEntry(string ItemId, [property: RandomWeight] long Weight)
            {
                [RandomWeight]
                public long Bonus { get; init; }
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        Assert.Equal(2, result.Diagnostics.Length);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal("SSALR002", diagnostic.Id));
        Assert.All(
            result.Diagnostics,
            diagnostic => Assert.Contains("'Bonus', 'Weight'", diagnostic.GetMessage(), StringComparison.Ordinal));
    }

    /// <summary>
    /// The promoted property goes through the same rules as any other weight member, which is the
    /// reason the syntax-driven branch reuses the shared parser rather than validating anything of its
    /// own.
    /// </summary>
    [Theory]
    [InlineData("public sealed record LootEntry<TItem>(TItem Item, [property: RandomWeight] long Weight);", "SSALR005")]
    [InlineData("public sealed record LootEntry(string ItemId, [property: RandomWeight] decimal Weight);", "SSALR001")]
    [InlineData("internal sealed record LootEntry([property: RandomWeight] string Weight);", "SSALR001")]
    public void PromotedProperty_IsSubjectToTheOrdinaryRules(string declaration, string expectedId) =>
        AssertSingleDiagnostic(Wrap(declaration), expectedId);

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
    /// Puts a whole type declaration into the namespace the assertions here name, for the cases where
    /// the declaration's own shape -- a positional record, a primary constructor -- is the subject.
    /// </summary>
    private static string Wrap(string typeDeclaration) => $"""
        using SsalKit.Randomness;

        namespace Game.Loot;

        {typeDeclaration}
        """;

    /// <summary>
    /// Asserts the generator neither generated nor reported anything, which is the contract for every
    /// attribute application the compiler has already rejected: it is not the generator's place to
    /// pile a second message onto a CS**** the user is going to fix anyway.
    /// </summary>
    private static void AssertSilence(string source)
    {
        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        Assert.Empty(result.Diagnostics);
    }

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
        DiagnosticAssert.SpanStartsWith(diagnostic, "RandomWeight", source);

        return diagnostic;
    }
}
