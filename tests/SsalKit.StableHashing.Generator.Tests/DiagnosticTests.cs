using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit.Testing;
using SsalKit.StableHashing.Generator.Tests.TestSupport;

namespace SsalKit.StableHashing.Generator.Tests;

/// <summary>
/// One test per <c>SSALH</c> rule (and per distinct trigger within a rule): the id and severity
/// that get reported, and the fact that a contract with any <see cref="DiagnosticSeverity.Error"/>
/// diagnostic gets no extension class at all -- there is no partial generation. Warning-severity
/// rules (SSALH010-012) are the exception: they never block generation, and each test here checks
/// that explicitly.
/// </summary>
public class DiagnosticTests
{
    [Fact]
    public void SSALH001_DuplicateMemberId_IsReportedOnEveryOffendingMember()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.dup-id", Version = 1)]
            public sealed class DupId
            {
                [StableHashMember(1)] public int A { get; init; }
                [StableHashMember(1)] public int B { get; init; }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        Assert.Equal(2, result.Diagnostics.Length);
        Assert.All(result.Diagnostics, d => Assert.Equal("SSALH001", d.Id));
        Assert.All(result.Diagnostics, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
    }

    [Theory]
    [InlineData("public System.Collections.Generic.Dictionary<string, int> Value { get; init; } = new();", "Dictionary")]
    [InlineData("public System.Collections.Generic.HashSet<int> Value { get; init; } = new();", "HashSet")]
    [InlineData("public object Value { get; init; } = new();", "object")]
    public void SSALH002_UnsupportedMemberType(string memberDeclaration, string expectedTypeFragment)
    {
        var source = WrapInClass("game.unsupported-type", "[StableHashMember(1)]\n    " + memberDeclaration);

        var diagnostic = AssertSingleContractDiagnostic(source, "SSALH002");
        Assert.Contains(expectedTypeFragment, diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void SSALH003_DateTimeMember()
    {
        var source = WrapInClass("game.datetime", "[StableHashMember(1)] public System.DateTime Value { get; init; }");

        AssertSingleContractDiagnostic(source, "SSALH003");
    }

    [Fact]
    public void SSALH004_UserTypeWithoutContract()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            public sealed class Plain
            {
                public int X { get; init; }
            }

            [StableHashContract("game.missing-contract", Version = 1)]
            public sealed class Owner
            {
                [StableHashMember(1)] public Plain Value { get; init; } = new();
            }
            """;

        AssertSingleContractDiagnostic(source, "SSALH004");
    }

    [Fact]
    public void SSALH005_DirectSelfReferenceIsACircularGraph()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.node", Version = 1)]
            public sealed class Node
            {
                [StableHashMember(1)] public Node? Next { get; init; }
            }
            """;

        AssertSingleContractDiagnostic(source, "SSALH005");
    }

    [Fact]
    public void SSALH005_IndirectCycleThroughTwoContracts_IsReportedOnBothTypes()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.a", Version = 1)]
            public sealed class A
            {
                [StableHashMember(1)] public B Value { get; init; } = null!;
            }

            [StableHashContract("game.b", Version = 1)]
            public sealed class B
            {
                [StableHashMember(1)] public A Value { get; init; } = null!;
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        Assert.Equal(2, result.Diagnostics.Length);
        Assert.All(result.Diagnostics, d => Assert.Equal("SSALH005", d.Id));
    }

    [Fact]
    public void SSALH006_NonSealedClassContract()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.not-sealed", Version = 1)]
            public class NotSealed
            {
                [StableHashMember(1)] public int Value { get; init; }
            }
            """;

        AssertSingleContractDiagnostic(source, "SSALH006");
    }

    [Theory]
    [InlineData("public static int Value { get; set; }")]
    [InlineData("public int Value { private get; init; }")]
    [InlineData("public int this[int index] => 0;")]
    [InlineData("public int Value { set { } }")]
    [InlineData("private int Value { get; init; }")]
    public void SSALH007_MemberAccessProblems(string memberDeclaration)
    {
        var source = WrapInClass("game.inaccessible", "[StableHashMember(1)]\n    " + memberDeclaration);

        AssertSingleContractDiagnostic(source, "SSALH007");
    }

    [Fact]
    public void SSALH007_PrivateNestedContractType()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            public static class Outer
            {
                [StableHashContract("game.private-nested", Version = 1)]
                private sealed class Inner
                {
                    [StableHashMember(1)] public int Value { get; init; }
                }
            }
            """;

        AssertSingleContractDiagnostic(source, "SSALH007");
    }

    [Fact]
    public void SSALH008_MemberIdLessThanOne()
    {
        var source = WrapInClass("game.bad-id", "[StableHashMember(0)] public int Value { get; init; }");

        AssertSingleContractDiagnostic(source, "SSALH008");
    }

    [Theory]
    [InlineData("   ", 1)]
    [InlineData("game.bad-version", 0)]
    public void SSALH009_InvalidNameOrVersion(string name, int version)
    {
        var source = $$"""
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("{{name}}", Version = {{version}})]
            public sealed class BadContract
            {
                [StableHashMember(1)] public int Value { get; init; }
            }
            """;

        AssertSingleContractDiagnostic(source, "SSALH009");
    }

    [Fact]
    public void SSALH010_ZeroMembers_IsAWarningAndStillGenerates()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.empty", Version = 1)]
            public sealed class Empty
            {
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        DiagnosticAssert.Single(result.Diagnostics, "SSALH010", DiagnosticSeverity.Warning, exclusive: true);
        Assert.Single(result.GeneratedSources);
        Assert.Empty(result.GetCompilationErrors());
    }

    [Fact]
    public void SSALH011_DuplicateContractName_IsAWarningAndStillGeneratesBoth()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.shared-name", Version = 1)]
            public sealed class First
            {
                [StableHashMember(1)] public int Value { get; init; }
            }

            [StableHashContract("game.shared-name", Version = 1)]
            public sealed class Second
            {
                [StableHashMember(1)] public int Value { get; init; }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Equal(2, result.Diagnostics.Length);
        Assert.All(result.Diagnostics, d => Assert.Equal("SSALH011", d.Id));
        Assert.All(result.Diagnostics, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));
        Assert.Equal(2, result.GeneratedSources.Length);
        Assert.Empty(result.GetCompilationErrors());
    }

    [Fact]
    public void SSALH012_OrphanMemberAttribute_OnATypeWithNoContract()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            public sealed class NotAContract
            {
                [StableHashMember(1)] public int Value { get; init; }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALH012", DiagnosticSeverity.Warning, exclusive: true);
        DiagnosticAssert.SpanStartsWith(diagnostic, "StableHashMember", source);
    }

    [Fact]
    public void SSALH013_GenericContractType()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.generic", Version = 1)]
            public sealed class Generic<T>
            {
                [StableHashMember(1)] public int Value { get; init; }
            }
            """;

        AssertSingleContractDiagnostic(source, "SSALH013");
    }

    [Fact]
    public void ValidContract_ReportsNoDiagnostics()
    {
        const string source = """
            using SsalKit.StableHashing;

            namespace Game.Snapshots;

            [StableHashContract("game.valid", Version = 1)]
            public sealed class Valid
            {
                [StableHashMember(1)] public int Value { get; init; }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedSources);
        Assert.Empty(result.GetCompilationErrors());
    }

    [Fact]
    public void EveryDiagnosticUsesTheSsalKitStableHashingCategory()
    {
        var source = WrapInClass("game.datetime", "[StableHashMember(1)] public System.DateTime Value { get; init; }");

        var diagnostic = AssertSingleContractDiagnostic(source, "SSALH003");

        Assert.Equal("SsalKit.StableHashing", diagnostic.Descriptor.Category);
    }

    private static string WrapInClass(string contractName, string memberDeclaration) => $$"""
        using SsalKit.StableHashing;

        namespace Game.Snapshots;

        [StableHashContract("{{contractName}}", Version = 1)]
        public sealed class Contract
        {
            {{memberDeclaration}}
        }
        """;

    private static Diagnostic AssertSingleContractDiagnostic(string source, string expectedId)
    {
        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);

        return DiagnosticAssert.Single(result.Diagnostics, expectedId, DiagnosticSeverity.Error, exclusive: true);
    }
}
