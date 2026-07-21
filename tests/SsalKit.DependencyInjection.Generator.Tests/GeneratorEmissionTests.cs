using SsalKit.DependencyInjection.Generator.Tests.TestSupport;

namespace SsalKit.DependencyInjection.Generator.Tests;

public class GeneratorEmissionTests
{
    private const string Usings = """
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    [Fact]
    public void NoServiceAttributes_ProducesNoOutput()
    {
        const string source = Usings + """
            namespace TestNs;

            public class Foo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
    }

    [Theory]
    [InlineData("ServiceLifetime.Singleton", "AddSingleton")]
    [InlineData("ServiceLifetime.Scoped", "AddScoped")]
    [InlineData("ServiceLifetime.Transient", "AddTransient")]
    public void SingleInterface_NoAs_RegistersDirectlyWithGivenLifetime(string lifetimeArg, string expectedMethod)
    {
        var source = Usings + $$"""
            namespace TestNs;

            public interface IFoo { }

            [Service({{lifetimeArg}})]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        var generated = result.GetSingleSource();
        Assert.Contains($"services.{expectedMethod}<global::TestNs.IFoo, global::TestNs.Foo>();", generated);
    }

    [Fact]
    public void DefaultLifetime_IsSingleton()
    {
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
    public void As_Specified_RegistersOnlyThatServiceType()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(ServiceLifetime.Transient, As = typeof(IFoo))]
            public class Foo : IFoo, IBar { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("services.AddTransient<global::TestNs.IFoo, global::TestNs.Foo>();", generated);
        Assert.DoesNotContain("IBar", generated);
    }

    [Fact]
    public void As_NotSpecified_NoInterfaces_RegistersConcreteSelf()
    {
        const string source = Usings + """
            namespace TestNs;

            [Service(ServiceLifetime.Scoped)]
            public class Foo { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("services.AddScoped<global::TestNs.Foo, global::TestNs.Foo>();", generated);
    }

    [Fact]
    public void As_NotSpecified_MultipleInterfaces_Transient_RegistersEachDirectly()
    {
        // Transient never needs instance-sharing forwarding, even with 2+ service types.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(ServiceLifetime.Transient)]
            public class Foo : IFoo, IBar { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("services.AddTransient<global::TestNs.IBar, global::TestNs.Foo>();", generated);
        Assert.Contains("services.AddTransient<global::TestNs.IFoo, global::TestNs.Foo>();", generated);
        Assert.DoesNotContain("GetRequiredService", generated);
    }

    [Fact]
    public void As_NotSpecified_MultipleInterfaces_Singleton_ForwardsSharedInstance()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(ServiceLifetime.Singleton)]
            public class Foo : IFoo, IBar { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("services.AddSingleton<global::TestNs.Foo, global::TestNs.Foo>();", generated);
        Assert.Contains(
            "services.AddSingleton<global::TestNs.IBar>(sp => sp.GetRequiredService<global::TestNs.Foo>());",
            generated);
        Assert.Contains(
            "services.AddSingleton<global::TestNs.IFoo>(sp => sp.GetRequiredService<global::TestNs.Foo>());",
            generated);
    }

    [Fact]
    public void As_NotSpecified_MultipleInterfaces_Scoped_ForwardsSharedInstance()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(ServiceLifetime.Scoped)]
            public class Foo : IFoo, IBar { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("services.AddScoped<global::TestNs.Foo, global::TestNs.Foo>();", generated);
        Assert.Contains(
            "services.AddScoped<global::TestNs.IBar>(sp => sp.GetRequiredService<global::TestNs.Foo>());",
            generated);
    }

    [Fact]
    public void DirectlyImplementedInterfaces_ExcludeDisposableAndBaseTypeInterfaces()
    {
        const string source = Usings + """
            using System;

            namespace TestNs;

            public interface IFoo { }

            public class Base : IDisposable
            {
                public void Dispose() { }
            }

            [Service]
            public class Foo : Base, IFoo, IAsyncDisposable
            {
                public System.Threading.Tasks.ValueTask DisposeAsync() => default;
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        // Only IFoo (directly implemented, not IDisposable/IAsyncDisposable, not base-inherited).
        Assert.Contains("services.AddSingleton<global::TestNs.IFoo, global::TestNs.Foo>();", generated);
        Assert.DoesNotContain("IDisposable", generated);
        Assert.DoesNotContain("IAsyncDisposable", generated);
    }

    [Fact]
    public void Mode_TryAdd_NonKeyed_UsesTryAddExtension()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Mode = RegistrationMode.TryAdd)]
            public class Foo : IFoo { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("services.TryAddSingleton<global::TestNs.IFoo, global::TestNs.Foo>();", generated);
    }

    [Fact]
    public void Mode_TryAddEnumerable_NonKeyed_UsesServiceDescriptor()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Mode = RegistrationMode.TryAddEnumerable)]
            public class Foo : IFoo { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::TestNs.IFoo, global::TestNs.Foo>());",
            generated);
    }

    [Fact]
    public void Mode_TryAddEnumerable_MultipleInterfaces_RegistersEachDirectly_WithoutForwarding()
    {
        // Unlike Add/TryAdd/Replace, TryAddEnumerable never uses the self-registration +
        // forwarding-factory pattern, even with 2+ directly-implemented interfaces: a forwarding
        // factory descriptor has no fixed implementation type for TryAddEnumerable to compare
        // against, so each interface instead gets its own independent
        // ServiceDescriptor.Singleton<TService, TImpl>() (see RegistrationEntryModel.RequiresForwarding).
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAddEnumerable)]
            public class Foo : IFoo, IBar { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::TestNs.IBar, global::TestNs.Foo>());",
            generated);
        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::TestNs.IFoo, global::TestNs.Foo>());",
            generated);
        Assert.DoesNotContain("GetRequiredService", generated);
    }

    [Fact]
    public void Mode_Replace_NonKeyed_UsesServiceDescriptorReplace()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Mode = RegistrationMode.Replace)]
            public class Foo : IFoo { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.Replace(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::TestNs.IFoo, global::TestNs.Foo>());",
            generated);
    }

    [Fact]
    public void Mode_Add_IsDefault()
    {
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
    public void Keyed_NonForwarding_UsesAddKeyedWithLiteralKey()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(ServiceLifetime.Singleton, Key = "my-key")]
            public class Foo : IFoo { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>(\"my-key\");", generated);
    }

    [Fact]
    public void Keyed_TryAdd_UsesTryAddKeyedExtension()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAdd, Key = 42)]
            public class Foo : IFoo { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("services.TryAddKeyedSingleton<global::TestNs.IFoo, global::TestNs.Foo>(42);", generated);
    }

    [Fact]
    public void Keyed_Replace_UsesKeyedServiceDescriptor()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(ServiceLifetime.Scoped, Mode = RegistrationMode.Replace, Key = "k")]
            public class Foo : IFoo { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.Replace(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.KeyedScoped<global::TestNs.IFoo, global::TestNs.Foo>(\"k\"));",
            generated);
    }

    [Fact]
    public void Keyed_MultipleInterfaces_Singleton_ForwardsSharedKeyedInstance()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(ServiceLifetime.Singleton, Key = "my-key")]
            public class Foo : IFoo, IBar { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("services.AddKeyedSingleton<global::TestNs.Foo, global::TestNs.Foo>(\"my-key\");", generated);
        Assert.Contains(
            "services.AddKeyedSingleton<global::TestNs.IBar>(\"my-key\", (sp, key) => sp.GetRequiredKeyedService<global::TestNs.Foo>(key));",
            generated);
        Assert.Contains(
            "services.AddKeyedSingleton<global::TestNs.IFoo>(\"my-key\", (sp, key) => sp.GetRequiredKeyedService<global::TestNs.Foo>(key));",
            generated);
    }

    [Fact]
    public void EnumKey_FormatsAsFullyQualifiedEnumMember()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Color { Red, Blue }

            public interface IFoo { }

            [Service(Key = Color.Blue)]
            public class Foo : IFoo { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("global::TestNs.Color.Blue", generated);
    }

    [Fact]
    public void AllowMultiple_BothAttributesAreEmitted()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(ServiceLifetime.Singleton)]
            [Service(ServiceLifetime.Transient, As = typeof(IFoo), Mode = RegistrationMode.TryAdd)]
            public class Foo : IFoo { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        // First attribute: no As, one interface -> direct singleton registration as IFoo.
        Assert.Contains("services.AddSingleton<global::TestNs.IFoo, global::TestNs.Foo>();", generated);
        // Second attribute: explicit As + TryAdd + Transient.
        Assert.Contains("services.TryAddTransient<global::TestNs.IFoo, global::TestNs.Foo>();", generated);
    }

    [Theory]
    [InlineData("SsalKit.Sample", "SsalKitSample")]
    [InlineData("My-Cool.Lib", "MyCoolLib")]
    [InlineData("simple", "Simple")]
    [InlineData("123.my-app", "_123MyApp")]
    public void AssemblyName_IsSanitizedIntoPascalCaseIdentifier(string assemblyName, string expectedPrefix)
    {
        const string source = """
            using SsalKit.DependencyInjection;

            namespace TestNs;

            public interface IFoo { }

            [Service]
            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source, assemblyName);
        var generated = result.GetSingleSource();

        Assert.Contains($"public static class {expectedPrefix}ServiceCollectionExtensions", generated);
        Assert.Contains($"Add{expectedPrefix}Services", generated);
    }

    [Fact]
    public void GeneratedFile_HasAutoGeneratedHeaderAndNullableEnable()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public class Foo : IFoo { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.StartsWith("// <auto-generated/>", generated);
        Assert.Contains("#nullable enable", generated);
        Assert.Contains("namespace Microsoft.Extensions.DependencyInjection", generated);
        Assert.Contains("return services;", generated);
    }

    [Fact]
    public void GeneratedCode_TypeChecksAgainstRealDependencyInjectionApi_ForEveryModeAndKeyedCombination()
    {
        // Exercises every {mode} x {keyed, non-keyed} x {forwarding, direct} combination the
        // emitter can produce and confirms the resulting extension method actually compiles
        // against the real Microsoft.Extensions.DependencyInjection.Abstractions API -- i.e. every
        // ServiceCollectionServiceExtensions/ServiceCollectionDescriptorExtensions/ServiceDescriptor
        // overload the emitter assumes to exist really does exist.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(ServiceLifetime.Transient)]
            public class AddDirect : IFoo { }

            [Service(ServiceLifetime.Transient, Mode = RegistrationMode.TryAdd)]
            public class TryAddDirect : IFoo { }

            [Service(ServiceLifetime.Transient, Mode = RegistrationMode.TryAddEnumerable)]
            public class TryAddEnumerableDirect : IFoo { }

            [Service(ServiceLifetime.Transient, Mode = RegistrationMode.Replace)]
            public class ReplaceDirect : IFoo { }

            [Service(ServiceLifetime.Singleton, Key = "k")]
            public class AddKeyedDirect : IFoo { }

            [Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAdd, Key = "k")]
            public class TryAddKeyedDirect : IFoo { }

            [Service(ServiceLifetime.Singleton, Mode = RegistrationMode.Replace, Key = "k")]
            public class ReplaceKeyedDirect : IFoo { }

            [Service(ServiceLifetime.Singleton)]
            public class AddForwarded : IFoo, IBar { }

            [Service(ServiceLifetime.Scoped, Mode = RegistrationMode.TryAdd)]
            public class TryAddForwarded : IFoo, IBar { }

            // Not actually forwarded: TryAddEnumerable always registers each service type with
            // its own direct <TService, TImpl> descriptor, even with 2+ interfaces (see
            // RegistrationEntryModel.RequiresForwarding).
            [Service(ServiceLifetime.Singleton, Mode = RegistrationMode.TryAddEnumerable)]
            public class TryAddEnumerableMultiInterface : IFoo, IBar { }

            [Service(ServiceLifetime.Scoped, Mode = RegistrationMode.Replace)]
            public class ReplaceForwarded : IFoo, IBar { }

            [Service(ServiceLifetime.Singleton, Key = "k")]
            public class AddKeyedForwarded : IFoo, IBar { }

            [Service(ServiceLifetime.Scoped, Mode = RegistrationMode.TryAdd, Key = "k")]
            public class TryAddKeyedForwarded : IFoo, IBar { }

            [Service(ServiceLifetime.Singleton, Mode = RegistrationMode.Replace, Key = "k")]
            public class ReplaceKeyedForwarded : IFoo, IBar { }

            [Service]
            public class SelfRegistered { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);

        var errors = result.GetOutputCompilationErrors();
        Assert.True(errors.IsEmpty, "Generated code failed to compile:\n" + string.Join('\n', errors) + "\n\n" + result.GetSingleSource());
    }

    [Fact]
    public void NestedClass_IsSupported()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            public class Outer
            {
                [Service]
                internal class Inner : IFoo { }
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("global::TestNs.Outer.Inner", generated);
    }

    [Fact]
    public void RecordClass_IsDiscoveredAndRegistered()
    {
        // record class is a distinct syntax node (RecordDeclarationSyntax) from a plain class
        // (ClassDeclarationSyntax); the generator's ForAttributeWithMetadataName predicate must
        // match both, even though both are TypeKind.Class at the symbol level.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public record class Foo : IFoo;
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("services.AddSingleton<global::TestNs.IFoo, global::TestNs.Foo>();", generated);
    }

    [Fact]
    public void RecordClass_DoesNotRegisterCompilerSynthesizedSelfIEquatable()
    {
        // Regression test: a record class implicitly implements IEquatable<TSelf> as part of its
        // compiler-generated equality members, and that interface shows up in
        // INamedTypeSymbol.Interfaces exactly as if hand-written. Without excluding it (mirroring
        // the existing IDisposable/IAsyncDisposable exclusion), this record would gain a
        // nonsensical IEquatable<Foo> registration, and -- because it would then have 2 "service
        // types" instead of 1 -- would incorrectly switch to the self+forwarding pattern instead
        // of registering IFoo directly.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public record class Foo : IFoo;
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("services.AddSingleton<global::TestNs.IFoo, global::TestNs.Foo>();", generated);
        Assert.DoesNotContain("IEquatable", generated);
        Assert.DoesNotContain("GetRequiredService", generated);
    }

    [Fact]
    public void OrdinaryClass_ExplicitlyImplementingSelfIEquatable_RegistersItAsAServiceType()
    {
        // Regression test: the IEquatable<TSelf> exclusion exists only to compensate for the
        // *compiler-synthesized* interface a record class implicitly gains; it must not apply to
        // an ordinary class that deliberately implements IEquatable<Foo> by hand, since there it is
        // a real, intentional service type like any other directly-implemented interface.
        const string source = Usings + """
            using System;

            namespace TestNs;

            [Service]
            public class Foo : IEquatable<Foo>
            {
                public bool Equals(Foo? other) => ReferenceEquals(this, other);

                public override bool Equals(object? obj) => Equals(obj as Foo);

                public override int GetHashCode() => 0;
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Contains("services.AddSingleton<global::System.IEquatable<global::TestNs.Foo>, global::TestNs.Foo>();", generated);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void RecordClass_WithoutClassKeyword_IsDiscoveredAndRegistered()
    {
        // `record Foo` (no explicit `class` keyword) still parses as RecordDeclarationSyntax.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public record Foo : IFoo;
            """;

        var generated = GeneratorTestHelper.RunGenerator(source).GetSingleSource();

        Assert.Contains("services.AddSingleton<global::TestNs.IFoo, global::TestNs.Foo>();", generated);
    }
}
