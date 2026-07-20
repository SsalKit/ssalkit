using SsalKit.DependencyInjection.Generator.Tests.TestSupport;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Verifies that classes/attribute applications the analyzer would report as an error (SSAL001,
/// SSAL002, SSAL003, SSAL005) are silently excluded from generation, rather than producing
/// generated code that would itself fail to compile.
/// </summary>
public class GeneratorExclusionTests
{
    private const string Usings = """
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    [Fact]
    public void AbstractClass_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public abstract class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void StaticClass_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            [Service]
            public static class Foo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void GenericClass_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            [Service]
            public class Foo<T> { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void InvalidAsType_ExcludesOnlyThatAttribute_KeepsOtherValidOnes()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IOther { }

            [Service(As = typeof(IOther))]
            [Service(As = typeof(IFoo))]
            public class Foo : IFoo { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.DoesNotContain("IOther", generated);
        Assert.Contains("services.AddSingleton<global::TestNs.IFoo, global::TestNs.Foo>();", generated);
    }

    [Fact]
    public void KeyedTryAddEnumerable_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Mode = RegistrationMode.TryAddEnumerable, Key = "k")]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void KeyedTryAddEnumerable_ExcludesOnlyThatAttribute_KeepsOtherValidOnes()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Mode = RegistrationMode.TryAddEnumerable, Key = "k")]
            [Service(ServiceLifetime.Singleton)]
            public class Foo : IFoo { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.DoesNotContain("TryAddEnumerable", generated);
        Assert.Contains("services.AddSingleton<global::TestNs.IFoo, global::TestNs.Foo>();", generated);
    }
}
