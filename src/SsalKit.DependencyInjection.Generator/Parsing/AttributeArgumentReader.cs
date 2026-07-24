using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Models;

namespace SsalKit.DependencyInjection.Generator.Parsing;

/// <summary>
/// Reads the constructor/named arguments of a <c>[Service]</c> attribute application. Shared
/// between <see cref="ServiceAttributeParser"/> and <c>ServiceAttributeAnalyzer</c> so both agree
/// on exactly how each argument is resolved (including compiler-supplied defaults for omitted
/// optional arguments).
/// </summary>
internal static class AttributeArgumentReader
{
    public static int GetLifetime(AttributeData attributeData)
    {
        var constructorArguments = attributeData.ConstructorArguments;
        if (constructorArguments.Length > 0 && constructorArguments[0].Value is int lifetimeValue)
        {
            return lifetimeValue;
        }

        return (int)WellKnownLifetime.Singleton;
    }

    public static int GetMode(AttributeData attributeData)
    {
        foreach (var namedArgument in attributeData.NamedArguments)
        {
            if (namedArgument.Key == "Mode" && namedArgument.Value.Value is int modeValue)
            {
                return modeValue;
            }
        }

        return (int)WellKnownRegistrationMode.Add;
    }

    public static ITypeSymbol? GetAsType(AttributeData attributeData)
    {
        foreach (var namedArgument in attributeData.NamedArguments)
        {
            if (namedArgument.Key == "As" && namedArgument.Value.Value is ITypeSymbol typeSymbol)
            {
                return typeSymbol;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the raw <c>Key</c> constant, or <see langword="null"/> if the argument was not
    /// supplied at all (as opposed to being explicitly set to <see langword="null"/>).
    /// </summary>
    public static TypedConstant? GetKeyConstant(AttributeData attributeData)
    {
        foreach (var namedArgument in attributeData.NamedArguments)
        {
            if (namedArgument.Key == "Key")
            {
                return namedArgument.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the <c>Factory</c> named-argument string, or <see langword="null"/> when it was not
    /// supplied, or was supplied but explicitly set to <see langword="null"/> -- both mean "no
    /// factory", so callers need not distinguish them.
    /// </summary>
    public static string? GetFactoryName(AttributeData attributeData)
    {
        foreach (var namedArgument in attributeData.NamedArguments)
        {
            if (namedArgument.Key == "Factory" && namedArgument.Value.Value is string factoryName)
            {
                return factoryName;
            }
        }

        return null;
    }
}
