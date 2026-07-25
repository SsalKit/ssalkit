using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Tests.TestSupport;
using SsalKit.Generators.Toolkit.Testing;

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = DiagnosticAssert.Single(diagnostics, "SSAL001", DiagnosticSeverity.Error, exclusive: true);
    }

    [Fact]
    public async Task SSAL001_StaticClass_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            [Service]
            public static class Foo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL002");
    }

    [Fact]
    public async Task SSAL003_ClassNestedInsideGenericType_OwnArityZero_ReportsError()
    {
        // A non-generic class (own arity 0) nested inside a generic type still carries the
        // container's type parameters and cannot be registered.
        const string source = Usings + """
            namespace TestNs;

            public class Outer<T>
            {
                [Service]
                public class Inner { }
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL003", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL003_ClassNestedInsideGenericType_OwnArityGreaterThanZero_ReportsError()
    {
        // An open generic class (own arity > 0) nested inside a generic type still carries the
        // container's type parameters in addition to its own and cannot be registered.
        const string source = Usings + """
            namespace TestNs;

            public class Outer<T>
            {
                [Service]
                public class Inner<U> { }
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL003");
    }

    [Fact]
    public async Task SSAL003_OpenGenericClass_NonGenericContainer_DoesNotReport()
    {
        // The new, supported shape: own arity > 0, but no containing type has type parameters of
        // its own.
        const string source = Usings + """
            namespace TestNs;

            [Service]
            public class Foo<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = DiagnosticAssert.Single(diagnostics, "SSAL004", DiagnosticSeverity.Warning, exclusive: true);
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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        // Different implementation types => not a duplicate (ServiceType, ImplementationType, Key)
        // triple. This shape is SSAL015's business instead (see SSAL015_* below).
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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL004");
    }

    [Fact]
    public async Task SSAL004_NamedAndUnnamedTupleTypeofKeys_ReportsWarning()
    {
        // Regression test: `(int A, string B)` and `(int, string)` produce the exact same runtime
        // System.Type (tuple element names are erased entirely); the source-level spelling used by
        // KeyLiteralFormatter for the *generated code* must not leak into duplicate-key detection.
        // Duplicate-key detection is keyed on (ServiceType, ImplementationType, Key), so both
        // attribute applications must be on the same class to exercise it.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(As = typeof(IFoo), Key = typeof((int A, string B)))]
            [Service(As = typeof(IFoo), Key = typeof((int, string)))]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL004", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL004_NintAndIntPtrTypeofKeys_ReportsWarning()
    {
        // Regression test: `nint` is a compile-time-only spelling of `System.IntPtr` -- the same
        // runtime System.Type -- so `typeof(nint)` and `typeof(IntPtr)` keys must collide too.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(As = typeof(IFoo), Key = typeof(nint))]
            [Service(As = typeof(IFoo), Key = typeof(System.IntPtr))]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL004", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL004_NuintAndUIntPtrTypeofKeys_ReportsWarning()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(As = typeof(IFoo), Key = typeof(nuint))]
            [Service(As = typeof(IFoo), Key = typeof(System.UIntPtr))]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL004", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL004_TupleKeyNestedInGenericTypeArgument_ReportsWarning()
    {
        // The tuple/nint normalization must recurse into generic type arguments, not just apply at
        // the top level of the Key type.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(As = typeof(IFoo), Key = typeof(System.Collections.Generic.List<(int A, string B)>))]
            [Service(As = typeof(IFoo), Key = typeof(System.Collections.Generic.List<(int, string)>))]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL004", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL004_DifferentTypeofKeys_DoesNotReport()
    {
        // Guards against over-normalization: genuinely different key types (including a tuple
        // whose element types differ, and an unrelated integral type) must never collide.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(As = typeof(IFoo), Key = typeof((int, string)))]
            [Service(As = typeof(IFoo), Key = typeof((int, long)))]
            [Service(As = typeof(IFoo), Key = typeof(uint))]
            [Service(As = typeof(IFoo), Key = typeof(int))]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL005");
    }

    [Fact]
    public async Task NoServiceAttribute_ReportsNoDiagnostics()
    {
        const string source = Usings + """
            namespace TestNs;

            public class Foo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        DiagnosticAssert.Single(diagnostics, "SSAL006", DiagnosticSeverity.Error, exclusive: true);
    }

    [Fact]
    public async Task SSAL006_ExplicitAsSelfType_TryAddEnumerable_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            [Service(Mode = RegistrationMode.TryAddEnumerable, As = typeof(Foo))]
            public class Foo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL007");
    }

    [Fact]
    public async Task SSAL007_InaccessiblePointerTypeofKey_ReportsError()
    {
        // Regression test: TypeAccessibilityChecker's `_ => true` fallback used to let a pointer
        // type through unconditionally, so `typeof(PrivateMarker*)` was wrongly accepted even
        // though the pointed-at type is not accessible from the generated code (CS0122).
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            public class Outer
            {
                private class PrivateMarker { }

                [Service(Key = typeof(PrivateMarker*))]
                public unsafe class Foo : IFoo { }
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source, GeneratorTestSupport.Unsafe);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL007", diagnostic.Id);
    }

    [Fact]
    public async Task SSAL007_AccessiblePointerTypeofKey_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IMarker { }

            [Service(Key = typeof(IMarker*))]
            public unsafe class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source, GeneratorTestSupport.Unsafe);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL008");
    }

    [Fact]
    public async Task SSAL015_TwoImplementations_ReportsWarningOnEveryAttribute()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(As = typeof(IFoo))]
            public class Foo : IFoo { }

            [Service(As = typeof(IFoo))]
            public class OtherFoo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        // Both registrations are equally responsible for the ambiguity, so both are reported --
        // unlike SSAL004, which spares the first occurrence.
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d =>
        {
            Assert.Equal("SSAL015", d.Id);
            Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, d.Severity);
            Assert.Equal(
                "The service type 'global::TestNs.IFoo' is registered with 2 different implementation "
                + "types (global::TestNs.Foo, global::TestNs.OtherFoo); a single-instance resolution "
                + "returns whichever of them is registered last, and the generator emits registrations "
                + "ordered by implementation type name rather than by source order",
                d.GetMessage());
        });

        // One diagnostic per [Service] attribute application, i.e. at two distinct locations.
        Assert.Equal(2, diagnostics.Select(d => d.Location.SourceSpan).Distinct().Count());
    }

    [Fact]
    public async Task SSAL015_InferredInterfaceServiceType_ReportsWarning()
    {
        // No explicit `As`: the conflict must also be detected when the service type is inferred
        // from the directly-implemented interface.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public class Foo : IFoo { }

            [Service]
            public class OtherFoo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("SSAL015", d.Id));
    }

    [Fact]
    public async Task SSAL015_AddAndTryAddMixed_ReportsWarning()
    {
        // TryAdd is still a single-instance registration: it only backs off if *something* already
        // registered the service type, which does not make the resulting winner any less
        // order-dependent.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Mode = RegistrationMode.Add)]
            public class Foo : IFoo { }

            [Service(Mode = RegistrationMode.TryAdd)]
            public class OtherFoo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("SSAL015", d.Id));
    }

    [Fact]
    public async Task SSAL015_TryAddEnumerableMixedWithAdd_ReportsWarning()
    {
        // A group is only exempt when *every* registration in it is TryAddEnumerable; a single
        // non-TryAddEnumerable registration reintroduces the "which one wins" ambiguity, so the
        // whole group -- including the TryAddEnumerable one -- is reported.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Mode = RegistrationMode.TryAddEnumerable)]
            public class Foo : IFoo { }

            [Service(Mode = RegistrationMode.Add)]
            public class OtherFoo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("SSAL015", d.Id));
    }

    [Fact]
    public async Task SSAL015_SameKey_DifferentImplementations_ReportsWarning()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(As = typeof(IFoo), Key = "k")]
            public class Foo : IFoo { }

            [Service(As = typeof(IFoo), Key = "k")]
            public class OtherFoo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d =>
        {
            Assert.Equal("SSAL015", d.Id);
            Assert.Contains("""'global::TestNs.IFoo' with key "k" is registered""", d.GetMessage(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task SSAL015_ThreeImplementations_ListsAllThreeOnEachAttribute()
    {
        // The listed implementation types are ordinal-sorted, which is exactly the order
        // ServiceRegistrationEmitter emits them in -- so the last one named is the one that wins.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(As = typeof(IFoo))]
            public class Foo : IFoo { }

            [Service(As = typeof(IFoo))]
            public class OtherFoo : IFoo { }

            [Service(As = typeof(IFoo))]
            public class ThirdFoo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d =>
        {
            Assert.Equal("SSAL015", d.Id);
            Assert.Contains(
                "registered with 3 different implementation types (global::TestNs.Foo, "
                + "global::TestNs.OtherFoo, global::TestNs.ThirdFoo)",
                d.GetMessage(),
                StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task SSAL015_DuplicateAndConflictingRegistrations_ReportBothDiagnostics()
    {
        // The two diagnostics are independent: SSAL004 fires once for the repeated (IFoo, Foo,
        // <none>) triple, and SSAL015 fires for all three registrations bound to IFoo.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(As = typeof(IFoo))]
            [Service(As = typeof(IFoo))]
            public class Foo : IFoo { }

            [Service(As = typeof(IFoo))]
            public class OtherFoo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Single(diagnostics, d => d.Id == "SSAL004");
        Assert.Equal(3, diagnostics.Count(d => d.Id == "SSAL015"));
    }

    [Fact]
    public async Task SSAL015_AllTryAddEnumerable_DoesNotReport()
    {
        // The intended way to bind several implementations to one service type: nothing shadows
        // anything, they are all consumed together as IEnumerable<IFoo>.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Mode = RegistrationMode.TryAddEnumerable)]
            public class Foo : IFoo { }

            [Service(Mode = RegistrationMode.TryAddEnumerable)]
            public class OtherFoo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL015");
    }

    [Fact]
    public async Task SSAL015_DifferentKeys_DoesNotReport()
    {
        // Keyed registrations under distinct keys never shadow each other.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(As = typeof(IFoo), Key = "a")]
            public class Foo : IFoo { }

            [Service(As = typeof(IFoo), Key = "b")]
            public class OtherFoo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL015");
    }

    [Fact]
    public async Task SSAL015_KeyedAndUnkeyed_DoesNotReport()
    {
        // A keyed registration lives in a separate resolution space from the unkeyed one, so the
        // "<none>" key identity must not group together with a real key.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(As = typeof(IFoo), Key = "a")]
            public class Foo : IFoo { }

            [Service(As = typeof(IFoo))]
            public class OtherFoo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL015");
    }

    [Fact]
    public async Task SSAL015_SameImplementationRegisteredTwice_DoesNotReport()
    {
        // One implementation type registered repeatedly is SSAL004's business; SSAL015 requires
        // two or more *different* implementation types.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(As = typeof(IFoo))]
            [Service(As = typeof(IFoo))]
            public class Foo : IFoo { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "SSAL004");
        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL015");
    }

    [Fact]
    public async Task SSAL015_DifferentServiceTypes_DoesNotReport()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(As = typeof(IFoo))]
            public class Foo : IFoo, IBar { }

            [Service(As = typeof(IBar))]
            public class OtherFoo : IFoo, IBar { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL015");
    }

    [Fact]
    public async Task SSAL015_OpenGenericAndClosedGenericServiceType_DoesNotReport()
    {
        // IRepo<> (the open generic registration's service type identity) and IRepo<int> are
        // distinct service types to Microsoft.Extensions.DependencyInjection -- a closed request
        // for IRepo<int> matches the closed registration outright -- so they must not be grouped.
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service]
            public class OpenRepo<T> : IRepo<T> { }

            [Service]
            public class ClosedRepo : IRepo<int> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "SSAL015");
    }

    [Fact]
    public async Task SSAL015_TwoOpenGenericImplementations_ReportsWarning()
    {
        // Two open generic classes bound to the same open generic service type conflict exactly
        // like their non-generic counterparts; the typeof-form identity ("IRepo<>") groups them.
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service]
            public class RepoA<T> : IRepo<T> { }

            [Service]
            public class RepoB<T> : IRepo<T> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d =>
        {
            Assert.Equal("SSAL015", d.Id);
            Assert.Contains(
                "The service type 'global::TestNs.IRepo<>' is registered with 2 different "
                + "implementation types (global::TestNs.RepoA<>, global::TestNs.RepoB<>)",
                d.GetMessage(),
                StringComparison.Ordinal);
        });
    }
}
