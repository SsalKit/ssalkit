using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Parsing;
using SsalKit.DependencyInjection.Generator.Tests.TestSupport;
using SsalKit.Generators.Toolkit.Testing;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Unit tests for <see cref="OpenGenericTypeofFormatter"/> in isolation, using
/// <see cref="Compilation.GetTypeByMetadataName(string)"/> to fetch symbols directly rather than
/// running the full generator/analyzer pipeline.
/// </summary>
public class OpenGenericTypeofFormatterTests
{
    private const string Source = """
        namespace Ns;

        public interface IRepo<T> { }
        public interface IThing<A, B> { }
        public interface IBig<A, B, C> { }

        public class Outer
        {
            public interface IInner<T> { }
        }
        """;

    [Theory]
    [InlineData("Ns.IRepo`1", "global::Ns.IRepo<>")]
    [InlineData("Ns.IThing`2", "global::Ns.IThing<,>")]
    [InlineData("Ns.IBig`3", "global::Ns.IBig<,,>")]
    public void Format_RendersArityPlaceholder(string metadataName, string expected)
    {
        var compilation = GeneratorTest.CreateCompilation(Source, GeneratorTestSupport.Options);
        var symbol = compilation.GetTypeByMetadataName(metadataName);

        Assert.NotNull(symbol);
        Assert.Equal(expected, OpenGenericTypeofFormatter.Format(symbol!));
    }

    [Fact]
    public void Format_NestedInsideNonGenericType_IncludesContainingTypeName()
    {
        var compilation = GeneratorTest.CreateCompilation(Source, GeneratorTestSupport.Options);
        var symbol = compilation.GetTypeByMetadataName("Ns.Outer+IInner`1");

        Assert.NotNull(symbol);
        Assert.Equal("global::Ns.Outer.IInner<>", OpenGenericTypeofFormatter.Format(symbol!));
    }

    [Fact]
    public void Format_NamespaceLessType_OmitsNamespaceSegment()
    {
        const string source = "public interface IFoo<T> { }";

        var compilation = GeneratorTest.CreateCompilation(source, GeneratorTestSupport.Options);
        var symbol = compilation.GetTypeByMetadataName("IFoo`1");

        Assert.NotNull(symbol);
        Assert.Equal("global::IFoo<>", OpenGenericTypeofFormatter.Format(symbol!));
    }

    [Fact]
    public void Format_NonGenericType_RendersNoArityPlaceholder()
    {
        const string source = """
            namespace Ns;

            public class Foo { }
            """;

        var compilation = GeneratorTest.CreateCompilation(source, GeneratorTestSupport.Options);
        var symbol = compilation.GetTypeByMetadataName("Ns.Foo");

        Assert.NotNull(symbol);
        Assert.Equal("global::Ns.Foo", OpenGenericTypeofFormatter.Format(symbol!));
    }
}
