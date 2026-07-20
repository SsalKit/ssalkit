using System.Text;

namespace SsalKit.DependencyInjection.Generator.Emission;

/// <summary>
/// Turns an assembly name into a PascalCase identifier fragment suitable for use in a generated
/// type/method name, e.g. <c>SsalKit.Sample</c> becomes <c>SsalKitSample</c>.
/// </summary>
internal static class AssemblyNameSanitizer
{
    public static string ToPascalCaseIdentifier(string? assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
        {
            return "Assembly";
        }

        var builder = new StringBuilder(assemblyName!.Length);
        var startOfSegment = true;

        foreach (var c in assemblyName)
        {
            if (!char.IsLetterOrDigit(c))
            {
                startOfSegment = true;
                continue;
            }

            builder.Append(startOfSegment ? char.ToUpperInvariant(c) : c);
            startOfSegment = false;
        }

        if (builder.Length == 0)
        {
            return "Assembly";
        }

        // A valid C# identifier cannot start with a digit.
        if (char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }
}
