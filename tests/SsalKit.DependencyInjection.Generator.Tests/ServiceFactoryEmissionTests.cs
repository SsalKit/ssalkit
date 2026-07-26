using Microsoft.CodeAnalysis;
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
        var result = GeneratorTestSupport.RunGenerator(BasicFactory, GeneratorTestSupport.SampleAssembly);

        Assert.Equal(
            new[] { "SsalKitSampleServiceCollectionExtensions.g.cs", "TestNs.IPaymentProcessorFactory.ServiceFactory.g.cs" },
            result.GeneratedSources.Select(s => s.HintName).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Factory_RegistersImplementationAsSingletonAgainstTheInterface()
    {
        var generated = GeneratorTestSupport.RunGenerator(BasicFactory, GeneratorTestSupport.SampleAssembly)
            .GetSource("ServiceCollectionExtensions.g.cs");

        Assert.Contains(
            "services.AddSingleton<global::TestNs.IPaymentProcessorFactory, global::SsalKit.DependencyInjection.Generated.TestNs.IPaymentProcessorFactoryImplementation>();",
            generated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Factory_ImplementationDelegatesToGetRequiredKeyedService()
    {
        var generated = GeneratorTestSupport
            .RunGenerator(BasicFactory, GeneratorTestSupport.SampleAssembly)
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
        var result = GeneratorTestSupport.RunGenerator(BasicFactory, GeneratorTestSupport.SampleAssembly);

        Assert.Empty(result.GetCompilationErrors());
    }

    /// <summary>
    /// SSAL017's inherited-member rule has to drop the interface here as well, or the generator
    /// emits a class that does not implement <c>IExtra.Extra</c> (CS0535) -- and one broken factory
    /// takes the whole registration file down with it.
    /// </summary>
    [Fact]
    public void FactoryInheritingAnInterfaceWithMembers_IsNotGenerated()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            public interface IExtra
            {
                void Extra();
            }

            [ServiceFactory]
            public interface IInheritingFactory : IExtra
            {
                IFoo Create(Kind kind);
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);

        Assert.Empty(result.GeneratedSources);
    }

    /// <summary>
    /// A marker base interface is allowed, and the generated implementation picks it up for free by
    /// implementing the factory interface -- so the emitted class satisfies both.
    /// </summary>
    [Fact]
    public void FactoryInheritingAMarkerInterface_IsGeneratedAndCompiles()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            public interface IMarker { }

            [ServiceFactory]
            public interface IMarkedFactory : IMarker
            {
                IFoo Create(Kind kind);
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);

        Assert.Contains(
            "internal sealed class IMarkedFactoryImplementation : global::TestNs.IMarkedFactory",
            result.GetSource("TestNs.IMarkedFactory.ServiceFactory.g.cs"),
            StringComparison.Ordinal);
        Assert.Empty(result.GetCompilationErrors());
    }

    /// <summary>
    /// The generated method's signature must reproduce the interface's nullable annotations
    /// exactly; the plain fully-qualified format drops them, which the compiler reports as a
    /// nullability mismatch (CS8613/CS8766) inside a file the consumer cannot edit -- and as an
    /// error under <c>TreatWarningsAsErrors</c>.
    /// </summary>
    [Theory]
    [InlineData(
        "System.Collections.Generic.IList<string?>",
        "global::System.Collections.Generic.IList<string?>")]
    [InlineData("IFoo?", "global::TestNs.IFoo?")]
    public void NullableAnnotationsInTheFactorySignature_AreReproduced(string declared, string expected)
    {
        var source = Usings + $$"""
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            [ServiceFactory]
            public interface INullableFactory
            {
                {{declared}} Create(Kind kind);
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);
        var generated = result.GetSource("TestNs.INullableFactory.ServiceFactory.g.cs");

        Assert.Contains($"public {expected} Create(global::TestNs.Kind kind)", generated, StringComparison.Ordinal);
        Assert.Empty(result.GetCompilationErrors());

        // Nothing the generator emitted may warn either: the file is inside the consumer's
        // compilation, so a nullability warning here becomes their build error.
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning
                && diagnostic.Location.SourceTree?.FilePath.EndsWith("ServiceFactory.g.cs", StringComparison.Ordinal) == true));
    }

    /// <summary>
    /// Two <c>[ServiceFactory]</c> applications on the parts of one <c>partial</c> interface are
    /// matched twice and produce identical models. <c>AddSource</c> throws on a repeated hint name,
    /// and that exception takes down the <em>whole</em> generator -- the registration extension
    /// method included -- so the duplicate has to be collapsed before emission.
    /// </summary>
    /// <remarks>
    /// The source is deliberately invalid C# (<c>[ServiceFactory]</c> is
    /// <c>AllowMultiple = false</c>, so the second application is CS0579), which is exactly the
    /// situation worth protecting: a mistake in one interface must not delete every other file the
    /// generator produces.
    /// </remarks>
    [Fact]
    public void PartialInterfaceDecoratedTwice_EmitsOneFilePerInterface()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            [ServiceFactory]
            public partial interface IPartialFactory
            {
                IFoo Create(Kind kind);
            }

            [ServiceFactory]
            public partial interface IPartialFactory { }
            """;

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);

        Assert.Null(result.RawResult.Results.Single().Exception);
        Assert.Equal(
            new[] { "SsalKitSampleServiceCollectionExtensions.g.cs", "TestNs.IPartialFactory.ServiceFactory.g.cs" },
            result.GeneratedSources.Select(s => s.HintName));
    }

    /// <summary>
    /// <c>HintNameSanitizer</c> caps a hint name at 200 characters by keeping its tail, so two
    /// interfaces whose qualified names differ only near the front sanitize to the same name. They
    /// are two genuinely different factories, so both must be emitted -- under names that differ.
    /// </summary>
    [Fact]
    public void FactoriesWhoseHintNamesCollideAfterTruncation_AreBothEmitted()
    {
        var longSegment = new string('x', 200);

        var source = Usings + $$"""
            namespace Aaa.{{longSegment}}
            {
                public enum Kind { A }

                public interface IFoo { }

                [ServiceFactory]
                public interface ILongFactory
                {
                    IFoo Create(Kind kind);
                }
            }

            namespace Bbb.{{longSegment}}
            {
                [ServiceFactory]
                public interface ILongFactory
                {
                    global::Aaa.{{longSegment}}.IFoo Create(global::Aaa.{{longSegment}}.Kind kind);
                }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);

        Assert.Null(result.RawResult.Results.Single().Exception);
        Assert.Equal(3, result.GeneratedSources.Length);
        Assert.Equal(
            result.GeneratedSources.Length,
            result.GeneratedSources.Select(s => s.HintName).Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(result.GetCompilationErrors());
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

        var generated = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly)
            .GetSource("ServiceCollectionExtensions.g.cs");

        var alphaIndex = generated.IndexOf("global::TestNs.IAlphaFactory,", StringComparison.Ordinal);
        var zebraIndex = generated.IndexOf("global::TestNs.IZebraFactory,", StringComparison.Ordinal);

        Assert.True(alphaIndex >= 0 && zebraIndex >= 0);
        Assert.True(alphaIndex < zebraIndex);
    }

    [Fact]
    public void FactoryOnly_NoServiceAttribute_StillEmitsRegistrationMethod()
    {
        var generated = GeneratorTestSupport.RunGenerator(BasicFactory, GeneratorTestSupport.SampleAssembly)
            .GetSource("ServiceCollectionExtensions.g.cs");

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

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);

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

        var generated = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly).GetSingleSource();

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

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);
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
            result.GetSource("ServiceCollectionExtensions.g.cs"),
            StringComparison.Ordinal);
        Assert.Empty(result.GetCompilationErrors());
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

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);
        var registration = result.GetSource("ServiceCollectionExtensions.g.cs");

        Assert.Contains(
            "global::SsalKit.DependencyInjection.Generated.TestNs.Outer.IBarImplementation>();",
            registration,
            StringComparison.Ordinal);
        Assert.Contains(
            "global::SsalKit.DependencyInjection.Generated.TestNs.Outer_IBarImplementation>();",
            registration,
            StringComparison.Ordinal);
        Assert.Empty(result.GetCompilationErrors());
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

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);

        Assert.Contains(
            "namespace SsalKit.DependencyInjection.Generated.TestNs.@class",
            result.GetSource("TestNs._class.IKeywordNestedFactory.ServiceFactory.g.cs"),
            StringComparison.Ordinal);
        Assert.Empty(result.GetCompilationErrors());
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

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);
        var generated = result.GetSource("IRootFactory.ServiceFactory.g.cs");

        Assert.Contains("namespace SsalKit.DependencyInjection.Generated", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace SsalKit.DependencyInjection.Generated.", generated, StringComparison.Ordinal);
        Assert.Contains(
            "services.AddSingleton<global::IRootFactory, global::SsalKit.DependencyInjection.Generated.IRootFactoryImplementation>();",
            result.GetSource("ServiceCollectionExtensions.g.cs"),
            StringComparison.Ordinal);
        Assert.Empty(result.GetCompilationErrors());
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

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);

        Assert.Contains(
            "internal sealed class IInternalFactoryImplementation : global::TestNs.IInternalFactory",
            result.GetSource("TestNs.IInternalFactory.ServiceFactory.g.cs"),
            StringComparison.Ordinal);
        Assert.Empty(result.GetCompilationErrors());
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

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);
        var generated = result.GetSource("TestNs.IKeywordFactory.ServiceFactory.g.cs");

        Assert.Contains("public global::TestNs.IFoo @new(global::TestNs.Kind @class)", generated, StringComparison.Ordinal);
        Assert.Contains("GetRequiredKeyedService<global::TestNs.IFoo>(this._provider, @class);", generated, StringComparison.Ordinal);
        Assert.Empty(result.GetCompilationErrors());
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

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);
        var generated = result.GetSource("TestNs.IShadowingFactory.ServiceFactory.g.cs");

        Assert.Contains(
            "GetRequiredKeyedService<global::TestNs.IFoo>(this._provider, _provider);",
            generated,
            StringComparison.Ordinal);
        Assert.Empty(result.GetCompilationErrors());
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

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);
        var generated = result.GetSource("TestNs.IListFactory.ServiceFactory.g.cs");

        Assert.Contains(
            "public global::System.Collections.Generic.IReadOnlyList<global::TestNs.IFoo> Create(global::TestNs.Kind kind)",
            generated,
            StringComparison.Ordinal);
        Assert.Empty(result.GetCompilationErrors());
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

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);

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

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);
        var registration = result.GetSource("ServiceCollectionExtensions.g.cs");

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

        var result = GeneratorTestSupport.RunGenerator(source, GeneratorTestSupport.SampleAssembly);

        Assert.Equal(3, result.GeneratedSources.Length);
        Assert.Contains(
            "namespace SsalKit.DependencyInjection.Generated.A",
            result.GetSource("A.IFactory.ServiceFactory.g.cs"),
            StringComparison.Ordinal);
        Assert.Contains(
            "namespace SsalKit.DependencyInjection.Generated.B",
            result.GetSource("B.IFactory.ServiceFactory.g.cs"),
            StringComparison.Ordinal);
        Assert.Empty(result.GetCompilationErrors());
    }
}
