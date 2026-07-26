using Microsoft.CodeAnalysis;

namespace SsalKit.Generators.Toolkit.Testing.Tests.Harness;

/// <summary>
/// A generator that throws from its source-output stage, which Roslyn catches and records on
/// <see cref="GeneratorRunResult.Exception"/> (plus a <c>CS8785</c> <em>warning</em>) instead of
/// letting it escape.
/// </summary>
/// <remarks>
/// This is the failure mode the harness has to refuse to hand back: a crashed run produces no
/// files, reports none of the generator's own diagnostics, and leaves a compilation that still
/// compiles cleanly -- so <c>AssertNoGeneratedSources</c>, <c>DiagnosticAssert.None</c> and
/// <c>AssertCompilesCleanly</c> all pass, every one of them for the wrong reason.
/// </remarks>
public sealed class ThrowingGenerator : IIncrementalGenerator
{
    public const string FailureMessage = "ThrowingGenerator failed on purpose.";

    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        context.RegisterSourceOutput(
            context.CompilationProvider,
            static (_, _) => throw new InvalidOperationException(FailureMessage));
}
