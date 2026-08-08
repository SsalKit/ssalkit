using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using SsalKit.Determinism.Analyzer.Diagnostics;

namespace SsalKit.Determinism.Analyzer.Tests;

/// <summary>
/// Pins every property of every descriptor in <see cref="DiagnosticDescriptors"/> to a full-table
/// snapshot.
/// </summary>
/// <remarks>
/// <para>
/// A diagnostic id and its wording become part of the package's public contract the moment they
/// ship: the id is what a consumer writes in <c>.editorconfig</c> or a <c>#pragma</c>, and the
/// message is what names the replacement API. Ids, titles, message formats, categories, severities,
/// the enabled-by-default flag, descriptions, help links and custom tags are all rendered, so no
/// property can drift unnoticed.
/// </para>
/// <para>
/// The fields are discovered by reflection rather than listed, so a descriptor that is added,
/// removed or renamed changes the snapshot too -- with the count assertion below as the tripwire
/// that says which of the two happened.
/// </para>
/// </remarks>
public class DiagnosticDescriptorSnapshotTests
{
    private const int ExpectedDescriptorCount = 8;

    private static readonly FieldInfo[] DescriptorFields = typeof(DiagnosticDescriptors)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(DiagnosticDescriptor))
        .OrderBy(field => field.Name, StringComparer.Ordinal)
        .ToArray();

    [Fact]
    public void DescriptorTable_HasTheExpectedNumberOfDescriptors()
    {
        Assert.Equal(ExpectedDescriptorCount, DescriptorFields.Length);
    }

    [Fact]
    public void DescriptorTable_IdsAreUniqueAndSequential()
    {
        var ids = DescriptorFields
            .Select(field => Descriptor(field).Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        var expected = Enumerable.Range(1, ExpectedDescriptorCount)
            .Select(number => $"SSALD{number:D3}")
            .ToArray();

        Assert.Equal(expected, ids);
    }

    [Fact]
    public Task DescriptorTable_MatchesSnapshot()
    {
        var builder = new StringBuilder();

        foreach (var field in DescriptorFields)
        {
            var descriptor = Descriptor(field);

            builder.Append(field.Name).Append('\n');
            Append(builder, "Id", descriptor.Id);
            Append(builder, "Title", descriptor.Title.ToString());
            Append(builder, "MessageFormat", descriptor.MessageFormat.ToString());
            Append(builder, "Category", descriptor.Category);
            Append(builder, "DefaultSeverity", descriptor.DefaultSeverity.ToString());
            Append(builder, "IsEnabledByDefault", descriptor.IsEnabledByDefault.ToString());
            Append(builder, "Description", descriptor.Description.ToString());
            Append(builder, "HelpLinkUri", descriptor.HelpLinkUri);
            Append(builder, "CustomTags", string.Join(", ", descriptor.CustomTags));
            builder.Append('\n');
        }

        return Verifier.Verify(builder.ToString()).UseDirectory("Snapshots");
    }

    private static void Append(StringBuilder builder, string name, string value) =>
        builder.Append("  ").Append(name).Append(": ").Append(value).Append('\n');

    private static DiagnosticDescriptor Descriptor(FieldInfo field) => (DiagnosticDescriptor)field.GetValue(null)!;
}
