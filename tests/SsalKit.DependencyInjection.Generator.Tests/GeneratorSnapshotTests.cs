using SsalKit.DependencyInjection.Generator.Tests.TestSupport;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Full-file snapshot tests for the generator's output, covering the shape of the generated
/// registration extension class end-to-end (as opposed to <see cref="GeneratorEmissionTests"/>,
/// which asserts on individual statements).
/// </summary>
public class GeneratorSnapshotTests
{
    [Fact]
    public Task MultiInterfaceSingleton_ForwardsSharedInstance()
    {
        const string source = """
            using SsalKit.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection;

            namespace TestNs;

            public interface IFoo { }
            public interface IBar { }

            [Service(ServiceLifetime.Singleton)]
            public class Foo : IFoo, IBar { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample").GetSingleSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task OpenGeneric_Basic_UsesTypeBasedRegistration()
    {
        const string source = """
            using SsalKit.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection;

            namespace TestNs;

            public interface IRepository<T> { }

            [Service(ServiceLifetime.Singleton)]
            public class Repository<T> : IRepository<T> { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample").GetSingleSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task OpenGeneric_MultiInterface_RegistersEachIndependently()
    {
        const string source = """
            using SsalKit.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection;

            namespace TestNs;

            public interface IReader<T> { }
            public interface IWriter<T> { }

            [Service(ServiceLifetime.Singleton)]
            public class Store<T> : IReader<T>, IWriter<T> { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample").GetSingleSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task OpenGeneric_MixedWithClosedClass_BothEmitCorrectly()
    {
        const string source = """
            using SsalKit.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection;

            namespace TestNs;

            public interface IFoo { }
            public interface IRepository<T> { }

            [Service]
            public class Foo : IFoo { }

            [Service(ServiceLifetime.Scoped, Mode = RegistrationMode.TryAdd, Key = "k")]
            public class Repository<T> : IRepository<T> { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample").GetSingleSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }
}
