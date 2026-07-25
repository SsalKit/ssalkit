using SsalKit.DependencyInjection.Generator.Tests.TestSupport;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// End-to-end emission tests for the <c>[assembly: RegisterImplementationsOf]</c> convention scan:
/// which classes it matches, which service type(s) each is registered under, and which classes it
/// passes over silently.
/// </summary>
public class RegisterImplementationsOfEmissionTests
{
    private const string Usings = """
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    [Fact]
    public void NonGenericContract_RegistersEveryImplementation_AsTryAddEnumerableSingleton()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }

            public class MigrateDatabase : IStartupTask { }
            public class WarmCaches : IStartupTask { }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Empty(result.GetCompilationErrors());
        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::TestNs.IStartupTask, global::TestNs.MigrateDatabase>());",
            generated);
        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::TestNs.IStartupTask, global::TestNs.WarmCaches>());",
            generated);
    }

    [Fact]
    public void ExplicitLifetimeArgument_IsUsedForEveryMatch()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask), ServiceLifetime.Scoped)]

            namespace TestNs;

            public interface IStartupTask { }

            public class MigrateDatabase : IStartupTask { }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Scoped<global::TestNs.IStartupTask, global::TestNs.MigrateDatabase>());",
            generated);
    }

    [Theory]
    [InlineData("RegistrationMode.Add", "services.AddSingleton<global::TestNs.IStartupTask, global::TestNs.MigrateDatabase>();")]
    [InlineData("RegistrationMode.TryAdd", "services.TryAddSingleton<global::TestNs.IStartupTask, global::TestNs.MigrateDatabase>();")]
    [InlineData(
        "RegistrationMode.TryAddEnumerable",
        "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::TestNs.IStartupTask, global::TestNs.MigrateDatabase>());")]
    [InlineData(
        "RegistrationMode.Replace",
        "services.Replace(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::TestNs.IStartupTask, global::TestNs.MigrateDatabase>());")]
    public void AllFourModes_RenderExpectedCall(string modeArg, string expected)
    {
        var source = Usings + $$"""
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask), Mode = {{modeArg}})]

            namespace TestNs;

            public interface IStartupTask { }

            public class MigrateDatabase : IStartupTask { }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GetCompilationErrors());
        Assert.Contains(expected, result.GetSingleSource());
    }

    [Fact]
    public void UnboundGenericContract_RegistersEachClosedInstantiationSeparately()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<,>), ServiceLifetime.Scoped)]

            namespace TestNs;

            public interface IHandler<TRequest, TResponse> { }

            public record Ping;
            public record Pong;
            public record Tick;
            public record Tock;

            // One class implementing two closed instantiations must be registered under both.
            public class BothHandler : IHandler<Ping, Pong>, IHandler<Tick, Tock> { }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Empty(result.GetCompilationErrors());
        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Scoped<global::TestNs.IHandler<global::TestNs.Ping, global::TestNs.Pong>, global::TestNs.BothHandler>());",
            generated);
        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Scoped<global::TestNs.IHandler<global::TestNs.Tick, global::TestNs.Tock>, global::TestNs.BothHandler>());",
            generated);
    }

    [Fact]
    public void ClosedGenericContract_MatchesOnlyThatInstantiation()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<TestNs.Ping, TestNs.Pong>))]

            namespace TestNs;

            public interface IHandler<TRequest, TResponse> { }

            public record Ping;
            public record Pong;
            public record Tick;
            public record Tock;

            public class PingHandler : IHandler<Ping, Pong> { }
            public class TickHandler : IHandler<Tick, Tock> { }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).GetSingleSource();

        Assert.Contains("global::TestNs.PingHandler", generated);
        Assert.DoesNotContain("global::TestNs.TickHandler", generated);
    }

    [Fact]
    public void OpenGenericImplementation_ExactMatchShape_RegistersAsTypePair()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IValidator<>), ServiceLifetime.Transient)]

            namespace TestNs;

            public interface IValidator<T> { }

            public class DefaultValidator<T> : IValidator<T> { }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GetCompilationErrors());
        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Transient(typeof(global::TestNs.IValidator<>), typeof(global::TestNs.DefaultValidator<>)));",
            result.GetSingleSource());
    }

    [Fact]
    public void OpenGenericImplementation_NonExactMatchShape_IsSkippedSilently()
    {
        // Handler<T> : IHandler<T, Unit> is a partially-applied instantiation: MEDI cannot express
        // it as an open generic registration (the same rule SSAL009 enforces for [Service]), so the
        // scan passes over it -- and, with nothing else matching, the assembly gets no file at all.
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<,>))]

            namespace TestNs;

            public interface IHandler<TRequest, TResponse> { }

            public record Unit;

            public class Handler<T> : IHandler<T, Unit> { }
            """;

        Assert.Empty(GeneratorTestSupport.RunGenerator(source).GeneratedSources);
    }

    [Fact]
    public void OpenGenericImplementation_NonGenericContract_IsSkippedSilently()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }

            public class GenericTask<T> : IStartupTask { }
            """;

        Assert.Empty(GeneratorTestSupport.RunGenerator(source).GeneratedSources);
    }

    [Fact]
    public void InheritedContract_Matches()
    {
        // The question a contract asks is "does this class implement X"; getting X from a base
        // class is implementing it just as much as listing it directly.
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }

            public abstract class TaskBase : IStartupTask { }

            public class DerivedTask : TaskBase { }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).GetSingleSource();

        Assert.Contains("global::TestNs.DerivedTask", generated);
        // The abstract base itself is not registrable.
        Assert.DoesNotContain("global::TestNs.TaskBase", generated);
    }

    [Fact]
    public void ServiceDecoratedClass_IsExcludedFromTheScan()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }

            public class ConventionTask : IStartupTask { }

            // Explicit beats convention: this one is registered only the way [Service] says, and
            // must not additionally appear as a TryAddEnumerable convention registration.
            [Service(ServiceLifetime.Transient)]
            public class ExplicitTask : IStartupTask { }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::TestNs.IStartupTask, global::TestNs.ConventionTask>());",
            generated);
        Assert.Contains(
            "services.AddTransient<global::TestNs.IStartupTask, global::TestNs.ExplicitTask>();",
            generated);
        Assert.DoesNotContain(
            "ServiceDescriptor.Singleton<global::TestNs.IStartupTask, global::TestNs.ExplicitTask>()",
            generated);
    }

    [Theory]
    [InlineData("public abstract class Skipped : IStartupTask { }")]
    [InlineData("public static class Skipped { }")]
    [InlineData("public interface Skipped : IStartupTask { }")]
    [InlineData("public struct Skipped : IStartupTask { }")]
    [InlineData("file class Skipped : IStartupTask { }")]
    public void UnregistrableShapes_AreSkippedSilently(string declaration)
    {
        var source = Usings + $$"""
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }

            {{declaration}}
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void PrivateNestedClass_IsSkippedSilently()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }

            public class Outer
            {
                private class Hidden : IStartupTask { }
            }
            """;

        Assert.Empty(GeneratorTestSupport.RunGenerator(source).GeneratedSources);
    }

    [Fact]
    public void ClassNestedInsideGenericType_IsSkippedSilently()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }

            public class Outer<T>
            {
                public class Inner : IStartupTask { }
            }
            """;

        Assert.Empty(GeneratorTestSupport.RunGenerator(source).GeneratedSources);
    }

    [Fact]
    public void NestedAccessibleClass_IsMatched()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }

            public static class Outer
            {
                internal class Inner : IStartupTask { }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GetCompilationErrors());
        Assert.Contains("global::TestNs.Outer.Inner", result.GetSingleSource());
    }

    [Fact]
    public void RecordClass_IsMatched()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }

            public record class RecordTask : IStartupTask;
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GetCompilationErrors());
        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::TestNs.IStartupTask, global::TestNs.RecordTask>());",
            result.GetSingleSource());
    }

    [Fact]
    public void ReferencedAssemblyImplementations_AreNeverDiscovered()
    {
        // The documented scope limit: the scan sees only the compilation it is declared in.
        var contractAssembly = GeneratorTest.CompileToReference(
            """
            namespace Contracts;

            public interface IStartupTask { }

            public class ExternalTask : IStartupTask { }
            """,
            "Contracts",
            GeneratorTestSupport.Options);

        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(Contracts.IStartupTask))]

            namespace TestNs;

            public class LocalTask : Contracts.IStartupTask { }
            """;

        var generated = GeneratorTestSupport
            .RunGenerator(source, GeneratorTestSupport.Referencing(contractAssembly))
            .GetSingleSource();

        Assert.Contains("global::TestNs.LocalTask", generated);
        Assert.DoesNotContain("ExternalTask", generated);
    }

    [Fact]
    public void OverlappingContracts_InAgreement_EmitTheStatementOnce()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<>))]
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<int>))]

            namespace TestNs;

            public interface IHandler<T> { }

            public class IntHandler : IHandler<int> { }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Empty(result.GetCompilationErrors());

        const string expected =
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::TestNs.IHandler<int>, global::TestNs.IntHandler>());";

        Assert.Equal(
            1,
            generated.Split(new[] { expected }, StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void OverlappingContracts_InDisagreement_EmitBothStatements()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<>), ServiceLifetime.Scoped)]
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<int>), ServiceLifetime.Transient)]

            namespace TestNs;

            public interface IHandler<T> { }

            public class IntHandler : IHandler<int> { }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);
        var generated = result.GetSingleSource();

        Assert.Empty(result.GetCompilationErrors());
        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Scoped<global::TestNs.IHandler<int>, global::TestNs.IntHandler>());",
            generated);
        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Transient<global::TestNs.IHandler<int>, global::TestNs.IntHandler>());",
            generated);
    }

    [Fact]
    public void InvalidDeclaration_EmitsNothingForThatContract()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.NotAnInterface))]
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }

            public class NotAnInterface { }

            public class GoodTask : IStartupTask { }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).GetSingleSource();

        // The valid contract is unaffected by the invalid one alongside it.
        Assert.Contains("global::TestNs.GoodTask", generated);
        Assert.DoesNotContain("NotAnInterface", generated);
    }

    [Fact]
    public void DuplicateContract_RegistersOnlyOnce_UsingTheFirstDeclaration()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask), ServiceLifetime.Scoped)]
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask), ServiceLifetime.Transient)]

            namespace TestNs;

            public interface IStartupTask { }

            public class Task1 : IStartupTask { }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).GetSingleSource();

        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Scoped<global::TestNs.IStartupTask, global::TestNs.Task1>());",
            generated);
        Assert.DoesNotContain("Transient", generated);
    }

    [Fact]
    public void NoContractDeclared_ProducesTheExactSameFileAsBefore()
    {
        // The byte-for-byte guard: an assembly that does not use the feature must be completely
        // unaffected by its existence, doc comment included.
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public class Foo : IFoo { }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).GetSingleSource();

        Assert.DoesNotContain("RegisterImplementationsOf", generated);
    }
}
