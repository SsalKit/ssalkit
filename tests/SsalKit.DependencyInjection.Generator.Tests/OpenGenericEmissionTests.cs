using SsalKit.DependencyInjection.Generator.Tests.TestSupport;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// End-to-end emission tests for open generic classes: MEDI Type-based registration calls
/// (<c>typeof(...)</c> arguments) rather than the closed <c>&lt;TService, TImpl&gt;</c>
/// generic-argument form used for a non-generic class.
/// </summary>
public class OpenGenericEmissionTests
{
    private const string Usings = """
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    [Fact]
    public void BasicOpenGeneric_Singleton_Add_UsesTypeBasedRegistration()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IRepository<T> { }

            [Service(ServiceLifetime.Singleton)]
            public class Repository<T> : IRepository<T> { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.AddSingleton(typeof(global::TestNs.IRepository<>), typeof(global::TestNs.Repository<>));",
            generated);
    }

    [Fact]
    public void Arity2OpenGeneric_RendersDoubleCommaPlaceholder()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IThing<A, B> { }

            [Service]
            public class Thing<K, V> : IThing<K, V> { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.AddSingleton(typeof(global::TestNs.IThing<,>), typeof(global::TestNs.Thing<,>));",
            generated);
    }

    [Theory]
    [InlineData("RegistrationMode.Add", "services.AddSingleton(typeof(global::TestNs.IRepo<>), typeof(global::TestNs.Repo<>));")]
    [InlineData("RegistrationMode.TryAdd", "services.TryAddSingleton(typeof(global::TestNs.IRepo<>), typeof(global::TestNs.Repo<>));")]
    [InlineData(
        "RegistrationMode.TryAddEnumerable",
        "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(typeof(global::TestNs.IRepo<>), typeof(global::TestNs.Repo<>)));")]
    [InlineData(
        "RegistrationMode.Replace",
        "services.Replace(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(typeof(global::TestNs.IRepo<>), typeof(global::TestNs.Repo<>)));")]
    public void AllFourModes_NonKeyed_RenderExpectedCall(string modeArg, string expected)
    {
        var source = Usings + $$"""
            namespace TestNs;

            public interface IRepo<T> { }

            [Service(Mode = {{modeArg}})]
            public class Repo<T> : IRepo<T> { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(expected, generated);
    }

    [Theory]
    [InlineData("RegistrationMode.Add", "services.AddKeyedSingleton(typeof(global::TestNs.IRepo<>), \"k\", typeof(global::TestNs.Repo<>));")]
    [InlineData("RegistrationMode.TryAdd", "services.TryAddKeyedSingleton(typeof(global::TestNs.IRepo<>), \"k\", typeof(global::TestNs.Repo<>));")]
    [InlineData(
        "RegistrationMode.Replace",
        "services.Replace(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.KeyedSingleton(typeof(global::TestNs.IRepo<>), \"k\", typeof(global::TestNs.Repo<>)));")]
    public void KeyedModes_RenderKeyBetweenServiceAndImplementationTypes(string modeArg, string expected)
    {
        // Unlike the closed generic-argument form (where the key is the sole call argument), the
        // Type-based overloads place the key between the two Type arguments:
        // AddKeyedSingleton(Type serviceType, object? key, Type implementationType).
        var source = Usings + $$"""
            namespace TestNs;

            public interface IRepo<T> { }

            [Service(Mode = {{modeArg}}, Key = "k")]
            public class Repo<T> : IRepo<T> { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(expected, generated);
    }

    [Fact]
    public void SelfRegistration_NoInterfaces_UsesSameTypeForBothArguments()
    {
        const string source = Usings + """
            namespace TestNs;

            [Service]
            public class Box<T> { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.AddSingleton(typeof(global::TestNs.Box<>), typeof(global::TestNs.Box<>));",
            generated);
    }

    [Fact]
    public void As_UnboundGenericType_RegistersOnlyThatServiceType()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }
            public interface IOther<T> { }

            [Service(As = typeof(IRepo<>))]
            public class Repo<T> : IRepo<T>, IOther<T> { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.AddSingleton(typeof(global::TestNs.IRepo<>), typeof(global::TestNs.Repo<>));",
            generated);
        Assert.DoesNotContain("IOther", generated);
    }

    [Fact]
    public void GenericRecordClass_ExcludesSynthesizedSelfIEquatable()
    {
        // Regression test: the compiler-synthesized IEquatable<TSelf> on a generic record class
        // R<T> is IEquatable<R<T>>, whose type argument is R<T>'s own self-construction (the same
        // definition symbol as the class itself, by symbol identity) -- ServiceTypeResolver's
        // existing self-IEquatable exclusion must still recognize and drop it for a generic
        // record, exactly as it already does for a non-generic one.
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service]
            public record class Repo<T> : IRepo<T>;
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.AddSingleton(typeof(global::TestNs.IRepo<>), typeof(global::TestNs.Repo<>));",
            generated);
        Assert.DoesNotContain("IEquatable", generated);
    }

    [Fact]
    public void MultiInterfaceOpenGeneric_RegistersEachIndependently_WithoutForwarding()
    {
        // Forwarding is impossible for open generics (no way to write a factory delegate returning
        // an open generic type), so each interface gets its own independent Type-pair registration.
        const string source = Usings + """
            namespace TestNs;

            public interface IReader<T> { }
            public interface IWriter<T> { }

            [Service(ServiceLifetime.Singleton)]
            public class Store<T> : IReader<T>, IWriter<T> { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.AddSingleton(typeof(global::TestNs.IReader<>), typeof(global::TestNs.Store<>));",
            generated);
        Assert.Contains(
            "services.AddSingleton(typeof(global::TestNs.IWriter<>), typeof(global::TestNs.Store<>));",
            generated);
        Assert.DoesNotContain("GetRequiredService", generated);
    }

    [Fact]
    public void MixedCompilation_ClosedAndOpenGenericClasses_BothEmitCorrectly()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IRepo<T> { }

            [Service]
            public class Foo : IFoo { }

            [Service]
            public class Repo<T> : IRepo<T> { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("services.AddSingleton<global::TestNs.IFoo, global::TestNs.Foo>();", generated);
        Assert.Contains(
            "services.AddSingleton(typeof(global::TestNs.IRepo<>), typeof(global::TestNs.Repo<>));",
            generated);
    }

    [Fact]
    public void GeneratedCode_OpenGeneric_TypeChecksAgainstRealDependencyInjectionApi()
    {
        // Mirrors GeneratorEmissionTests.GeneratedCode_TypeChecksAgainstRealDependencyInjectionApi_...
        // but for open generic classes: confirms every Type-based overload the emitter assumes to
        // exist (AddSingleton(Type,Type), AddKeyedSingleton(Type,object?,Type),
        // ServiceDescriptor.Singleton(Type,Type)/KeyedSingleton(Type,object?,Type), ...) really
        // does exist on the real Microsoft.Extensions.DependencyInjection.Abstractions API.
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }
            public interface IReader<T> { }
            public interface IWriter<T> { }

            [Service(ServiceLifetime.Transient)]
            public class AddDirect<T> : IRepo<T> { }

            [Service(ServiceLifetime.Transient, Mode = RegistrationMode.TryAdd)]
            public class TryAddDirect<T> : IRepo<T> { }

            [Service(ServiceLifetime.Transient, Mode = RegistrationMode.TryAddEnumerable)]
            public class TryAddEnumerableDirect<T> : IRepo<T> { }

            [Service(ServiceLifetime.Transient, Mode = RegistrationMode.Replace)]
            public class ReplaceDirect<T> : IRepo<T> { }

            [Service(ServiceLifetime.Singleton, Key = "k")]
            public class AddKeyedDirect<T> : IRepo<T> { }

            [Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAdd, Key = "k")]
            public class TryAddKeyedDirect<T> : IRepo<T> { }

            [Service(ServiceLifetime.Singleton, Mode = RegistrationMode.Replace, Key = "k")]
            public class ReplaceKeyedDirect<T> : IRepo<T> { }

            [Service(ServiceLifetime.Singleton)]
            public class MultiInterface<T> : IReader<T>, IWriter<T> { }

            [Service]
            public class SelfRegistered<T> { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        var errors = result.GetOutputCompilationErrors();
        Assert.True(errors.IsEmpty, "Generated code failed to compile:\n" + string.Join('\n', errors) + "\n\n" + result.GetSingleSource());
    }

    [Fact]
    public void As_UnboundGenericType_MultipleInstantiations_ConformingOneFirst_RegistersExactMatch()
    {
        // Regression test: C<T> implements two instantiations of IRepo<>: the non-conforming
        // closed IRepo<string> (declared first) and the conforming IRepo<T>. Declaration order
        // must not change which one gets registered -- the conforming instantiation must win.
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service(As = typeof(IRepo<>))]
            public class C<T> : IRepo<string>, IRepo<T> { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.AddSingleton(typeof(global::TestNs.IRepo<>), typeof(global::TestNs.C<>));",
            generated);
    }

    [Fact]
    public void As_UnboundGenericType_MultipleInstantiations_ConformingOneLast_RegistersExactMatch()
    {
        // Same as above with the conforming and non-conforming instantiations declared in the
        // opposite order.
        const string source = Usings + """
            namespace TestNs;

            public interface IRepo<T> { }

            [Service(As = typeof(IRepo<>))]
            public class C<T> : IRepo<T>, IRepo<string> { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.AddSingleton(typeof(global::TestNs.IRepo<>), typeof(global::TestNs.C<>));",
            generated);
    }
}
