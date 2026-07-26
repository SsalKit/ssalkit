namespace SsalKit.Generators.Toolkit.Testing;

/// <summary>
/// The failure signal for every assertion in this package.
/// </summary>
/// <remarks>
/// <para>
/// The harness deliberately does not reference xunit, NUnit, MSTest, or TUnit: it reports a failed
/// assertion by throwing this exception instead of calling a framework's <c>Assert</c> class. Every
/// test framework treats an unhandled exception thrown from a test as a failed test, so the same
/// harness works unchanged in all of them, and a project that uses two of them at once does not end
/// up with two flavours of the harness.
/// </para>
/// <para>
/// The trade-off is that failures surface as an exception rather than as the framework's native
/// assertion type, so the message carries the diagnosis: expected versus actual, the hint names of
/// every generated file, the compiler errors from the regenerated compilation, or the per-step
/// incremental cache state, depending on which assertion failed.
/// </para>
/// </remarks>
public sealed class GeneratorAssertionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratorAssertionException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the failed assertion.</param>
    public GeneratorAssertionException(string message)
        : base(message)
    {
    }
}
