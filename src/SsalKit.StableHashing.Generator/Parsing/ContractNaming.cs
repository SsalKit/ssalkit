using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;

namespace SsalKit.StableHashing.Generator.Parsing;

/// <summary>
/// Builds the generated extension class's name and hint name for a contract type, and the
/// <c>global::</c>-qualified reference to that class -- the one piece of naming logic every
/// contract-typed member (its own, or one it recursively holds through a collection/nullable
/// wrapper) needs to agree on, so it is written once here rather than twice.
/// </summary>
internal static class ContractNaming
{
    private const string ExtensionClassSuffix = "StableHashing";
    private const string HintNameSuffix = ".StableHash";

    /// <summary>
    /// Builds <c>Outer_InnerStableHashing</c> for a nested contract type and
    /// <c>PlayerSnapshotStableHashing</c> for a top-level one. Flattening (rather than nesting the
    /// generated class) keeps it a top-level type in the contract's namespace, which is what makes
    /// its extension methods usable without an extra <c>using</c>.
    /// </summary>
    public static string BuildExtensionClassName(INamedTypeSymbol contractType)
    {
        var names = new List<string>();
        for (var current = contractType; current is not null; current = current.ContainingType)
        {
            names.Add(current.Name);
        }

        names.Reverse();
        return CSharpNaming.JoinIdentifierSegments(names) + ExtensionClassSuffix;
    }

    /// <summary>
    /// The <c>global::</c>-qualified name of <paramref name="contractType"/>'s generated extension
    /// class, for a caller (another contract's emitted <c>AppendStableHash</c> body) that needs to
    /// call into it.
    /// </summary>
    public static string BuildExtensionsFqn(INamedTypeSymbol contractType)
    {
        var namespaceName = SymbolFacts.GetContainingNamespaceName(contractType);
        var className = BuildExtensionClassName(contractType);

        return namespaceName.Length == 0 ? "global::" + className : "global::" + namespaceName + "." + className;
    }

    // HintNameSanitizer strips the "global::" qualifier itself, which is what keeps it out of every
    // generated file name; the qualifier carries no information here.
    public static string BuildHintName(string typeFqn) => HintNameSanitizer.Sanitize(typeFqn + HintNameSuffix);
}
