using System.Collections.Immutable;
using SsalKit.Determinism;
using SsalKit.Randomness;
using SsalKit.StableHashing;
using SsalKit.Timekeeping;

/// <summary>
/// The player's input for a single tick -- together with the seed, the entire input of a run.
/// Everything else the simulation does is derived from these two.
/// </summary>
internal enum BattleCommand
{
    Attack,
    Special,
    Guard,
}

/// <summary>Events the simulation schedules for itself, at logical ticks rather than instants.</summary>
internal enum BattleEvent
{
    EnemyWave,
}

/// <summary>
/// The observable state of a simulation after a tick, as a stable-hash contract so two runs can be
/// compared by checksum instead of field by field (see the [Desync] and [Replay] groups).
/// </summary>
[StableHashContract("sample.determinism.battle-state", Version = 1)]
internal sealed record BattleState
{
    [StableHashMember(1)] public long Tick { get; init; }

    [StableHashMember(2)] public int PlayerHp { get; init; }

    [StableHashMember(3)] public int EnemyHp { get; init; }

    [StableHashMember(4)] public int PendingEvents { get; init; }
}

/// <summary>
/// A lockstep battle core: given a seed and a command script, every run of it produces the same
/// states, on any machine, in any process, forever.
/// </summary>
/// <remarks>
/// <para>
/// This is the "three libraries used together" demonstration the whole sample exists for. All three
/// of the usual sources of drift are handled by construction, which is exactly why the analyzer has
/// nothing to report here:
/// </para>
/// <list type="bullet">
/// <item><description>Randomness comes from a seeded <see cref="DeterministicRandom"/> whose
/// algorithm is a versioned contract -- not <c>Random.Shared</c>, and not <c>new Random(seed)</c>
/// either, whose sequence is not guaranteed across runtime versions (SSALD002).</description></item>
/// <item><description>Time is logical. <see cref="TickSchedule{TEvent}"/> queues events at ticks,
/// and the <see cref="Cooldown"/> is evaluated against an instant <em>derived from the tick number</em>
/// (<see cref="InstantAt"/>) rather than read from a clock (SSALD001). The epoch below is a fixed
/// literal, so "now" is a pure function of the tick.</description></item>
/// <item><description>Identity is content-derived: state is compared by
/// <c>ComputeStableHash()</c>, never by <c>GetHashCode()</c>/<c>HashCode.Combine</c>, whose seed is
/// randomized per process (SSALD004).</description></item>
/// </list>
/// <para>
/// <c>[Deterministic]</c> is on the type, so the scope covers every member of it -- including the
/// property initializers and the local function bodies. Note that it does not extend to types this
/// one calls into: the analysis is shallow by design, so a helper type would have to be marked too.
/// </para>
/// </remarks>
[Deterministic]
internal sealed class BattleSimulation
{
    private const int WaveInterval = 3;
    private const int MaxPlayerHp = 100;
    private const int StartingEnemyHp = 200;

    /// <summary>
    /// The instant tick 0 corresponds to. A fixed literal, never a clock reading: it is what turns
    /// the elapsed-time API (<see cref="Cooldown"/>, which is specified against instants) into a
    /// pure function of the tick number.
    /// </summary>
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan SpecialCooldown = TimeSpan.FromSeconds(4);

    private readonly DeterministicRandom _random;

    private TickSchedule<BattleEvent> _schedule;
    private Cooldown _special;
    private long _tick;
    private int _playerHp = MaxPlayerHp;
    private int _enemyHp = StartingEnemyHp;

    public BattleSimulation(ulong seed)
    {
        _random = new DeterministicRandom(seed);
        _special = Cooldown.Create(SpecialCooldown, InstantAt(0));
        _schedule = TickSchedule<BattleEvent>.Empty.Add(BattleEvent.EnemyWave, WaveInterval);
    }

    /// <summary>Gets the state after the last <see cref="Advance"/> call.</summary>
    public BattleState State => new()
    {
        Tick = _tick,
        PlayerHp = _playerHp,
        EnemyHp = _enemyHp,
        PendingEvents = _schedule.Count,
    };

    /// <summary>Gets the content checksum of <see cref="State"/>.</summary>
    public StableHash64 Checksum => State.ComputeStableHash();

    /// <summary>Maps a logical tick onto the virtual instant the elapsed-time API is asked about.</summary>
    /// <param name="tick">The logical tick.</param>
    /// <returns>The instant that tick corresponds to.</returns>
    public static DateTimeOffset InstantAt(long tick) => Epoch.AddSeconds(tick);

    /// <summary>Advances the simulation by one tick.</summary>
    /// <param name="command">The command issued for this tick.</param>
    /// <param name="injectedDamage">Extra damage applied to the enemy this tick. Always <c>0</c>
    /// except in the [Desync] group, which uses it to corrupt one of two otherwise identical runs;
    /// a real simulation would have no such parameter.</param>
    public void Advance(BattleCommand command, int injectedDamage = 0)
    {
        _tick++;

        // "Now" for this tick, derived from the tick number rather than read from a clock.
        var asOf = InstantAt(_tick);

        // Scheduled events first, in the schedule's permanent (DueTick, Sequence) dispatch order --
        // which does not depend on storage order, so it survives a save/restore round trip.
        var due = _schedule.PopDue(_tick, out var afterPop);
        _schedule = afterPop;

        foreach (var entry in due)
        {
            if (entry.Event == BattleEvent.EnemyWave)
            {
                _playerHp = Math.Max(0, _playerHp - _random.Next(3, 8));
                _schedule = _schedule.Add(BattleEvent.EnemyWave, _tick + WaveInterval);
            }
        }

        var damage = 0;

        switch (command)
        {
            case BattleCommand.Attack:
                damage = _random.Next(5, 15);
                break;

            case BattleCommand.Special:
                // The cooldown is asked about `asOf`, an argument -- the type never reads a clock of
                // its own, which is what makes a replay of the same tick sequence land identically.
                if (_special.TryUse(asOf, out var afterUse))
                {
                    _special = afterUse;
                    damage = _random.Next(20, 30);
                }

                break;

            case BattleCommand.Guard:
            default:
                _playerHp = Math.Min(MaxPlayerHp, _playerHp + 3);
                break;
        }

        _enemyHp = Math.Max(0, _enemyHp - damage - injectedDamage);
    }
}

/// <summary>
/// The fixed run every group replays: one seed, one command script. Sharing them is what lets the
/// [Simulation], [Desync], and [Replay] groups compare their outputs against each other.
/// </summary>
internal static class BattleScript
{
    /// <summary>The seed of every run in this sample. A literal, so runs are comparable across processes.</summary>
    public const ulong Seed = 20260731;

    /// <summary>
    /// The command script, one entry per tick. The <see cref="BattleCommand.Special"/> at tick 4
    /// deliberately falls inside the cooldown started at tick 2 and fizzles; the one at tick 6 lands
    /// exactly on the ready instant, which the boundary-inclusive cooldown accepts.
    /// </summary>
    public static readonly ImmutableArray<BattleCommand> Commands =
    [
        BattleCommand.Attack,  // tick 1
        BattleCommand.Special, // tick 2  -- lands, starts a 4-second cooldown
        BattleCommand.Attack,  // tick 3  -- first enemy wave is due here
        BattleCommand.Special, // tick 4  -- still on cooldown, fizzles
        BattleCommand.Guard,   // tick 5
        BattleCommand.Special, // tick 6  -- exactly at ReadyAt, lands; second enemy wave due
        BattleCommand.Attack,  // tick 7
        BattleCommand.Attack,  // tick 8
    ];

    /// <summary>Renders a command for the console table.</summary>
    /// <param name="command">The command to render.</param>
    /// <returns>The lower-case command name, padded to a fixed width.</returns>
    public static string Label(BattleCommand command) => command switch
    {
        BattleCommand.Attack => "attack ",
        BattleCommand.Special => "special",
        _ => "guard  ",
    };
}
