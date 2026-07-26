using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Diagnostics;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Pins every property of every descriptor in <see cref="DiagnosticDescriptors"/> to a full-table
/// snapshot.
/// </summary>
/// <remarks>
/// <para>
/// This exists to make a refactoring of the table -- most immediately its conversion from 26
/// hand-written <see cref="DiagnosticDescriptor"/> initializers to
/// <c>SsalKit.Generators.Toolkit.DiagnosticDescriptorFactory</c> calls -- provably behaviour-free:
/// the snapshot was taken before that conversion and must survive it byte for byte. Ids, titles,
/// message formats, categories, severities, the enabled-by-default flag, descriptions, help links
/// and custom tags are all rendered, so no property can drift unnoticed.
/// </para>
/// <para>
/// The fields are discovered by reflection rather than listed, so a descriptor that is added,
/// removed or renamed changes the snapshot too -- which is the point, since a diagnostic id and its
/// wording are part of the package's public contract the moment they ship.
/// </para>
/// </remarks>
public class DiagnosticDescriptorSnapshotTests
{
    private const int ExpectedDescriptorCount = 28;

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
            .Select(number => $"SSAL{number:D3}")
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
