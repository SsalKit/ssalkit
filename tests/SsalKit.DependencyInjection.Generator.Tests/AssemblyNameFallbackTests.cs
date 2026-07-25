using SsalKit.Generators.Toolkit;

namespace SsalKit.DependencyInjection.Generator.Tests;

/// <summary>
/// Direct unit tests for the DI Generator's specific use of
/// <see cref="CSharpNaming.ToPascalCaseIdentifier(string?, string)"/>: it always passes
/// <c>fallback: "Assembly"</c> (see <c>ServiceRegistrationEmitter.Emit</c>), preserving the
/// original <c>AssemblyNameSanitizer</c> behavior now that the implementation has moved to
/// <c>SsalKit.Generators.Toolkit</c>.
/// </summary>
/// <remarks>
/// This class previously covered the null/empty/all-symbols fallback plus ordinary PascalCase
/// conversion cases (leading-digit prefix, dotted names, single lowercase segment, multiple
/// separators). Those non-fallback conversion cases -- and the null-input-with-"Assembly"-fallback
/// case -- are pure duplicates of <c>SsalKit.Generators.Toolkit.Tests.CSharpNamingTests</c> and
/// were deleted rather than retargeted. Only the empty-string and all-symbols cases remain here,
/// since <c>CSharpNamingTests</c> only exercises those two edge cases against the toolkit's own
/// default fallback ("Identifier"), not against the "Assembly" fallback the DI Generator actually
/// uses. The <c>AssemblyName_IsSanitizedIntoPascalCaseIdentifier</c> theory in
/// <c>GeneratorEmissionTests</c> additionally verifies the ordinary conversion cases indirectly,
/// through the emitter.
/// </remarks>
public class AssemblyNameFallbackTests
{
    [Fact]
    public void EmptyString_FallsBackToAssembly()
    {
        var result = CSharpNaming.ToPascalCaseIdentifier(string.Empty, "Assembly");

        Assert.Equal("Assembly", result);
    }

    [Fact]
    public void AllSymbols_FallsBackToAssembly()
    {
        var result = CSharpNaming.ToPascalCaseIdentifier("!!!", "Assembly");

        Assert.Equal("Assembly", result);
    }
}
