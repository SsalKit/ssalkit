using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace SsalKit.Generators.Toolkit.Testing;

/// <summary>
/// Resolves the metadata references a test compilation is built against.
/// </summary>
internal static class ReferenceLoader
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> LazyHostReferences =
        new(static () => LoadTrustedPlatformReferences(AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string));

    /// <summary>
    /// Every reference assembly the current test host trusts, which gives a full and correct BCL
    /// surface without hand-picking individual assemblies or depending on a reference-assembly
    /// package. Loaded once per process.
    /// </summary>
    internal static ImmutableArray<MetadataReference> HostReferences => LazyHostReferences.Value;

    /// <summary>
    /// Parses a <c>TRUSTED_PLATFORM_ASSEMBLIES</c>-shaped path list into metadata references,
    /// skipping entries that are not on disk.
    /// </summary>
    /// <param name="trustedPlatformAssemblies">The
    /// <see cref="Path.PathSeparator"/>-delimited path list.</param>
    /// <returns>One reference per entry that exists on disk.</returns>
    /// <exception cref="GeneratorAssertionException">The list is absent, empty, or names nothing
    /// that exists -- in which case no compilation this harness builds could resolve so much as
    /// <c>System.Object</c>, and every later failure would be a wall of spurious
    /// <c>CS0518</c>/<c>CS0246</c> errors about the BCL rather than about the generator.</exception>
    internal static ImmutableArray<MetadataReference> LoadTrustedPlatformReferences(string? trustedPlatformAssemblies)
    {
        var builder = ImmutableArray.CreateBuilder<MetadataReference>();

        foreach (var path in (trustedPlatformAssemblies ?? string.Empty).Split(Path.PathSeparator))
        {
            if (File.Exists(path))
            {
                builder.Add(MetadataReference.CreateFromFile(path));
            }
        }

        if (builder.Count == 0)
        {
            throw new GeneratorAssertionException(
                "No reference assemblies could be resolved: the test host reported " +
                (string.IsNullOrEmpty(trustedPlatformAssemblies)
                    ? "no TRUSTED_PLATFORM_ASSEMBLIES list at all"
                    : "a TRUSTED_PLATFORM_ASSEMBLIES list none of whose entries exist on disk") +
                ". Every compilation this harness builds would then be missing the BCL, and each of " +
                "them would fail with errors about System.Object rather than about the generator " +
                "under test. This normally means the tests are being run in a host that is not a " +
                "regular .NET test process -- a single-file or trimmed publish, or a custom " +
                "AppDomain/AssemblyLoadContext that does not set the switch.");
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// The host references plus whatever <paramref name="options"/> adds.
    /// </summary>
    internal static ImmutableArray<MetadataReference> Resolve(GeneratorTestOptions options)
    {
        if (options.AdditionalReferences.IsEmpty && options.AdditionalAssemblies.IsEmpty)
        {
            return HostReferences;
        }

        var builder = ImmutableArray.CreateBuilder<MetadataReference>();
        builder.AddRange(HostReferences);
        builder.AddRange(options.AdditionalReferences);

        foreach (var assembly in options.AdditionalAssemblies)
        {
            builder.Add(FromAssembly(assembly));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// A metadata reference to an assembly that is already loaded in the test host.
    /// </summary>
    internal static MetadataReference FromAssembly(Assembly assembly)
    {
        var location = assembly.Location;

        if (location.Length == 0)
        {
            throw new GeneratorAssertionException(
                $"Cannot reference assembly '{assembly.FullName}': it has no file location, so it was most likely " +
                "loaded from memory or published as a single file. Pass a MetadataReference through " +
                $"{nameof(GeneratorTestOptions)}.{nameof(GeneratorTestOptions.AdditionalReferences)} instead.");
        }

        return MetadataReference.CreateFromFile(location);
    }
}
