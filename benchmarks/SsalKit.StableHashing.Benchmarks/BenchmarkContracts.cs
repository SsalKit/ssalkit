using SsalKit.StableHashing;

namespace SsalKit.StableHashing.Benchmarks;

/// <summary>A small, scalar-only contract (4 members) -- the cheapest realistic case.</summary>
[StableHashContract("bench.small", Version = 1)]
public sealed record SmallContract
{
    [StableHashMember(1)] public int Id { get; init; }

    [StableHashMember(2)] public long Value { get; init; }

    [StableHashMember(3)] public bool Flag { get; init; }

    [StableHashMember(4)] public double Ratio { get; init; }
}

/// <summary>The nested contract member used by <see cref="MediumContract"/>.</summary>
[StableHashContract("bench.position", Version = 1)]
public sealed record Position
{
    [StableHashMember(1)] public int X { get; init; }

    [StableHashMember(2)] public int Y { get; init; }
}

/// <summary>
/// A medium-sized contract (12 members) spanning most scalar kinds, two string members, and a
/// nested <see cref="Position"/> contract member -- representative of a real domain object rather
/// than a synthetic worst case.
/// </summary>
[StableHashContract("bench.medium", Version = 1)]
public sealed record MediumContract
{
    [StableHashMember(1)] public int Id { get; init; }

    [StableHashMember(2)] public string Name { get; init; } = "";

    [StableHashMember(3)] public long Timestamp { get; init; }

    [StableHashMember(4)] public double Score { get; init; }

    [StableHashMember(5)] public bool Active { get; init; }

    [StableHashMember(6)] public Guid CorrelationId { get; init; }

    [StableHashMember(7)] public byte Level { get; init; }

    [StableHashMember(8)] public short Category { get; init; }

    [StableHashMember(9)] public ulong Checksum { get; init; }

    [StableHashMember(10)] public decimal Balance { get; init; }

    [StableHashMember(11)] public string Description { get; init; } = "";

    [StableHashMember(12)] public Position Position { get; init; } = new();
}

/// <summary>A contract whose only member is a <c>long[]</c>, used to measure collection scaling.</summary>
[StableHashContract("bench.collection", Version = 1)]
public sealed record CollectionContract
{
    [StableHashMember(1)] public long[] Values { get; init; } = [];
}

/// <summary>A contract whose only member is a <see langword="string"/>, used to compare encoding
/// paths across ASCII, multi-byte, and stackalloc-threshold-exceeding inputs.</summary>
[StableHashContract("bench.string", Version = 1)]
public sealed record StringContract
{
    [StableHashMember(1)] public string Text { get; init; } = "";
}

/// <summary>Shared fixture data so <see cref="ContractHashBenchmarks"/> and
/// <see cref="BaselineComparisonBenchmarks"/> measure the exact same medium-sized payload.</summary>
internal static class BenchmarkFixtures
{
    public static MediumContract CreateMedium() => new()
    {
        Id = 42,
        Name = "medium-contract-benchmark",
        Timestamp = 638_000_000_000_000_000L,
        Score = 98.6,
        Active = true,
        CorrelationId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        Level = 7,
        Category = 1200,
        Checksum = 0xDEAD_BEEF_CAFE_BABEUL,
        Balance = 1234.5678m,
        Description = "a representative medium-sized contract with a string and a nested contract member",
        Position = new Position { X = 10, Y = -20 },
    };
}
