using SsalKit.DependencyInjection.Generator.Tests.TestSupport;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Emission tests for <c>[ServiceFactory]</c>: which files are produced, how the implementation
/// class is named and namespaced, and how the registration extension method picks it up.
/// </summary>
public class ServiceFactoryEmissionTests
{
    private const string Usings = """
        using System;
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    private const string BasicFactory = Usings + """
        namespace TestNs;

        public enum PaymentMethod { Card, Bank }

        public interface IPaymentProcessor { }

        [ServiceFactory]
        public interface IPaymentProcessorFactory
        {
            IPaymentProcessor Create(PaymentMethod method);
        }
        """;

    [Fact]
    public void Factory_EmitsRegistrationFileAndOneImplementationFile()
    {
        var result = GeneratorTestHelper.RunGenerator(BasicFactory, "SsalKit.Sample");

        Assert.Equal(
            new[] { "SsalKitSampleServiceCollectionExtensions.g.cs", "TestNs.IPaymentProcessorFactory.ServiceFactory.g.cs" },
            result.GeneratedSources.Select(s => s.HintName).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Factory_RegistersImplementationAsSingletonAgainstTheInterface()
    {
        var generated = GeneratorTestHelper.RunGenerator(BasicFactory, "SsalKit.Sample").GetRegistrationSource();

        Assert.Contains(
            "services.AddSingleton<global::TestNs.IPaymentProcessorFactory, global::SsalKit.DependencyInjection.Generated.TestNs.IPaymentProcessorFactoryImplementation>();",
            generated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Factory_ImplementationDelegatesToGetRequiredKeyedService()
    {
        var generated = GeneratorTestHelper
            .RunGenerator(BasicFactory, "SsalKit.Sample")
            .GetSource("TestNs.IPaymentProcessorFactory.ServiceFactory.g.cs");

        Assert.Contains("namespace SsalKit.DependencyInjection.Generated.TestNs", generated, StringComparison.Ordinal);
        Assert.Contains(
            "internal sealed class IPaymentProcessorFactoryImplementation : global::TestNs.IPaymentProcessorFactory",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "public global::TestNs.IPaymentProcessor Create(global::TestNs.PaymentMethod method)",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "=> global::Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions.GetRequiredKeyedService<global::TestNs.IPaymentProcessor>(this._provider, method);",
            generated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Factory_GeneratedCodeCompiles()
    {
        var result = GeneratorTestHelper.RunGenerator(BasicFactory, "SsalKit.Sample");

        Assert.Empty(result.GetOutputCompilationErrors());
    }

    /// <summary>
    /// The two factory registrations are ordered by interface FQN, independently of the
    /// <c>[Service]</c> registrations, which keep their own implementation-FQN ordering.
    /// </summary>
    [Fact]
    public void MultipleFactories_AreRegisteredInInterfaceFqnOrder()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            [ServiceFactory]
            public interface IZebraFactory
            {
                IFoo Create(Kind kind);
            }

            [ServiceFactory]
            public interface IAlphaFactory
            {
                IFoo Make(Kind kind);
            }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample").GetRegistrationSource();

        var alphaIndex = generated.IndexOf("global::TestNs.IAlphaFactory,", StringComparison.Ordinal);
        var zebraIndex = generated.IndexOf("global::TestNs.IZebraFactory,", StringComparison.Ordinal);

        Assert.True(alphaIndex >= 0 && zebraIndex >= 0);
        Assert.True(alphaIndex < zebraIndex);
    }

    [Fact]
    public void FactoryOnly_NoServiceAttribute_StillEmitsRegistrationMethod()
    {
        var generated = GeneratorTestHelper.RunGenerator(BasicFactory, "SsalKit.Sample").GetRegistrationSource();

        Assert.Contains("public static class SsalKitSampleServiceCollectionExtensions", generated, StringComparison.Ordinal);
        Assert.Contains("AddSsalKitSampleServices(", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// A compilation with neither a <c>[Service]</c> class nor a <c>[ServiceFactory]</c> interface
    /// still produces nothing at all -- the registration file is not emitted "just in case".
    /// </summary>
    [Fact]
    public void NoServicesAndNoFactories_EmitsNothing()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            public class Foo : IFoo { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample");

        Assert.Empty(result.GeneratedSources);
    }

    /// <summary>
    /// An assembly with no factories must produce byte-for-byte what it produced before
    /// <c>[ServiceFactory]</c> existed, which includes not mentioning it in the XML documentation.
    /// </summary>
    [Fact]
    public void NoFactories_RegistrationFileDoesNotMentionServiceFactory()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public class Foo : IFoo { }
            """;

        var generated = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample").GetSingleSource();

        Assert.DoesNotContain("ServiceFactory", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void NestedInterface_MirrorsContainingTypeChainAsNamespaceSegments()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            public static class Outer
            {
                public static class Inner
                {
                    [ServiceFactory]
                    public interface INestedFactory
                    {
                        IFoo Create(Kind kind);
                    }
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample");
        var generated = result.GetSource("TestNs.Outer.Inner.INestedFactory.ServiceFactory.g.cs");

        Assert.Contains(
            "namespace SsalKit.DependencyInjection.Generated.TestNs.Outer.Inner",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "internal sealed class INestedFactoryImplementation : global::TestNs.Outer.Inner.INestedFactory",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "services.AddSingleton<global::TestNs.Outer.Inner.INestedFactory, global::SsalKit.DependencyInjection.Generated.TestNs.Outer.Inner.INestedFactoryImplementation>();",
            result.GetRegistrationSource(),
            StringComparison.Ordinal);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    /// <summary>
    /// A nested <c>Outer.IFoo</c> and a top-level <c>Outer_IFoo</c> in the same namespace produce
    /// distinct generated names, because the containing type becomes a namespace segment rather
    /// than being flattened into the class name with a separator.
    /// </summary>
    [Fact]
    public void NestedAndUnderscoredSiblings_DoNotCollide()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            public static class Outer
            {
                [ServiceFactory]
                public interface IBar
                {
                    IFoo Create(Kind kind);
                }
            }

            [ServiceFactory]
            public interface Outer_IBar
            {
                IFoo Create(Kind kind);
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample");
        var registration = result.GetRegistrationSource();

        Assert.Contains(
            "global::SsalKit.DependencyInjection.Generated.TestNs.Outer.IBarImplementation>();",
            registration,
            StringComparison.Ordinal);
        Assert.Contains(
            "global::SsalKit.DependencyInjection.Generated.TestNs.Outer_IBarImplementation>();",
            registration,
            StringComparison.Ordinal);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    /// <summary>
    /// A containing type whose name is a reserved keyword becomes a namespace segment, so it has to
    /// carry its <c>@</c> back with it.
    /// </summary>
    [Fact]
    public void KeywordNamedContainingType_IsEscapedInTheGeneratedNamespace()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            public static class @class
            {
                [ServiceFactory]
                public interface IKeywordNestedFactory
                {
                    IFoo Create(Kind kind);
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample");

        Assert.Contains(
            "namespace SsalKit.DependencyInjection.Generated.TestNs.@class",
            result.GetSource("TestNs._class.IKeywordNestedFactory.ServiceFactory.g.cs"),
            StringComparison.Ordinal);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void GlobalNamespaceInterface_EmitsIntoTheGeneratedNamespaceRoot()
    {
        const string source = Usings + """
            public enum Kind { A }

            public interface IFoo { }

            [ServiceFactory]
            public interface IRootFactory
            {
                IFoo Create(Kind kind);
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample");
        var generated = result.GetSource("IRootFactory.ServiceFactory.g.cs");

        Assert.Contains("namespace SsalKit.DependencyInjection.Generated", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace SsalKit.DependencyInjection.Generated.", generated, StringComparison.Ordinal);
        Assert.Contains(
            "services.AddSingleton<global::IRootFactory, global::SsalKit.DependencyInjection.Generated.IRootFactoryImplementation>();",
            result.GetRegistrationSource(),
            StringComparison.Ordinal);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void InternalInterface_StillGeneratesAndCompiles()
    {
        const string source = Usings + """
            namespace TestNs;

            internal enum Kind { A }

            internal interface IFoo { }

            [ServiceFactory]
            internal interface IInternalFactory
            {
                IFoo Create(Kind kind);
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample");

        Assert.Contains(
            "internal sealed class IInternalFactoryImplementation : global::TestNs.IInternalFactory",
            result.GetSource("TestNs.IInternalFactory.ServiceFactory.g.cs"),
            StringComparison.Ordinal);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    /// <summary>
    /// The method and parameter names come straight from the interface, so a name that happens to
    /// be a C# keyword has to be escaped before it is emitted as a declaration.
    /// </summary>
    [Fact]
    public void KeywordMethodAndParameterNames_AreEscaped()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            [ServiceFactory]
            public interface IKeywordFactory
            {
                IFoo @new(Kind @class);
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample");
        var generated = result.GetSource("TestNs.IKeywordFactory.ServiceFactory.g.cs");

        Assert.Contains("public global::TestNs.IFoo @new(global::TestNs.Kind @class)", generated, StringComparison.Ordinal);
        Assert.Contains("GetRequiredKeyedService<global::TestNs.IFoo>(this._provider, @class);", generated, StringComparison.Ordinal);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    /// <summary>
    /// The backing field is always read through <c>this.</c>, so a parameter that happens to be
    /// named <c>_provider</c> shadows nothing that matters.
    /// </summary>
    [Fact]
    public void ParameterNamedLikeTheBackingField_StillResolvesTheProvider()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            [ServiceFactory]
            public interface IShadowingFactory
            {
                IFoo Create(Kind _provider);
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample");
        var generated = result.GetSource("TestNs.IShadowingFactory.ServiceFactory.g.cs");

        Assert.Contains(
            "GetRequiredKeyedService<global::TestNs.IFoo>(this._provider, _provider);",
            generated,
            StringComparison.Ordinal);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Fact]
    public void GenericReturnType_IsEmittedClosed()
    {
        const string source = Usings + """
            using System.Collections.Generic;

            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            [ServiceFactory]
            public interface IListFactory
            {
                IReadOnlyList<IFoo> Create(Kind kind);
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample");
        var generated = result.GetSource("TestNs.IListFactory.ServiceFactory.g.cs");

        Assert.Contains(
            "public global::System.Collections.Generic.IReadOnlyList<global::TestNs.IFoo> Create(global::TestNs.Kind kind)",
            generated,
            StringComparison.Ordinal);
        Assert.Empty(result.GetOutputCompilationErrors());
    }

    [Theory]
    [InlineData("IEmptyFactory { }")]
    [InlineData("IBadFactory { void Create(Kind kind); }")]
    public void InvalidFactory_EmitsNoImplementationFile(string declaration)
    {
        var source = Usings + $$"""
            namespace TestNs;

            public enum Kind { A }

            [ServiceFactory]
            public interface {{declaration}}
            """;

        var result = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample");

        Assert.Empty(result.GeneratedSources);
    }

    /// <summary>
    /// Partial generation is never a thing: an invalid factory drops out entirely while a valid one
    /// alongside it, and the assembly's <c>[Service]</c> registrations, are emitted as usual.
    /// </summary>
    [Fact]
    public void InvalidFactoryAlongsideValidOnes_OnlyDropsTheInvalidOne()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            [Service]
            public class Foo : IFoo { }

            [ServiceFactory]
            public interface IGoodFactory
            {
                IFoo Create(Kind kind);
            }

            [ServiceFactory]
            public interface IBadFactory { }
            """;

        var result = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample");
        var registration = result.GetRegistrationSource();

        Assert.Equal(2, result.GeneratedSources.Length);
        Assert.Contains("global::TestNs.IGoodFactory,", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("IBadFactory", registration, StringComparison.Ordinal);
        Assert.Contains("services.AddSingleton<global::TestNs.IFoo, global::TestNs.Foo>();", registration, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two same-named interfaces in different namespaces must not collide, either on the hint name
    /// or on the generated class's own fully-qualified name.
    /// </summary>
    [Fact]
    public void SameNamedFactoriesInDifferentNamespaces_DoNotCollide()
    {
        const string source = Usings + """
            namespace A
            {
                public enum Kind { X }

                public interface IFoo { }

                [ServiceFactory]
                public interface IFactory
                {
                    IFoo Create(Kind kind);
                }
            }

            namespace B
            {
                public enum Kind { X }

                public interface IFoo { }

                [ServiceFactory]
                public interface IFactory
                {
                    IFoo Create(Kind kind);
                }
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source, "SsalKit.Sample");

        Assert.Equal(3, result.GeneratedSources.Length);
        Assert.Contains(
            "namespace SsalKit.DependencyInjection.Generated.A",
            result.GetSource("A.IFactory.ServiceFactory.g.cs"),
            StringComparison.Ordinal);
        Assert.Contains(
            "namespace SsalKit.DependencyInjection.Generated.B",
            result.GetSource("B.IFactory.ServiceFactory.g.cs"),
            StringComparison.Ordinal);
        Assert.Empty(result.GetOutputCompilationErrors());
    }
}
