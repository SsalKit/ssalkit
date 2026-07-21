using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Emission;
using SsalKit.DependencyInjection.Generator.Models;
using SsalKit.DependencyInjection.Generator.Parsing;

namespace SsalKit.DependencyInjection.Generator;

/// <summary>
/// Generates a single <c>IServiceCollection</c> extension method per assembly that registers
/// every class decorated with <c>[SsalKit.DependencyInjection.Service]</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ServiceRegistrationGenerator : IIncrementalGenerator
{
    private const string ServiceAttributeMetadataName = "SsalKit.DependencyInjection.ServiceAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ClassRegistrationModel?> classes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ServiceAttributeMetadataName,
                // A record class (RecordDeclarationSyntax) is a distinct syntax node from a plain
                // class (ClassDeclarationSyntax), even though the analyzer -- which inspects the
                // bound symbol rather than syntax -- sees both as TypeKind.Class alike. Without
                // matching both node kinds here, a `[Service] public record class Foo : IFoo` would
                // be silently skipped by the generator despite the analyzer treating it as valid.
                // A record *struct* needs no special handling: [AttributeUsage(Class)] and this
                // predicate both already exclude it, and TypeKind.Class in the parser/analyzer
                // rejects it too, for the (impossible) case syntax analysis alone let it through.
                predicate: static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax
                    or Microsoft.CodeAnalysis.CSharp.Syntax.RecordDeclarationSyntax,
                transform: static (ctx, ct) => ServiceAttributeParser.GetModel(ctx, ct))
            .WithTrackingName(TrackingNames.Classes);

        IncrementalValuesProvider<ClassRegistrationModel> validClasses = classes
            .Where(static model => model is not null)
            .WithTrackingName(TrackingNames.ValidClasses)!;

        IncrementalValueProvider<ImmutableArray<ClassRegistrationModel>> collectedClasses = validClasses
            .Collect()
            .WithTrackingName(TrackingNames.CollectedClasses);

        IncrementalValueProvider<string?> assemblyName = context.CompilationProvider
            .Select(static (compilation, _) => compilation.AssemblyName)
            .WithTrackingName(TrackingNames.AssemblyName);

        IncrementalValueProvider<(ImmutableArray<ClassRegistrationModel> Classes, string? AssemblyName)> combined =
            collectedClasses
                .Combine(assemblyName)
                .WithTrackingName(TrackingNames.Combined);

        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            if (source.Classes.IsDefaultOrEmpty)
            {
                return;
            }

            var (hintName, generatedSource) = ServiceRegistrationEmitter.Emit(source.Classes, source.AssemblyName);
            spc.AddSource(hintName, generatedSource);
        });
    }

    /// <summary>
    /// Names assigned to each pipeline stage via <c>WithTrackingName</c>, so tests can inspect
    /// <see cref="IncrementalGeneratorRunStep"/> results (via
    /// <c>GeneratorDriverRunResult.Results[i].TrackedSteps</c>) to confirm the pipeline caches
    /// correctly across runs that don't change its inputs.
    /// </summary>
    internal static class TrackingNames
    {
        public const string Classes = "Classes";
        public const string ValidClasses = "ValidClasses";
        public const string CollectedClasses = "CollectedClasses";
        public const string AssemblyName = "AssemblyName";
        public const string Combined = "Combined";
    }
}
