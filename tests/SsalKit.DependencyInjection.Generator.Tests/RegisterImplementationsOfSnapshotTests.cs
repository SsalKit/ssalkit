using SsalKit.DependencyInjection.Generator.Tests.TestSupport;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Full-file snapshot tests for the <c>[assembly: RegisterImplementationsOf]</c> output (as opposed
/// to <see cref="RegisterImplementationsOfEmissionTests"/>, which asserts on individual lines), so
/// the block's position within the generated method, its ordering, and the extra doc-comment lines
/// are all pinned.
/// </summary>
public class RegisterImplementationsOfSnapshotTests
{
    private const string Usings = """
        using System;
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    [Fact]
    public Task NonGenericContract_MultipleImplementations()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }

            public class WarmCaches : IStartupTask { }
            public class MigrateDatabase : IStartupTask { }
            public class SeedData : IStartupTask { }
            """;

        return Verify(source);
    }

    [Fact]
    public Task UnboundGenericContract_ClosedInstantiations()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<,>), ServiceLifetime.Scoped)]

            namespace TestNs;

            public interface IHandler<TRequest, TResponse> { }

            public record Ping;
            public record Pong;
            public record Tick;
            public record Tock;

            public class BothHandler : IHandler<Ping, Pong>, IHandler<Tick, Tock> { }
            public class PingHandler : IHandler<Ping, Pong> { }
            """;

        return Verify(source);
    }

    [Fact]
    public Task OpenGenericImplementation_UsesTypeBasedRegistration()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IValidator<>), ServiceLifetime.Transient)]

            namespace TestNs;

            public interface IValidator<T> { }

            public class DefaultValidator<T> : IValidator<T> { }
            public class StringValidator : IValidator<string> { }

            // Partially applied: not an exact-match open generic shape, so it is skipped silently.
            public class PairValidator<T> : IValidator<System.Collections.Generic.KeyValuePair<T, int>> { }
            """;

        return Verify(source);
    }

    [Fact]
    public Task ServiceDecoratedClassIsExcluded_AndTheTwoBlocksCoexist()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }
            public interface IClock { }

            public class ConventionTask : IStartupTask { }

            [Service(ServiceLifetime.Transient)]
            public class ExplicitTask : IStartupTask { }

            [Service]
            public class SystemClock : IClock { }
            """;

        return Verify(source);
    }

    [Fact]
    public Task NonDefaultMode_AndMultipleContracts()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask), Mode = RegistrationMode.Add)]
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<>), ServiceLifetime.Scoped, Mode = RegistrationMode.TryAdd)]

            namespace TestNs;

            public interface IStartupTask { }
            public interface IHandler<T> { }

            public class MigrateDatabase : IStartupTask { }
            public class IntHandler : IHandler<int> { }
            """;

        return Verify(source);
    }

    [Fact]
    public Task ConventionScanAlongsideServiceFactory()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public enum NotifierKind { Email, Sms }

            public interface IStartupTask { }
            public interface INotifier { }

            public class MigrateDatabase : IStartupTask { }

            [ServiceFactory]
            public interface INotifierFactory
            {
                INotifier Resolve(NotifierKind kind);
            }
            """;

        return Verify(source);
    }

    /// <summary>
    /// All three features in one compilation, which is the arrangement nothing pinned until now:
    /// the emitter writes the <c>[Service]</c> block, then the convention block, then the factory
    /// block, and that order is a contract rather than an implementation detail -- it is what
    /// decides which registration wins a single-instance resolution when two of them bind the same
    /// service type (see SSAL027), so a change to it changes consumers' runtime behaviour silently.
    /// </summary>
    [Fact]
    public Task ServiceAndConventionAndFactory_CoexistInAFixedBlockOrder()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]
            [assembly: RegisterImplementationsOf(typeof(TestNs.IValidator<>), ServiceLifetime.Transient)]

            namespace TestNs;

            public enum NotifierKind { Email, Sms }

            public interface IStartupTask { }
            public interface INotifier { }
            public interface IValidator<T> { }
            public interface IClock { }

            // Sorts after the convention block's implementations alphabetically, so the snapshot
            // also proves the blocks are not merged and sorted as one list.
            [Service(ServiceLifetime.Singleton)]
            public class ZuluClock : IClock { }

            [Service(ServiceLifetime.Scoped, Key = NotifierKind.Email)]
            public class EmailNotifier : INotifier { }

            public class MigrateDatabase : IStartupTask { }
            public class WarmCaches : IStartupTask { }

            public class Validator<T> : IValidator<T> { }

            [ServiceFactory]
            public interface INotifierFactory
            {
                INotifier Resolve(NotifierKind kind);
            }
            """;

        return Verify(source);
    }

    private static Task Verify(string source)
    {
        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);

        Assert.Empty(result.GetCompilationErrors());

        return Verifier.Verify(result.ToSnapshotText()).UseDirectory("Snapshots");
    }
}
