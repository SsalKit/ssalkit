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

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG001", DiagnosticSeverity.Error);
        Assert.Equal("SsalKit.Guard", diagnostic.Descriptor.Category);
        AssertReportedOnAttribute(diagnostic, source, "ErrorCode<");

        // The offending registration is dropped; the container itself is still generated.
        var generated = result.AssertCompilesCleanly().GetSingleSource();
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

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG002", DiagnosticSeverity.Error);
        Assert.Contains(expectedReason, diagnostic.GetMessage(), StringComparison.Ordinal);
        AssertReportedOnAttribute(diagnostic, source, "ErrorCodes<");
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

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG004", DiagnosticSeverity.Error);
        Assert.Contains(expectedReason, diagnostic.GetMessage(), StringComparison.Ordinal);
        AssertReportedOnAttribute(diagnostic, source, "ExternalErrorCode<");

        // The registration is dropped, and the rest of the container is generated as if it had
        // never been written.
        var generated = result.AssertCompilesCleanly().GetSingleSource();
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

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG005", DiagnosticSeverity.Error);
        Assert.Contains(expectedReason, diagnostic.GetMessage(), StringComparison.Ordinal);
        AssertReportedOnAttribute(diagnostic, source, "ErrorCode<");

        var generated = result.AssertCompilesCleanly().GetSingleSource();
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

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG006", DiagnosticSeverity.Warning);
        Assert.Contains("Game.UserNotFoundException", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Game.GameErrors", diagnostic.GetMessage(), StringComparison.Ordinal);
        AssertReportedOnAttribute(diagnostic, source, "ErrorCode<");

        var generated = result.AssertCompilesCleanly().GetSingleSource();
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

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG007", DiagnosticSeverity.Error);
        AssertReportedOnAttribute(diagnostic, source, "ErrorCodes<");
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

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG008", DiagnosticSeverity.Warning);
        Assert.Contains("Game.GameStatusCode", diagnostic.GetMessage(), StringComparison.Ordinal);
        AssertReportedOnAttribute(diagnostic, source, "ErrorCode<");
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

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG009", DiagnosticSeverity.Error);
        Assert.Contains(expectedReason, diagnostic.GetMessage(), StringComparison.Ordinal);
        AssertReportedOnAttribute(diagnostic, source, "ErrorCode<");

        // Dropping the registration is the point: leaving it in would emit a file naming a type the
        // file cannot see, turning a mistake in the user's code into an error in generated code.
        var generated = result.AssertCompilesCleanly().GetSingleSource();
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

        var diagnostic = DiagnosticAssert.Single(result.Diagnostics, "SSALG009", DiagnosticSeverity.Error);
        Assert.Contains("it is a file-local type", diagnostic.GetMessage(), StringComparison.Ordinal);

        var generated = result.AssertCompilesCleanly().GetSingleSource();
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

        var generated = result.AssertCompilesCleanly().GetSingleSource();
        Assert.Contains("internal static global::Game.Holder.UserNotFoundException UserNotFound", generated, StringComparison.Ordinal);
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

    /// <summary>
    /// The reported span must cover the attribute application the user wrote (the attribute syntax
    /// itself, i.e. without the enclosing brackets), so the squiggle lands on the token they can
    /// delete.
    /// </summary>
    private static void AssertReportedOnAttribute(Diagnostic diagnostic, string source, string expectedPrefix)
    {
        var span = diagnostic.Location.SourceSpan;
        var reportedText = source.Substring(span.Start, span.Length);

        Assert.StartsWith(expectedPrefix, reportedText, StringComparison.Ordinal);
    }
}
