namespace SsalKit.DependencyInjection.Generator.Models;

/// <summary>
/// An equatable, compilation-independent representation of one valid <c>[ServiceFactory]</c>
/// interface: everything the emitter needs to write its implementation class, and the registration
/// line that binds the two together.
/// </summary>
/// <remarks>
/// Every component is a <see cref="string"/>, so the record's value equality is exactly what the
/// incremental pipeline needs: no <c>ISymbol</c>, syntax node, or <c>Compilation</c> is retained.
/// </remarks>
/// <param name="InterfaceTypeFqn">
/// The fully-qualified (<c>global::</c>-prefixed) name of the decorated interface. Doubles as the
/// service type the generated implementation is registered against, and as the ordering key that
/// keeps the emitted registrations deterministic.
/// </param>
/// <param name="ImplementationNamespace">
/// The namespace the implementation class is emitted into: the reserved
/// <c>SsalKit.DependencyInjection.Generated</c> root, with the interface's own namespace and
/// containing-type chain appended as further segments, so two interfaces can only produce the same
/// generated name if their own qualified names were identical.
/// </param>
/// <param name="ImplementationTypeName">
/// The implementation class's simple name: the interface's own name suffixed with
/// <c>Implementation</c>.
/// </param>
/// <param name="ImplementationTypeFqn">
/// The fully-qualified name of the implementation class, i.e. <see cref="ImplementationNamespace"/>
/// and <see cref="ImplementationTypeName"/> combined. Precomputed here so the registration emitter
/// never has to reassemble it.
/// </param>
/// <param name="MethodName">
/// The factory method's name, already keyword-escaped, ready to be emitted as a declaration.
/// </param>
/// <param name="ParameterName">
/// The factory method's single parameter name, already keyword-escaped. Reused verbatim in the
/// generated override so the implementation reads like the interface it implements.
/// </param>
/// <param name="ParameterTypeFqn">The fully-qualified name of the enum type used as the service key.</param>
/// <param name="ReturnTypeFqn">
/// The fully-qualified name of the resolved service type, used both as the generated method's
/// return type and as the type argument to <c>GetRequiredKeyedService</c>.
/// </param>
/// <param name="HintName">
/// The <c>AddSource</c> hint name, derived from <see cref="InterfaceTypeFqn"/> and therefore
/// unique across the compilation.
/// </param>
internal sealed record ServiceFactoryModel(
    string InterfaceTypeFqn,
    string ImplementationNamespace,
    string ImplementationTypeName,
    string ImplementationTypeFqn,
    string MethodName,
    string ParameterName,
    string ParameterTypeFqn,
    string ReturnTypeFqn,
    string HintName);
