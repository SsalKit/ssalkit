using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        var trackedSteps = RunTwice(
            ValidSource,
            compilation => compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText("// unrelated comment", new CSharpParseOptions(LanguageVersion.Latest))));

        AssertEveryStageReused(trackedSteps);
    }

    [Fact]
    public void DiagnosticProducingSource_UnrelatedChange_ReusesEveryCollectedStage()
    {
        // A DiagnosticInfo carries a LocationInfo, not a Location: if it held the real thing, the
        // pipeline would compare two runs' diagnostics by reference and never cache.
        var trackedSteps = RunTwice(
            DiagnosticSource,
            compilation => compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText("// unrelated comment", new CSharpParseOptions(LanguageVersion.Latest))));

        AssertEveryStageReused(trackedSteps);
    }

    [Fact]
    public void UnrelatedMethodAddedToADecoratedException_ReusesTheCollectedAndAnalysedStages()
    {
        // The "Exceptions" transform necessarily re-runs (the target's syntax tree changed), but the
        // candidates it produces are identical, so nothing downstream may recompute.
        var trackedSteps = RunTwice(ValidSource, Replace(ValidSourceWithEditedBody));

        AssertEveryStageReused(trackedSteps);
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
        var trackedSteps = RunTwice(ValidSource, Replace(ValidSourceWithAnExternalRegistration));

        AssertSomeOutputRecomputed(trackedSteps, ErrorCodesGenerator.TrackingNames.Models);
    }

    /// <summary>
    /// The same for the constructor shape, which only reaches the emitter through the exception
    /// candidate: widening the exception's constructor has to widen the generated helpers.
    /// </summary>
    [Fact]
    public void WidenedConstructor_InvalidatesTheEmissionModel()
    {
        var trackedSteps = RunTwice(ValidSource, Replace(ValidSourceWithAWiderConstructor));

        AssertSomeOutputRecomputed(trackedSteps, ErrorCodesGenerator.TrackingNames.Models);
    }

    /// <summary>
    /// And a change that only breaks a rule has to invalidate the diagnostics.
    /// </summary>
    [Fact]
    public void NewlyBrokenRule_InvalidatesTheDiagnostics()
    {
        var trackedSteps = RunTwice(ValidSource, Replace(DiagnosticSource));

        AssertSomeOutputRecomputed(trackedSteps, ErrorCodesGenerator.TrackingNames.Diagnostics);
    }

    private static Func<Compilation, Compilation> Replace(string source) =>
        compilation => compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.Single(),
            CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)));

    private static ImmutableDictionary<string, ImmutableArray<IncrementalGeneratorRunStep>> RunTwice(
        string source, Func<Compilation, Compilation> change)
    {
        var generator = new ErrorCodesGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { generator.AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(change(compilation));

        return driver.GetRunResult().Results.Single().TrackedSteps;
    }

    private static void AssertEveryStageReused(
        ImmutableDictionary<string, ImmutableArray<IncrementalGeneratorRunStep>> trackedSteps)
    {
        AssertAllOutputsCachedOrUnchanged(trackedSteps, ErrorCodesGenerator.TrackingNames.CollectedExceptions);
        AssertAllOutputsCachedOrUnchanged(trackedSteps, ErrorCodesGenerator.TrackingNames.CollectedContainers);
        AssertAllOutputsCachedOrUnchanged(trackedSteps, ErrorCodesGenerator.TrackingNames.Analysis);
        AssertAllOutputsCachedOrUnchanged(trackedSteps, ErrorCodesGenerator.TrackingNames.Models);
        AssertAllOutputsCachedOrUnchanged(trackedSteps, ErrorCodesGenerator.TrackingNames.Diagnostics);
    }

    private static void AssertAllOutputsCachedOrUnchanged(
        ImmutableDictionary<string, ImmutableArray<IncrementalGeneratorRunStep>> trackedSteps,
        string stepName)
    {
        Assert.True(trackedSteps.TryGetValue(stepName, out var steps), $"No tracked steps found for '{stepName}'.");
        Assert.NotEmpty(steps);

        foreach (var step in steps)
        {
            Assert.NotEmpty(step.Outputs);

            foreach (var (_, reason) in step.Outputs)
            {
                Assert.True(
                    reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                    $"Expected step '{stepName}' output reason to be Cached or Unchanged after an unrelated " +
                    $"compilation change, but was '{reason}'.");
            }
        }
    }

    private static void AssertSomeOutputRecomputed(
        ImmutableDictionary<string, ImmutableArray<IncrementalGeneratorRunStep>> trackedSteps,
        string stepName)
    {
        Assert.True(trackedSteps.TryGetValue(stepName, out var steps), $"No tracked steps found for '{stepName}'.");

        var recomputed = steps
            .SelectMany(step => step.Outputs)
            .Any(output => output.Reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New);

        Assert.True(
            recomputed,
            $"Expected at least one '{stepName}' output to be Modified or New after a change the model captures, " +
            "but every output was reused.");
    }
}
