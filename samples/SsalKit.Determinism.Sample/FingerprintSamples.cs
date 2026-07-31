using SsalKit.Determinism;
using SsalKit.StableHashing;

// [Fingerprint]
internal static class FingerprintSamples
{
    public static void Run()
    {
        // ---------------------------------------------------------------------------------------
        // 4. Cache keys and bucketing -- the everyday case, and the one that bites quietly.
        //    HashCode.Combine and string.GetHashCode() are seeded per process, so a key built from
        //    them changes on every restart: a distributed cache never hits, a shard assignment moves
        //    under load, and an A/B experiment reshuffles its participants on each deploy. None of
        //    that fails a test; it just silently costs.
        //
        //    SSALD004 is the diagnostic for exactly this, and the replacement it names is what the
        //    [Deterministic] type below uses: a [StableHashContract] plus ComputeStableHash(), whose
        //    encoding and algorithm are both versioned contracts.
        // ---------------------------------------------------------------------------------------
        RenderCacheKey[] keys =
        [
            new() { TenantId = "acme", DocumentId = "invoice-2026-07", Revision = 3 },
            new() { TenantId = "acme", DocumentId = "invoice-2026-07", Revision = 4 },
            new() { TenantId = "acme", DocumentId = "invoice-2026-08", Revision = 3 },
            new() { TenantId = "globex", DocumentId = "invoice-2026-07", Revision = 3 },
        ];

        Console.WriteLine("[Fingerprint]    cache keys and A/B buckets derived from content, not from a per-process hash seed");
        Console.WriteLine("                 tenant   document          rev  fingerprint       bucket  group");

        foreach (var key in keys)
        {
            var fingerprint = CacheKeys.Fingerprint(key);
            var bucket = CacheKeys.Bucket(key);

            Console.WriteLine(
                $"                 {key.TenantId,-7}  {key.DocumentId,-16}  {key.Revision,3}  {fingerprint}  {bucket,6}  {(bucket < 50 ? "A" : "B")}");
        }

        // Recomputing from a separately constructed key proves only that the function is pure. The
        // stronger claim -- that these exact numbers come back after a restart, on another machine,
        // in another process -- is what the printed values themselves demonstrate: they are checked
        // into this file's expected output and have never moved.
        var recomputed = new RenderCacheKey { TenantId = "acme", DocumentId = "invoice-2026-07", Revision = 3 };

        Console.WriteLine($"                 rebuilt key -> {CacheKeys.Fingerprint(recomputed)}  (same as row 1: {CacheKeys.Fingerprint(recomputed) == CacheKeys.Fingerprint(keys[0])})");
        Console.WriteLine("                 the same fingerprints and buckets print on every process, host, and runtime version;");
        Console.WriteLine("                 HashCode.Combine over the same three fields would give four new numbers per restart.");
        Console.WriteLine();
    }
}

/// <summary>The identity of a rendered document, as a content contract rather than an object identity.</summary>
[StableHashContract("sample.determinism.render-cache-key", Version = 1)]
internal readonly record struct RenderCacheKey
{
    [StableHashMember(1)] public string TenantId { get; init; }

    [StableHashMember(2)] public string DocumentId { get; init; }

    [StableHashMember(3)] public int Revision { get; init; }
}

/// <summary>The key computation, marked so it cannot regress into a randomized hash.</summary>
[Deterministic]
internal static class CacheKeys
{
    /// <summary>Computes the stable fingerprint of a key.</summary>
    /// <param name="key">The key to fingerprint.</param>
    /// <returns>The content fingerprint.</returns>
    // HashCode.Combine(key.TenantId, key.DocumentId, key.Revision) here would be SSALD004: it
    // compiles, it looks right, and it produces different numbers after every restart.
    public static StableHash64 Fingerprint(RenderCacheKey key) => key.ComputeStableHash();

    /// <summary>Assigns a key to one of 100 buckets.</summary>
    /// <param name="key">The key to assign.</param>
    /// <returns>The bucket, in <c>[0, 100)</c>.</returns>
    public static int Bucket(RenderCacheKey key) => (int)(Fingerprint(key).Value % 100);
}
