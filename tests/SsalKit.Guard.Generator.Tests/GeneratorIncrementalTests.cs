using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SsalKit.Generators.Toolkit.Testing;
using SsalKit.Guard.Generator.Tests.TestSupport;

namespace SsalKit.Guard.Generator.Tests;

/// <summary>
/// Verifies that <see cref="ErrorCodesGenerator"/>'s pipeline actually caches: an unrelated
/// compilation change must not force the collected/analysed stages to recompute, which relies on
/// every model that flows through the pipeline being value-equal across runs (records over
/// primitives, <c>EquatableArray&lt;T&gt;</c> for collections, and <c>LocationInfo</c> instead of a
/// <see cref="Location"/> that would pin a syntax tree).
/// </summary>
public class GeneratorIncrementalTests
{
    // The container is declared before the exceptions on purpose: the tests below edit an
    // exception's body, and everything a candidate carries -- including the LocationInfo of an
    // attribute -- has to be genuinely unchanged for the caching assertions to mean anything.
    private const string ValidSource = """
        using SsalKit.Guard;

        namespace Game;

        public enum GameStatusCode
        {
            UserNotFound = 1001,
            ServerBusy = 2001,
        }

        [ErrorCodes<GameStatusCode>]
        public static partial class GameErrors
        {
        }

        [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
        public sealed class UserNotFoundException : ErrorCodedException
        {
            public UserNotFoundException(string? message = null) : base(message) { }
        }
        """;

    private const string DiagnosticSource = """
        using SsalKit.Guard;

        namespace Game;

        public enum GameStatusCode
        {
            UserNotFound = 1001,
        }

        [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
        public sealed class UserNotFoundException : System.Exception
        {
            public UserNotFoundException(string? message = null) : base(message) { }
        }

        [ErrorCodes<GameStatusCode>]
        public static partial class GameErrors
        {
        }
        """;

    private const string ValidSourceWithAnExternalRegistration = """
        using SsalKit.Guard;

        namespace Game;

        public enum GameStatusCode
        {
            UserNotFound = 1001,
            ServerBusy = 2001,
        }

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
        """;

    private const string ValidSourceWithAWiderConstructor = """
        using SsalKit.Guard;

        namespace Game;

        public enum GameStatusCode
        {
            UserNotFound = 1001,
            ServerBusy = 2001,
        }

        [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
        public sealed class UserNotFoundException : ErrorCodedException
        {
            public UserNotFoundException(string? message = null) : base(message) { }

            public UserNotFoundException(string? message, System.Exception? innerException)
                : base(message, innerException) { }
        }

        [ErrorCodes<GameStatusCode>]
        public static partial class GameErrors
        {
        }
        """;

    private const string ValidSourceWithEditedBody = """
        using SsalKit.Guard;

        namespace Game;

        public enum GameStatusCode
        {
            UserNotFound = 1001,
            ServerBusy = 2001,
        }

        [ErrorCodes<GameStatusCode>]
        public static partial class GameErrors
        {
        }

        [ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
        public sealed class UserNotFoundException : ErrorCodedException
        {
            public UserNotFoundException(string? message = null) : base(message) { }

            public string Describe()
            {
                // Body added; nothing the model captures changed, so every downstream stage must
                // still be able to reuse its previous output.
                return Message;
            }
        }
        """;

    [Fact]
    public void UnrelatedSyntaxTreeAddition_ReusesEveryCollectedStage()
    {
        var (_, second) = RunWithUnrelatedSyntaxTreeAdded(ValidSource);

        AssertEveryStageReused(second);
    }

    [Fact]
    public void DiagnosticProducingSource_UnrelatedChange_ReusesEveryCollectedStage()
    {
        // A DiagnosticInfo carries a LocationInfo, not a Location: if it held the real thing, the
        // pipeline would compare two runs' diagnostics by reference and never cache.
        var (_, second) = RunWithUnrelatedSyntaxTreeAdded(DiagnosticSource);

        AssertEveryStageReused(second);
    }

    [Fact]
    public void UnrelatedMethodAddedToADecoratedException_ReusesTheCollectedAndAnalysedStages()
    {
        // The "Exceptions" transform necessarily re-runs (the target's syntax tree changed), but the
        // candidates it produces are identical, so nothing downstream may recompute.
        var (_, second) = RunTwice(ValidSource, ValidSourceWithEditedBody);

        AssertEveryStageReused(second);
    }

    /// <summary>
    /// The other side of the caching contract: a change the model <em>does</em> capture has to
    /// invalidate it. An added <c>[ExternalErrorCode]</c> only reaches the emitter through the
    /// container model, so if the registrations were left out of it, the new row would never appear
    /// in the generated table.
    /// </summary>
    [Fact]
    public void AddedExternalRegistration_InvalidatesTheEmissionModel()
    {
        var (_, second) = RunTwice(ValidSource, ValidSourceWithAnExternalRegistration);

        IncrementalAssert.SomeOutputRecomputed(second, ErrorCodesGenerator.TrackingNames.Models);
    }

    /// <summary>
    /// The same for the constructor shape, which only reaches the emitter through the exception
    /// candidate: widening the exception's constructor has to widen the generated helpers.
    /// </summary>
    [Fact]
    public void WidenedConstructor_InvalidatesTheEmissionModel()
    {
        var (_, second) = RunTwice(ValidSource, ValidSourceWithAWiderConstructor);

        IncrementalAssert.SomeOutputRecomputed(second, ErrorCodesGenerator.TrackingNames.Models);
    }

    /// <summary>
    /// And a change that only breaks a rule has to invalidate the diagnostics.
    /// </summary>
    [Fact]
    public void NewlyBrokenRule_InvalidatesTheDiagnostics()
    {
        var (_, second) = RunTwice(ValidSource, DiagnosticSource);

        IncrementalAssert.SomeOutputRecomputed(second, ErrorCodesGenerator.TrackingNames.Diagnostics);
    }

    private static (GeneratorTestResult First, GeneratorTestResult Second) RunTwice(
        string source, string editedSource) =>
        GeneratorTest.RunTwice<ErrorCodesGenerator>(source, _ => editedSource, GeneratorTestSupport.Options);

    private static (GeneratorTestResult First, GeneratorTestResult Second) RunWithUnrelatedSyntaxTreeAdded(
        string source) =>
        GeneratorTest.RunTwiceWithCompilationChange<ErrorCodesGenerator>(
            source,
            compilation => compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText("// unrelated comment", new CSharpParseOptions(LanguageVersion.Latest))),
            GeneratorTestSupport.Options);

    private static void AssertEveryStageReused(GeneratorTestResult secondRun) =>
        IncrementalAssert.AllCachedOrUnchanged(
            secondRun,
            ErrorCodesGenerator.TrackingNames.CollectedExceptions,
            ErrorCodesGenerator.TrackingNames.CollectedContainers,
            ErrorCodesGenerator.TrackingNames.Analysis,
            ErrorCodesGenerator.TrackingNames.Models,
            ErrorCodesGenerator.TrackingNames.Diagnostics);
}
