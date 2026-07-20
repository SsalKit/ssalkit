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
}
