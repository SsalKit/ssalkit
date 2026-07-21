using SsalKit.DependencyInjection.Generator.Tests.TestSupport;

namespace SsalKit.DependencyInjection.Generator.Tests;

public class ServiceAttributeAnalyzerTests
{
    private const string Usings = """
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    [Fact]
    public async Task SSAL001_AbstractClass_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public abstract class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL001", diagnostic.Id);
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public async Task SSAL001_StaticClass_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            [Service]
            public static class Foo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "SSAL001");
    }

    [Fact]
    public async Task SSAL001_ConcreteClass_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL001");
    }

    [Fact]
    public async Task SSAL002_AsTypeNotImplemented_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IOther { }

            [Service(As = typeof(IOther))]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL002", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL002_AsTypeImplemented_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(As = typeof(IFoo))]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL002");
    }

    [Fact]
    public async Task SSAL002_AsBaseClass_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public class Base { }

            [Service(As = typeof(Base))]
            public class Foo : Base { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL002");
    }

    [Fact]
    public async Task SSAL003_GenericClass_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            [Service]
            public class Foo<T> { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL003", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL003_NonGenericClass_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            [Service]
            public class Foo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL003");
    }

    [Fact]
    public async Task SSAL004_DuplicateAttributesOnSameClass_ReportsWarning()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(As = typeof(IFoo))]
            [Service(As = typeof(IFoo))]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL004", diagnostic.Id);
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public async Task SSAL004_SameServiceType_DifferentImplementationTypes_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(As = typeof(IFoo))]
            public class Foo : IFoo { }

            [Service(As = typeof(IFoo))]
            public class OtherFoo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        // Different implementation types => not a duplicate (ServiceType, ImplementationType, Key) triple.
        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL004");
    }

    [Fact]
    public async Task SSAL004_DifferentAsTypes_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(As = typeof(IFoo))]
            [Service(As = typeof(IBar))]
            public class Foo : IFoo, IBar { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL004");
    }

    [Fact]
    public async Task SSAL004_DifferentKeys_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(As = typeof(IFoo), Key = "a")]
            [Service(As = typeof(IFoo), Key = "b")]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL004");
    }

    [Fact]
    public async Task SSAL005_KeyedTryAddEnumerable_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Mode = RegistrationMode.TryAddEnumerable, Key = "k")]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL005", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL005_TryAddEnumerableWithoutKey_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Mode = RegistrationMode.TryAddEnumerable)]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL005");
    }

    [Fact]
    public async Task SSAL005_KeyedWithoutTryAddEnumerable_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Mode = RegistrationMode.TryAdd, Key = "k")]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL005");
    }

    [Fact]
    public async Task NoServiceAttribute_ReportsNoDiagnostics()
    {
        const string source = Usings + """
            namespace TestNs;

            public class Foo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task SSAL006_SelfRegistration_TryAddEnumerable_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            [Service(Mode = RegistrationMode.TryAddEnumerable)]
            public class Foo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL006", diagnostic.Id);
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public async Task SSAL006_ExplicitAsSelfType_TryAddEnumerable_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            [Service(Mode = RegistrationMode.TryAddEnumerable, As = typeof(Foo))]
            public class Foo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL006", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL006_MultipleInterfaces_TryAddEnumerable_DoesNotReport()
    {
        // 2+ directly-implemented interfaces + TryAddEnumerable is valid: each interface gets its
        // own direct descriptor instead of the self-registration + forwarding pattern, so there is
        // no self-as-service-type registration to reject.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAddEnumerable)]
            public class Foo : IFoo, IBar { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL006");
    }

    [Fact]
    public async Task SSAL006_SingleInterface_TryAddEnumerable_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Mode = RegistrationMode.TryAddEnumerable)]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL006");
    }

    [Fact]
    public async Task SSAL007_PrivateNestedClass_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            public class Outer
            {
                [Service]
                private class Foo : IFoo { }
            }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL007", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL007_ProtectedNestedClass_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            public class Outer
            {
                [Service]
                protected class Foo : IFoo { }
            }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "SSAL007");
    }

    [Fact]
    public async Task SSAL007_FileLocalClass_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service]
            file class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL007", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL007_InaccessibleImplicitInterface_ReportsErrorForServiceType()
    {
        const string source = Usings + """
            namespace TestNs;

            public class Outer
            {
                private interface IFoo { }

                [Service]
                public class Foo : IFoo { }
            }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL007", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL007_InternalNestedClass_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            public class Outer
            {
                [Service]
                internal class Foo : IFoo { }
            }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL007");
    }

    [Fact]
    public async Task SSAL007_PublicTopLevelClass_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL007");
    }

    [Fact]
    public async Task SSAL007_InaccessibleTypeofKey_ReportsError()
    {
        // Regression test: `Key = typeof(PrivateMarker)` is accessible at the [Service] attribute
        // application site (nested private types are visible within their own containing class),
        // but the generated `typeof(...)` reference lives in a separate top-level static class, so
        // it must be rejected exactly like an inaccessible implementation/service type.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            public class Outer
            {
                private class PrivateMarker { }

                [Service(Key = typeof(PrivateMarker))]
                public class Foo : IFoo { }
            }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL007", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL007_AccessibleTypeofKey_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IMarker { }

            [Service(Key = typeof(IMarker))]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL007");
    }

    [Fact]
    public async Task SSAL007_InaccessibleGenericTypeArgumentOnServiceType_ReportsError()
    {
        // Regression test: the recursive accessibility check must also cover the type arguments of
        // an *implemented interface* service type -- IHandler<T> itself is public, but a private
        // nested type argument still makes the closed-constructed IHandler<PrivateNested>
        // unreferenceable from the generated code.
        const string source = Usings + """
            namespace TestNs;

            public interface IHandler<T> { }

            public class Outer
            {
                private class PrivateNested { }

                [Service]
                public class Foo : IHandler<PrivateNested> { }
            }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL007", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL007_AccessibleGenericTypeArgumentOnServiceType_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IHandler<T> { }
            public class Nested { }

            [Service]
            public class Foo : IHandler<Nested> { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL007");
    }

    [Fact]
    public async Task SSAL008_UndefinedLifetime_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service((ServiceLifetime)42)]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL008", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL008_UndefinedMode_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Mode = (RegistrationMode)99)]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL008", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL008_NegativeLifetime_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service((ServiceLifetime)(-1))]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL008", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL008_AllDefinedLifetimeAndModeValues_DoNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(ServiceLifetime.Singleton, Mode = RegistrationMode.Add)]
            public class Foo1 : IFoo { }

            [Service(ServiceLifetime.Scoped, Mode = RegistrationMode.TryAdd)]
            public class Foo2 : IFoo { }

            [Service(ServiceLifetime.Transient, Mode = RegistrationMode.Replace)]
            public class Foo3 : IFoo { }
            """;

        var diagnostics = await GeneratorTestHelper.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL008");
    }
}
