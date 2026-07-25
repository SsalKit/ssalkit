using SsalKit.DependencyInjection.Generator.Tests.TestSupport;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Full-file snapshot tests for the <c>[ServiceFactory]</c> output, covering both the generated
/// implementation class and the registration extension method that picks it up (as opposed to
/// <see cref="ServiceFactoryEmissionTests"/>, which asserts on individual lines).
/// </summary>
public class ServiceFactorySnapshotTests
{
    private const string Usings = """
        using System;
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    [Fact]
    public Task Basic_EmitsImplementationAndRegistration()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum PaymentMethod { Card, Bank }

            public interface IPaymentProcessor { }

            [ServiceFactory]
            public interface IPaymentProcessorFactory
            {
                IPaymentProcessor Create(PaymentMethod method);
            }
            """;

        return VerifyAll(source);
    }

    [Fact]
    public Task MultipleFactories_AlongsideServiceRegistrations()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum PaymentMethod { Card, Bank }
            public enum NotifierKind { Email, Sms }

            public interface IPaymentProcessor { }
            public interface INotifier { }

            [Service(ServiceLifetime.Singleton, Key = PaymentMethod.Card)]
            public class CardProcessor : IPaymentProcessor { }

            [ServiceFactory]
            public interface IPaymentProcessorFactory
            {
                IPaymentProcessor Create(PaymentMethod method);
            }

            [ServiceFactory]
            public interface INotifierFactory
            {
                INotifier Resolve(NotifierKind kind);
            }
            """;

        return VerifyAll(source);
    }

    [Fact]
    public Task NestedAndGlobalNamespaceInterfaces()
    {
        const string source = Usings + """
            public enum RootKind { A }

            public interface IRootService { }

            [ServiceFactory]
            public interface IRootFactory
            {
                IRootService Create(RootKind kind);
            }

            namespace TestNs
            {
                public enum NestedKind { A }

                public interface INestedService { }

                public static class Outer
                {
                    [ServiceFactory]
                    public interface INestedFactory
                    {
                        INestedService Create(NestedKind kind);
                    }
                }
            }
            """;

        return VerifyAll(source);
    }

    [Fact]
    public Task InternalInterface_GeneratesInternalImplementation()
    {
        const string source = Usings + """
            namespace TestNs;

            internal enum Kind { A, B }

            internal interface IInternalService { }

            [ServiceFactory]
            internal interface IInternalFactory
            {
                IInternalService Create(Kind kind);
            }
            """;

        return VerifyAll(source);
    }

    /// <summary>
    /// Verifies every generated file at once, each preceded by its hint name, so a snapshot also
    /// records which files were produced and under what names.
    /// </summary>
    private static Task VerifyAll(string source)
    {
        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);

        Assert.Empty(result.GetCompilationErrors());

        return Verifier.Verify(result.ToSnapshotText()).UseDirectory("Snapshots");
    }
}
