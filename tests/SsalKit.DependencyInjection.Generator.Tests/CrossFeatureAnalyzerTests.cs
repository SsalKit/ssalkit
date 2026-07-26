using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Tests.TestSupport;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Analyzer tests for the two rules that span <c>[Service]</c> and the convention scan: SSAL027
/// (both bind the same service type, and the emission order decides the winner) and SSAL028 (a
/// registered class the container cannot activate).
/// </summary>
public class CrossFeatureAnalyzerTests
{
    private const string Usings = """
        using System;
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    /// <summary>
    /// Carrying <c>[Service]</c> excludes <em>that class</em> from the scan, which is a
    /// scan-exclusion rule and not a resolution-priority one: the contract still matches every other
    /// implementation, and the convention block is emitted after the <c>[Service]</c> block, so
    /// last-registration-wins hands the resolution to the convention.
    /// </summary>
    [Theory]
    [InlineData("RegistrationMode.Add")]
    [InlineData("RegistrationMode.TryAdd")]
    [InlineData("RegistrationMode.Replace")]
    public async Task SSAL027_ConventionCompetesWithAnExplicitRegistration_ReportsWarning(string mode)
    {
        var source = Usings + $$"""
            [assembly: RegisterImplementationsOf(typeof(TestNs.IClock), Mode = {{mode}})]

            namespace TestNs;

            public interface IClock { }

            [Service]
            public sealed class ExplicitClock : IClock { }

            public sealed class ConventionClock : IClock { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = DiagnosticAssert.Single(diagnostics, "SSAL027", DiagnosticSeverity.Warning, exclusive: true);
        Assert.Contains("global::TestNs.IClock", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'global::TestNs.IClock'", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The default mode is exempt: <c>TryAddEnumerable</c> is additive by construction, and having
    /// an explicit registration alongside a scanned one is the ordinary way to give one
    /// implementation a lifetime of its own while the rest come from the rule.
    /// </summary>
    [Fact]
    public async Task SSAL027_TryAddEnumerableConvention_ReportsNothing()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }

            [Service(ServiceLifetime.Transient)]
            public sealed class PersistStep : IStartupTask { }

            public sealed class WarmCaches : IStartupTask { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// A convention registration is never keyed, so a keyed explicit registration is resolved
    /// through a different lookup entirely and the two cannot shadow one another.
    /// </summary>
    [Fact]
    public async Task SSAL027_KeyedExplicitRegistration_ReportsNothing()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IClock), Mode = RegistrationMode.TryAdd)]

            namespace TestNs;

            public interface IClock { }

            [Service(Key = "utc")]
            public sealed class ExplicitClock : IClock { }

            public sealed class ConventionClock : IClock { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// A contract that matches nothing but the excluded class registers nothing, so there is no
    /// convention registration to compete -- SSAL022 is the whole story there.
    /// </summary>
    [Fact]
    public async Task SSAL027_ContractMatchingOnlyTheExcludedClass_ReportsOnlySSAL022()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IClock), Mode = RegistrationMode.TryAdd)]

            namespace TestNs;

            public interface IClock { }

            [Service]
            public sealed class ExplicitClock : IClock { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        DiagnosticAssert.Single(diagnostics, "SSAL022", DiagnosticSeverity.Warning, exclusive: true);
    }

    /// <summary>
    /// Every contract that contributes is named, so the message points at all the places the
    /// conflict can be resolved.
    /// </summary>
    [Fact]
    public async Task SSAL027_TwoOverlappingContracts_AreBothNamed()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<>), Mode = RegistrationMode.TryAdd)]
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<int>), Mode = RegistrationMode.Add)]

            namespace TestNs;

            public interface IHandler<T> { }

            [Service]
            public sealed class ExplicitHandler : IHandler<int> { }

            public sealed class ConventionHandler : IHandler<int> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var ssal027 = Assert.Single(diagnostics.Where(diagnostic => diagnostic.Id == "SSAL027"));
        Assert.Contains("'global::TestNs.IHandler<>'", ssal027.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'global::TestNs.IHandler<int>'", ssal027.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SSAL028_ServiceClassWithOnlyANonPublicConstructor_ReportsWarning()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public sealed class Foo : IFoo
            {
                private Foo() { }
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = DiagnosticAssert.Single(diagnostics, "SSAL028", DiagnosticSeverity.Warning, exclusive: true);
        Assert.Contains("global::TestNs.Foo", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("internal Foo() { }")]
    [InlineData("protected Foo() { }")]
    [InlineData("private protected Foo() { }")]
    public async Task SSAL028_EveryNonPublicAccessibility_ReportsWarning(string constructor)
    {
        var source = Usings + $$"""
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public class Foo : IFoo
            {
                {{constructor}}
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        DiagnosticAssert.Single(diagnostics, "SSAL028", DiagnosticSeverity.Warning, exclusive: true);
    }

    /// <summary>
    /// The generated code calls the factory method and never a constructor, so the class's own
    /// constructors are irrelevant -- which is exactly why the <c>Factory</c> feature exists.
    /// </summary>
    [Fact]
    public async Task SSAL028_FactoryRegistration_ReportsNothing()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            [Service(Factory = nameof(Create))]
            public sealed class Foo : IFoo
            {
                private Foo() { }

                public static Foo Create() => new();
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// A class with an implicit (public) parameterless constructor, or an explicit public one
    /// alongside non-public overloads, is activatable and must stay silent.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("public Foo() { }")]
    [InlineData("private Foo(int x) { } public Foo() { }")]
    public async Task SSAL028_ActivatableClass_ReportsNothing(string constructors)
    {
        var source = Usings + $$"""
            namespace TestNs;

            public interface IFoo { }

            [Service]
            public sealed class Foo : IFoo
            {
                {{constructors}}
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// The rule is identical for an open generic registration: the container substitutes the type
    /// arguments and then activates through a public constructor just the same.
    /// </summary>
    [Fact]
    public async Task SSAL028_OpenGenericClass_ReportsWarning()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IRepository<T> { }

            [Service]
            public sealed class Repository<T> : IRepository<T>
            {
                private Repository() { }
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        DiagnosticAssert.Single(diagnostics, "SSAL028", DiagnosticSeverity.Warning, exclusive: true);
    }

    /// <summary>
    /// A convention-scanned class is registered exactly as an explicit one is, so it is subject to
    /// the same activation rule -- reported once for the class, at its declaration, since there is
    /// no attribute to point at.
    /// </summary>
    [Fact]
    public async Task SSAL028_ConventionMatchedClass_ReportsWarningOncePerClass()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]
            [assembly: RegisterImplementationsOf(typeof(TestNs.IOtherContract))]

            namespace TestNs;

            public interface IStartupTask { }

            public interface IOtherContract { }

            public sealed class WarmCaches : IStartupTask, IOtherContract
            {
                private WarmCaches() { }
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = DiagnosticAssert.Single(diagnostics, "SSAL028", DiagnosticSeverity.Warning, exclusive: true);
        Assert.Contains("global::TestNs.WarmCaches", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A class no contract matched is not registered by anything, so its constructors are nobody's
    /// business.
    /// </summary>
    [Fact]
    public async Task SSAL028_UnmatchedClass_ReportsNothing()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }

            public sealed class WarmCaches : IStartupTask { }

            public sealed class Unrelated
            {
                private Unrelated() { }
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Empty(diagnostics);
    }
}
