using SsalKit.Generators.Toolkit;

namespace SsalKit.DependencyInjection.Generator.Models;

/// <summary>
/// An equatable, compilation-independent representation of all the valid <c>[Service]</c>
/// registrations declared on a single class.
/// </summary>
/// <param name="ImplementationTypeFqn">The fully-qualified (<c>global::</c>-prefixed) name of the implementation class.</param>
/// <param name="Entries">One entry per valid attribute application on the class, in source order.</param>
internal sealed record ClassRegistrationModel(
    string ImplementationTypeFqn,
    EquatableArray<RegistrationEntryModel> Entries);
