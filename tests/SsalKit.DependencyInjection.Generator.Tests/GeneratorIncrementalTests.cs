using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SsalKit.DependencyInjection.Generator.Tests.TestSupport;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Verifies that <see cref="ServiceRegistrationGenerator"/>'s incremental pipeline actually
/// caches: an unrelated compilation change (a new syntax tree with no <c>[Service]</c>
/// attributes) must not force the class-registration stages to recompute, which relies on
/// <c>EquatableArray{T}</c>/record value-equality on the models flowing through the pipeline.
/// </summary>
public class GeneratorIncrementalTests
{
    private const string Source = """
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNs;

        public interface IFoo { }

        [Service]
        public class Foo : IFoo { }
        """;

    private const string OpenGenericSource = """
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNs;

        public interface IRepo<T> { }

        [Service]
        public class Repo<T> : IRepo<T> { }
        """;

    private const string ServiceFactorySource = """
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNs;

        public enum PaymentMethod { Card, Bank }

        public interface IPaymentProcessor { }

        [ServiceFactory]
        public interface IPaymentProcessorFactory
        {
            IPaymentProcessor Create(PaymentMethod method);
        }
        """;

    private const string ConventionSource = """
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

        namespace TestNs;

        public interface IStartupTask { }

        public class MigrateDatabase : IStartupTask { }
        """;

    private const string FactorySourceOriginalBody = """
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNs;

        public interface IFoo { }

        [Service(Factory = nameof(Foo.Create))]
        public class Foo : IFoo
        {
            public static Foo Create() => new Foo();
        }
        """;

    private const string FactorySourceEditedBody = """
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNs;

        public interface IFoo { }

        [Service(Factory = nameof(Foo.Create))]
        public class Foo : IFoo
        {
            public static Foo Create()
            {
                // Body changed; the signature -- and therefore the resolved FactoryModel -- is
                // identical, so this must not invalidate the pipeline's collected/combined output.
                return new Foo();
            }
        }
        """;

    [Fact]
    public void UnrelatedSyntaxTreeAddition_ReusesCollectedClassesAndCombinedSteps()
    {
        var (_, second) = RunWithUnrelatedSyntaxTreeAdded(Source);

        IncrementalAssert.AllCachedOrUnchanged(
            second,
            ServiceRegistrationGenerator.TrackingNames.CollectedClasses,
            ServiceRegistrationGenerator.TrackingNames.Combined);
    }

    [Fact]
    public void OpenGenericClass_UnrelatedSyntaxTreeAddition_ReusesCollectedClassesAndCombinedSteps()
    {
        // The open generic model (IsOpenGeneric flag, typeof-form FQN strings) must be just as
        // cacheable as the closed-class model: it is still strings/primitives only, so
        // EquatableArray/record value-equality should let the pipeline skip recomputation exactly
        // as it does for a closed class.
        var (_, second) = RunWithUnrelatedSyntaxTreeAdded(OpenGenericSource);

        IncrementalAssert.AllCachedOrUnchanged(
            second,
            ServiceRegistrationGenerator.TrackingNames.CollectedClasses,
            ServiceRegistrationGenerator.TrackingNames.Combined);
    }

    [Fact]
    public void FactoryMethodBodyEdit_ReusesCollectedClassesAndCombinedSteps()
    {
        // Editing the factory method's *body* (not its signature) must still re-run the "Classes"
        // transform for this class -- the target syntax node changed -- but the resulting
        // FactoryModel (method name + AcceptsServiceProvider, both primitives) is unaffected by a
        // body-only edit, so record equality lets downstream stages skip recomputation entirely.
        var (_, second) = GeneratorTest.RunTwice<ServiceRegistrationGenerator>(
            FactorySourceOriginalBody,
            _ => FactorySourceEditedBody,
            GeneratorTestSupport.Options);

        IncrementalAssert.AllCachedOrUnchanged(
            second,
            ServiceRegistrationGenerator.TrackingNames.CollectedClasses,
            ServiceRegistrationGenerator.TrackingNames.Combined);
    }

    [Fact]
    public void ServiceFactory_UnrelatedSyntaxTreeAdditionReusesCollectedFactoriesAndCombinedSteps()
    {
        var (_, second) = RunWithUnrelatedSyntaxTreeAdded(ServiceFactorySource);

        IncrementalAssert.AllCachedOrUnchanged(
            second,
            ServiceRegistrationGenerator.TrackingNames.CollectedFactories,
            ServiceRegistrationGenerator.TrackingNames.Combined);
    }

    [Fact]
    public void ServiceFactory_UnrelatedServiceClassEdit_ReusesCollectedFactoriesStep()
    {
        // The factory implementation files come off CollectedFactories alone, so adding a new
        // [Service] class -- which necessarily re-runs CollectedClasses and Combined -- must leave
        // every already-emitted factory file untouched.
        var (_, second) = GeneratorTest.RunTwiceWithCompilationChange<ServiceRegistrationGenerator>(
            ServiceFactorySource,
            compilation => compilation.AddSyntaxTrees(Parse(
                """
                using SsalKit.DependencyInjection;

                namespace TestNs;

                public interface IUnrelated { }

                [Service]
                public class Unrelated : IUnrelated { }
                """)),
            GeneratorTestSupport.Options);

        IncrementalAssert.AllCachedOrUnchanged(
            second, ServiceRegistrationGenerator.TrackingNames.CollectedFactories);
    }

    [Fact]
    public void ConventionScan_UnrelatedSyntaxTreeAddition_LeavesConventionsAndCombinedUnchanged()
    {
        // The convention scan is the one stage that cannot be driven by a per-node syntax provider
        // -- which classes a contract matches is a property of the whole compilation -- so its
        // input (CompilationProvider) is Modified by any edit at all and the scan itself always
        // re-runs. What must hold is that its *output* is value-equal when nothing it looked at
        // changed, so the combine/emit stages downstream are still skipped.
        var (_, second) = RunWithUnrelatedSyntaxTreeAdded(ConventionSource);

        IncrementalAssert.AllCachedOrUnchanged(
            second,
            ServiceRegistrationGenerator.TrackingNames.Conventions,
            ServiceRegistrationGenerator.TrackingNames.Combined);
    }

    [Fact]
    public void NoConventionDeclared_UnrelatedSyntaxTreeAddition_LeavesConventionsAndCombinedUnchanged()
    {
        // The fast path every assembly that does not use the feature takes: the scan returns an
        // empty (and therefore always equal) array, so adding it to the pipeline cannot cost an
        // existing consumer a single regenerated file.
        var (_, second) = RunWithUnrelatedSyntaxTreeAdded(Source);

        IncrementalAssert.AllCachedOrUnchanged(
            second,
            ServiceRegistrationGenerator.TrackingNames.Conventions,
            ServiceRegistrationGenerator.TrackingNames.Combined);
    }

    [Fact]
    public void ConventionScan_NewMatchingClass_RecomputesConventionsAndRegeneratesTheSameWay()
    {
        // The complement of the caching tests: when the scan's result genuinely does change, the
        // change must actually flow through -- a convention registration for a class added in a
        // brand new syntax tree, which no per-node provider of this generator ever saw.
        var (first, second) = GeneratorTest.RunTwiceWithCompilationChange<ServiceRegistrationGenerator>(
            ConventionSource,
            compilation => compilation.AddSyntaxTrees(Parse(
                """
                namespace TestNs;

                public class WarmCaches : IStartupTask { }
                """)),
            GeneratorTestSupport.Options);

        Assert.DoesNotContain("WarmCaches", first.GetSingleSource());
        Assert.Contains(
            "services.TryAddEnumerable(global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<global::TestNs.IStartupTask, global::TestNs.WarmCaches>());",
            second.GetSingleSource());
    }

    /// <summary>
    /// The change every caching test above makes: a brand new syntax tree with nothing the
    /// generator looks at in it.
    /// </summary>
    private static (GeneratorTestResult First, GeneratorTestResult Second) RunWithUnrelatedSyntaxTreeAdded(
        string source) =>
        GeneratorTest.RunTwiceWithCompilationChange<ServiceRegistrationGenerator>(
            source,
            compilation => compilation.AddSyntaxTrees(Parse("// unrelated comment")),
            GeneratorTestSupport.Options);

    private static SyntaxTree Parse(string source) =>
        CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
}
