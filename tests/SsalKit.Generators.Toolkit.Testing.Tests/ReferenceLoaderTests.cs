using System.Reflection;
using Microsoft.CodeAnalysis;

namespace SsalKit.Generators.Toolkit.Testing.Tests;

/// <summary>
/// The reference set every test compilation is built on. Taking it from the test host's own trusted
/// platform assemblies is what gives a full, correct BCL surface without hand-picking assemblies or
/// depending on a reference-assembly package -- but it means the list has to survive entries that
/// are not on disk.
/// </summary>
public class ReferenceLoaderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void LoadTrustedPlatformReferences_WithoutAList_ProducesNoReferences(string? list) =>
        Assert.Empty(ReferenceLoader.LoadTrustedPlatformReferences(list));

    [Fact]
    public void LoadTrustedPlatformReferences_SkipsEntriesThatAreNotOnDisk()
    {
        var real = typeof(object).Assembly.Location;
        var list = string.Join(Path.PathSeparator, "not-a-real-file.dll", string.Empty, real);

        var references = ReferenceLoader.LoadTrustedPlatformReferences(list);

        Assert.Equal(real, Assert.IsAssignableFrom<PortableExecutableReference>(Assert.Single(references)).FilePath);
    }

    [Fact]
    public void HostReferences_AreLoadedOnceAndCoverTheBcl()
    {
        Assert.NotEmpty(ReferenceLoader.HostReferences);
        Assert.Same(ReferenceLoader.HostReferences[0], ReferenceLoader.HostReferences[0]);
    }

    [Fact]
    public void FromAssembly_AnAssemblyWithNoFileLocation_ExplainsWhatToPassInstead()
    {
        var compilation = GeneratorTest.CreateCompilation(
            "public class InMemoryOnly { }", new GeneratorTestOptions { AssemblyName = "InMemoryOnly" });

        using var stream = new MemoryStream();
        Assert.True(compilation.Emit(stream).Success);
        var assembly = Assembly.Load(stream.ToArray());

        var exception = Assert.Throws<GeneratorAssertionException>(() => ReferenceLoader.FromAssembly(assembly));

        Assert.Contains(nameof(GeneratorTestOptions.AdditionalReferences), exception.Message, StringComparison.Ordinal);
    }
}
