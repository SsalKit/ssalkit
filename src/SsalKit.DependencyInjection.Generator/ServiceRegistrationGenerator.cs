using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SsalKit.DependencyInjection.Generator.Emission;
using SsalKit.DependencyInjection.Generator.Models;
using SsalKit.DependencyInjection.Generator.Parsing;

namespace SsalKit.DependencyInjection.Generator;

/// <summary>
/// Generates a single <c>IServiceCollection</c> extension method per assembly that registers
/// every class decorated with <c>[SsalKit.DependencyInjection.Service]</c>, plus one implementation
/// class per interface decorated with <c>[SsalKit.DependencyInjection.ServiceFactory]</c> (each of
/// which that same extension method registers as a singleton).
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ServiceRegistrationGenerator : IIncrementalGenerator
{
    private const string ServiceAttributeMetadataName = "SsalKit.DependencyInjection.ServiceAttribute";
    private const string ServiceFactoryAttributeMetadataName = "SsalKit.DependencyInjection.ServiceFactoryAttribute";

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

        IncrementalValuesProvider<ServiceFactoryModel?> factories = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ServiceFactoryAttributeMetadataName,
                // Deliberately unfiltered on syntax kind. [AttributeUsage(AttributeTargets.Interface)]
                // already restricts what can legally carry the attribute, and an illegal target is
                // still bound onto its symbol (alongside CS0592) -- letting it through to the
                // symbol-based validation keeps this predicate from being the thing that decides
                // which invalid shapes are silently ignored versus rejected by SSAL016.
                predicate: static (_, _) => true,
                transform: static (ctx, ct) => ServiceFactoryParser.GetModel(ctx, ct))
            .WithTrackingName(TrackingNames.Factories);

        IncrementalValuesProvider<ServiceFactoryModel> validFactories = factories
            .Where(static model => model is not null)
            .WithTrackingName(TrackingNames.ValidFactories)!;

        IncrementalValueProvider<ImmutableArray<ServiceFactoryModel>> collectedFactories = validFactories
            .Collect()
            .WithTrackingName(TrackingNames.CollectedFactories);

        IncrementalValueProvider<string?> assemblyName = context.CompilationProvider
            .Select(static (compilation, _) => compilation.AssemblyName)
            .WithTrackingName(TrackingNames.AssemblyName);

        IncrementalValueProvider<((ImmutableArray<ClassRegistrationModel> Classes, ImmutableArray<ServiceFactoryModel> Factories) Registrations, string? AssemblyName)> combined =
            collectedClasses
                .Combine(collectedFactories)
                .Combine(assemblyName)
                .WithTrackingName(TrackingNames.Combined);

        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var (classes, factoryModels) = source.Registrations;
            if (classes.IsDefaultOrEmpty && factoryModels.IsDefaultOrEmpty)
            {
                return;
            }

            var (hintName, generatedSource) = ServiceRegistrationEmitter.Emit(classes, factoryModels, source.AssemblyName);
            spc.AddSource(hintName, generatedSource);
        });

        // A source output of its own, not folded into the one above: the implementation classes
        // depend on nothing but their own models, so an edit that only changes a [Service] class
        // (or the assembly name) leaves every factory file untouched.
        context.RegisterSourceOutput(collectedFactories, static (spc, models) =>
        {
            foreach (var model in models)
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                spc.AddSource(model.HintName, ServiceFactoryImplementationEmitter.Emit(model));
            }
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
        public const string Factories = "Factories";
        public const string ValidFactories = "ValidFactories";
        public const string CollectedFactories = "CollectedFactories";
        public const string AssemblyName = "AssemblyName";
        public const string Combined = "Combined";
    }
}
