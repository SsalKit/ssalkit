using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SsalKit.Generators.Toolkit.Tests;

/// <summary>
/// Direct unit tests for <see cref="AttributeLocations"/>, covering all three answers: the
/// attribute's own syntax, the decorated symbol's first location, and
/// <see cref="Location.None"/>.
/// </summary>
public class AttributeLocationsTests
{
    private const string AttributeDeclaration = """
        namespace Sample;

        public class MarkerAttribute : System.Attribute { }
        """;

    [Fact]
    public void GetLocation_ForAnAttributeWrittenInSource_ReturnsTheAttributeApplication()
    {
        const string source = AttributeDeclaration + """

            [Marker]
            public class Decorated { }
            """;

        var compilation = CreateCompilation(source, "Decorated.cs");
        var type = compilation.GetTypeByMetadataName("Sample.Decorated")!;
        var attribute = type.GetAttributes().Single();

        var location = AttributeLocations.GetLocation(attribute, type);

        Assert.Equal("Decorated.cs", location.SourceTree!.FilePath);
        Assert.Equal("Marker", location.SourceTree.GetText().ToString(location.SourceSpan));
    }

    /// <summary>
    /// An attribute read from metadata has no <see cref="AttributeData.ApplicationSyntaxReference"/>
    /// at all, so the decorated symbol is the only thing left to point at.
    /// </summary>
    [Fact]
    public void GetLocation_ForAnAttributeFromMetadata_FallsBackToTheDecoratedSymbol()
    {
        var reference = CompileToReference(AttributeDeclaration + """

            [Marker]
            public class DecoratedInMetadata { }
            """);

        var compilation = CreateCompilation("public class Consumer { }", "Consumer.cs", reference);
        var type = compilation.GetTypeByMetadataName("Sample.DecoratedInMetadata")!;
        var attribute = type.GetAttributes().Single();

        Assert.Null(attribute.ApplicationSyntaxReference);

        var location = AttributeLocations.GetLocation(attribute, type);

        Assert.Same(type.Locations[0], location);
        Assert.Null(location.SourceTree);
    }

    /// <summary>
    /// A synthesized symbol -- an array type, here -- has no locations at all, which is the only
    /// way the last fallback is reached.
    /// </summary>
    [Fact]
    public void GetLocation_WhenTheFallbackSymbolHasNoLocations_ReturnsNone()
    {
        var reference = CompileToReference(AttributeDeclaration + """

            [Marker]
            public class DecoratedInMetadata { }
            """);

        var compilation = CreateCompilation("public class Consumer { }", "Consumer.cs", reference);
        var attribute = compilation.GetTypeByMetadataName("Sample.DecoratedInMetadata")!.GetAttributes().Single();
        var locationless = compilation.CreateArrayTypeSymbol(compilation.ObjectType);

        Assert.Empty(locationless.Locations);
        Assert.Same(Location.None, AttributeLocations.GetLocation(attribute, locationless));
    }

    /// <summary>
    /// The location is built from the syntax reference's tree and span rather than by materializing
    /// the attribute node, so the two spellings must still agree exactly.
    /// </summary>
    [Fact]
    public void GetLocation_MatchesWhatMaterializingTheAttributeNodeWouldHaveGiven()
    {
        const string source = AttributeDeclaration + """

            [Marker]
            public class Decorated { }
            """;

        var compilation = CreateCompilation(source, "Decorated.cs");
        var attribute = compilation.GetTypeByMetadataName("Sample.Decorated")!.GetAttributes().Single();

        var fromReference = AttributeLocations.GetLocation(attribute, compilation.ObjectType);
        var fromNode = attribute.ApplicationSyntaxReference!.GetSyntax().GetLocation();

        Assert.Equal(fromNode, fromReference);
    }

    [Fact]
    public void GetLocationInfo_ForAnAttributeWrittenInSource_ProjectsTheAttributeApplication()
    {
        const string source = AttributeDeclaration + """

            [Marker]
            public class Decorated { }
            """;

        var compilation = CreateCompilation(source, "Decorated.cs");
        var type = compilation.GetTypeByMetadataName("Sample.Decorated")!;
        var attribute = type.GetAttributes().Single();

        var info = AttributeLocations.GetLocationInfo(attribute, type);

        Assert.NotNull(info);
        Assert.Equal("Decorated.cs", info.FilePath);
        Assert.Equal(AttributeLocations.GetLocation(attribute, type).SourceSpan, info.TextSpan);
    }

    /// <summary>
    /// A location that is not in source has no projection, which is exactly the <c>null</c>
    /// <c>Diagnostic.Create</c> accepts as "report without a position" -- so the pipeline value needs
    /// no special case for it.
    /// </summary>
    [Fact]
    public void GetLocationInfo_WhenThereIsNoSourceLocationToProject_IsNull()
    {
        var reference = CompileToReference(AttributeDeclaration + """

            [Marker]
            public class DecoratedInMetadata { }
            """);

        var compilation = CreateCompilation("public class Consumer { }", "Consumer.cs", reference);
        var type = compilation.GetTypeByMetadataName("Sample.DecoratedInMetadata")!;
        var attribute = type.GetAttributes().Single();

        Assert.Null(AttributeLocations.GetLocationInfo(attribute, type));
    }

    private static MetadataReference CompileToReference(string source)
    {
        var compilation = CreateCompilation(source, "Referenced.cs");

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);

        Assert.True(result.Success, string.Join("\n", result.Diagnostics));

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static CSharpCompilation CreateCompilation(string source, string path, params MetadataReference[] extraReferences) =>
        CSharpCompilation.Create(
            "AttributeLocationsTests",
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest), path) },
            ReferenceAssemblies().Concat(extraReferences),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static IEnumerable<MetadataReference> ReferenceAssemblies() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(path => path.Length > 0)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));
}
