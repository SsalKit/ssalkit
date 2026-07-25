namespace SsalKit.Generators.Toolkit.Testing.Tests.Harness;

/// <summary>
/// The value-equal model <see cref="MiniGenerator"/> flows through its pipeline: strings and a
/// <see cref="MiniLocation"/>, so two runs over equivalent source produce equal models and the
/// downstream stages can be cached.
/// </summary>
public sealed record MiniModel(
    string Namespace,
    string TypeName,
    string Greeting,
    string? DiagnosticId,
    MiniLocation Location);
