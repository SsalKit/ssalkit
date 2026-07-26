using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit.Testing;
using SsalKit.Guard.Generator.Tests.TestSupport;

namespace SsalKit.Guard.Generator.Tests;

/// <summary>
/// One test per <c>SSALG</c> rule (and per distinct trigger within a rule): the id and severity that
/// get reported, the attribute application they point at, and what survives -- a rule about one
/// registration leaves the rest of the container standing, while a rule about the container itself
/// suppresses its file entirely.
/// </summary>
public class DiagnosticTests
{
    private const string CodeEnum = """
        public enum GameStatusCode
        {
            Unknown = 0,
            UserNotFound = 1001,
            ServerBusy = 2001,
        }
        """;

    [Fact]
    public void SSALG001_ExceptionThatDoesNotDeriveFromErrorCodedException()
    {
        var source = Wrap("""
            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : System.Exception
            {
                public UserNotFoundException(string? message = null) : base(message) { }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG001", DiagnosticSeverity.Error, exclusive: true);
        Assert.Equal("SsalKit.Guard", diagnostic.Descriptor.Category);
        DiagnosticAssert.SpanStartsWith(diagnostic, "ErrorCode<", source);

        // The offending registration is dropped; the container itself is still generated.
        var generated = result.AssertCompilesCleanlyAndGetSource();
        Assert.DoesNotContain("UserNotFoundException", generated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("public static class GameErrors", "not partial")]
    [InlineData("public partial class GameErrors", "not static")]
    [InlineData("public class GameErrors", "not static and not partial")]
    [InlineData("public partial record GameErrors", "not a class and not static")]
    public void SSALG002_ContainerThatIsNotAStaticPartialClass(string declaration, string expectedReason)
    {
        var source = Wrap($$"""
            [ErrorCodes<GameStatusCode>]
            {{declaration}}
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG002", DiagnosticSeverity.Error, exclusive: true);
        Assert.Contains(expectedReason, diagnostic.GetMessage(), StringComparison.Ordinal);
        DiagnosticAssert.SpanStartsWith(diagnostic, "ErrorCodes<", source);
    }

    /// <summary>
    /// A <c>file</c>-local container reports as <c>internal</c> and may perfectly well be
    /// <c>static partial</c>, so it passes every modifier check -- while the generated part, being
    /// another file, would declare a second, unrelated type of the same name. Nothing would fail to
    /// compile; the user's container would just silently stay empty.
    /// </summary>
    [Fact]
    public void SSALG002_FileLocalContainer()
    {
        var source = Wrap("""
            [ErrorCodes<GameStatusCode>]
            file static partial class GameErrors
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG002", DiagnosticSeverity.Error, exclusive: true);
        Assert.Contains("file-local", diagnostic.GetMessage(), StringComparison.Ordinal);
        DiagnosticAssert.SpanStartsWith(diagnostic, "ErrorCodes<", source);
    }

    /// <summary>
    /// The generated file re-declares the whole nesting chain, so every type in it has to be
    /// <c>partial</c> too. Without the rule the user gets CS0260 pointing at generated code.
    /// </summary>
    [Fact]
    public void SSALG002_ContainerNestedInsideANonPartialType()
    {
        var source = Wrap("""
            public static class Outer
            {
                [ErrorCodes<GameStatusCode>]
                public static partial class GameErrors
                {
                }
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG002", DiagnosticSeverity.Error, exclusive: true);
        Assert.Contains("nested inside 'Game.Outer', which is not partial", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The outermost offender is not the one reported: the walk stops at the first containing type
    /// that is not partial, counting from the container outwards.
    /// </summary>
    [Fact]
    public void SSALG002_ReportsTheInnermostNonPartialContainingType()
    {
        var source = Wrap("""
            public partial class Outer
            {
                public class Middle
                {
                    [ErrorCodes<GameStatusCode>]
                    public static partial class GameErrors
                    {
                    }
                }
            }
            """);

        var diagnostic = Assert.Single(GeneratorTestSupport.RunGenerator(source).Diagnostics);

        Assert.Equal("SSALG002", diagnostic.Id);
        Assert.Contains("nested inside 'Game.Outer.Middle', which is not partial", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void SSALG003_SameExceptionRegisteredTwice_IsReportedOnBothSites()
    {
        var source = Wrap("""
            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : ErrorCodedException
            {
                public UserNotFoundException(string? message = null) : base(message) { }
            }

            [ErrorCodes<GameStatusCode>]
            [ExternalErrorCode<GameStatusCode>(typeof(UserNotFoundException), GameStatusCode.ServerBusy)]
            public static partial class GameErrors
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        // No mapping at all for an ambiguous container: there is no winner to pick.
        Assert.Empty(result.GeneratedSources);
        Assert.Equal(2, result.Diagnostics.Length);
        Assert.All(result.Diagnostics, diagnostic =>
        {
            Assert.Equal("SSALG003", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Contains("Game.UserNotFoundException", diagnostic.GetMessage(), StringComparison.Ordinal);
            Assert.Contains("Game.GameErrors", diagnostic.GetMessage(), StringComparison.Ordinal);
        });

        // Both registration sites are highlighted, not just the second one.
        Assert.Equal(2, result.Diagnostics.Select(d => d.Location.SourceSpan.Start).Distinct().Count());
    }

    [Fact]
    public void SSALG003_SameExternalTypeRegisteredTwice()
    {
        var source = Wrap("""
            [ErrorCodes<GameStatusCode>]
            [ExternalErrorCode<GameStatusCode>(typeof(System.TimeoutException), GameStatusCode.ServerBusy)]
            [ExternalErrorCode<GameStatusCode>(typeof(System.TimeoutException), GameStatusCode.Unknown)]
            public static partial class GameErrors
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        Assert.Equal(2, result.Diagnostics.Length);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal("SSALG003", diagnostic.Id));
    }

    [Theory]
    [InlineData("typeof(string)", "does not derive from 'System.Exception'")]
    [InlineData("typeof(GameStatusCode)", "does not derive from 'System.Exception'")]
    [InlineData("typeof(System.Collections.Generic.List<>)", "unbound generic type")]
    public void SSALG004_ExternalRegistrationOfSomethingThatIsNotAnException(
        string typeExpression, string expectedReason)
    {
        var source = Wrap($$"""
            [ErrorCodes<GameStatusCode>]
            [ExternalErrorCode<GameStatusCode>({{typeExpression}}, GameStatusCode.ServerBusy)]
            public static partial class GameErrors
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG004", DiagnosticSeverity.Error, exclusive: true);
        Assert.Contains(expectedReason, diagnostic.GetMessage(), StringComparison.Ordinal);
        DiagnosticAssert.SpanStartsWith(diagnostic, "ExternalErrorCode<", source);

        // The registration is dropped, and the rest of the container is generated as if it had
        // never been written.
        var generated = result.AssertCompilesCleanlyAndGetSource();
        Assert.Contains("MapOrDefault", generated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("public abstract class UserNotFoundException : ErrorCodedException", "abstract")]
    [InlineData("public sealed class UserNotFoundException<T> : ErrorCodedException", "generic")]
    public void SSALG005_ExceptionThatCannotBeNamedOrConstructed(string declaration, string expectedReason)
    {
        var source = Wrap($$"""
            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            {{declaration}}
            {
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG005", DiagnosticSeverity.Error, exclusive: true);
        Assert.Contains(expectedReason, diagnostic.GetMessage(), StringComparison.Ordinal);
        DiagnosticAssert.SpanStartsWith(diagnostic, "ErrorCode<", source);

        var generated = result.AssertCompilesCleanlyAndGetSource();
        Assert.DoesNotContain("UserNotFoundException", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void SSALG005_ExceptionNestedInsideAGenericType()
    {
        var source = Wrap("""
            public static class Wrapper<T>
            {
                [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
                public sealed class UserNotFoundException : ErrorCodedException
                {
                    public UserNotFoundException(string? message = null) : base(message) { }
                }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("SSALG005", diagnostic.Id);
        Assert.Contains("nested inside a generic type", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void SSALG006_ExceptionWithNoRecognisedConstructor_StillTakesPartInTheMapping()
    {
        var source = Wrap("""
            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : ErrorCodedException
            {
                public UserNotFoundException(int userId) : base("user " + userId) { }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG006", DiagnosticSeverity.Warning, exclusive: true);
        Assert.Contains("Game.UserNotFoundException", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Game.GameErrors", diagnostic.GetMessage(), StringComparison.Ordinal);
        DiagnosticAssert.SpanStartsWith(diagnostic, "ErrorCode<", source);

        var generated = result.AssertCompilesCleanlyAndGetSource();
        Assert.Contains("is global::Game.UserNotFoundException", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("ThrowUserNotFound", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void SSALG006_IsReportedForANonPublicConstructor()
    {
        // Only public constructors are mirrored. An internal one is reachable from the generated
        // container -- it is the same assembly -- but mirroring it would turn a constructor the type
        // deliberately kept internal into a public factory.
        var source = Wrap("""
            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : ErrorCodedException
            {
                internal UserNotFoundException(string? message) : base(message) { }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """);

        var diagnostic = Assert.Single(GeneratorTestSupport.RunGenerator(source).Diagnostics);

        Assert.Equal("SSALG006", diagnostic.Id);
    }

    /// <summary>
    /// Only by-value parameters are mirrored. A <c>ref string</c> looks like the message shape to a
    /// check that reads the parameter type alone, and the helper generated from it would pass an
    /// argument by value into a by-reference parameter -- CS1620, inside a file the user cannot
    /// edit.
    /// </summary>
    [Theory]
    [InlineData("ref")]
    [InlineData("in")]
    public void SSALG006_IsReportedForAByReferenceMessageParameter(string refKind)
    {
        var source = Wrap($$"""
            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : ErrorCodedException
            {
                public UserNotFoundException({{refKind}} string message) : base(message) { }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Equal("SSALG006", Assert.Single(result.Diagnostics).Id);

        var generated = result.AssertCompilesCleanlyAndGetSource();
        Assert.Contains("is global::Game.UserNotFoundException", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("ThrowUserNotFound", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same for the inner-exception parameter, which would otherwise widen the mirrored shape
    /// from <c>(string?)</c> to <c>(string?, Exception?)</c>.
    /// </summary>
    [Fact]
    public void ByReferenceInnerExceptionParameter_DoesNotWidenTheMirroredShape()
    {
        var source = Wrap("""
            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : ErrorCodedException
            {
                public UserNotFoundException(string? message) : base(message) { }

                public UserNotFoundException(string? message, ref System.Exception innerException)
                    : base(message, innerException) { }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.Diagnostics);

        var generated = result.AssertCompilesCleanlyAndGetSource();
        Assert.Contains("ThrowUserNotFound(string? message = null)", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("innerException", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void SSALG007_GenericContainer()
    {
        var source = Wrap("""
            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors<T>
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG007", DiagnosticSeverity.Error, exclusive: true);
        DiagnosticAssert.SpanStartsWith(diagnostic, "ErrorCodes<", source);
    }

    [Fact]
    public void SSALG007_ContainerNestedInsideAGenericType()
    {
        var source = Wrap("""
            public partial class Outer<T>
            {
                [ErrorCodes<GameStatusCode>]
                public static partial class GameErrors
                {
                }
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        Assert.Equal("SSALG007", Assert.Single(result.Diagnostics).Id);
    }

    /// <summary>
    /// A code enum nested inside a generic type is refused for the same reason a generic container
    /// is: its display name (<c>Holder&lt;int&gt;.Code</c>) is spliced into the generated
    /// documentation and into a <c>cref</c>, where the angle brackets are XML rather than C# and
    /// would leave the generated file's comments unparseable (CS1570).
    /// </summary>
    [Fact]
    public void SSALG007_CodeEnumNestedInsideAGenericType()
    {
        const string source = """
            using SsalKit.Guard;

            namespace Game;

            public static class Holder<T>
            {
                public enum Code
                {
                    Unknown = 0,
                }
            }

            [ErrorCodes<Holder<int>.Code>]
            public static partial class GameErrors
            {
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG007", DiagnosticSeverity.Error, exclusive: true);
        Assert.Contains(
            "its code enum 'Game.Holder<int>.Code' is nested inside a generic type",
            diagnostic.GetMessage(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A container maps exactly one code enum -- a second <c>[ErrorCodes]</c> on the same class is
    /// CS0579 -- so an <c>[ExternalErrorCode]</c> naming any other enum has no container to join and
    /// is always a typo. It used to be dropped without a word.
    /// </summary>
    [Fact]
    public void SSALG010_ExternalRegistrationForADifferentCodeEnum()
    {
        var source = Wrap("""
            public enum BillingStatusCode
            {
                CardDeclined = 5001,
            }

            [ErrorCodes<GameStatusCode>]
            [ExternalErrorCode<BillingStatusCode>(typeof(System.TimeoutException), BillingStatusCode.CardDeclined)]
            public static partial class GameErrors
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG010", DiagnosticSeverity.Warning, exclusive: true);
        Assert.Contains("Game.BillingStatusCode", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Game.GameErrors", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Game.GameStatusCode", diagnostic.GetMessage(), StringComparison.Ordinal);
        DiagnosticAssert.SpanStartsWith(diagnostic, "ExternalErrorCode<", source);

        // The registration is dropped and the rest of the container is generated as if it had never
        // been written -- in particular, TimeoutException does not end up in the table.
        var generated = result.AssertCompilesCleanlyAndGetSource();
        Assert.DoesNotContain("TimeoutException", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void SSALG008_DecoratedExceptionWithNoContainerAnywhere()
    {
        var source = Wrap("""
            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : ErrorCodedException
            {
                public UserNotFoundException(string? message = null) : base(message) { }
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG008", DiagnosticSeverity.Warning, exclusive: true);
        Assert.Contains("Game.GameStatusCode", diagnostic.GetMessage(), StringComparison.Ordinal);
        DiagnosticAssert.SpanStartsWith(diagnostic, "ErrorCode<", source);
    }

    [Fact]
    public void SSALG008_IsNotReportedWhenAContainerExistsForAnotherCodeEnum_ButOneDoesForThisOne()
    {
        var source = Wrap("""
            public enum BillingStatusCode
            {
                CardDeclined = 5001,
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : ErrorCodedException
            {
                public UserNotFoundException(string? message = null) : base(message) { }
            }

            [ErrorCodes<BillingStatusCode>]
            public static partial class BillingErrors
            {
            }
            """);

        var diagnostic = Assert.Single(GeneratorTestSupport.RunGenerator(source).Diagnostics);

        Assert.Equal("SSALG008", diagnostic.Id);
    }

    /// <summary>
    /// A container that exists but was itself rejected still counts as a container, so the user is
    /// left with the one rule to fix rather than a warning on every exception on top of it.
    /// </summary>
    [Fact]
    public void SSALG008_IsNotReportedWhenTheContainerExistsButIsRejected()
    {
        var source = Wrap("""
            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : ErrorCodedException
            {
                public UserNotFoundException(string? message = null) : base(message) { }
            }

            [ErrorCodes<GameStatusCode>]
            public static class GameErrors
            {
            }
            """);

        var diagnostic = Assert.Single(GeneratorTestSupport.RunGenerator(source).Diagnostics);

        Assert.Equal("SSALG002", diagnostic.Id);
    }

    [Theory]
    [InlineData("private", "it is declared 'private'")]
    [InlineData("protected", "it is declared 'protected'")]
    [InlineData("private protected", "it is declared 'private protected'")]
    public void SSALG009_ExceptionThatTheGeneratedFileCannotName(string accessibility, string expectedReason)
    {
        var source = Wrap($$"""
            public class Holder
            {
                [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
                {{accessibility}} sealed class UserNotFoundException : ErrorCodedException
                {
                    public UserNotFoundException(string? message = null) : base(message) { }
                }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG009", DiagnosticSeverity.Error, exclusive: true);
        Assert.Contains(expectedReason, diagnostic.GetMessage(), StringComparison.Ordinal);
        DiagnosticAssert.SpanStartsWith(diagnostic, "ErrorCode<", source);

        // Dropping the registration is the point: leaving it in would emit a file naming a type the
        // file cannot see, turning a mistake in the user's code into an error in generated code.
        var generated = result.AssertCompilesCleanlyAndGetSource();
        Assert.DoesNotContain("UserNotFoundException", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// The nesting chain is walked, not just the type's own modifier: a public type nested inside a
    /// private one is exactly as unreachable as a private one.
    /// </summary>
    [Fact]
    public void SSALG009_PublicExceptionNestedInsideAPrivateType()
    {
        var source = Wrap("""
            public class Holder
            {
                private static class Inner
                {
                    [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
                    public sealed class UserNotFoundException : ErrorCodedException
                    {
                        public UserNotFoundException(string? message = null) : base(message) { }
                    }
                }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("SSALG009", diagnostic.Id);
        Assert.Contains("nested inside 'Game.Holder.Inner'", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("which is declared 'private'", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A <c>file</c>-local type reports as <see cref="Accessibility.Internal"/>, so it has to be
    /// asked about separately -- it is scoped to one source file, and the generated part is another.
    /// </summary>
    [Fact]
    public void SSALG009_FileLocalException()
    {
        var source = Wrap("""
            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            file sealed class UserNotFoundException : ErrorCodedException
            {
                public UserNotFoundException(string? message = null) : base(message) { }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG009", DiagnosticSeverity.Error, exclusive: true);
        Assert.Contains("it is a file-local type", diagnostic.GetMessage(), StringComparison.Ordinal);

        var generated = result.AssertCompilesCleanlyAndGetSource();
        Assert.DoesNotContain("UserNotFoundException", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// The rule stops at <c>internal</c>: the generated part is another file of the same assembly,
    /// so an internal exception nested in an internal type is perfectly nameable there.
    /// </summary>
    [Fact]
    public void SSALG009_IsNotReportedForAnInternalExceptionNestedInAnInternalType()
    {
        var source = Wrap("""
            internal class Holder
            {
                [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
                internal sealed class UserNotFoundException : ErrorCodedException
                {
                    public UserNotFoundException(string? message = null) : base(message) { }
                }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.Diagnostics);

        var generated = result.AssertCompilesCleanlyAndGetSource();
        Assert.Contains("internal static global::Game.Holder.UserNotFoundException UserNotFound", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// The generator only sees the compilation it runs in, so a container placed in a different
    /// assembly from the <c>[ErrorCode]</c> exceptions collects nothing -- and, before this rule,
    /// said nothing about it either. The result compiles and the mapping is simply always empty,
    /// which is the failure mode this whole library exists to remove.
    /// </summary>
    [Fact]
    public void SSALG011_ContainerForAnotherAssemblysCodeEnumWithNothingRegistered()
    {
        var options = OptionsWithTheDomainAssembly();

        const string source = """
            using SsalKit.Guard;

            namespace Consumer;

            [ErrorCodes<Domain.GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source, options);

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG011", DiagnosticSeverity.Warning, exclusive: true);
        Assert.Contains("Consumer.GameErrors", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Domain.GameStatusCode", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("DomainAssembly", diagnostic.GetMessage(), StringComparison.Ordinal);
        DiagnosticAssert.SpanStartsWith(diagnostic, "ErrorCodes<", source);

        // The warning is about emptiness, not about validity: the container is still generated so
        // the call sites referencing it keep compiling.
        var generated = result.AssertCompilesCleanlyAndGetSource();
        Assert.Contains("MapOrDefault", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("UserNotFoundException", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// The documented way out of the cross-assembly limitation is <c>[ExternalErrorCode]</c>, which
    /// works across assembly boundaries -- so a container that uses it is deliberate and stays
    /// silent.
    /// </summary>
    [Fact]
    public void SSALG011_IsNotReportedWhenTheContainerRegistersSomethingExplicitly()
    {
        var options = OptionsWithTheDomainAssembly();

        const string source = """
            using SsalKit.Guard;

            namespace Consumer;

            [ErrorCodes<Domain.GameStatusCode>]
            [ExternalErrorCode<Domain.GameStatusCode>(typeof(Domain.UserNotFoundException), Domain.GameStatusCode.UserNotFound)]
            public static partial class GameErrors
            {
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source, options);

        Assert.Empty(result.Diagnostics);

        var generated = result.AssertCompilesCleanlyAndGetSource();
        Assert.Contains("is global::Domain.UserNotFoundException", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the rule is about the assembly boundary, not about emptiness on its own: a container for
    /// an enum declared right here is a perfectly ordinary starting point.
    /// </summary>
    [Fact]
    public void SSALG011_IsNotReportedForAnEmptyContainerInTheSameAssembly()
    {
        var source = Wrap("""
            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """);

        Assert.Empty(GeneratorTestSupport.RunGenerator(source).Diagnostics);
    }

    /// <summary>
    /// A separately compiled assembly declaring the code enum and an <c>[ErrorCode]</c> exception the
    /// generator running in the consuming compilation cannot see.
    /// </summary>
    private static GeneratorTestOptions OptionsWithTheDomainAssembly()
    {
        const string domain = """
            using SsalKit.Guard;

            namespace Domain;

            public enum GameStatusCode
            {
                Unknown = 0,
                UserNotFound = 1001,
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : ErrorCodedException
            {
                public UserNotFoundException(string? message = null) : base(message) { }
            }
            """;

        var reference = GeneratorTest.CompileToReference(domain, "DomainAssembly", GeneratorTestSupport.Options);

        return GeneratorTestSupport.Options with { AdditionalReferences = [reference] };
    }

    [Fact]
    public void ValidDeclarations_ReportNothing()
    {
        var source = Wrap("""
            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : ErrorCodedException
            {
                public UserNotFoundException(string? message = null) : base(message) { }
            }

            [ErrorCodes<GameStatusCode>]
            [ExternalErrorCode<GameStatusCode>(typeof(System.TimeoutException), GameStatusCode.ServerBusy)]
            public static partial class GameErrors
            {
            }
            """);

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedSources);
        Assert.Empty(result.GetCompilationErrors());
    }

    private static string Wrap(string declarations) => $$"""
        using SsalKit.Guard;

        namespace Game;

        {{CodeEnum}}

        {{declarations}}
        """;
}
