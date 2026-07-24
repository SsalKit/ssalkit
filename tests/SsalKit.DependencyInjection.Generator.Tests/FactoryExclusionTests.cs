using SsalKit.DependencyInjection.Generator.Tests.TestSupport;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Verifies that every invalid <c>Factory</c> shape the analyzer would report as an error
/// (SSAL011, SSAL012, SSAL013, SSAL014) causes <c>ServiceAttributeParser</c> to silently drop that
/// attribute application, mirroring <see cref="Analysis.ServiceAttributeAnalyzer"/>'s validation
/// exactly but asserting on generator output instead of diagnostics.
/// </summary>
public class FactoryExclusionTests
{
    private const string Usings = """
        using System;
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    [Fact]
    public void FactoryNameNotFound_IsExcludedEntirely()
    {
        // "DoesNotExist" matches no member at all on Foo (a string literal, not nameof, since
        // nameof requires an existing member).
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = "DoesNotExist")]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void FactoryNameMatchesNonMethodMember_IsExcludedEntirely()
    {
        // A member named "Create" exists, but it isn't a method at all (MethodKind.Ordinary) --
        // this must be treated the same as no matching method (SSAL011), not silently ignored.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                public static int Create = 42;
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void EmptyStringFactory_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = "")]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void InstanceFactoryMethod_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                public Foo Create() => new Foo();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void GenericFactoryMethod_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                public static Foo Create<T>() => new Foo();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void WrongReturnType_BaseClass_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            public class Base { }

            [Service(Factory = nameof(Foo.Create))]
            public class Foo : Base, IFoo
            {
                public static Base Create() => new Foo();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void WrongReturnType_Interface_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                public static IFoo Create() => new Foo();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void WrongParameterType_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                public static Foo Create(string notAServiceProvider) => new Foo();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void TwoParameters_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                public static Foo Create(IServiceProvider sp, string extra) => new Foo();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void RefServiceProviderParameter_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                public static Foo Create(ref IServiceProvider sp) => new Foo();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void PrivateFactoryMethod_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                private static Foo Create() => new Foo();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void ProtectedFactoryMethod_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                protected static Foo Create() => new Foo();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void InternalFactoryMethod_IsGenerated()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                internal static Foo Create() => new Foo();
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("global::TestNs.Foo.Create()", generated);
    }

    [Fact]
    public void OpenGenericClass_WithFactory_IsExcludedEntirely()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service(Factory = "Create")]
            public class Repo<T> : IRepo<T>
            {
                public static Repo<T> Create() => new Repo<T>();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void InheritedFactoryMethod_NotDeclaredDirectly_IsExcludedEntirely()
    {
        // A factory method declared only on a base class must not be found -- inherited methods
        // can live in a different syntax tree and would break incremental caching.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            public class Base
            {
                public static Foo Create() => new Foo();
            }

            [Service(Factory = nameof(Foo.Create))]
            public class Foo : Base, IFoo
            {
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void InvalidFactory_ExcludesOnlyThatAttribute_KeepsOtherValidOnes()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = "DoesNotExist")]
            [Service(ServiceLifetime.Singleton)]
            public class Foo : IFoo { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.DoesNotContain("DoesNotExist", generated);
        Assert.Contains("services.AddSingleton<global::TestNs.IFoo, global::TestNs.Foo>();", generated);
    }
}
