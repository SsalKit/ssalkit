namespace SsalKit.DependencyInjection.Generator.Models;

/// <summary>
/// An equatable, compilation-independent representation of a <c>[Service]</c> attribute's
/// <c>Factory</c> argument, once resolved to a specific, usable, accessible method by
/// <see cref="Parsing.FactoryMethodResolver"/>. <see cref="MethodName"/> holds only the method's
/// simple name (not a symbol), ready to be embedded verbatim into generated source as
/// <c>{ImplementationTypeFqn}.{MethodName}(...)</c>.
/// </summary>
/// <param name="HasFactory">Whether the attribute application specifies a (valid) <c>Factory</c>.</param>
/// <param name="MethodName">The resolved factory method's simple name, or <see langword="null"/> when <see cref="HasFactory"/> is <see langword="false"/>.</param>
/// <param name="AcceptsServiceProvider">
/// Whether the resolved method takes a single <see cref="System.IServiceProvider"/> parameter (as
/// opposed to none). Meaningless when <see cref="HasFactory"/> is <see langword="false"/>.
/// </param>
internal readonly record struct FactoryModel(bool HasFactory, string? MethodName, bool AcceptsServiceProvider)
{
    public static FactoryModel None { get; } = new(false, null, false);
}
