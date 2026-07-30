using BenchmarkDotNet.Running;

// Running with no arguments would otherwise print the interactive benchmark picker and wait on
// stdin; default to "run everything" instead so `dotnet run -c Release` just works.
string[] effectiveArgs = args.Length == 0 ? ["--filter", "*"] : args;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(effectiveArgs);

internal static partial class Program;
