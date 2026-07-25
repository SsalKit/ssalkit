using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Tests.TestSupport;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Analyzer tests for <c>[assembly: RegisterImplementationsOf]</c> validation: SSAL021 (contract is
/// not an interface), SSAL022 (contract matched nothing), SSAL023 (duplicate contract), SSAL024
/// (undefined enum value), SSAL025 (inaccessible contract), and SSAL026 (overlapping contracts that
/// disagree), plus the cases each of them must stay silent for.
/// </summary>
public class RegisterImplementationsOfAnalyzerTests
{
    private const string Usings = """
        using System;
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    [Fact]
    public async Task ValidContractWithMatches_ReportsNothing()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask), ServiceLifetime.Scoped)]

            namespace TestNs;

            public interface IStartupTask { }

            public class MigrateDatabase : IStartupTask { }
            """;

        Assert.Empty(await GeneratorTestSupport.RunAnalyzerAsync(source));
    }

    [Fact]
    public async Task UnboundGenericContractWithMatches_ReportsNothing()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<,>))]

            namespace TestNs;

            public interface IHandler<TRequest, TResponse> { }

            public class Handler : IHandler<int, string> { }
            """;

        Assert.Empty(await GeneratorTestSupport.RunAnalyzerAsync(source));
    }

    [Fact]
    public async Task NoAttributeAtAll_ReportsNothing()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IStartupTask { }

            public class MigrateDatabase : IStartupTask { }
            """;

        Assert.Empty(await GeneratorTestSupport.RunAnalyzerAsync(source));
    }

    [Theory]
    [InlineData("TestNs.SomeClass", "a class")]
    [InlineData("TestNs.SomeStruct", "a struct")]
    [InlineData("TestNs.SomeEnum", "an enum")]
    [InlineData("TestNs.SomeDelegate", "a delegate type")]
    public async Task ContractIsNotAnInterface_ReportsSSAL021(string contract, string expectedDetail)
    {
        var source = Usings + $$"""
            [assembly: RegisterImplementationsOf(typeof({{contract}}))]

            namespace TestNs;

            public class SomeClass { }
            public struct SomeStruct { }
            public enum SomeEnum { A }
            public delegate void SomeDelegate();
            """;

        var diagnostic = DiagnosticAssert.Single(
            await GeneratorTestSupport.RunAnalyzerAsync(source), "SSAL021", DiagnosticSeverity.Error, exclusive: true);
        Assert.Contains(expectedDetail, diagnostic.GetMessage());
    }

    [Fact]
    public async Task ContractIsAnArrayType_ReportsSSAL021()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask[]))]

            namespace TestNs;

            public interface IStartupTask { }
            """;

        var diagnostic = Assert.Single(await GeneratorTestSupport.RunAnalyzerAsync(source));

        Assert.Equal("SSAL021", diagnostic.Id);
        Assert.Contains("not an interface", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ContractIsNull_ReportsSSAL021()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(null)]

            namespace TestNs;

            public interface IStartupTask { }
            """;

        var diagnostic = Assert.Single(await GeneratorTestSupport.RunAnalyzerAsync(source));

        Assert.Equal("SSAL021", diagnostic.Id);
        Assert.Contains("null", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ContractMatchesNothing_ReportsSSAL022()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }
            """;

        var diagnostic = DiagnosticAssert.Single(
            await GeneratorTestSupport.RunAnalyzerAsync(source), "SSAL022", DiagnosticSeverity.Warning, exclusive: true);
        Assert.Contains("global::TestNs.IStartupTask", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ContractMatchedOnlySkippedClasses_ReportsSSAL022()
    {
        // Every silent-skip reason still leaves the contract itself unfulfilled, which is exactly
        // the case SSAL022 exists to make visible.
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }

            public abstract class AbstractTask : IStartupTask { }

            [Service]
            public class ExplicitTask : IStartupTask { }
            """;

        var diagnostic = Assert.Single(await GeneratorTestSupport.RunAnalyzerAsync(source));

        Assert.Equal("SSAL022", diagnostic.Id);
    }

    [Fact]
    public async Task OneContractMatchesAndAnotherDoesNot_ReportsSSAL022OnlyForTheEmptyOne()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]
            [assembly: RegisterImplementationsOf(typeof(TestNs.IUnused))]

            namespace TestNs;

            public interface IStartupTask { }
            public interface IUnused { }

            public class MigrateDatabase : IStartupTask { }
            """;

        var diagnostic = Assert.Single(await GeneratorTestSupport.RunAnalyzerAsync(source));

        Assert.Equal("SSAL022", diagnostic.Id);
        Assert.Contains("global::TestNs.IUnused", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReferencedAssemblyOnlyImplementations_ReportSSAL022()
    {
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

            public class Unrelated { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(
            source, GeneratorTestSupport.Referencing(contractAssembly));

        var diagnostic = Assert.Single(diagnostics);

        Assert.Equal("SSAL022", diagnostic.Id);
        Assert.Contains("never ones in referenced assemblies", diagnostic.GetMessage());
    }

    [Fact]
    public async Task DuplicateContract_ReportsSSAL023OnEveryDeclarationAfterTheFirst()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask), ServiceLifetime.Scoped)]
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask), ServiceLifetime.Transient)]

            namespace TestNs;

            public interface IStartupTask { }

            public class MigrateDatabase : IStartupTask { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d =>
        {
            Assert.Equal("SSAL023", d.Id);
            Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        });
    }

    [Fact]
    public async Task UnboundAndClosedFormsOfTheSameDefinition_AreNotDuplicates()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<>))]
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<string>))]

            namespace TestNs;

            public interface IHandler<T> { }

            public class IntHandler : IHandler<int> { }
            public class StringHandler : IHandler<string> { }
            """;

        Assert.Empty(await GeneratorTestSupport.RunAnalyzerAsync(source));
    }

    [Theory]
    [InlineData("(ServiceLifetime)42", "42", "ServiceLifetime")]
    [InlineData("ServiceLifetime.Singleton, Mode = (RegistrationMode)9", "9", "RegistrationMode")]
    public async Task UndefinedEnumValue_ReportsSSAL024(string arguments, string expectedValue, string expectedEnum)
    {
        var source = Usings + $$"""
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask), {{arguments}})]

            namespace TestNs;

            public interface IStartupTask { }

            public class MigrateDatabase : IStartupTask { }
            """;

        var diagnostic = DiagnosticAssert.Single(
            await GeneratorTestSupport.RunAnalyzerAsync(source), "SSAL024", DiagnosticSeverity.Error, exclusive: true);
        Assert.Contains(expectedValue, diagnostic.GetMessage());
        Assert.Contains(expectedEnum, diagnostic.GetMessage());
    }

    [Fact]
    public async Task FileLocalContract_ReportsSSAL025()
    {
        // Legal to name at the application site (same file), impossible to name from the generated
        // registration code.
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(IFileLocalTask))]

            file interface IFileLocalTask { }

            file class FileLocalTask : IFileLocalTask { }
            """;

        DiagnosticAssert.Single(
            await GeneratorTestSupport.RunAnalyzerAsync(source), "SSAL025", DiagnosticSeverity.Error, exclusive: true);
    }

    [Fact]
    public async Task ClosedContractWithInaccessibleTypeArgument_ReportsSSAL025()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<TestNs.Outer.Hidden>))]

            namespace TestNs;

            public interface IHandler<T> { }

            public class Outer
            {
                private class Hidden { }
            }
            """;

        var diagnostic = Assert.Single(await GeneratorTestSupport.RunAnalyzerAsync(source));

        Assert.Equal("SSAL025", diagnostic.Id);
    }

    [Fact]
    public async Task OverlappingContractsThatAgree_ReportNothing()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<>))]
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<int>))]

            namespace TestNs;

            public interface IHandler<T> { }

            public class IntHandler : IHandler<int> { }
            """;

        Assert.Empty(await GeneratorTestSupport.RunAnalyzerAsync(source));
    }

    [Fact]
    public async Task OverlappingContractsThatDisagree_ReportSSAL026OnEachDeclaration()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<>), ServiceLifetime.Scoped)]
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<int>), ServiceLifetime.Transient)]

            namespace TestNs;

            public interface IHandler<T> { }

            public class IntHandler : IHandler<int> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d =>
        {
            Assert.Equal("SSAL026", d.Id);
            Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
            Assert.Contains("global::TestNs.IntHandler", d.GetMessage());
            Assert.Contains("global::TestNs.IHandler<int>", d.GetMessage());
        });
    }

    [Fact]
    public async Task OverlappingContractsDisagreeingOnModeOnly_ReportSSAL026()
    {
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<>))]
            [assembly: RegisterImplementationsOf(typeof(TestNs.IHandler<int>), Mode = RegistrationMode.Add)]

            namespace TestNs;

            public interface IHandler<T> { }

            public class IntHandler : IHandler<int> { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("SSAL026", d.Id));
    }

    [Fact]
    public async Task InvalidDeclaration_SuppressesTheScanRulesForItselfOnly()
    {
        // The invalid contract reports SSAL021 and takes no further part; the valid one alongside it
        // still reports SSAL022 for matching nothing.
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.SomeClass))]
            [assembly: RegisterImplementationsOf(typeof(TestNs.IUnused))]

            namespace TestNs;

            public class SomeClass { }
            public interface IUnused { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Equal(2, diagnostics.Length);
        Assert.Contains(diagnostics, d => d.Id == "SSAL021");
        Assert.Contains(diagnostics, d => d.Id == "SSAL022");
    }

    [Fact]
    public async Task ConventionScanWithDefaultMode_DoesNotReportSSAL015()
    {
        // TryAddEnumerable is the default precisely so that a many-implementations convention scan
        // is not itself a "conflicting implementations" warning.
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }

            public class TaskA : IStartupTask { }
            public class TaskB : IStartupTask { }
            """;

        Assert.Empty(await GeneratorTestSupport.RunAnalyzerAsync(source));
    }

    [Fact]
    public async Task ConventionScanWithModeAdd_StillDoesNotReportSSAL015()
    {
        // SSAL015 is ServiceAttributeAnalyzer's rule and only ever considers [Service]
        // registrations; a convention scan's own multi-implementation binding is visible in the
        // generated file rather than reported here.
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask), Mode = RegistrationMode.Add)]

            namespace TestNs;

            public interface IStartupTask { }

            public class TaskA : IStartupTask { }
            public class TaskB : IStartupTask { }
            """;

        Assert.Empty(await GeneratorTestSupport.RunAnalyzerAsync(source));
    }

    [Fact]
    public async Task ExplicitServiceAlongsideConventionScan_ReportsSSAL015AsUsual()
    {
        // Two [Service] classes bound to one service type is still SSAL015's business; the
        // convention scan running in the same assembly changes nothing about that.
        const string source = Usings + """
            [assembly: RegisterImplementationsOf(typeof(TestNs.IStartupTask))]

            namespace TestNs;

            public interface IStartupTask { }
            public interface IOther { }

            public class ConventionTask : IStartupTask { }

            [Service]
            public class OtherA : IOther { }

            [Service]
            public class OtherB : IOther { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("SSAL015", d.Id));
    }
}
