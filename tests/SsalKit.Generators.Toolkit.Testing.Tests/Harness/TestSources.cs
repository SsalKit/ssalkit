namespace SsalKit.Generators.Toolkit.Testing.Tests.Harness;

/// <summary>
/// The source snippets the harness tests run <see cref="MiniGenerator"/> over. The marker attribute
/// is declared in the test source itself rather than emitted by the generator, so a run over source
/// with no marked type produces genuinely zero files.
/// </summary>
public static class TestSources
{
    public const string MarkerAttribute = """
        namespace Mini
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class MarkerAttribute : System.Attribute
            {
                public MarkerAttribute(string greeting) => Greeting = greeting;

                public string Greeting { get; }
            }
        }
        """;

    /// <summary>One marked type, no diagnostic: the happy path.</summary>
    public const string OneMarkedType = $$"""
        {{MarkerAttribute}}

        namespace Demo
        {
            [Mini.Marker("hello")]
            public sealed class Widget
            {
            }
        }
        """;

    /// <summary>The same as <see cref="OneMarkedType"/> with a different greeting, which the model captures.</summary>
    public const string OneMarkedTypeWithOtherGreeting = $$"""
        {{MarkerAttribute}}

        namespace Demo
        {
            [Mini.Marker("goodbye")]
            public sealed class Widget
            {
            }
        }
        """;

    /// <summary>Two marked types declared out of alphabetical order, for the hint-name sorting option.</summary>
    public const string TwoMarkedTypes = $$"""
        {{MarkerAttribute}}

        namespace Demo
        {
            [Mini.Marker("hello")]
            public sealed class Zeta
            {
            }

            [Mini.Marker("hello")]
            public sealed class Alpha
            {
            }
        }
        """;

    /// <summary>Nothing is marked, so the generator produces nothing.</summary>
    public const string NoMarkedType = $$"""
        {{MarkerAttribute}}

        namespace Demo
        {
            public sealed class Widget
            {
            }
        }
        """;

    /// <summary>Triggers MINI001 (error, reported on the attribute).</summary>
    public const string BadlyNamedType = $$"""
        {{MarkerAttribute}}

        namespace Demo
        {
            [Mini.Marker("hello")]
            public sealed class BadWidget
            {
            }
        }
        """;

    /// <summary>Triggers MINI002 (warning, reported with no location).</summary>
    public const string OddlyNamedType = $$"""
        {{MarkerAttribute}}

        namespace Demo
        {
            [Mini.Marker("hello")]
            public sealed class OddWidget
            {
            }
        }
        """;

    /// <summary>Triggers MINI001 twice, so an "exactly one" assertion has something to reject.</summary>
    public const string TwoBadlyNamedTypes = $$"""
        {{MarkerAttribute}}

        namespace Demo
        {
            [Mini.Marker("hello")]
            public sealed class BadWidget
            {
            }

            [Mini.Marker("hello")]
            public sealed class BadGadget
            {
            }
        }
        """;

    /// <summary>Triggers both MINI001 and MINI002 in one run.</summary>
    public const string BadAndOddlyNamedTypes = $$"""
        {{MarkerAttribute}}

        namespace Demo
        {
            [Mini.Marker("hello")]
            public sealed class BadWidget
            {
            }

            [Mini.Marker("hello")]
            public sealed class OddWidget
            {
            }
        }
        """;
}
