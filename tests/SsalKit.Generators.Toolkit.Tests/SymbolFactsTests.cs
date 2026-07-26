using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace SsalKit.Generators.Toolkit.Tests;

/// <summary>
/// Direct unit tests for <see cref="SymbolFacts"/>: name formatting, the generic test, the
/// effectively-public test, the generated-code accessibility walk, and diagnostic ordering.
/// </summary>
public class SymbolFactsTests
{
    private const string Source = """
        namespace Outer.Inner;

        public class PublicHost
        {
            public class PublicNested { }
            internal class InternalNested { }
            protected internal class ProtectedInternalNested { }
            protected class ProtectedNested { }
            private protected class PrivateProtectedNested { }
            private class PrivateNested { }

            public class PublicUnderPublic
            {
                public class DeepPublic { }
            }
        }

        internal class InternalHost
        {
            public class PublicUnderInternal { }
            private class PrivateUnderInternal
            {
                public class PublicUnderPrivate { }
            }
        }

        public class GenericHost<T>
        {
            public class NonGenericNested { }
        }

        public class Broken : ThisTypeDoesNotExist { }

        file class FileLocal { }

        public class GlobalReference { }
        """;

    private const string GlobalNamespaceSource = "public class TopLevelInGlobalNamespace { }";

    private static readonly CSharpCompilation Compilation = CreateCompilation();

    private static readonly DiagnosticDescriptorFactory Factory = new("TEST", "Testing");

    private static readonly DiagnosticDescriptor RuleA = Factory.Error(1, "A", "a", "A");

    private static readonly DiagnosticDescriptor RuleB = Factory.Error(2, "B", "b", "B");

    // ---- ToFqn ---------------------------------------------------------------------------------

    [Fact]
    public void ToFqn_QualifiesWithGlobalAlias()
    {
        Assert.Equal("global::Outer.Inner.PublicHost", SymbolFacts.ToFqn(Type("Outer.Inner.PublicHost")));
    }

    [Fact]
    public void ToFqn_WritesNestedTypesAndTypeArguments()
    {
        Assert.Equal(
            "global::Outer.Inner.GenericHost<T>.NonGenericNested",
            SymbolFacts.ToFqn(Type("Outer.Inner.GenericHost`1+NonGenericNested")));
    }

    [Fact]
    public void ToFqn_AcceptsAnySymbolKind()
    {
        Assert.Equal("global::Outer.Inner", SymbolFacts.ToFqn(Type("Outer.Inner.PublicHost").ContainingNamespace));
    }

    // ---- GetContainingNamespaceName ------------------------------------------------------------

    [Fact]
    public void GetContainingNamespaceName_ReturnsTheDottedName()
    {
        Assert.Equal("Outer.Inner", SymbolFacts.GetContainingNamespaceName(Type("Outer.Inner.PublicHost")));
    }

    [Fact]
    public void GetContainingNamespaceName_ForTheGlobalNamespace_ReturnsEmpty()
    {
        var type = Compilation.GetTypeByMetadataName("TopLevelInGlobalNamespace")!;

        Assert.True(type.ContainingNamespace.IsGlobalNamespace);
        Assert.Equal(string.Empty, SymbolFacts.GetContainingNamespaceName(type));
    }

    /// <summary>
    /// The global namespace itself has no containing namespace at all, which is the only way the
    /// <see langword="null"/> branch is reached.
    /// </summary>
    [Fact]
    public void GetContainingNamespaceName_WhenThereIsNoContainingNamespace_ReturnsEmpty()
    {
        Assert.Null(Compilation.GlobalNamespace.ContainingNamespace);
        Assert.Equal(string.Empty, SymbolFacts.GetContainingNamespaceName(Compilation.GlobalNamespace));
    }

    // ---- IsEffectivelyPublic -------------------------------------------------------------------

    [Theory]
    [InlineData("Outer.Inner.PublicHost", true)]
    [InlineData("Outer.Inner.PublicHost+PublicNested", true)]
    [InlineData("Outer.Inner.PublicHost+PublicUnderPublic+DeepPublic", true)]
    [InlineData("Outer.Inner.PublicHost+InternalNested", false)]
    [InlineData("Outer.Inner.InternalHost", false)]
    [InlineData("Outer.Inner.InternalHost+PublicUnderInternal", false)]
    public void IsEffectivelyPublic_WalksTheWholeNestingChain(string metadataName, bool expected)
    {
        Assert.Equal(expected, SymbolFacts.IsEffectivelyPublic(Type(metadataName)));
    }

    // ---- IsGenericOrNestedInGeneric ------------------------------------------------------------

    [Theory]
    [InlineData("Outer.Inner.GenericHost`1", true)]
    [InlineData("Outer.Inner.GenericHost`1+NonGenericNested", true)]
    [InlineData("Outer.Inner.PublicHost", false)]
    [InlineData("Outer.Inner.PublicHost+PublicNested", false)]
    public void IsGenericOrNestedInGeneric_WalksTheWholeNestingChain(string metadataName, bool expected)
    {
        Assert.Equal(expected, SymbolFacts.IsGenericOrNestedInGeneric(Type(metadataName)));
    }

    // ---- IsAtLeastInternal ---------------------------------------------------------------------

    [Theory]
    [InlineData(Accessibility.Public, true)]
    [InlineData(Accessibility.Internal, true)]
    [InlineData(Accessibility.ProtectedOrInternal, true)]
    [InlineData(Accessibility.Protected, false)]
    [InlineData(Accessibility.ProtectedAndInternal, false)]
    [InlineData(Accessibility.Private, false)]
    [InlineData(Accessibility.NotApplicable, false)]
    public void IsAtLeastInternal_ClassifiesEveryAccessibility(Accessibility accessibility, bool expected)
    {
        Assert.Equal(expected, SymbolFacts.IsAtLeastInternal(accessibility));
    }

    // ---- The generated-code accessibility walk -------------------------------------------------

    [Theory]
    [InlineData("Outer.Inner.PublicHost")]
    [InlineData("Outer.Inner.PublicHost+PublicNested")]
    [InlineData("Outer.Inner.PublicHost+InternalNested")]
    [InlineData("Outer.Inner.PublicHost+ProtectedInternalNested")]
    [InlineData("Outer.Inner.InternalHost+PublicUnderInternal")]
    public void FindGeneratedCodeAccessBlocker_ForANameableType_ReturnsNull(string metadataName)
    {
        var type = Type(metadataName);

        Assert.Null(SymbolFacts.FindGeneratedCodeAccessBlocker(type));
        Assert.True(SymbolFacts.IsAccessibleFromGeneratedCode(type));
    }

    [Theory]
    [InlineData("Outer.Inner.PublicHost+ProtectedNested")]
    [InlineData("Outer.Inner.PublicHost+PrivateProtectedNested")]
    [InlineData("Outer.Inner.PublicHost+PrivateNested")]
    public void FindGeneratedCodeAccessBlocker_ForATooPrivateType_ReturnsThatTypeItself(string metadataName)
    {
        var type = Type(metadataName);

        Assert.Same(type, SymbolFacts.FindGeneratedCodeAccessBlocker(type));
        Assert.False(SymbolFacts.IsAccessibleFromGeneratedCode(type));
    }

    [Fact]
    public void FindGeneratedCodeAccessBlocker_ForATypeNestedInAPrivateOne_ReturnsTheContainer()
    {
        var container = Type("Outer.Inner.InternalHost+PrivateUnderInternal");
        var nested = Type("Outer.Inner.InternalHost+PrivateUnderInternal+PublicUnderPrivate");

        Assert.Same(container, SymbolFacts.FindGeneratedCodeAccessBlocker(nested));
    }

    /// <summary>
    /// A file-local type reports <see cref="Accessibility.Internal"/>, so it is only rejected
    /// because <see cref="INamedTypeSymbol.IsFileLocal"/> is asked about separately.
    /// </summary>
    [Fact]
    public void FindGeneratedCodeAccessBlocker_ForAFileLocalType_ReturnsIt()
    {
        var type = FileLocalType();

        Assert.Equal(Accessibility.Internal, type.DeclaredAccessibility);
        Assert.Same(type, SymbolFacts.FindGeneratedCodeAccessBlocker(type));
    }

    /// <summary>
    /// Pins the judgement documented on <see cref="SymbolFacts.IsAtLeastInternal"/>: the only named
    /// type that reports <see cref="Accessibility.NotApplicable"/> is an error type, and generated
    /// code cannot name one, so the walk must reject it rather than wave it through.
    /// </summary>
    [Fact]
    public void FindGeneratedCodeAccessBlocker_ForAnErrorType_ReturnsIt()
    {
        var errorType = Type("Outer.Inner.Broken").BaseType!;

        Assert.IsAssignableFrom<IErrorTypeSymbol>(errorType);
        Assert.Equal(Accessibility.NotApplicable, errorType.DeclaredAccessibility);
        Assert.Same(errorType, SymbolFacts.FindGeneratedCodeAccessBlocker(errorType));
        Assert.False(SymbolFacts.IsAccessibleFromGeneratedCode(errorType));
    }

    // ---- SortForDiagnosticDeterminism ----------------------------------------------------------

    [Fact]
    public void SortForDiagnosticDeterminism_OrdersByFileThenPositionThenId()
    {
        var sorted = SymbolFacts.SortForDiagnosticDeterminism(ImmutableArray.Create(
            Info(RuleB, "b.cs", 5),
            Info(RuleA, "a.cs", 20),
            Info(RuleB, "a.cs", 20),
            Info(RuleA, "a.cs", 1)));

        Assert.Equal(
            new[] { ("TEST001", "a.cs", 1), ("TEST001", "a.cs", 20), ("TEST002", "a.cs", 20), ("TEST002", "b.cs", 5) },
            sorted.Select(info => (info.Descriptor.Id, info.Location!.FilePath, info.Location!.TextSpan.Start)));
    }

    [Fact]
    public void SortForDiagnosticDeterminism_PutsLocationlessDiagnosticsLast()
    {
        var withoutLocation = new DiagnosticInfo(RuleA, location: null);

        var sorted = SymbolFacts.SortForDiagnosticDeterminism(ImmutableArray.Create(
            withoutLocation,
            Info(RuleB, "z.cs", 999)));

        Assert.Equal(new[] { "TEST002", "TEST001" }, sorted.Select(info => info.Descriptor.Id));
        Assert.Same(withoutLocation, sorted[1]);
    }

    /// <summary>
    /// Two location-less diagnostics still have to come out in a fixed order, which is what the
    /// <c>?? string.Empty</c> / <c>?? 0</c> fallbacks in the sort keys exist for: they collapse to
    /// the same key and the id decides.
    /// </summary>
    [Fact]
    public void SortForDiagnosticDeterminism_BreaksTiesBetweenLocationlessDiagnosticsById()
    {
        var sorted = SymbolFacts.SortForDiagnosticDeterminism(ImmutableArray.Create(
            new DiagnosticInfo(RuleB, location: null),
            new DiagnosticInfo(RuleA, location: null)));

        Assert.Equal(new[] { "TEST001", "TEST002" }, sorted.Select(info => info.Descriptor.Id));
    }

    /// <summary>
    /// File, position and id together do not identify a diagnostic: one rule fires more than once at
    /// one position whenever the reported location is the declaration and the offending detail is a
    /// member of it. Without the message arguments as a final key, those come out in whatever order
    /// the pipeline produced them -- the exact dependency this method exists to remove.
    /// </summary>
    [Fact]
    public void SortForDiagnosticDeterminism_BreaksTiesAtOnePositionByMessageArguments()
    {
        var sorted = SymbolFacts.SortForDiagnosticDeterminism(ImmutableArray.Create(
            Info(RuleA, "a.cs", 1, "Weight"),
            Info(RuleA, "a.cs", 1, "Chance"),
            Info(RuleA, "a.cs", 1, "Odds")));

        Assert.Equal(
            new[] { "Chance", "Odds", "Weight" },
            sorted.Select(info => info.MessageArgs[0]));
    }

    /// <summary>
    /// The same holds for the location-less diagnostics, which all share the one "position" of
    /// having none.
    /// </summary>
    [Fact]
    public void SortForDiagnosticDeterminism_BreaksTiesBetweenLocationlessDiagnosticsByMessageArguments()
    {
        var sorted = SymbolFacts.SortForDiagnosticDeterminism(ImmutableArray.Create(
            new DiagnosticInfo(RuleA, location: null, "second"),
            new DiagnosticInfo(RuleA, location: null, "first")));

        Assert.Equal(new[] { "first", "second" }, sorted.Select(info => info.MessageArgs[0]));
    }

    /// <summary>
    /// The join that builds the key uses a separator no message argument carries, so two argument
    /// lists that concatenate to the same text still order by their real boundaries.
    /// </summary>
    [Fact]
    public void SortForDiagnosticDeterminism_DoesNotConflateArgumentListsThatConcatenateAlike()
    {
        var sorted = SymbolFacts.SortForDiagnosticDeterminism(ImmutableArray.Create(
            Info(RuleA, "a.cs", 1, "ab", "c"),
            Info(RuleA, "a.cs", 1, "a", "bc")));

        Assert.Equal(
            new[] { "a", "ab" },
            sorted.Select(info => info.MessageArgs[0]));
    }

    [Fact]
    public void SortForDiagnosticDeterminism_OnAnEmptyArray_ReturnsEmpty()
    {
        Assert.Empty(SymbolFacts.SortForDiagnosticDeterminism(ImmutableArray<DiagnosticInfo>.Empty));
    }

    // ---- fixtures ------------------------------------------------------------------------------

    private static DiagnosticInfo Info(
        DiagnosticDescriptor descriptor, string filePath, int start, params string[] messageArgs) =>
        new(
            descriptor,
            new LocationInfo(filePath, new TextSpan(start, 1), new LinePositionSpan(new LinePosition(0, start), new LinePosition(0, start + 1))),
            messageArgs);

    private static INamedTypeSymbol Type(string metadataName) => Compilation.GetTypeByMetadataName(metadataName)!;

    private static INamedTypeSymbol FileLocalType() =>
        Compilation.SyntaxTrees
            .Select(tree => Compilation.GetSemanticModel(tree))
            .SelectMany(model => model.SyntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
                .Select(declaration => model.GetDeclaredSymbol(declaration)))
            .OfType<INamedTypeSymbol>()
            .Single(type => type.IsFileLocal);

    private static CSharpCompilation CreateCompilation()
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);

        return CSharpCompilation.Create(
            "SymbolFactsTests",
            new[]
            {
                CSharpSyntaxTree.ParseText(Source, parseOptions, "Source.cs"),
                CSharpSyntaxTree.ParseText(GlobalNamespaceSource, parseOptions, "GlobalNamespaceSource.cs"),
            },
            ReferenceAssemblies(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static IEnumerable<MetadataReference> ReferenceAssemblies() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(path => path.Length > 0)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));
}
