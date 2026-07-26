namespace SsalKit.Generators.Toolkit.Testing;

/// <summary>
/// One file a generator added to the compilation: the hint name it was registered under and the
/// source text it produced.
/// </summary>
/// <param name="HintName">
/// The hint name the generator passed to <c>SourceProductionContext.AddSource</c>, including its
/// extension (Roslyn appends <c>.cs</c> when the generator omits it).
/// </param>
/// <param name="Text">The generated source text.</param>
public readonly record struct GeneratedSource(string HintName, string Text);
