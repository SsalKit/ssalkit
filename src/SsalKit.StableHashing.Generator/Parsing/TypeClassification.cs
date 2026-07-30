using System;
using Microsoft.CodeAnalysis;

namespace SsalKit.StableHashing.Generator.Parsing;

/// <summary>
/// The outcome of classifying a single member's (or a collection element's) type: either the
/// <c>TypeShape</c> <see cref="Emission.StableHashEmitter"/> needs, or the diagnostic that
/// disqualifies it.
/// </summary>
/// <remarks>
/// This is a transient, call-local value -- unlike <c>TypeShape</c> itself, it never enters a
/// cached incremental-pipeline model, so it is free to hold a bare
/// <see cref="DiagnosticDescriptor"/> reference (a shared static singleton, safe anywhere) without
/// the <c>DiagnosticInfo</c>/<c>LocationInfo</c> ceremony a stored value would need.
/// </remarks>
internal readonly struct TypeClassification
{
    private TypeClassification(Models.TypeShape? shape, DiagnosticDescriptor? errorDescriptor, string[] errorArgs)
    {
        Shape = shape;
        ErrorDescriptor = errorDescriptor;
        ErrorArgs = errorArgs;
    }

    /// <summary>The resolved shape, when classification succeeded.</summary>
    public Models.TypeShape? Shape { get; }

    /// <summary>The diagnostic to report, when classification failed.</summary>
    public DiagnosticDescriptor? ErrorDescriptor { get; }

    /// <summary>
    /// Extra message arguments for <see cref="ErrorDescriptor"/>, beyond the member display name
    /// the caller always supplies as the first argument.
    /// </summary>
    public string[] ErrorArgs { get; }

    /// <summary>Whether classification failed.</summary>
    public bool IsError => ErrorDescriptor is not null;

    public static TypeClassification Ok(Models.TypeShape shape) => new(shape, null, Array.Empty<string>());

    public static TypeClassification Error(DiagnosticDescriptor descriptor, params string[] extraArgs) =>
        new(null, descriptor, extraArgs);
}
