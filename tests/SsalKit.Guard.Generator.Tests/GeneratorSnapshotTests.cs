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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    /// <summary>
    /// Every name the generated file will declare is reserved before any helper is named: the two
    /// mapping methods, the container's own name -- a member may not share it (CS0542) -- and, for
    /// each exception, the factory and the throw helper as a pair.
    /// </summary>
    /// <remarks>
    /// The pair is what makes <c>FooException</c> and <c>ThrowFooException</c> work. Their factory
    /// names differ (<c>Foo</c> and <c>ThrowFoo</c>), so a check that only looked at those would let
    /// both through -- and then emit <c>ThrowFoo</c> twice, once as the first one's throw helper and
    /// once as the second one's factory (CS0111).
    /// </remarks>
    [Fact]
    public Task NamesTheGeneratedFileAlreadyUses_ArePushedToTheFallbackName()
    {
        const string source = """
            using SsalKit.Guard;

            namespace Game;

            public enum GameStatusCode
            {
                Foo = 1,
                ThrowFoo = 2,
                Container = 3,
                Lookup = 4,
                Fallback = 5,
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.Foo)]
            public sealed class FooException : ErrorCodedException
            {
                public FooException(string? message = null) : base(message) { }
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.ThrowFoo)]
            public sealed class ThrowFooException : ErrorCodedException
            {
                public ThrowFooException(string? message = null) : base(message) { }
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.Container)]
            public sealed class GameErrorsException : ErrorCodedException
            {
                public GameErrorsException(string? message = null) : base(message) { }
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.Lookup)]
            public sealed class TryMapException : ErrorCodedException
            {
                public TryMapException(string? message = null) : base(message) { }
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.Fallback)]
            public sealed class MapOrDefaultException : ErrorCodedException
            {
                public MapOrDefaultException(string? message = null) : base(message) { }
            }

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }
            """;

        // AssertCompilesCleanly is the assertion that matters here: every one of these collisions
        // used to produce a generated file the compiler rejected.
        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }

    /// <summary>
    /// The last resort of the naming walk: when even the flattened fully qualified name is taken --
    /// <c>Game.Sub.Conflict</c> and a global-namespace <c>Game_Sub_Conflict</c> flatten to the same
    /// identifier -- underscores are appended until the pair is free.
    /// </summary>
    [Fact]
    public Task ExhaustedHelperNames_FallBackToASuffixedName()
    {
        const string source = """
            using SsalKit.Guard;

            [ErrorCode<Game.GameStatusCode>(Game.GameStatusCode.Flattened)]
            public sealed class Game_Sub_Conflict : ErrorCodedException
            {
                public Game_Sub_Conflict(string? message = null) : base(message) { }
            }

            namespace Game
            {
                public enum GameStatusCode
                {
                    Trimmed = 1,
                    Nested = 2,
                    Flattened = 3,
                }

                [ErrorCode<GameStatusCode>(GameStatusCode.Trimmed)]
                public sealed class Conflict : ErrorCodedException
                {
                    public Conflict(string? message = null) : base(message) { }
                }

                [ErrorCodes<GameStatusCode>]
                public static partial class GameErrors
                {
                }
            }

            namespace Game.Sub
            {
                [ErrorCode<GameStatusCode>(GameStatusCode.Nested)]
                public sealed class Conflict : ErrorCodedException
                {
                    public Conflict(string? message = null) : base(message) { }
                }
            }
            """;

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        Assert.Contains("Game_Sub_Conflict_(", generated, StringComparison.Ordinal);

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

        var generated = GeneratorTestSupport.RunGenerator(source).AssertCompilesCleanlyAndGetSource();

        return Verifier.Verify(generated).UseDirectory("Snapshots");
    }
}
