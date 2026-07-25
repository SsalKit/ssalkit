using Microsoft.CodeAnalysis;
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

        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostic = Assert.Single(result.SsalgDiagnostics);
        Assert.Equal("SSALG001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("SsalKit.Guard", diagnostic.Descriptor.Category);
        AssertReportedOnAttribute(diagnostic, source, "ErrorCode<");

        // The offending registration is dropped; the container itself is still generated.
        var generated = result.AssertCompilesCleanly();
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

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);

        var diagnostic = Assert.Single(result.SsalgDiagnostics);
        Assert.Equal("SSALG002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
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

        var result = GeneratorTestHelper.RunGenerator(source);

        // No mapping at all for an ambiguous container: there is no winner to pick.
        Assert.Empty(result.GeneratedSources);
        Assert.Equal(2, result.SsalgDiagnostics.Length);
        Assert.All(result.SsalgDiagnostics, diagnostic =>
        {
            Assert.Equal("SSALG003", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Contains("Game.UserNotFoundException", diagnostic.GetMessage(), StringComparison.Ordinal);
            Assert.Contains("Game.GameErrors", diagnostic.GetMessage(), StringComparison.Ordinal);
        });

        // Both registration sites are highlighted, not just the second one.
        Assert.Equal(2, result.SsalgDiagnostics.Select(d => d.Location.SourceSpan.Start).Distinct().Count());
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

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        Assert.Equal(2, result.SsalgDiagnostics.Length);
        Assert.All(result.SsalgDiagnostics, diagnostic => Assert.Equal("SSALG003", diagnostic.Id));
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

        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostic = Assert.Single(result.SsalgDiagnostics);
        Assert.Equal("SSALG004", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains(expectedReason, diagnostic.GetMessage(), StringComparison.Ordinal);
        AssertReportedOnAttribute(diagnostic, source, "ExternalErrorCode<");

        // The registration is dropped, and the rest of the container is generated as if it had
        // never been written.
        var generated = result.AssertCompilesCleanly();
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

        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostic = Assert.Single(result.SsalgDiagnostics);
        Assert.Equal("SSALG005", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains(expectedReason, diagnostic.GetMessage(), StringComparison.Ordinal);
        AssertReportedOnAttribute(diagnostic, source, "ErrorCode<");

        var generated = result.AssertCompilesCleanly();
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

        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostic = Assert.Single(result.SsalgDiagnostics);
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

        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostic = Assert.Single(result.SsalgDiagnostics);
        Assert.Equal("SSALG006", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Game.UserNotFoundException", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Game.GameErrors", diagnostic.GetMessage(), StringComparison.Ordinal);
        AssertReportedOnAttribute(diagnostic, source, "ErrorCode<");

        var generated = result.AssertCompilesCleanly();
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

        var diagnostic = Assert.Single(GeneratorTestHelper.RunGenerator(source).SsalgDiagnostics);

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

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);

        var diagnostic = Assert.Single(result.SsalgDiagnostics);
        Assert.Equal("SSALG007", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
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

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        Assert.Equal("SSALG007", Assert.Single(result.SsalgDiagnostics).Id);
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

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.GeneratedSources);

        var diagnostic = Assert.Single(result.SsalgDiagnostics);
        Assert.Equal("SSALG008", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
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

        var diagnostic = Assert.Single(GeneratorTestHelper.RunGenerator(source).SsalgDiagnostics);

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

        var diagnostic = Assert.Single(GeneratorTestHelper.RunGenerator(source).SsalgDiagnostics);

        Assert.Equal("SSALG002", diagnostic.Id);
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

        var result = GeneratorTestHelper.RunGenerator(source);

        Assert.Empty(result.SsalgDiagnostics);
        Assert.Single(result.GeneratedSources);
        Assert.Empty(result.GetOutputCompilationErrors());
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
