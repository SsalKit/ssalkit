using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Tests.TestSupport;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Regression tests for the two cross-assembly SSAL007 accessibility gaps that only manifest with
/// a second, separately-compiled assembly: a type reachable only through an <c>extern alias</c>
/// (no <c>global</c> alias), and a <c>protected internal</c> nested type declared in another
/// assembly's base class (accessible at the derived-class attribute site via the "protected" half
/// of the grant, but not from the generated top-level, non-derived static class without
/// <c>[InternalsVisibleTo]</c>).
/// </summary>
public class TypeAccessibilityCrossAssemblyTests
{
    private const string Usings = """
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    private const string AliasLibrarySource = """
        namespace AliasLibNs
        {
            public class Marker { }
            public interface IMarker { }
        }
        """;

    private static MetadataReference CreateAliasOnlyReference() =>
        GeneratorTestHelper.CompileToReference(AliasLibrarySource, "AliasLib").WithAliases(new[] { "AliasNs" });

    private static MetadataReference CreateGloballyReferencedAliasLibrary() =>
        GeneratorTestHelper.CompileToReference(AliasLibrarySource, "AliasLib");

    [Fact]
    public async Task SSAL007_AliasOnlyTypeofKey_ReportsError()
    {
        // Regression test: the generated registration code emits only `global::`-qualified names
        // and never an `extern alias` directive, so a type only reachable through a non-global
        // alias cannot be named there at all (CS0400), even though it's perfectly nameable at the
        // [Service] attribute application site via `extern alias AliasNs; ... AliasNs::...`.
        const string source = """
            extern alias AliasNs;

            """ + Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = typeof(AliasNs::AliasLibNs.Marker))]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source, extraReferences: new[] { CreateAliasOnlyReference() });

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL007", diagnostic.Id);
    }

    [Fact]
    public void AliasOnlyTypeofKey_IsExcludedEntirely()
    {
        const string source = """
            extern alias AliasNs;

            """ + Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = typeof(AliasNs::AliasLibNs.Marker))]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source, extraReferences: new[] { CreateAliasOnlyReference() });

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public async Task SSAL007_AliasOnlyAsType_ReportsError()
    {
        // The same alias gate applies to an `As` service type, not just a `Key`, since both share
        // TypeAccessibilityChecker.
        const string source = """
            extern alias AliasNs;

            """ + Usings + """
            namespace TestNs;

            [Service(As = typeof(AliasNs::AliasLibNs.IMarker))]
            public class Foo : AliasNs::AliasLibNs.IMarker { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source, extraReferences: new[] { CreateAliasOnlyReference() });

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL007", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL007_GloballyReferencedTypeofKey_DoesNotReport()
    {
        // The exact same library type, but referenced normally (no alias restriction) -- this must
        // continue to be accepted, proving the new check doesn't over-reject ordinary cross-
        // assembly references.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = typeof(AliasLibNs.Marker))]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source, extraReferences: new[] { CreateGloballyReferencedAliasLibrary() });

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL007");
    }

    private const string ProtectedInternalLibrarySource = """
        namespace LibNs
        {
            public class Base
            {
                protected internal class Nested { }
            }
        }
        """;

    private const string ProtectedInternalLibraryWithIvtSource = """
        using System.Runtime.CompilerServices;

        [assembly: InternalsVisibleTo("TestAssembly")]

        namespace LibNs
        {
            public class Base
            {
                protected internal class Nested { }
            }
        }
        """;

    [Fact]
    public async Task SSAL007_ProtectedInternalTypeFromOtherAssembly_WithoutIvt_ReportsError()
    {
        // Regression test: `Nested` is nameable at the [Service] attribute site because `Foo`
        // derives from `Base` (the "protected" half of `protected internal` is satisfied by
        // inheritance), but the generated top-level static class is not a class derived from
        // `Base`, and without [InternalsVisibleTo] the "internal" half isn't satisfied either, so
        // referencing `Nested` there fails with CS0122.
        var libraryReference = GeneratorTestHelper.CompileToReference(ProtectedInternalLibrarySource, "ProtectedInternalLib");

        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = typeof(LibNs.Base.Nested))]
            public class Foo : LibNs.Base, IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source, extraReferences: new[] { libraryReference });

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL007", diagnostic.Id);
    }

    [Fact]
    public void ProtectedInternalTypeFromOtherAssembly_WithoutIvt_IsExcludedEntirely()
    {
        var libraryReference = GeneratorTestHelper.CompileToReference(ProtectedInternalLibrarySource, "ProtectedInternalLib");

        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = typeof(LibNs.Base.Nested))]
            public class Foo : LibNs.Base, IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source, extraReferences: new[] { libraryReference });

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public async Task SSAL007_ProtectedInternalTypeFromOtherAssembly_WithIvt_DoesNotReport()
    {
        var libraryReference = GeneratorTestHelper.CompileToReference(ProtectedInternalLibraryWithIvtSource, "ProtectedInternalLibWithIvt");

        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = typeof(LibNs.Base.Nested))]
            public class Foo : LibNs.Base, IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source, extraReferences: new[] { libraryReference });

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL007");
    }

    [Fact]
    public void ProtectedInternalTypeFromOtherAssembly_WithIvt_IsGenerated()
    {
        var libraryReference = GeneratorTestHelper.CompileToReference(ProtectedInternalLibraryWithIvtSource, "ProtectedInternalLibWithIvt");

        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = typeof(LibNs.Base.Nested))]
            public class Foo : LibNs.Base, IFoo { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source, extraReferences: new[] { libraryReference }).GetSingleSource();

        Assert.Contains("typeof(global::LibNs.Base.Nested)", generated);
    }
}
