// SsalKit.Randomness sample
//
// Walks through the library's main features in order: determinism, state export/restore,
// Fork(), shuffling/picking, weighted sampling (single-shot, pre-built sampler, and
// without-replacement), and the IRandomSource abstraction shared across deterministic,
// shared, and cryptographic sources.
//
// A fixed seed is used throughout so the DeterministicRandom-driven output below is exactly
// reproducible from run to run (the two lines that use SharedRandomSource/CryptoRandomSource
// at the very end are the deliberate exception -- unpredictability is the whole point of
// those two sources).

using SsalKit.Randomness;

const ulong Seed = 42;

Console.WriteLine("== SsalKit.Randomness sample ==");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 1. Determinism: two independently constructed instances seeded with the same value
//    produce the exact same output sequence, call for call, forever.
// ---------------------------------------------------------------------------------------
var detA = new DeterministicRandom(Seed);
var detB = new DeterministicRandom(Seed);

Console.WriteLine("[Determinism]    two DeterministicRandom(seed: 42) instances, side by side");
for (int i = 0; i < 3; i++)
{
    int a = detA.Next(1, 100);
    int b = detB.Next(1, 100);
    Console.WriteLine($"                 Next(1, 100)  -> A: {a,3}  B: {b,3}  match: {a == b}");
}

for (int i = 0; i < 2; i++)
{
    double a = detA.NextDouble();
    double b = detB.NextDouble();
    Console.WriteLine($"                 NextDouble()  -> A: {a:F6}  B: {b:F6}  match: {a == b}");
}
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 2. State round-trip: export detA's state right where the determinism demo above left it,
//    draw a few more values, then restore a brand-new instance from the exported state and
//    confirm it reproduces exactly the continuation that followed the export.
// ---------------------------------------------------------------------------------------
RandomState savedState = detA.ExportState();

var continuedFromA = new int[3];
for (int i = 0; i < continuedFromA.Length; i++)
{
    continuedFromA[i] = detA.Next(1, 100);
}

var restored = DeterministicRandom.FromState(savedState);
var reproducedFromRestored = new int[3];
for (int i = 0; i < reproducedFromRestored.Length; i++)
{
    reproducedFromRestored[i] = restored.Next(1, 100);
}

Console.WriteLine("[State]          ExportState() taken right after the determinism draws above");
Console.WriteLine($"                 detA continues              -> [{string.Join(", ", continuedFromA)}]");
Console.WriteLine($"                 FromState(saved) reproduces -> [{string.Join(", ", reproducedFromRestored)}]");
Console.WriteLine($"                 sequences match: {continuedFromA.SequenceEqual(reproducedFromRestored)}");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 3. Fork: Fork()'s contract is `Fork() == new DeterministicRandom(this.NextUInt64())` --
//    the parent is advanced by exactly one draw each call, so forking twice in a row from
//    an already-advancing parent yields two independent children, while forking once from
//    the *same* parent state (a freshly re-seeded parent, before any other draws) reproduces
//    the first child exactly.
// ---------------------------------------------------------------------------------------
const ulong ParentSeed = 777;

var parent1 = new DeterministicRandom(ParentSeed);
int[] childAValues = NextFew(parent1.Fork());
int[] childBValues = NextFew(parent1.Fork());

var parent2 = new DeterministicRandom(ParentSeed); // identical starting state to parent1
int[] childAReproduced = NextFew(parent2.Fork());  // parent2's first Fork() == parent1's first Fork()

Console.WriteLine("[Fork]           parent seeded with 777, forked twice in a row");
Console.WriteLine($"                 child A                      -> [{string.Join(", ", childAValues)}]");
Console.WriteLine($"                 child B                      -> [{string.Join(", ", childBValues)}]");
Console.WriteLine($"                 A and B are independent: {!childAValues.SequenceEqual(childBValues)}");
Console.WriteLine($"                 re-seeded parent's first Fork() reproduces child A: {childAReproduced.SequenceEqual(childAValues)}");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 4. Shuffle / Pick: Fisher-Yates shuffle of a deck, plus a uniform single-item pick, both
//    driven through the IRandomSource extension methods rather than DeterministicRandom's
//    own instance methods -- same algorithm, same output, either way.
// ---------------------------------------------------------------------------------------
string[] deck = ["A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K"];

var deckRng = new DeterministicRandom(Seed);
deckRng.Shuffle(deck.AsSpan());
string pickedCard = deckRng.Pick(deck.AsSpan());

Console.WriteLine("[Shuffle]        13-card deck after a Fisher-Yates shuffle");
Console.WriteLine($"                 [{string.Join(", ", deck)}]");
Console.WriteLine($"[Pick]           uniformly picked card -> {pickedCard}");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 5. Weighted single-shot picks: PickWeighted draws with probability proportional to
//    weight. Repeating it 1000 times shows the observed frequency tracking the expected
//    probability for a typical gacha-style rarity table.
// ---------------------------------------------------------------------------------------
string[] tierNames = ["N", "R", "SR", "SSR"];
long[] tierWeights = [60, 30, 9, 1];
long tierTotal = tierWeights.Sum();
Dictionary<string, long> tierWeightByName = new()
{
    ["N"] = 60,
    ["R"] = 30,
    ["SR"] = 9,
    ["SSR"] = 1,
};

var singleShotRng = new DeterministicRandom(Seed);
Dictionary<string, int> singleShotCounts = tierNames.ToDictionary(name => name, _ => 0);

const int SingleShotTrials = 1000;
for (int i = 0; i < SingleShotTrials; i++)
{
    string tier = singleShotRng.PickWeighted(tierNames.AsSpan(), tierWeights.AsSpan());
    singleShotCounts[tier]++;
}

Console.WriteLine($"[PickWeighted]   {SingleShotTrials} draws from [N 60, R 30, SR 9, SSR 1]");
PrintTierDistribution(tierNames, tierWeights, tierTotal, singleShotCounts, SingleShotTrials);
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 6. WeightedSampler<T>: build the alias table once from the same tier table, then draw
//    repeatedly in O(1) per pick -- same expected-vs-actual comparison, higher volume.
// ---------------------------------------------------------------------------------------
var sampler = WeightedSampler<string>.Create(tierNames, tierWeights.AsSpan());
var samplerRng = new DeterministicRandom(Seed);

const int SamplerTrials = 10_000;
string[] samplerDraws = sampler.PickMany(samplerRng, SamplerTrials);
Dictionary<string, int> samplerCounts = tierNames.ToDictionary(name => name, _ => 0);
foreach (string tier in samplerDraws)
{
    samplerCounts[tier]++;
}

Console.WriteLine($"[WeightedSampler] {SamplerTrials} draws from a sampler built once over the same table");
PrintTierDistribution(tierNames, tierWeights, tierTotal, samplerCounts, SamplerTrials);
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 7. Without replacement: PickManyWeightedDistinct draws several distinct items from the
//    same table, still weighted among whichever candidates remain at each step.
// ---------------------------------------------------------------------------------------
var distinctRng = new DeterministicRandom(Seed);
string[] distinctPicks = distinctRng.PickManyWeightedDistinct(tierNames, tier => tierWeightByName[tier], count: 3);

Console.WriteLine("[Distinct]       PickManyWeightedDistinct(count: 3) from the same tier table");
Console.WriteLine($"                 [{string.Join(", ", distinctPicks)}]  (no repeats: {distinctPicks.Length == distinctPicks.Distinct().Count()})");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 8. Source abstraction: the exact same helper, written once against IRandomSource, runs
//    unmodified against a deterministic, a shared, and a cryptographic source -- only the
//    caller decides which one to hand in.
// ---------------------------------------------------------------------------------------
Console.WriteLine("[IRandomSource]  same RollThreeD20(IRandomSource) helper, three different sources");
Console.WriteLine($"                 DeterministicRandom -> {RollThreeD20(new DeterministicRandom(Seed))}");
Console.WriteLine($"                 SharedRandomSource   -> {RollThreeD20(SharedRandomSource.Instance)}  (unpredictable, varies every run)");
Console.WriteLine($"                 CryptoRandomSource   -> {RollThreeD20(CryptoRandomSource.Instance)}  (unpredictable, varies every run)");

// Draws `count` values from a DeterministicRandom in [1, 100), used to compare two children
// (or a parent's continuation) draw-for-draw.
static int[] NextFew(DeterministicRandom rng, int count = 3)
{
    var values = new int[count];
    for (int i = 0; i < count; i++)
    {
        values[i] = rng.Next(1, 100);
    }

    return values;
}

// Prints the expected-vs-actual percentage for each tier of the weighted rarity table used
// in sections 5 and 6.
static void PrintTierDistribution(
    string[] tierNames,
    long[] tierWeights,
    long tierTotal,
    Dictionary<string, int> counts,
    int trials)
{
    for (int i = 0; i < tierNames.Length; i++)
    {
        string name = tierNames[i];
        double expected = 100.0 * tierWeights[i] / tierTotal;
        double actual = 100.0 * counts[name] / trials;
        Console.WriteLine($"                 {name,-4} expected {expected,5:F1}%  actual {actual,5:F1}%  ({counts[name],5} draws)");
    }
}

// Rolls three d20s (Next(1, 21)) from whatever IRandomSource is handed in -- deterministic,
// shared, or cryptographic all satisfy the same contract.
static string RollThreeD20(IRandomSource source)
{
    int[] rolls = [source.Next(1, 21), source.Next(1, 21), source.Next(1, 21)];
    return $"[{string.Join(", ", rolls)}]";
}
