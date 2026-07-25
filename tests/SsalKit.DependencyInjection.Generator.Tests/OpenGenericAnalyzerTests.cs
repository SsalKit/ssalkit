using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Tests.TestSupport;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Analyzer tests for open generic <c>[Service]</c> validation: SSAL009 (exact-match service
/// type), SSAL010 (no instance sharing), and how SSAL004/SSAL005/SSAL006 interact with open
/// generic classes.
/// </summary>
public class OpenGenericAnalyzerTests
{
    private const string Usings = """
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    [Fact]
    public async Task SSAL009_ClosedInterface_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service]
            public class Repo<T> : IRepo<string> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL009", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL009_NonGenericInterface_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public class Repo<T> : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL009", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL009_ReorderedTypeParameters_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IThing<A, B> { }

            [Service]
            public class Thing<K, V> : IThing<V, K> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL009", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL009_PartiallyAppliedTypeParameters_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IThing<A> { }

            [Service]
            public class Thing<K, V> : IThing<K> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL009", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL009_WrappedTypeParameter_ReportsError()
    {
        const string source = Usings + """
            using System.Collections.Generic;

            namespace TestNs;

            public interface IRepo<T> { }

            [Service]
            public class Repo<T> : IRepo<IEnumerable<T>> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL009", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL009_ArityMismatch_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IPair<A, B> { }

            [Service]
            public class Triple<K, V, W> : IPair<K, V> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL009", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL009_AsClosedTypeOnOpenGenericClass_ReportsError()
    {
        // Even though Repo<T> genuinely implements ISomething<string> directly (unrelated to T),
        // a closed As service type can never be valid for an open generic implementation type --
        // this must be SSAL009, not silently accepted.
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }
            public interface ISomething<T> { }

            [Service(As = typeof(ISomething<string>))]
            public class Repo<T> : IRepo<T>, ISomething<string> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL009", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL002_AsUnboundGenericType_NotImplementedAtAll_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }
            public interface IOther<T> { }

            [Service(As = typeof(IOther<>))]
            public class Repo<T> : IRepo<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL002", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL009_AsUnboundGenericType_ImplementedButNonConforming_ReportsError()
    {
        // Repo<T> implements IThing<T, string> -- an instantiation of IThing<,> -- but arity 2
        // does not match Repo<T>'s own arity 1, so it isn't an exact-match shape.
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }
            public interface IThing<A, B> { }

            [Service(As = typeof(IThing<,>))]
            public class Repo<T> : IRepo<T>, IThing<T, string> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL009", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL009_ExactMatchInterface_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service]
            public class Repo<T> : IRepo<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL009");
    }

    [Fact]
    public async Task SSAL009_AsUnboundGenericType_ExactMatch_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }
            public interface IOther<T> { }

            [Service(As = typeof(IRepo<>))]
            public class Repo<T> : IRepo<T>, IOther<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL009");
    }

    [Fact]
    public async Task SSAL009_SelfRegistration_NoInterfaces_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            [Service]
            public class Box<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL009");
    }

    [Fact]
    public async Task SSAL010_OpenGenericSingleton_MultipleServiceTypes_ReportsWarning()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IReader<T> { }
            public interface IWriter<T> { }

            [Service(ServiceLifetime.Singleton)]
            public class Store<T> : IReader<T>, IWriter<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        DiagnosticAssert.Single(diagnostics, "SSAL010", DiagnosticSeverity.Warning);
    }

    [Fact]
    public async Task SSAL010_OpenGenericScoped_MultipleServiceTypes_ReportsWarning()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IReader<T> { }
            public interface IWriter<T> { }

            [Service(ServiceLifetime.Scoped)]
            public class Store<T> : IReader<T>, IWriter<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "SSAL010");
    }

    [Fact]
    public async Task SSAL010_SingleServiceType_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service(ServiceLifetime.Singleton)]
            public class Repo<T> : IRepo<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL010");
    }

    [Fact]
    public async Task SSAL010_Transient_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IReader<T> { }
            public interface IWriter<T> { }

            [Service(ServiceLifetime.Transient)]
            public class Store<T> : IReader<T>, IWriter<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL010");
    }

    [Fact]
    public async Task SSAL010_TryAddEnumerable_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IReader<T> { }
            public interface IWriter<T> { }

            [Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAddEnumerable)]
            public class Store<T> : IReader<T>, IWriter<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL010");
    }

    [Fact]
    public async Task SSAL010_ClosedClass_DoesNotReport()
    {
        // A non-generic class's Singleton/Scoped multi-interface registration uses the
        // self+forwarding pattern instead, so instances *are* shared -- SSAL010 must not fire.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(ServiceLifetime.Singleton)]
            public class Foo : IFoo, IBar { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL010");
    }

    [Fact]
    public async Task SSAL005_KeyedTryAddEnumerable_OnOpenGenericClass_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service(Mode = RegistrationMode.TryAddEnumerable, Key = "k")]
            public class Repo<T> : IRepo<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL005", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL006_SelfRegistration_TryAddEnumerable_OnOpenGenericClass_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            [Service(Mode = RegistrationMode.TryAddEnumerable)]
            public class Box<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL006", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL004_InferredInterfaceAndExplicitUnboundAsOnSameClass_ReportsWarning()
    {
        // Regression test: `[Service]` infers IRepo<T> from the implemented interface, and
        // `[Service(As = typeof(IRepo<>))]` explicitly requests the same open generic service type
        // -- both attribute applications must be recognized as registering the exact same
        // (ServiceType, ImplementationType) pair via their shared typeof-form identity, even though
        // one arrived at it via the class's own type parameter and the other via an explicit
        // unbound `typeof(...)`.
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service]
            [Service(As = typeof(IRepo<>))]
            public class Repo<T> : IRepo<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL004", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL006_UnboundAsPointingAtSelf_OnOpenGenericClass_ReportsError()
    {
        // Regression test: `As = typeof(C<>)` resolves to the class itself (self registration),
        // but as an *unbound* symbol its display FQN ("global::TestNs.C<>") never string-matches
        // the implementation's display FQN ("global::TestNs.C<T>") -- SSAL006 must still fire via
        // a symbol-based check (ServiceTypeResolver.IsSelfServiceType), not an FQN comparison,
        // otherwise this TryAddEnumerable-as-self case is silently dropped with no diagnostic at
        // all, which is exactly what SSAL006 exists to prevent.
        const string source = Usings + """
            namespace TestNs;

            public interface ISomething<T> { }

            [Service(Mode = RegistrationMode.TryAddEnumerable, As = typeof(C<>))]
            public class C<T> : ISomething<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL006", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL009_AsUnboundGenericType_MultipleInstantiations_ConformingOneFirst_DoesNotReport()
    {
        // A class can implement 2+ distinct instantiations of the same generic interface
        // definition. When one of them is an exact-match shape, declaration order must not
        // matter: AllInterfaces happening to enumerate the non-conforming closed instantiation
        // before the conforming one must not cause a false SSAL009.
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service(As = typeof(IRepo<>))]
            public class C<T> : IRepo<string>, IRepo<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL009");
    }

    [Fact]
    public async Task SSAL009_AsUnboundGenericType_MultipleInstantiations_ConformingOneLast_DoesNotReport()
    {
        // Same as above with the conforming and non-conforming instantiations declared in the
        // opposite order, to confirm the fix isn't itself order-dependent.
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service(As = typeof(IRepo<>))]
            public class C<T> : IRepo<T>, IRepo<string> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL009");
    }

    [Fact]
    public async Task SSAL009_AsUnboundGenericType_NoConformingInstantiation_ReportsErrorNamingOne()
    {
        // When genuinely none of the implemented instantiations conform, SSAL009 must still fire
        // (naming whichever instantiation was found first).
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service(As = typeof(IRepo<>))]
            public class C<T> : IRepo<string>, IRepo<int> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL009", diagnostic.Id);
    }
}
