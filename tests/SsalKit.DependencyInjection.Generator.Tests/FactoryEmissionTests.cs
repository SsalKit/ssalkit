using SsalKit.DependencyInjection.Generator.Tests.TestSupport;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Emission tests for <c>[Service(Factory = "...")]</c>: the factory only changes how the
/// non-forwarded, self-constructing statement builds the implementation instance, across every
/// lifetime, mode, keyed/non-keyed combination, and forwarding shape.
/// </summary>
public class FactoryEmissionTests
{
    private const string Usings = """
        using System;
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    [Theory]
    [InlineData("ServiceLifetime.Singleton", "AddSingleton")]
    [InlineData("ServiceLifetime.Scoped", "AddScoped")]
    [InlineData("ServiceLifetime.Transient", "AddTransient")]
    public void ParameterlessFactory_NonKeyed_SingleInterface_UsesFactoryLambda(string lifetimeArg, string expectedMethod)
    {
        var source = Usings + $$"""
            namespace TestNs;

            public interface IFoo { }

            [Service({{lifetimeArg}}, Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                public static Foo Create() => new Foo();
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            $"services.{expectedMethod}<global::TestNs.IFoo, global::TestNs.Foo>(sp => global::TestNs.Foo.Create());",
            generated);
    }

    [Fact]
    public void ServiceProviderFactory_NonKeyed_SingleInterface_PassesSp()
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

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.AddSingleton<global::TestNs.IFoo, global::TestNs.Foo>(sp => global::TestNs.Foo.Create(sp));",
            generated);
    }

    [Fact]
    public void BothOverloadsExist_PrefersServiceProviderOverload()
    {
        // SSAL012's "usable candidates" rule: when both a parameterless and an
        // IServiceProvider-accepting usable overload named 'Create' exist, the
        // IServiceProvider-accepting one wins, deterministically.
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

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.AddSingleton<global::TestNs.IFoo, global::TestNs.Foo>(sp => global::TestNs.Foo.Create(sp));",
            generated);
    }

    [Theory]
    [InlineData("RegistrationMode.Add", "services.AddSingleton<global::TestNs.IFoo, global::TestNs.Foo>(sp => global::TestNs.Foo.Create());")]
    // Unlike Add/TryAddEnumerable/Replace, TryAdd's factory-accepting overload has only one
    // generic type parameter (Microsoft.Extensions.DependencyInjection has no
    // TryAddXxx<TService, TImplementation>(Func<IServiceProvider, TImplementation>) overload), so
    // only the service type is passed as a generic argument here.
    [InlineData("RegistrationMode.TryAdd", "services.TryAddSingleton<global::TestNs.IFoo>(sp => global::TestNs.Foo.Create());")]
    [InlineData("RegistrationMode.TryAddEnumerable", "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::TestNs.IFoo, global::TestNs.Foo>(sp => global::TestNs.Foo.Create()));")]
    [InlineData("RegistrationMode.Replace", "services.Replace(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::TestNs.IFoo, global::TestNs.Foo>(sp => global::TestNs.Foo.Create()));")]
    public void EveryMode_NonKeyed_UsesFactory(string modeArg, string expectedStatement)
    {
        var source = Usings + $$"""
            namespace TestNs;

            public interface IFoo { }

            [Service(Mode = {{modeArg}}, Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                public static Foo Create() => new Foo();
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(expectedStatement, generated);
    }

    [Theory]
    [InlineData("RegistrationMode.Add", "services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>(\"k\", (sp, key) => global::TestNs.Foo.Create());")]
    [InlineData("RegistrationMode.TryAdd", "services.TryAddKeyedSingleton<global::TestNs.IFoo>(\"k\", (sp, key) => global::TestNs.Foo.Create());")]
    [InlineData("RegistrationMode.Replace", "services.Replace(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.KeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>(\"k\", (sp, key) => global::TestNs.Foo.Create()));")]
    public void Keyed_ParameterlessFactory_IgnoresKeyParameterInLambda(string modeArg, string expectedStatement)
    {
        var source = Usings + $$"""
            namespace TestNs;

            public interface IFoo { }

            [Service(Mode = {{modeArg}}, Key = "k", Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                public static Foo Create() => new Foo();
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(expectedStatement, generated);
    }

    [Fact]
    public void Keyed_ServiceProviderFactory_PassesSpOnly_NotKey()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Key = "k", Factory = nameof(Foo.Create))]
            public class Foo : IFoo
            {
                public static Foo Create(IServiceProvider sp) => new Foo();
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>(\"k\", (sp, key) => global::TestNs.Foo.Create(sp));",
            generated);
    }

    [Fact]
    public void SelfRegistration_NoInterfaces_UsesFactory()
    {
        const string source = Usings + """
            namespace TestNs;

            [Service(Factory = nameof(Foo.Create))]
            public class Foo
            {
                public static Foo Create() => new Foo();
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.AddSingleton<global::TestNs.Foo, global::TestNs.Foo>(sp => global::TestNs.Foo.Create());",
            generated);
    }

    [Fact]
    public void As_CombinedWithFactory_UsesFactoryForTheAsServiceType()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(As = typeof(IFoo), Factory = nameof(Foo.Create))]
            public class Foo : IFoo, IBar
            {
                public static Foo Create() => new Foo();
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.AddSingleton<global::TestNs.IFoo, global::TestNs.Foo>(sp => global::TestNs.Foo.Create());",
            generated);
        Assert.DoesNotContain("IBar", generated);
    }

    [Fact]
    public void MultipleInterfaces_Singleton_Forwarding_OnlySelfStatementUsesFactory()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(ServiceLifetime.Singleton, Factory = nameof(Foo.Create))]
            public class Foo : IFoo, IBar
            {
                public static Foo Create() => new Foo();
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        // Self-registration statement invokes the factory.
        Assert.Contains(
            "services.AddSingleton<global::TestNs.Foo, global::TestNs.Foo>(sp => global::TestNs.Foo.Create());",
            generated);
        // Forwarded statements are unchanged: they resolve the shared instance, never the factory.
        Assert.Contains(
            "services.AddSingleton<global::TestNs.IBar>(sp => sp.GetRequiredService<global::TestNs.Foo>());",
            generated);
        Assert.Contains(
            "services.AddSingleton<global::TestNs.IFoo>(sp => sp.GetRequiredService<global::TestNs.Foo>());",
            generated);
    }

    [Fact]
    public void MultipleInterfaces_Keyed_Singleton_Forwarding_OnlySelfStatementUsesFactory()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(ServiceLifetime.Singleton, Key = "k", Factory = nameof(Foo.Create))]
            public class Foo : IFoo, IBar
            {
                public static Foo Create(IServiceProvider sp) => new Foo();
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.AddKeyedSingleton<global::TestNs.Foo, global::TestNs.Foo>(\"k\", (sp, key) => global::TestNs.Foo.Create(sp));",
            generated);
        Assert.Contains(
            "services.AddKeyedSingleton<global::TestNs.IBar>(\"k\", (sp, key) => sp.GetRequiredKeyedService<global::TestNs.Foo>(key));",
            generated);
        Assert.Contains(
            "services.AddKeyedSingleton<global::TestNs.IFoo>(\"k\", (sp, key) => sp.GetRequiredKeyedService<global::TestNs.Foo>(key));",
            generated);
    }

    [Fact]
    public void MultipleInterfaces_TryAddEnumerable_EachStatementUsesFactoryIndependently()
    {
        // TryAddEnumerable never forwards (see RegistrationEntryModel.RequiresForwarding); every
        // interface gets its own direct, factory-backed descriptor.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAddEnumerable, Factory = nameof(Foo.Create))]
            public class Foo : IFoo, IBar
            {
                public static Foo Create() => new Foo();
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::TestNs.IFoo, global::TestNs.Foo>(sp => global::TestNs.Foo.Create()));",
            generated);
        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::TestNs.IBar, global::TestNs.Foo>(sp => global::TestNs.Foo.Create()));",
            generated);
        Assert.DoesNotContain("GetRequiredService", generated);
    }

    [Fact]
    public void NoFactory_StillUsesParameterlessRegistration()
    {
        // Regression guard: a [Service] without Factory must keep emitting exactly the pre-Factory
        // shape (no lambda at all), not e.g. an empty factory lambda.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public class Foo : IFoo { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("services.AddSingleton<global::TestNs.IFoo, global::TestNs.Foo>();", generated);
    }

    [Fact]
    public void GeneratedCode_WithFactory_TypeChecksAgainstRealDependencyInjectionApi_ForEveryModeAndKeyedCombination()
    {
        // Mirrors GeneratorEmissionTests.GeneratedCode_TypeChecksAgainstRealDependencyInjectionApi_
        // ForEveryModeAndKeyedCombination, but with Factory set everywhere, confirming the
        // Func<IServiceProvider, TImplementation> overloads the emitter assumes to exist for every
        // mode/keyed/forwarding combination really do exist on the real API surface.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(ServiceLifetime.Transient, Factory = nameof(AddDirect.Create))]
            public class AddDirect : IFoo
            {
                public static AddDirect Create() => new AddDirect();
            }

            [Service(ServiceLifetime.Transient, Mode = RegistrationMode.TryAdd, Factory = nameof(TryAddDirect.Create))]
            public class TryAddDirect : IFoo
            {
                public static TryAddDirect Create(IServiceProvider sp) => new TryAddDirect();
            }

            [Service(ServiceLifetime.Transient, Mode = RegistrationMode.TryAddEnumerable, Factory = nameof(TryAddEnumerableDirect.Create))]
            public class TryAddEnumerableDirect : IFoo
            {
                public static TryAddEnumerableDirect Create() => new TryAddEnumerableDirect();
            }

            [Service(ServiceLifetime.Transient, Mode = RegistrationMode.Replace, Factory = nameof(ReplaceDirect.Create))]
            public class ReplaceDirect : IFoo
            {
                public static ReplaceDirect Create(IServiceProvider sp) => new ReplaceDirect();
            }

            [Service(ServiceLifetime.Singleton, Key = "k", Factory = nameof(AddKeyedDirect.Create))]
            public class AddKeyedDirect : IFoo
            {
                public static AddKeyedDirect Create() => new AddKeyedDirect();
            }

            [Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAdd, Key = "k", Factory = nameof(TryAddKeyedDirect.Create))]
            public class TryAddKeyedDirect : IFoo
            {
                public static TryAddKeyedDirect Create(IServiceProvider sp) => new TryAddKeyedDirect();
            }

            [Service(ServiceLifetime.Singleton, Mode = RegistrationMode.Replace, Key = "k", Factory = nameof(ReplaceKeyedDirect.Create))]
            public class ReplaceKeyedDirect : IFoo
            {
                public static ReplaceKeyedDirect Create() => new ReplaceKeyedDirect();
            }

            [Service(ServiceLifetime.Singleton, Factory = nameof(AddForwarded.Create))]
            public class AddForwarded : IFoo, IBar
            {
                public static AddForwarded Create(IServiceProvider sp) => new AddForwarded();
            }

            [Service(ServiceLifetime.Scoped, Mode = RegistrationMode.TryAdd, Factory = nameof(TryAddForwarded.Create))]
            public class TryAddForwarded : IFoo, IBar
            {
                public static TryAddForwarded Create() => new TryAddForwarded();
            }

            [Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAddEnumerable, Factory = nameof(TryAddEnumerableMultiInterface.Create))]
            public class TryAddEnumerableMultiInterface : IFoo, IBar
            {
                public static TryAddEnumerableMultiInterface Create() => new TryAddEnumerableMultiInterface();
            }

            [Service(ServiceLifetime.Scoped, Mode = RegistrationMode.Replace, Factory = nameof(ReplaceForwarded.Create))]
            public class ReplaceForwarded : IFoo, IBar
            {
                public static ReplaceForwarded Create(IServiceProvider sp) => new ReplaceForwarded();
            }

            [Service(ServiceLifetime.Singleton, Key = "k", Factory = nameof(AddKeyedForwarded.Create))]
            public class AddKeyedForwarded : IFoo, IBar
            {
                public static AddKeyedForwarded Create() => new AddKeyedForwarded();
            }

            [Service(ServiceLifetime.Scoped, Mode = RegistrationMode.TryAdd, Key = "k", Factory = nameof(TryAddKeyedForwarded.Create))]
            public class TryAddKeyedForwarded : IFoo, IBar
            {
                public static TryAddKeyedForwarded Create(IServiceProvider sp) => new TryAddKeyedForwarded();
            }

            [Service(ServiceLifetime.Singleton, Mode = RegistrationMode.Replace, Key = "k", Factory = nameof(ReplaceKeyedForwarded.Create))]
            public class ReplaceKeyedForwarded : IFoo, IBar
            {
                public static ReplaceKeyedForwarded Create() => new ReplaceKeyedForwarded();
            }

            [Service(Factory = nameof(SelfRegistered.Create))]
            public class SelfRegistered
            {
                public static SelfRegistered Create() => new SelfRegistered();
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        var errors = result.GetOutputCompilationErrors();
        Assert.True(errors.IsEmpty, "Generated code failed to compile:\n" + string.Join('\n', errors) + "\n\n" + result.GetSingleSource());
    }
}
