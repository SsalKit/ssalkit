// SsalKit.StableHashing sample
//
// Walks through the library's main use cases in order: defining a [StableHashContract] and
// calling the generated ComputeStableHash(), the equality-consistency invariant that decimal /
// DateTimeOffset / float-double all have to honor (design doc section 4.2), using tick-by-tick
// hash comparison to pinpoint where two simulations desynced, deriving a reproducible
// DeterministicRandom seed from a contract hash, skipping redundant snapshot saves via a
// fingerprint comparison, and deterministic A/B bucketing.
//
// Every input below is fixed, so the output is byte-for-byte identical on every run -- that is
// the entire point of a "stable" hash.

using SsalKit.Randomness;
using SsalKit.StableHashing;

Console.WriteLine("== SsalKit.StableHashing sample ==");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 1. [Contract]: define a contract with [StableHashContract]/[StableHashMember], call the
//    generated ComputeStableHash(), and print it as hex. Two separately constructed instances
//    with the same member values produce the exact same hash -- CLR identity plays no part.
// ---------------------------------------------------------------------------------------
var snapshotA = new PlayerSnapshot { PlayerId = "player-42", Level = 17, Gold = 2_450 };
var snapshotB = new PlayerSnapshot { PlayerId = "player-42", Level = 17, Gold = 2_450 };
var snapshotC = new PlayerSnapshot { PlayerId = "player-42", Level = 17, Gold = 2_451 }; // one gold different

StableHash64 hashA = snapshotA.ComputeStableHash();
StableHash64 hashB = snapshotB.ComputeStableHash();
StableHash64 hashC = snapshotC.ComputeStableHash();

Console.WriteLine("[Contract]       PlayerSnapshot { PlayerId = \"player-42\", Level = 17, Gold = 2450 }");
Console.WriteLine($"                 instance A -> {hashA}");
Console.WriteLine($"                 instance B -> {hashB}  (separate instance, same values, match: {hashA == hashB})");
Console.WriteLine($"                 instance C -> {hashC}  (Gold off by 1, match: {hashA == hashC})");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 2. [Equality]: the equality-consistency invariant (design doc section 4.2) -- for every
//    supported type, a == b must imply encode(a) == encode(b). Three traps, each isolated by
//    holding the other two members fixed while only the demonstrated member differs.
// ---------------------------------------------------------------------------------------
var fixedTimestamp = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

var decimalProbeOneZero = new EqualityProbe { Amount = 1.0m, Timestamp = fixedTimestamp, Delta = 0.0 };
var decimalProbeOneZeroZero = new EqualityProbe { Amount = 1.00m, Timestamp = fixedTimestamp, Delta = 0.0 };

var noonUtc = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
var sameInstantPlusNine = new DateTimeOffset(2026, 7, 30, 21, 0, 0, TimeSpan.FromHours(9)); // same instant, different offset
var offsetProbeUtc = new EqualityProbe { Amount = 1.0m, Timestamp = noonUtc, Delta = 0.0 };
var offsetProbePlusNine = new EqualityProbe { Amount = 1.0m, Timestamp = sameInstantPlusNine, Delta = 0.0 };

var zeroProbeNegative = new EqualityProbe { Amount = 1.0m, Timestamp = fixedTimestamp, Delta = -0.0 };
var zeroProbePositive = new EqualityProbe { Amount = 1.0m, Timestamp = fixedTimestamp, Delta = +0.0 };

Console.WriteLine("[Equality]       equality-consistency invariant: a == b implies encode(a) == encode(b)");
Console.WriteLine($"                 1.0m vs 1.00m (different scale, decimal == true)              -> match: {decimalProbeOneZero.ComputeStableHash() == decimalProbeOneZeroZero.ComputeStableHash()}");
Console.WriteLine($"                 12:00+00:00 vs 21:00+09:00 (same instant, DateTimeOffset == true) -> match: {offsetProbeUtc.ComputeStableHash() == offsetProbePlusNine.ComputeStableHash()}");
Console.WriteLine($"                 -0.0 vs +0.0 (different bits, double == true)                  -> match: {zeroProbeNegative.ComputeStableHash() == zeroProbePositive.ComputeStableHash()}");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 3. [Desync]: run two lockstep simulations from the same starting state and the same
//    DeterministicRandom seed (so absent a bug, every tick matches). One simulation has a bug
//    injected at a fixed tick; comparing hashes tick by tick pinpoints exactly where they
//    diverge, the same technique used to catch desyncs in networked/replay simulations.
// ---------------------------------------------------------------------------------------
const int TickCount = 8;
const int BuggyTick = 5;

var stateA = new BattleState { Tick = 0, PlayerHp = 100, EnemyHp = 100 };
var stateB = stateA;

var rngA = new DeterministicRandom(999);
var rngB = new DeterministicRandom(999); // identical seed -- rngA and rngB draw identically

Console.WriteLine($"[Desync]         two BattleState simulations, seed 999, a bug injected at tick {BuggyTick}");

int? divergedAtTick = null;
for (int tick = 1; tick <= TickCount; tick++)
{
    int damageA = rngA.Next(5, 15);
    int damageB = rngB.Next(5, 15); // same sequence as damageA -- the seeds match

    // Simulation B has a bug: at BuggyTick it applies one point of extra, unintended damage.
    int bugDamage = tick == BuggyTick ? 1 : 0;

    stateA = stateA with { Tick = tick, EnemyHp = stateA.EnemyHp - damageA };
    stateB = stateB with { Tick = tick, EnemyHp = stateB.EnemyHp - damageB - bugDamage };

    StableHash64 tickHashA = stateA.ComputeStableHash();
    StableHash64 tickHashB = stateB.ComputeStableHash();
    bool tickMatches = tickHashA == tickHashB;
    divergedAtTick ??= tickMatches ? null : tick;

    Console.WriteLine($"                 tick {tick,2}  A={tickHashA}  B={tickHashB}  match: {tickMatches}");
}

Console.WriteLine($"                 first divergent tick -> {divergedAtTick}");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 4. [Seed]: a contract hash of (playerId, dayNumber) becomes the seed for a
//    DeterministicRandom, which then deterministically shuffles a fixed item pool into a daily
//    shop list. The same key always reproduces the same list -- two independently seeded
//    DeterministicRandom instances below prove it.
// ---------------------------------------------------------------------------------------
var shopSeedKey = new ShopSeedKey { PlayerId = "player-42", DayNumber = 19 };
StableHash64 shopSeedHash = shopSeedKey.ComputeStableHash();

string[] itemPool =
[
    "Health Potion", "Mana Potion", "Iron Sword", "Steel Shield",
    "Fire Scroll", "Ice Wand", "Leather Armor", "Silver Ring",
];

string[] shopListRun1 = BuildDailyShop(itemPool, new DeterministicRandom(shopSeedHash.Value));
string[] shopListRun2 = BuildDailyShop(itemPool, new DeterministicRandom(shopSeedHash.Value));

Console.WriteLine($"[Seed]           ShopSeedKey {{ PlayerId = \"player-42\", DayNumber = 19 }} -> hash {shopSeedHash}");
Console.WriteLine($"                 DeterministicRandom(hash.Value) run 1 -> [{string.Join(", ", shopListRun1)}]");
Console.WriteLine($"                 DeterministicRandom(hash.Value) run 2 -> [{string.Join(", ", shopListRun2)}]");
Console.WriteLine($"                 reproducible: {shopListRun1.SequenceEqual(shopListRun2)}");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 5. [Fingerprint]: compare a new snapshot's hash against the last-saved snapshot's hash before
//    writing anything out. Identical settings never trigger a save; a real change always does --
//    a common pattern for caches, config writers, and change-tracked persistence layers.
// ---------------------------------------------------------------------------------------
SettingsSnapshot[] settingsUpdates =
[
    new() { Volume = 80, Fullscreen = true, Resolution = "1920x1080" },
    new() { Volume = 80, Fullscreen = true, Resolution = "1920x1080" }, // unchanged
    new() { Volume = 60, Fullscreen = true, Resolution = "1920x1080" }, // volume changed
    new() { Volume = 60, Fullscreen = true, Resolution = "1920x1080" }, // unchanged
    new() { Volume = 60, Fullscreen = false, Resolution = "2560x1440" }, // fullscreen + resolution changed
];

Console.WriteLine("[Fingerprint]    saving only when the settings snapshot's hash actually changes");

StableHash64? lastSavedHash = null;
int savedCount = 0;
foreach (SettingsSnapshot update in settingsUpdates)
{
    StableHash64 updateHash = update.ComputeStableHash();
    bool changed = lastSavedHash is null || lastSavedHash.Value != updateHash;

    Console.WriteLine($"                 {update.Volume,3} vol, fullscreen={update.Fullscreen,-5}, {update.Resolution,-9} -> {updateHash}  {(changed ? "SAVE" : "skip (unchanged)")}");

    if (changed)
    {
        lastSavedHash = updateHash;
        savedCount++;
    }
}

Console.WriteLine($"                 saved {savedCount} of {settingsUpdates.Length} snapshots");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 6. [Bucketing]: hash % 100 turns a (userId, experimentName) contract hash into a stable A/B
//    bucket assignment. The same user always lands in the same bucket for the same experiment,
//    on any machine, on any run, without storing an assignment table anywhere.
// ---------------------------------------------------------------------------------------
string[] userIds = ["user-001", "user-002", "user-003", "user-004", "user-005", "user-006"];
const string ExperimentName = "checkout-redesign";

Console.WriteLine($"[Bucketing]      hash % 100 A/B assignment for experiment \"{ExperimentName}\"");
foreach (string userId in userIds)
{
    var bucketKey = new BucketKey { UserId = userId, ExperimentName = ExperimentName };
    ulong bucket = bucketKey.ComputeStableHash().Value % 100;
    string group = bucket < 50 ? "A" : "B";
    Console.WriteLine($"                 {userId,-9} bucket={bucket,2}  group={group}");
}

// Deterministically shuffles the item pool with the given source and returns the first four
// items -- used by section 4 to build a "daily shop" from a seed derived from a contract hash.
static string[] BuildDailyShop(string[] itemPool, IRandomSource source)
{
    string[] pool = (string[])itemPool.Clone();
    source.Shuffle(pool.AsSpan());
    return pool[..4];
}

// ---------------------------------------------------------------------------------------
// Contract types used by the sections above.
// ---------------------------------------------------------------------------------------

/// <summary>Section 1 -- a basic contract with only scalar members.</summary>
[StableHashContract("sample.player-snapshot", Version = 1)]
public sealed record PlayerSnapshot
{
    [StableHashMember(1)] public string PlayerId { get; init; } = "";

    [StableHashMember(2)] public int Level { get; init; }

    [StableHashMember(3)] public int Gold { get; init; }
}

/// <summary>Section 2 -- one member for each of the three equality-consistency traps.</summary>
[StableHashContract("sample.equality-probe", Version = 1)]
public sealed record EqualityProbe
{
    [StableHashMember(1)] public decimal Amount { get; init; }

    [StableHashMember(2)] public DateTimeOffset Timestamp { get; init; }

    [StableHashMember(3)] public double Delta { get; init; }
}

/// <summary>Section 3 -- a per-tick simulation snapshot compared across two lockstep runs.</summary>
[StableHashContract("sample.battle-state", Version = 1)]
public sealed record BattleState
{
    [StableHashMember(1)] public int Tick { get; init; }

    [StableHashMember(2)] public int PlayerHp { get; init; }

    [StableHashMember(3)] public int EnemyHp { get; init; }
}

/// <summary>Section 4 -- the key hashed into a named random stream seed.</summary>
[StableHashContract("sample.shop-seed-key", Version = 1)]
public readonly record struct ShopSeedKey
{
    [StableHashMember(1)] public string PlayerId { get; init; }

    [StableHashMember(2)] public int DayNumber { get; init; }
}

/// <summary>Section 5 -- a settings snapshot compared hash-to-hash before saving.</summary>
[StableHashContract("sample.settings-snapshot", Version = 1)]
public sealed record SettingsSnapshot
{
    [StableHashMember(1)] public int Volume { get; init; }

    [StableHashMember(2)] public bool Fullscreen { get; init; }

    [StableHashMember(3)] public string Resolution { get; init; } = "";
}

/// <summary>Section 6 -- the key hashed into a deterministic A/B bucket assignment.</summary>
[StableHashContract("sample.bucket-key", Version = 1)]
public readonly record struct BucketKey
{
    [StableHashMember(1)] public string UserId { get; init; }

    [StableHashMember(2)] public string ExperimentName { get; init; }
}
