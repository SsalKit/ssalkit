using SsalKit.Guard.Generator.Tests.TestSupport;

namespace SsalKit.Guard.Generator.Tests;

/// <summary>
/// Full-file snapshot tests for the generated containers, covering the matrix that changes the
/// emitted shape: which constructor the helpers mirror, how the mapping table is ordered, where the
/// registrations come from, the container's accessibility and nesting, and the naming of the
/// helpers.
/// </summary>
/// <remarks>
/// Every case also asserts the generated code actually compiles against the real SsalKit.Guard
/// surface before it is snapshotted, so a snapshot can never be updated to something that merely
/// looks plausible.
/// </remarks>
public class GeneratorSnapshotTests
{
    [Fact]
    public Task SingleContainer_GeneratesMappingAndHelpers()
    {
        const string source = """
            using SsalKit.Guard;

            namespace Game;

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

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanly().GetSingleSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    /// <summary>
    /// The whole point of generating the table: the derived exception is tested before the base one
    /// even though the base is declared first, so a <c>UserNotFoundException</c> can never be
    /// swallowed by its base's code.
    /// </summary>
    [Fact]
    public Task InheritanceChain_TestsTheDerivedTypeFirst()
    {
        const string source = """
            using SsalKit.Guard;

            namespace Game;

            public enum GameStatusCode
            {
                NotFound = 1000,
                UserNotFound = 1001,
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.NotFound)]
            public class NotFoundException : ErrorCodedException
            {
                public NotFoundException(string? message = null) : base(message) { }
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : NotFoundException
            {
                public UserNotFoundException(string? message = null) : base(message) { }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanly().GetSingleSource();

        Assert.True(
            generated.IndexOf("is global::Game.UserNotFoundException", StringComparison.Ordinal)
            < generated.IndexOf("is global::Game.NotFoundException", StringComparison.Ordinal),
            "The derived exception must be tested before its base:" + Environment.NewLine + generated);

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    /// <summary>
    /// External registrations take part in the same ordering as the decorated exceptions and get no
    /// helpers of their own -- this library cannot vouch for the constructor contract of a type it
    /// does not own.
    /// </summary>
    [Fact]
    public Task ExternalRegistrations_MapWithoutHelpers()
    {
        const string source = """
            using System;
            using SsalKit.Guard;

            namespace Game;

            public enum GameStatusCode
            {
                UserNotFound = 1001,
                ServerBusy = 2001,
                Conflict = 3001,
                GuardViolation = 9001,
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : ErrorCodedException
            {
                public UserNotFoundException(string? message = null) : base(message) { }
            }

            [ErrorCodes<GameStatusCode>]
            [ExternalErrorCode<GameStatusCode>(typeof(TimeoutException), GameStatusCode.ServerBusy)]
            [ExternalErrorCode<GameStatusCode>(typeof(ObjectDisposedException), GameStatusCode.Conflict)]
            [ExternalErrorCode<GameStatusCode>(typeof(InvalidOperationException), GameStatusCode.Conflict)]
            [ExternalErrorCode<GameStatusCode>(typeof(GuardViolationException), GameStatusCode.GuardViolation)]
            public static partial class GameErrors
            {
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanly().GetSingleSource();

        Assert.True(
            generated.IndexOf("is global::System.ObjectDisposedException", StringComparison.Ordinal)
            < generated.IndexOf("is global::System.InvalidOperationException", StringComparison.Ordinal),
            "A registered type must be tested before its registered base:" + Environment.NewLine + generated);

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    /// <summary>
    /// One snapshot per recognised constructor shape, in one container: the parameterless form, the
    /// message form, the message-and-inner form, and the widest-wins rule when a type declares
    /// several. The non-nullable parameter case proves the mirrored signature keeps the exception's
    /// own nullability instead of promising a null the constructor would warn about.
    /// </summary>
    [Fact]
    public Task ConstructorShapes_AreMirrored()
    {
        const string source = """
            using System;
            using SsalKit.Guard;

            namespace Game;

            public enum GameStatusCode
            {
                Empty = 1,
                MessageOnly = 2,
                Full = 3,
                Widest = 4,
                Required = 5,
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.Empty)]
            public sealed class EmptyException : ErrorCodedException
            {
                public EmptyException() { }
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.MessageOnly)]
            public sealed class MessageOnlyException : ErrorCodedException
            {
                public MessageOnlyException(string? message) : base(message) { }
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.Full)]
            public sealed class FullException : ErrorCodedException
            {
                public FullException(string? message, Exception? innerException) : base(message, innerException) { }
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.Widest)]
            public sealed class WidestException : ErrorCodedException
            {
                public WidestException() { }

                public WidestException(string? message) : base(message) { }

                public WidestException(string? message, Exception? innerException) : base(message, innerException) { }
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.Required)]
            public sealed class RequiredException : ErrorCodedException
            {
                public RequiredException(string message) : base(message) { }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanly().GetSingleSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    /// <summary>
    /// A public method may not expose a less accessible type, so an internal code enum and an
    /// internal exception force the generated members down with them -- inside a container that is
    /// itself re-declared exactly as the user wrote it.
    /// </summary>
    [Fact]
    public Task InternalContainer_GeneratesInternalMembers()
    {
        const string source = """
            using SsalKit.Guard;

            namespace Game;

            internal enum GameStatusCode
            {
                UserNotFound = 1001,
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            internal sealed class UserNotFoundException : ErrorCodedException
            {
                public UserNotFoundException(string? message = null) : base(message) { }
            }

            [ErrorCodes<GameStatusCode>]
            internal static partial class GameErrors
            {
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanly().GetSingleSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    /// <summary>
    /// Two code enums, two containers, and each one only sees the exceptions declaring its own enum:
    /// the join is on <c>TCode</c>, which is what lets unrelated domains keep unrelated codes.
    /// </summary>
    [Fact]
    public Task MultipleContainers_EachSeeOnlyTheirOwnCodeEnum()
    {
        const string source = """
            using SsalKit.Guard;

            namespace Game;

            public enum GameStatusCode
            {
                UserNotFound = 1001,
            }

            public enum BillingStatusCode
            {
                CardDeclined = 5001,
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : ErrorCodedException
            {
                public UserNotFoundException(string? message = null) : base(message) { }
            }

            [ErrorCode<BillingStatusCode>(BillingStatusCode.CardDeclined)]
            public sealed class CardDeclinedException : ErrorCodedException
            {
                public CardDeclinedException(string? message = null) : base(message) { }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }

            [ErrorCodes<BillingStatusCode>]
            public static partial class BillingErrors
            {
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Equal(2, result.GeneratedSources.Length);

        return Verifier.Verify(result.AssertCompilesCleanly().ToSnapshotText()).UseDirectory("Snapshots");
    }

    /// <summary>
    /// The <c>Exception</c> suffix is trimmed to name the helpers, so an exception that does not
    /// carry one keeps its whole name.
    /// </summary>
    [Fact]
    public Task ExceptionWithoutTheSuffix_KeepsItsWholeName()
    {
        const string source = """
            using SsalKit.Guard;

            namespace Game;

            public enum GameStatusCode
            {
                Kaboom = 1,
                OnlyTheSuffix = 2,
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.Kaboom)]
            public sealed class Kaboom : ErrorCodedException
            {
                public Kaboom(string? message = null) : base(message) { }
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.OnlyTheSuffix)]
            public sealed class Exception : ErrorCodedException
            {
                public Exception(string? message = null) : base(message) { }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanly().GetSingleSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    /// <summary>
    /// A container with nothing registered still gets its members: the call sites that reference
    /// them have to keep compiling while the first registration is being written.
    /// </summary>
    [Fact]
    public Task ContainerWithNoRegistrations_StillGetsItsMembers()
    {
        const string source = """
            using SsalKit.Guard;

            namespace Game;

            public enum GameStatusCode
            {
                Unknown = 0,
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanly().GetSingleSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    /// <summary>
    /// A nested container's generated part reproduces the whole nesting chain, with each containing
    /// type re-declared exactly as accessible as it was written.
    /// </summary>
    [Fact]
    public Task NestedContainer_ReproducesTheNestingChain()
    {
        const string source = """
            using SsalKit.Guard;

            namespace Game;

            public enum GameStatusCode
            {
                UserNotFound = 1001,
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : ErrorCodedException
            {
                public UserNotFoundException(string? message = null) : base(message) { }
            }

            public static partial class Domain
            {
                [ErrorCodes<GameStatusCode>]
                public static partial class GameErrors
                {
                }
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanly().GetSingleSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    [Fact]
    public Task GlobalNamespaceContainer_EmitsWithoutNamespaceBlock()
    {
        const string source = """
            using SsalKit.Guard;

            public enum GameStatusCode
            {
                UserNotFound = 1001,
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : ErrorCodedException
            {
                public UserNotFoundException(string? message = null) : base(message) { }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanly().GetSingleSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    /// <summary>
    /// Two exceptions whose trimmed names collide keep their full type names, so the container never
    /// declares the same helper twice.
    /// </summary>
    [Fact]
    public Task CollidingHelperNames_FallBackToTheWholeTypeName()
    {
        const string source = """
            using SsalKit.Guard;

            namespace Game;

            public enum GameStatusCode
            {
                Trimmed = 1,
                Whole = 2,
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.Trimmed)]
            public sealed class ConflictException : ErrorCodedException
            {
                public ConflictException(string? message = null) : base(message) { }
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.Whole)]
            public sealed class Conflict : ErrorCodedException
            {
                public Conflict(string? message = null) : base(message) { }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanly().GetSingleSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }
}
