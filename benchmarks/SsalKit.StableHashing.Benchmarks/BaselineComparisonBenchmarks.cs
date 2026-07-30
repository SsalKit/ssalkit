using System.IO.Hashing;
using BenchmarkDotNet.Attributes;

namespace SsalKit.StableHashing.Benchmarks;

/// <summary>
/// Compares the generated <c>ComputeStableHash()</c> for <see cref="MediumContract"/> (the
/// baseline) against a naive hand-rolled equivalent that serializes the same values into a
/// <see cref="byte"/>[] via <see cref="MemoryStream"/>/<see cref="BinaryWriter"/> and then hashes
/// that buffer with <c>XxHash64.HashToUInt64</c> from the System.IO.Hashing NuGet package -- the
/// same package the runtime's golden vector tests use as an independent reference oracle for
/// SsalKit.StableHashing's internal XxHash64 port. This isolates the benefit of the streaming-writer
/// design (design doc section 4.6): the naive approach must materialize an intermediate byte
/// buffer before it can hash anything, while the generated code never does.
/// </summary>
[MemoryDiagnoser]
public class BaselineComparisonBenchmarks
{
    private MediumContract _medium = null!;

    [GlobalSetup]
    public void Setup()
    {
        _medium = BenchmarkFixtures.CreateMedium();
    }

    [Benchmark(Baseline = true)]
    public ulong Generated() => _medium.ComputeStableHash().Value;

    [Benchmark]
    public ulong NaiveSerializeThenHash()
    {
        byte[] bytes = SerializeNaive(_medium);
        return XxHash64.HashToUInt64(bytes);
    }

    // Hand-rolled equivalent of the fields the generator would encode for MediumContract, but via
    // an intermediate byte[] buffer rather than streaming straight into a hash state -- exactly
    // the design the generated code avoids.
    private static byte[] SerializeNaive(MediumContract contract)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(contract.Id);
            writer.Write(contract.Name);
            writer.Write(contract.Timestamp);
            writer.Write(contract.Score);
            writer.Write(contract.Active);
            writer.Write(contract.CorrelationId.ToByteArray());
            writer.Write(contract.Level);
            writer.Write(contract.Category);
            writer.Write(contract.Checksum);
            writer.Write(contract.Balance);
            writer.Write(contract.Description);
            writer.Write(contract.Position.X);
            writer.Write(contract.Position.Y);
        }

        return stream.ToArray();
    }
}
