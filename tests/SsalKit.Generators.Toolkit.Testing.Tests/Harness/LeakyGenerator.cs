using Microsoft.CodeAnalysis;

namespace SsalKit.Generators.Toolkit.Testing.Tests.Harness;

/// <summary>
/// A generator that is deliberately not incremental: its model is a reference-equal class wrapping
/// the <see cref="Compilation"/> itself, which Roslyn hands out as a new instance on every run, so
/// the stage recomputes even when nothing relevant changed.
/// </summary>
/// <remarks>
/// This is the single most common way a real generator loses its caching, and it is what
/// <see cref="IncrementalAssert.AllCachedOrUnchanged"/> exists to catch -- so the harness needs a
/// generator that reliably fails it.
/// </remarks>
public sealed class LeakyGenerator : IIncrementalGenerator
{
    public static class TrackingNames
    {
        public const string Models = "LeakyModels";
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.CompilationProvider
            .Select(static (compilation, _) => new LeakyModel(compilation))
            .WithTrackingName(TrackingNames.Models);

        context.RegisterSourceOutput(
            models,
            static (context, model) => context.AddSource(
                "Leaky.g.cs", $"// assembly: {model.Compilation.AssemblyName}"));
    }

    private sealed class LeakyModel(Compilation compilation)
    {
        public Compilation Compilation { get; } = compilation;
    }
}
