using SsalKit.DependencyInjection.Generator.Tests.TestSupport;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Verifies that every open generic shape the analyzer would report as an error (SSAL002, SSAL005,
/// SSAL006, SSAL009) is silently excluded from generation by <c>ServiceAttributeParser</c>, mirroring
/// <see cref="OpenGenericAnalyzerTests"/> exactly but asserting on generator output instead of
/// diagnostics.
/// </summary>
public class OpenGenericExclusionTests
{
    private const string Usings = """
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    [Fact]
    public void SSAL009_ClosedInterface_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service]
            public class Repo<T> : IRepo<string> { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void SSAL009_ReorderedTypeParameters_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IThing<A, B> { }

            [Service]
            public class Thing<K, V> : IThing<V, K> { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void SSAL009_WrappedTypeParameter_IsExcludedEntirely()
    {
        const string source = Usings + """
            using System.Collections.Generic;

            namespace TestNs;

            public interface IRepo<T> { }

            [Service]
            public class Repo<T> : IRepo<IEnumerable<T>> { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void SSAL009_ArityMismatch_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IPair<A, B> { }

            [Service]
            public class Triple<K, V, W> : IPair<K, V> { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void SSAL009_AsClosedTypeOnOpenGenericClass_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }
            public interface ISomething<T> { }

            [Service(As = typeof(ISomething<string>))]
            public class Repo<T> : IRepo<T>, ISomething<string> { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void SSAL002_AsUnboundGenericType_NotImplementedAtAll_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }
            public interface IOther<T> { }

            [Service(As = typeof(IOther<>))]
            public class Repo<T> : IRepo<T> { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void SSAL009_AsUnboundGenericType_ImplementedButNonConforming_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }
            public interface IThing<A, B> { }

            [Service(As = typeof(IThing<,>))]
            public class Repo<T> : IRepo<T>, IThing<T, string> { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void SSAL009_InvalidAttribute_ExcludesOnlyThatAttribute_KeepsOtherValidOnes()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service(As = typeof(IRepo<string>))]
            [Service]
            public class Repo<T> : IRepo<T> { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.AddSingleton(typeof(global::TestNs.IRepo<>), typeof(global::TestNs.Repo<>));",
            generated);
        // Only one registration statement should be present -- the invalid closed-As attribute
        // must not contribute anything.
        Assert.DoesNotContain("IRepo<string>", generated);
    }

    [Fact]
    public void SSAL005_KeyedTryAddEnumerable_OnOpenGenericClass_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service(Mode = RegistrationMode.TryAddEnumerable, Key = "k")]
            public class Repo<T> : IRepo<T> { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void SSAL006_SelfRegistration_TryAddEnumerable_OnOpenGenericClass_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            [Service(Mode = RegistrationMode.TryAddEnumerable)]
            public class Box<T> { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void SSAL006_UnboundAsPointingAtSelf_OnOpenGenericClass_IsExcludedEntirely()
    {
        // Companion to OpenGenericAnalyzerTests.SSAL006_UnboundAsPointingAtSelf_...: the parser
        // must drop this entry too (nothing at all should be emitted for it).
        const string source = Usings + """
            namespace TestNs;

            public interface ISomething<T> { }

            [Service(Mode = RegistrationMode.TryAddEnumerable, As = typeof(C<>))]
            public class C<T> : ISomething<T> { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }
}
