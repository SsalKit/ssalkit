using Microsoft.CodeAnalysis;

namespace SsalKit.Generators.Toolkit.Testing.Tests.Harness;

/// <summary>
/// A generator whose output parses but does not type-check, so the driver happily adds it to the
/// compilation and only <see cref="GeneratorTestResult.AssertCompilesCleanly"/> notices.
/// </summary>
/// <remarks>
/// This is the failure mode a snapshot test cannot catch on its own: the snapshot merely records
/// whatever the generator emitted, so without a recompile the emitted text can be updated to
/// something that looks plausible and does not build.
/// </remarks>
public sealed class BrokenOutputGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        context.RegisterSourceOutput(
            context.CompilationProvider,
            static (context, _) => context.AddSource(
                "Broken.g.cs",
                """
                public static class Broken
                {
                    public static int Value => "not an int";
                }
                """));
}
