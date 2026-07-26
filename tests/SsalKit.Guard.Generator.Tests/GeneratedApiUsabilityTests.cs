using Microsoft.CodeAnalysis;
using SsalKit.Guard.Generator.Tests.TestSupport;

namespace SsalKit.Guard.Generator.Tests;

/// <summary>
/// Compiles call sites written against the generated container in the same compilation the
/// generator runs on. Unlike the snapshot tests -- which only prove the generated file itself
/// type-checks -- these prove the emitted members are actually reachable and bindable the way the
/// design promises: a factory usable directly in a <c>throw</c> expression, a throw helper the
/// compiler's flow analysis believes, and a lookup that hands back the code enum itself.
/// </summary>
public class GeneratedApiUsabilityTests
{
    [Fact]
    public void EveryGeneratedMemberBindsFromACallSite()
    {
        const string source = """
            using System;
            using SsalKit.Guard;

            namespace Game;

            public enum GameStatusCode
            {
                Unknown = 0,
                UserNotFound = 1001,
                ServerBusy = 2001,
            }

            [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
            public sealed class UserNotFoundException : ErrorCodedException
            {
                public UserNotFoundException(string? message = null, Exception? innerException = null)
                    : base(message, innerException) { }
            }

            [ErrorCodes<GameStatusCode>]
            [ExternalErrorCode<GameStatusCode>(typeof(TimeoutException), GameStatusCode.ServerBusy)]
            public static partial class GameErrors
            {
            }

            public static class Consumer
            {
                public static void Throwing() => throw GameErrors.UserNotFound("no such user");

                public static void ThrowingWithInner(Exception cause) =>
                    throw GameErrors.UserNotFound("no such user", cause);

                public static void ThrowingWithoutArguments() => throw GameErrors.UserNotFound();

                public static void ThrowingThroughTheHelper() => GameErrors.ThrowUserNotFound("no such user");

                public static GameStatusCode Map(Exception exception)
                {
                    if (GameErrors.TryMap(exception, out GameStatusCode code))
                    {
                        return code;
                    }

                    return GameErrors.MapOrDefault(exception, GameStatusCode.Unknown);
                }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.GetCompilationErrors());
    }

    /// <summary>
    /// The lookup documents a null reference as matching nothing, so its parameter has to accept
    /// one: <c>exception.InnerException</c> at a boundary is nullable, and a non-nullable parameter
    /// would warn (CS8604) for doing exactly what the documentation promises.
    /// </summary>
    [Fact]
    public void Lookup_AcceptsANullableException()
    {
        const string source = """
            using System;
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

            public static class Consumer
            {
                public static GameStatusCode MapTheCause(Exception exception) =>
                    GameErrors.MapOrDefault(exception.InnerException, GameStatusCode.Unknown);

                public static bool CauseIsMapped(Exception exception) =>
                    GameErrors.TryMap(exception.InnerException, out _);
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GetCompilationErrors());
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Id is "CS8604"));
    }

    /// <summary>
    /// The throw helper carries <c>[DoesNotReturn]</c>, so the compiler's nullable flow analysis
    /// treats the path through it as ended -- which is the whole reason to prefer it over
    /// <c>throw Factory(...)</c> in a guard-style early exit.
    /// </summary>
    [Fact]
    public void ThrowHelper_EndsTheFlowForNullableAnalysis()
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

            [ErrorCodes<GameStatusCode>]
            public static partial class GameErrors
            {
            }

            public static class Consumer
            {
                public static string RequireName(string? name)
                {
                    if (name is null)
                    {
                        GameErrors.ThrowUserNotFound("the user has no name");
                    }

                    // Reachable only when 'name' is non-null, which the compiler knows only because
                    // the helper above is marked [DoesNotReturn].
                    return name;
                }
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.GetCompilationErrors());
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Id is "CS8603" or "CS8602"));
    }

    [Fact]
    public void InternalContainer_BindsFromTheSameAssembly()
    {
        const string source = """
            using System;
            using SsalKit.Guard;

            namespace Game;

            internal enum GameStatusCode
            {
                Unknown = 0,
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

            internal static class Consumer
            {
                public static GameStatusCode Map(Exception exception) =>
                    GameErrors.MapOrDefault(exception, GameStatusCode.Unknown);

                public static void Throwing() => throw GameErrors.UserNotFound();
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.GetCompilationErrors());
    }

    /// <summary>
    /// A container the user has already written members into keeps them: the generated part adds to
    /// the class rather than replacing it.
    /// </summary>
    [Fact]
    public void HandWrittenMembersOnTheContainer_CoexistWithTheGeneratedOnes()
    {
        const string source = """
            using System;
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
                public static int ToTransportCode(Exception exception) => (int)MapOrDefault(exception, GameStatusCode.Unknown);
            }
            """;

        var result = GeneratorTestSupport.RunGenerator(source);

        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.GetCompilationErrors());
    }
}
