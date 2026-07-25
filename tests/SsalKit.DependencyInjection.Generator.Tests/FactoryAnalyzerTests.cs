using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Tests.TestSupport;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Analyzer tests for <c>[Service(Factory = "...")]</c> validation: SSAL011 (method not found),
/// SSAL012 (unusable signature), SSAL013 (open generic not supported), and SSAL014 (inaccessible),
/// plus that a valid factory reports nothing.
/// </summary>
public class FactoryAnalyzerTests
{
    private const string Usings = """
        using System;
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    [Fact]
    public async Task SSAL011_NoMatchingMethod_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = "DoesNotExist")]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        DiagnosticAssert.Single(diagnostics, "SSAL011", DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task SSAL011_EmptyStringFactory_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = "")]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL011", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL011_InheritedFactoryMethod_ReportsError()
    {
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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL011", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL011_NonMethodMemberWithSameName_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                public static int Create = 42;
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL011", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL012_InstanceMethod_ReportsError()
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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL012", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL012_GenericMethod_ReportsError()
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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL012", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL012_WrongReturnType_ReportsError()
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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL012", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL012_WrongParameterType_ReportsError()
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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL012", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL012_TwoParameters_ReportsError()
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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL012", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL013_OpenGenericClass_ReportsError()
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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL013", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL013_OpenGenericClass_TakesPrecedenceOverFactoryNotFound()
    {
        // Interaction ordering: an open generic class always reports SSAL013, even when the named
        // Factory also would not resolve to anything -- open-generic-ness is checked first.
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service(Factory = "DoesNotExist")]
            public class Repo<T> : IRepo<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL013", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL014_PrivateFactoryMethod_ReportsError()
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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL014", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL014_ProtectedFactoryMethod_ReportsError()
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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL014", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL014_InternalFactoryMethod_DoesNotReport()
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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL014");
    }

    [Fact]
    public async Task ValidParameterlessFactory_ReportsNothing()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                public static Foo Create() => new Foo();
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ValidServiceProviderFactory_ReportsNothing()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                public static Foo Create(IServiceProvider sp) => new Foo();
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ValidFactory_BothOverloadsExist_ReportsNothing()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                public static Foo Create() => new Foo();

                public static Foo Create(IServiceProvider sp) => new Foo();
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NoFactory_ReportsNothingFactoryRelated()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Empty(diagnostics);
    }
}
