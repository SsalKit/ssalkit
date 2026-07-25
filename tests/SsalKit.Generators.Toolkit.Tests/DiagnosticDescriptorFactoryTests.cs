using Microsoft.CodeAnalysis;
using SsalKit.Generators.Toolkit;

namespace SsalKit.Generators.Toolkit.Tests;

/// <summary>
/// Direct unit tests for <see cref="DiagnosticDescriptorFactory"/>: id formatting, category,
/// severity, the fixed <c>isEnabledByDefault: true</c> contract, and custom tag propagation for
/// both <see cref="DiagnosticDescriptorFactory.Error"/> and
/// <see cref="DiagnosticDescriptorFactory.Warning"/>.
/// </summary>
public class DiagnosticDescriptorFactoryTests
{
    [Fact]
    public void Error_FormatsIdWithThreeDigitPadding()
    {
        var factory = new DiagnosticDescriptorFactory("SSAL", "SsalKit.Guard");

        var descriptor = factory.Error(1, "Title", "Message {0}", "Description");

        Assert.Equal("SSAL001", descriptor.Id);
    }

    [Fact]
    public void Error_IdWiderThanThreeDigits_IsNotTruncated()
    {
        var factory = new DiagnosticDescriptorFactory("SSAL", "SsalKit.Guard");

        var descriptor = factory.Error(1234, "Title", "Message", "Description");

        Assert.Equal("SSAL1234", descriptor.Id);
    }

    [Fact]
    public void Error_SetsCategorySeverityAndDescription()
    {
        var factory = new DiagnosticDescriptorFactory("SSAL", "SsalKit.Guard");

        var descriptor = factory.Error(2, "My Title", "My message {0}", "My description");

        Assert.Equal("SsalKit.Guard", descriptor.Category);
        Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
        Assert.True(descriptor.IsEnabledByDefault);
        Assert.Equal("My Title", descriptor.Title.ToString());
        Assert.Equal("My message {0}", descriptor.MessageFormat.ToString());
        Assert.Equal("My description", descriptor.Description.ToString());
    }

    [Fact]
    public void Error_NoCustomTags_ProducesEmptyCustomTags()
    {
        var factory = new DiagnosticDescriptorFactory("SSAL", "SsalKit.Guard");

        var descriptor = factory.Error(3, "Title", "Message", "Description");

        Assert.Empty(descriptor.CustomTags);
    }

    [Fact]
    public void Error_CustomTags_ArePropagated()
    {
        var factory = new DiagnosticDescriptorFactory("SSAL", "SsalKit.Guard");

        var descriptor = factory.Error(4, "Title", "Message", "Description", "Tag1", "Tag2");

        Assert.Equal(new[] { "Tag1", "Tag2" }, descriptor.CustomTags);
    }

    [Fact]
    public void Warning_FormatsIdWithThreeDigitPadding()
    {
        var factory = new DiagnosticDescriptorFactory("SSAL", "SsalKit.Guard");

        var descriptor = factory.Warning(7, "Title", "Message", "Description");

        Assert.Equal("SSAL007", descriptor.Id);
    }

    [Fact]
    public void Warning_SetsCategorySeverityAndDescription()
    {
        var factory = new DiagnosticDescriptorFactory("SSAL", "SsalKit.AmbientContext");

        var descriptor = factory.Warning(8, "Warn Title", "Warn message {0}", "Warn description");

        Assert.Equal("SsalKit.AmbientContext", descriptor.Category);
        Assert.Equal(DiagnosticSeverity.Warning, descriptor.DefaultSeverity);
        Assert.True(descriptor.IsEnabledByDefault);
        Assert.Equal("Warn Title", descriptor.Title.ToString());
        Assert.Equal("Warn message {0}", descriptor.MessageFormat.ToString());
        Assert.Equal("Warn description", descriptor.Description.ToString());
    }

    [Fact]
    public void Warning_NoCustomTags_ProducesEmptyCustomTags()
    {
        var factory = new DiagnosticDescriptorFactory("SSAL", "SsalKit.Guard");

        var descriptor = factory.Warning(9, "Title", "Message", "Description");

        Assert.Empty(descriptor.CustomTags);
    }

    [Fact]
    public void Warning_CustomTags_ArePropagated()
    {
        var factory = new DiagnosticDescriptorFactory("SSAL", "SsalKit.Guard");

        var descriptor = factory.Warning(10, "Title", "Message", "Description", "Deprecated");

        Assert.Equal(new[] { "Deprecated" }, descriptor.CustomTags);
    }

    [Fact]
    public void DifferentPrefix_ChangesIdPrefix()
    {
        var factory = new DiagnosticDescriptorFactory("GEN", "SsalKit.Generators.Toolkit");

        var descriptor = factory.Error(1, "Title", "Message", "Description");

        Assert.Equal("GEN001", descriptor.Id);
    }
}
