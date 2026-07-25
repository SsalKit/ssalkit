using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace SsalKit.Generators.Toolkit.Tests;

/// <summary>
/// Direct unit tests for <see cref="DiagnosticInfo"/> and <see cref="LocationInfo"/>: the value
/// equality and hashing that make them safe to cache in an incremental pipeline, and the
/// round-trip back to a reportable <see cref="Diagnostic"/> with and without a source location.
/// </summary>
public class DiagnosticInfoTests
{
    // Built through the toolkit's own factory rather than by calling the DiagnosticDescriptor
    // constructor here: a bare constructor call in this project trips RS2008 (analyzer release
    // tracking), which does not apply to a test fixture.
    private static readonly DiagnosticDescriptorFactory Factory = new("TEST", "Testing");

    private static readonly DiagnosticDescriptor RuleA = Factory.Error(
        1, "Title A", "Message about '{0}' and '{1}'", "Description A");

    private static readonly DiagnosticDescriptor RuleB = Factory.Warning(
        2, "Title B", "Another message", "Description B");

    private static LocationInfo SampleLocation(string filePath = "Sample.cs", int start = 10, int length = 5) =>
        new(filePath, new TextSpan(start, length), new LinePositionSpan(new LinePosition(1, 2), new LinePosition(1, 7)));

    // ---- DiagnosticInfo: construction and round-trip -------------------------------------------

    [Fact]
    public void ParamsConstructor_StoresMessageArgsInOrder()
    {
        var info = new DiagnosticInfo(RuleA, location: null, "first", "second");

        Assert.Same(RuleA, info.Descriptor);
        Assert.Null(info.Location);
        Assert.Equal(2, info.MessageArgs.Length);
        Assert.Equal("first", info.MessageArgs[0]);
        Assert.Equal("second", info.MessageArgs[1]);
    }

    [Fact]
    public void ParamsConstructor_WithNoArgs_ProducesEmptyMessageArgs()
    {
        var info = new DiagnosticInfo(RuleB, location: null);

        Assert.Equal(0, info.MessageArgs.Length);
    }

    [Fact]
    public void EquatableArrayConstructor_StoresTheArrayAsGiven()
    {
        var args = new EquatableArray<string>(ImmutableArray.Create("only"));

        var info = new DiagnosticInfo(RuleA, SampleLocation(), args);

        Assert.Equal(args, info.MessageArgs);
    }

    [Fact]
    public void ToDiagnostic_WithoutLocation_ReportsWithNoLocationAndFormattedMessage()
    {
        var info = new DiagnosticInfo(RuleA, location: null, "alpha", "beta");

        var diagnostic = info.ToDiagnostic();

        Assert.Equal("TEST001", diagnostic.Id);
        Assert.Equal(Location.None, diagnostic.Location);
        Assert.Equal("Message about 'alpha' and 'beta'", diagnostic.GetMessage(null));
    }

    [Fact]
    public void ToDiagnostic_WithLocation_RestoresFilePathAndSpans()
    {
        var location = SampleLocation("Widget.cs", start: 42, length: 7);
        var info = new DiagnosticInfo(RuleB, location);

        var diagnostic = info.ToDiagnostic();

        Assert.Equal("TEST002", diagnostic.Id);
        Assert.Equal("Widget.cs", diagnostic.Location.GetLineSpan().Path);
        Assert.Equal(new TextSpan(42, 7), diagnostic.Location.SourceSpan);
        Assert.Equal(new LinePosition(1, 2), diagnostic.Location.GetLineSpan().StartLinePosition);
    }

    // ---- DiagnosticInfo: equality --------------------------------------------------------------

    [Fact]
    public void Equals_SameReference_IsTrue()
    {
        var info = new DiagnosticInfo(RuleA, SampleLocation(), "x");

        Assert.True(info.Equals(info));
    }

    [Fact]
    public void Equals_SameValues_IsTrueAndHashesMatch()
    {
        var left = new DiagnosticInfo(RuleA, SampleLocation(), "x", "y");
        var right = new DiagnosticInfo(RuleA, SampleLocation(), "x", "y");

        Assert.True(left.Equals(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equals_WithoutLocationOnBothSides_IsTrueAndHashesMatch()
    {
        var left = new DiagnosticInfo(RuleA, location: null, "x");
        var right = new DiagnosticInfo(RuleA, location: null, "x");

        Assert.True(left.Equals(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentDescriptor_IsFalse()
    {
        var left = new DiagnosticInfo(RuleA, location: null, "x");
        var right = new DiagnosticInfo(RuleB, location: null, "x");

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Equals_DifferentLocation_IsFalse()
    {
        var left = new DiagnosticInfo(RuleA, SampleLocation("A.cs"), "x");
        var right = new DiagnosticInfo(RuleA, SampleLocation("B.cs"), "x");

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Equals_LocationOnOneSideOnly_IsFalse()
    {
        var left = new DiagnosticInfo(RuleA, SampleLocation(), "x");
        var right = new DiagnosticInfo(RuleA, location: null, "x");

        Assert.False(left.Equals(right));
        Assert.False(right.Equals(left));
    }

    [Fact]
    public void Equals_DifferentMessageArgs_IsFalse()
    {
        var left = new DiagnosticInfo(RuleA, location: null, "x");
        var right = new DiagnosticInfo(RuleA, location: null, "y");

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Equals_Null_IsFalse()
    {
        var info = new DiagnosticInfo(RuleA, location: null, "x");

        Assert.False(info.Equals((DiagnosticInfo?)null));
        Assert.False(info.Equals((object?)null));
    }

    [Fact]
    public void Equals_DifferentType_IsFalse()
    {
        var info = new DiagnosticInfo(RuleA, location: null, "x");

        Assert.False(info.Equals("not a diagnostic"));
    }

    [Fact]
    public void EqualsObject_SameValues_IsTrue()
    {
        object left = new DiagnosticInfo(RuleA, location: null, "x");
        object right = new DiagnosticInfo(RuleA, location: null, "x");

        Assert.True(left.Equals(right));
    }

    [Fact]
    public void EqualityOperators_CoverNullAndValueCombinations()
    {
        var left = new DiagnosticInfo(RuleA, location: null, "x");
        var same = new DiagnosticInfo(RuleA, location: null, "x");
        var other = new DiagnosticInfo(RuleB, location: null, "x");
        DiagnosticInfo? none = null;

        Assert.True(none == null);
        Assert.False(none == left);
        Assert.False(left == none);
        Assert.True(left == same);
        Assert.False(left == other);

        Assert.False(none != null);
        Assert.True(left != none);
        Assert.False(left != same);
        Assert.True(left != other);
    }

    // ---- LocationInfo --------------------------------------------------------------------------

    [Fact]
    public void LocationInfo_Constructor_ExposesItsComponents()
    {
        var span = new TextSpan(3, 4);
        var lineSpan = new LinePositionSpan(new LinePosition(0, 3), new LinePosition(0, 7));

        var info = new LocationInfo("File.cs", span, lineSpan);

        Assert.Equal("File.cs", info.FilePath);
        Assert.Equal(span, info.TextSpan);
        Assert.Equal(lineSpan, info.LineSpan);
    }

    [Fact]
    public void LocationInfo_ToLocation_RoundTripsThroughLocationCreate()
    {
        var info = SampleLocation("Round.cs", start: 8, length: 3);

        var location = info.ToLocation();

        Assert.Equal("Round.cs", location.GetLineSpan().Path);
        Assert.Equal(new TextSpan(8, 3), location.SourceSpan);
        Assert.Equal(info.LineSpan, location.GetLineSpan().Span);
    }

    [Fact]
    public void CreateFrom_NullLocation_ReturnsNull()
    {
        Assert.Null(LocationInfo.CreateFrom((Location?)null));
    }

    [Fact]
    public void CreateFrom_LocationWithoutSourceTree_ReturnsNull()
    {
        Assert.Null(LocationInfo.CreateFrom(Location.None));
    }

    [Fact]
    public void CreateFrom_SourceLocation_ProjectsPathAndSpans()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { }", path: "Projected.cs");
        var location = tree.GetRoot().DescendantNodes().First().GetLocation();

        var info = LocationInfo.CreateFrom(location);

        Assert.NotNull(info);
        Assert.Equal("Projected.cs", info!.FilePath);
        Assert.Equal(location.SourceSpan, info.TextSpan);
        Assert.Equal(location.GetLineSpan().Span, info.LineSpan);
    }

    [Fact]
    public void CreateFrom_NullSyntaxNode_ReturnsNull()
    {
        Assert.Null(LocationInfo.CreateFrom((SyntaxNode?)null));
    }

    [Fact]
    public void CreateFrom_SyntaxNode_ProjectsThatNodesLocation()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { }", path: "Node.cs");
        var node = tree.GetRoot().DescendantNodes().First();

        var info = LocationInfo.CreateFrom(node);

        Assert.NotNull(info);
        Assert.Equal("Node.cs", info!.FilePath);
        Assert.Equal(node.GetLocation().SourceSpan, info.TextSpan);
    }

    [Fact]
    public void LocationInfo_Equals_SameReference_IsTrue()
    {
        var info = SampleLocation();

        Assert.True(info.Equals(info));
    }

    [Fact]
    public void LocationInfo_Equals_SameValues_IsTrueAndHashesMatch()
    {
        var left = SampleLocation();
        var right = SampleLocation();

        Assert.True(left.Equals(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void LocationInfo_Equals_DifferentFilePath_IsFalse()
    {
        Assert.False(SampleLocation("A.cs").Equals(SampleLocation("B.cs")));
    }

    [Fact]
    public void LocationInfo_Equals_DifferentTextSpan_IsFalse()
    {
        Assert.False(SampleLocation(start: 1).Equals(SampleLocation(start: 2)));
    }

    [Fact]
    public void LocationInfo_Equals_DifferentLineSpan_IsFalse()
    {
        var left = new LocationInfo(
            "Same.cs", new TextSpan(0, 1), new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 1)));
        var right = new LocationInfo(
            "Same.cs", new TextSpan(0, 1), new LinePositionSpan(new LinePosition(5, 0), new LinePosition(5, 1)));

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void LocationInfo_Equals_NullAndOtherType_IsFalse()
    {
        var info = SampleLocation();

        Assert.False(info.Equals((LocationInfo?)null));
        Assert.False(info.Equals((object?)null));
        Assert.False(info.Equals("not a location"));
    }

    [Fact]
    public void LocationInfo_EqualsObject_SameValues_IsTrue()
    {
        object left = SampleLocation();
        object right = SampleLocation();

        Assert.True(left.Equals(right));
    }

    [Fact]
    public void LocationInfo_EqualityOperators_CoverNullAndValueCombinations()
    {
        var left = SampleLocation();
        var same = SampleLocation();
        var other = SampleLocation("Other.cs");
        LocationInfo? none = null;

        Assert.True(none == null);
        Assert.False(none == left);
        Assert.False(left == none);
        Assert.True(left == same);
        Assert.False(left == other);

        Assert.False(none != null);
        Assert.True(left != none);
        Assert.False(left != same);
        Assert.True(left != other);
    }
}
