using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Tests.TestSupport;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Analyzer tests for <c>[ServiceFactory]</c> validation: SSAL016 (non-interface target),
/// SSAL017 (member shape), SSAL018 (method signature), SSAL019 (generic interface), and
/// SSAL020 (inaccessible type), plus that a valid factory reports nothing.
/// </summary>
public class ServiceFactoryAnalyzerTests
{
    private const string Usings = """
        using System;
        using SsalKit.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection;

        """;

    [Fact]
    public async Task ValidFactory_ReportsNothing()
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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task UndecoratedInterface_OfAnyShape_ReportsNothing()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface INotAFactory
            {
                int Value { get; }

                void DoSomething(string a, string b);
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// SSAL016 is normally pre-empted by CS0592 (<c>[AttributeUsage(AttributeTargets.Interface)]</c>),
    /// so this source is deliberately invalid C#. The attribute is still bound onto the class
    /// symbol, and <see cref="GeneratorTestSupport.RunAnalyzerAsync"/> filters the compiler's own
    /// diagnostics out, which lets the defensive rule be exercised for real.
    /// </summary>
    [Theory]
    [InlineData("public class Target { }", "a class")]
    [InlineData("public struct Target { }", "a struct")]
    [InlineData("public enum Target { A }", "an enum")]
    [InlineData("public delegate void Target();", "a delegate")]
    public async Task SSAL016_NonInterfaceTarget_ReportsError(string declaration, string expectedReason)
    {
        var source = Usings + $$"""
            namespace TestNs;

            [ServiceFactory]
            {{declaration}}
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = DiagnosticAssert.Single(diagnostics, "SSAL016", DiagnosticSeverity.Error, exclusive: true);
        Assert.Contains($"because it is {expectedReason}", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SSAL017_NoMembers_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            [ServiceFactory]
            public interface IEmptyFactory { }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = DiagnosticAssert.Single(diagnostics, "SSAL017", DiagnosticSeverity.Error, exclusive: true);
        Assert.Contains("it declares no members", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SSAL017_TwoMethods_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            [ServiceFactory]
            public interface ITwoMethodFactory
            {
                IFoo Create(Kind kind);

                IFoo CreateOther(Kind kind);
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL017", diagnostic.Id);
        Assert.Contains("it declares 2 members", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A property contributes both an <c>IPropertySymbol</c> and a <c>get_</c> accessor method to
    /// <c>GetMembers()</c>; the count in the message must still read as the one member the author
    /// actually wrote.
    /// </summary>
    [Fact]
    public async Task SSAL017_MethodPlusProperty_CountsPropertyOnce()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            [ServiceFactory]
            public interface IMixedFactory
            {
                IFoo Create(Kind kind);

                int Count { get; }
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL017", diagnostic.Id);
        Assert.Contains("it declares 2 members", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("int Count { get; }", "Count")]
    [InlineData("event Action Changed;", "Changed")]
    [InlineData("static int Shared => 1;", "Shared")]
    [InlineData("public static int Create(Kind kind) => 0;", "Create")]
    public async Task SSAL017_SoleMemberIsNotAnOrdinaryInstanceMethod_ReportsError(string member, string expectedName)
    {
        var source = Usings + $$"""
            namespace TestNs;

            public enum Kind { A }

            [ServiceFactory]
            public interface ISoleMemberFactory
            {
                {{member}}
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL017", diagnostic.Id);
        Assert.Contains(
            $"its only member '{expectedName}' is not an ordinary, non-static method",
            diagnostic.GetMessage(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A nested type imposes no implementation burden, but it is still a declared member: the rule
    /// is "exactly one member", not "exactly one implementable member".
    /// </summary>
    [Fact]
    public async Task SSAL017_NestedType_CountsAsAMember()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            [ServiceFactory]
            public interface INestedTypeFactory
            {
                IFoo Create(Kind kind);

                public sealed class Options { }
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL017", diagnostic.Id);
        Assert.Contains("it declares 2 members", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("IFoo Create<T>(Kind kind);", "it is generic")]
    [InlineData("IFoo Create();", "it has no parameters")]
    [InlineData("IFoo Create(Kind kind, int extra);", "it has 2 parameters")]
    [InlineData("IFoo Create(ref Kind kind);", "its parameter 'kind' is passed by reference")]
    [InlineData("IFoo Create(in Kind kind);", "its parameter 'kind' is passed by reference")]
    [InlineData("IFoo Create(out Kind kind);", "its parameter 'kind' is passed by reference")]
    [InlineData("IFoo Create(string name);", "its parameter 'name' is of type 'string', which is not an enum type")]
    [InlineData("IFoo Create(Kind? kind);", "which is not an enum type")]
    [InlineData("void Create(Kind kind);", "it returns void")]
    public async Task SSAL018_UnusableSignature_ReportsError(string method, string expectedReason)
    {
        var source = Usings + $$"""
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            [ServiceFactory]
            public interface IBadSignatureFactory
            {
                {{method}}
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = DiagnosticAssert.Single(diagnostics, "SSAL018", DiagnosticSeverity.Error, exclusive: true);
        Assert.Contains(expectedReason, diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("global::TestNs.IBadSignatureFactory.Create", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SSAL018_ReturnsByRef_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            [ServiceFactory]
            public interface IByRefFactory
            {
                ref int Create(Kind kind);
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL018", diagnostic.Id);
        Assert.Contains("it returns by reference", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SSAL019_GenericInterface_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            [ServiceFactory]
            public interface IGenericFactory<T>
            {
                IFoo Create(Kind kind);
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = DiagnosticAssert.Single(diagnostics, "SSAL019", DiagnosticSeverity.Error, exclusive: true);
        Assert.Contains("because it is generic", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SSAL019_NestedInsideGenericType_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            public class Outer<T>
            {
                [ServiceFactory]
                public interface INestedFactory
                {
                    IFoo Create(Kind kind);
                }
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL019", diagnostic.Id);
        Assert.Contains("because it is nested inside a generic type", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SSAL020_PrivateNestedFactoryInterface_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public interface IFoo { }

            public class Holder
            {
                [ServiceFactory]
                private interface IHiddenFactory
                {
                    IFoo Create(Kind kind);
                }
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = DiagnosticAssert.Single(diagnostics, "SSAL020", DiagnosticSeverity.Error, exclusive: true);
        Assert.Contains("global::TestNs.Holder.IHiddenFactory", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SSAL020_PrivateNestedEnumKey_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public interface IFoo { }

            public class Holder
            {
                private enum HiddenKind { A }

                [ServiceFactory]
                internal interface IFactory
                {
                    IFoo Create(HiddenKind kind);
                }
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL020", diagnostic.Id);
        Assert.Contains("global::TestNs.Holder.HiddenKind", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SSAL020_PrivateNestedReturnType_ReportsError()
    {
        const string source = Usings + """
            namespace TestNs;

            public enum Kind { A }

            public class Holder
            {
                private interface IHiddenService { }

                [ServiceFactory]
                internal interface IFactory
                {
                    IHiddenService Create(Kind kind);
                }
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL020", diagnostic.Id);
        Assert.Contains("global::TestNs.Holder.IHiddenService", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SSAL020_FileLocalReturnType_ReportsError()
    {
        const string source = Usings + """
            file interface IHiddenService { }

            public enum Kind { A }

            [ServiceFactory]
            internal interface IFactory
            {
                IHiddenService Create(Kind kind);
            }
            """;

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL020", diagnostic.Id);
    }

    /// <summary>
    /// One invalid factory must not suppress a second, valid one, nor a <c>[Service]</c>
    /// registration in the same compilation.
    /// </summary>
    [Fact]
    public async Task InvalidFactory_DoesNotAffectOtherDeclarations()
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

        var diagnostics = await GeneratorTestSupport.RunAnalyzerAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSAL017", diagnostic.Id);
        Assert.Contains("IBadFactory", diagnostic.GetMessage(), StringComparison.Ordinal);
    }
}
