// SsalKit.Guard sample
//
// Walks through the library in the order the pieces are meant to be met: the guard clauses and the
// expression text they capture for free, the [ErrorCode] declarations at the bottom of this file and
// the factory/throw helpers generated from them, the generated mapping table (including the
// derived-before-base guarantee and externally registered BCL exceptions), the whole thing assembled
// into the service boundary the pattern exists for, and finally the non-throwing half of the same
// contract -- judgements, which hand those very codes back instead of throwing them.
//
// Everything below runs against the real source generator: this project references
// SsalKit.Guard.Generator as an analyzer, exactly the way a consumer receives it inside the
// SsalKit.Guard package. Nothing here uses reflection, and no mapping is written by hand -- the
// entire GameErrors surface used below is generated from the three attributes at the bottom.

using SsalKit.Guard;

Console.WriteLine("== SsalKit.Guard sample ==");
Console.WriteLine();

var player = new Player(Id: "p-42", Level: 3, Name: "   ", Team: null);

// ---------------------------------------------------------------------------------------
// 1. Guard clauses. Each one is a domain invariant, not an argument check, and each failure
//    message contains the source text of the expression the caller wrote -- captured by
//    [CallerArgumentExpression], never typed out by hand.
// ---------------------------------------------------------------------------------------
Console.WriteLine("[Guard]          five clauses, all failing; note the caller's own expression inside each message");
PrintGuardFailure(() => Guard.That(player.Level >= 10));
PrintGuardFailure(() => Guard.NotNull(player.Team));
PrintGuardFailure(() => Guard.NotNullOrEmpty(player.Team?.Name));
PrintGuardFailure(() => Guard.NotNullOrWhiteSpace(player.Name));
PrintGuardFailure(() => Guard.InRange(player.Level, 10, 60));
Console.WriteLine();

// A passing clause returns its value, so a guard reads as part of the expression it protects
// rather than as a statement standing next to it.
var seasoned = new Player(Id: "p-7", Level: 42, Name: "Rook", Team: new Team("Blue"));
string teamName = Guard.NotNull(seasoned.Team).Name;
int level = Guard.InRange(seasoned.Level, 10, 60);

Console.WriteLine("[Guard]          on the success path a clause returns its value, so it composes");
Console.WriteLine($"                 Guard.NotNull(seasoned.Team).Name -> {teamName}");
Console.WriteLine($"                 Guard.InRange(seasoned.Level, 10, 60) -> {level}");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 2. Generated helpers. Every [ErrorCode] exception at the bottom of this file gets a factory
//    (an expression, so it composes with `throw`) and a [DoesNotReturn] throw helper, each
//    mirroring the exception's own constructor.
// ---------------------------------------------------------------------------------------
Console.WriteLine("[Helpers]        generated one pair per [ErrorCode] exception -- no hand-written throw site");

try
{
    throw GameErrors.UserNotFound($"player {player.Id} no longer exists");
}
catch (UserNotFoundException exception)
{
    Console.WriteLine($"                 throw GameErrors.UserNotFound(...) -> {exception.GetType().Name}: {exception.Message}");
}

try
{
    // The throw helper's [DoesNotReturn] is why RequireTeam below compiles without a null check
    // after it -- see the method at the bottom of this file.
    GameErrors.ThrowInvalidTeam("a team needs at least two members", new TimeoutException("roster lookup"));
}
catch (InvalidTeamException exception)
{
    Console.WriteLine($"                 GameErrors.ThrowInvalidTeam(...) -> {exception.GetType().Name}: {exception.Message}");
    Console.WriteLine($"                 inner exception mirrored through the helper -> {exception.InnerException?.GetType().Name}");
}

// The custom-exception overload of a guard: the factory runs only if the check fails, so the
// failure path is the only one that pays for building the exception.
try
{
    Guard.That(player.Level >= 10, () => GameErrors.InvalidTeam($"player {player.Id} is below the level floor"));
}
catch (InvalidTeamException exception)
{
    Console.WriteLine($"                 Guard.That(cond, () => GameErrors.InvalidTeam(...)) -> {exception.Message}");
}
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 3. The mapping table. One TryMap call answers for every registered type, and the order the
//    registrations are tested in is generated from their inheritance depth -- which is the
//    whole point: UserNotFoundException derives from NotFoundException and each carries its
//    own code, and a hand-written switch would have to be kept in that order by hand.
// ---------------------------------------------------------------------------------------
Console.WriteLine("[TryMap]         one lookup, every registration -- own codes, external codes, and misses");

Exception[] failures =
[
    new NotFoundException("no such match"),
    new UserNotFoundException("no such player"),
    new InvalidTeamException("a team needs at least two members"),
    new TimeoutException("the roster service did not answer"),
    new GuardViolationException("Guard.That (player.Level >= 10) failed."),
    new InvalidOperationException("nothing ever registered this one"),
];

foreach (Exception failure in failures)
{
    Console.WriteLine($"                 {failure.GetType().Name,-26} -> {Describe(failure)}");
}
Console.WriteLine();

// Derived-before-base, demonstrated where it actually bites: the instance is held in a variable
// of its base type, which is how it arrives at a catch clause. The match is on the runtime type,
// so it still yields the derived code.
NotFoundException heldAsBase = new UserNotFoundException("no such player");

Console.WriteLine("[Derived first]  the same instance, seen through three static types");
Console.WriteLine($"                 UserNotFoundException variable -> {Describe(new UserNotFoundException("x"))}");
Console.WriteLine($"                 NotFoundException variable     -> {Describe(heldAsBase)}");
Console.WriteLine($"                 Exception variable             -> {Describe((Exception)heldAsBase)}");
Console.WriteLine($"                 and a real base instance       -> {Describe(new NotFoundException("no such match"))}");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 4. External registrations. TimeoutException cannot carry [ErrorCode] -- nobody here owns it --
//    so it is registered on the container instead, together with GuardViolationException, which
//    is how a guard failure gets a code from this domain's own enum rather than a built-in one.
// ---------------------------------------------------------------------------------------
Console.WriteLine("[External]       types registered on the container, not on the exception");
Console.WriteLine($"                 TimeoutException         -> {Describe(new TimeoutException())}   (a BCL exception)");
Console.WriteLine($"                 GuardViolationException  -> {Describe(new GuardViolationException())}   (thrown by every Guard clause above)");
Console.WriteLine("                 no factory or throw helper is generated for either: this library does not");
Console.WriteLine("                 vouch for the constructor contract of a type it does not own.");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 5. The boundary. Catch, map, respond -- the shape the whole library builds up to. Note that
//    nothing tags an Activity or writes a log inside the exceptions themselves: they are pure
//    data, and observability belongs here, where the request context exists.
// ---------------------------------------------------------------------------------------
Console.WriteLine("[Boundary]       catch -> TryMap -> respond, one function for the whole surface");

(string Label, Action Operation)[] requests =
[
    ("GET  /players/p-42", static () => throw GameErrors.UserNotFound("player p-42 no longer exists")),
    ("POST /teams", static () => GameErrors.ThrowInvalidTeam("a team needs at least two members")),
    ("GET  /roster", static () => throw new TimeoutException("the roster service did not answer")),
    ("POST /matches", () => Guard.That(player.Level >= 10)),
    ("GET  /health", static () => { }),
    ("POST /audit", static () => throw new InvalidOperationException("nothing ever registered this one")),
];

foreach ((string label, Action operation) in requests)
{
    Console.WriteLine($"                 {label,-20} -> {Respond(operation)}");
}
Console.WriteLine();

Console.WriteLine("[RequireTeam]    [DoesNotReturn] on the throw helper ends the flow path for the compiler");
Console.WriteLine($"                 RequireTeam(seasoned) -> {RequireTeam(seasoned)}");
Console.WriteLine("                 RequireTeam(player)   -> throws; and because that path is analysed as ended,");
Console.WriteLine("                                          the nullable Team is known non-null after the call");
Console.WriteLine();

// ---------------------------------------------------------------------------------------
// 6. Judgements -- the non-throwing half of the same contract. Some rules cannot raise an
//    exception: one running inside an actor's message loop, or one whose entire job is to be
//    allowed to say no. A judgement hands back the very code an exception would have carried,
//    drawn from the same GameStatusCode enum everything above maps to. Note that not one factory
//    call below spells a type argument: Reject infers the enum from its argument, and the return
//    type it is written in supplies the rest.
// ---------------------------------------------------------------------------------------
Console.WriteLine("[Judgement]      a rule with a payload: the new state, or the one reason it refused");

Player[] candidates =
[
    seasoned,
    player,
    new Player(Id: "p-9", Level: 51, Name: "Vale", Team: new Team("Red")),
    new Player(Id: "p-13", Level: 27, Name: "Wren", Team: new Team("Red")),
];

// The granted payload is the point: it *is* the new state, so the caller keeps it and moves on,
// while a rejection leaves the roster it already had exactly as it was.
Roster current = new(MatchId: "m-88", Enlisted: 3, Capacity: 5, LevelFloor: 10);

foreach (Player candidate in candidates)
{
    Judgement<Enlistment, GameStatusCode> judgement = Enlist(current, candidate);

    // One call narrows both branches. Here `code` is GameStatusCode rather than GameStatusCode?,
    // so the refusing branch needs no `?? Unspecified` fallback for a code that is always there.
    if (judgement.TryGetRejection(out GameStatusCode code, out string message))
    {
        Console.WriteLine($"                 {candidate.Id,-5} refused  -> {code} ({(int)code}): {message}");
        continue;
    }

    // And below it there is no `!` and no second null test: ruling the rejection out is what
    // narrowed Granted to non-null, which is the whole reason the payload lives behind a T?.
    Enlistment enlistment = judgement.Granted;
    current = enlistment.Roster;

    Console.WriteLine($"                 {candidate.Id,-5} enlisted -> slot {enlistment.Slot} of {current.Capacity} on {current.MatchId}");
}
Console.WriteLine();

Console.WriteLine("[No payload]     a rule that only answers yes or no -- and cannot make the caller look");

foreach (Player candidate in candidates)
{
    Console.WriteLine($"                 {candidate.Id,-5} -> {Announce(CanClaimTitle(candidate))}");
}

Console.WriteLine($"                 and ToString stays short: {CanClaimTitle(player)}");
Console.WriteLine("                 nothing forces that check, though: with no payload to unwrap, a caller who");
Console.WriteLine("                 ignores the verdict simply carries on. Rules whose omission must stop the");
Console.WriteLine("                 build are modelled with a payload, like Enlist above.");
Console.WriteLine();

Console.WriteLine("[Fixed code]     every refusal above carries GameStatusCode.TitleNotEarned, so a three-line");
Console.WriteLine("                 helper (TitleJudgements, at the bottom of this file) returns the carrier and");
Console.WriteLine("                 each rule names only the reason -- this needs no library surface at all.");

// Runs a guard clause that is expected to fail and prints the message it produced. The expression
// text inside each message is the caller's own source, captured by the compiler.
static void PrintGuardFailure(Action clause)
{
    try
    {
        clause();
        Console.WriteLine("                 (the clause did not fail)");
    }
    catch (GuardViolationException exception)
    {
        Console.WriteLine($"                 {exception.Message}");
    }
}

// Renders what the generated mapping table says about one exception.
static string Describe(Exception exception) =>
    GameErrors.TryMap(exception, out GameStatusCode code)
        ? $"{code} ({(int)code})"
        : "<unmapped>";

// The boundary itself: every failure leaves as a code, and "no registration matched" stays
// distinguishable from any real code because TryMap reports it separately.
static string Respond(Action operation)
{
    try
    {
        operation();
        return "200 OK";
    }
    catch (Exception exception)
    {
        return GameErrors.TryMap(exception, out GameStatusCode code)
            ? $"{code} ({(int)code}) - {exception.Message}"
            : $"unmapped, rethrow - {exception.Message}";
    }
}

// The compile-time half of [DoesNotReturn]: `player.Team` is nullable, and the return statement
// dereferences it without a null check. That only compiles because ThrowUserNotFound is marked
// [DoesNotReturn], so the compiler treats the path through it as ended and knows the value is
// non-null afterwards.
static string RequireTeam(Player player)
{
    if (player.Team is null)
    {
        GameErrors.ThrowUserNotFound($"player {player.Id} is on no team");
    }

    return player.Team.Name;
}

// A domain rule that produces new state. Both refusals return a carrier that fits this return type
// without naming Enlistment -- a rejection has no payload, so the same carrier converts to either
// judgement form. That is what removes the payload type from the branch written most often.
static Judgement<Enlistment, GameStatusCode> Enlist(Roster roster, Player player)
{
    if (player.Level < roster.LevelFloor)
    {
        return Judgement.Reject(
            GameStatusCode.LevelTooLow,
            $"player {player.Id} is level {player.Level}; {roster.MatchId} starts at {roster.LevelFloor}");
    }

    if (roster.Enlisted >= roster.Capacity)
    {
        return Judgement.Reject(
            GameStatusCode.RosterFull,
            $"{roster.MatchId} already has all {roster.Capacity} places filled");
    }

    // Grant infers its payload type from the argument, exactly as it always could.
    return Judgement.Grant(new Enlistment(roster with { Enlisted = roster.Enlisted + 1 }, Slot: roster.Enlisted + 1));
}

// A rule with nothing to hand back. Every refusal in the title domain is the same code, so the
// three-line helper at the bottom of this file owns it and each site names only the reason. Both
// branches of the conditional are target-typed to the return type, so the whole rule is one
// expression with no type argument in sight.
static Judgement<GameStatusCode> CanClaimTitle(Player player) =>
    player.Level >= 40
        ? Judgement.Grant()
        : TitleJudgements.NotEarned($"player {player.Id} is level {player.Level}; the title is earned at 40");

// The other legal way to read a judgement, and the only one available without a payload: match the
// nullable code. TryGetRejection above exists because this shape hands back a GameStatusCode? that
// every consumer would otherwise have to `??` away.
static string Announce(Judgement<GameStatusCode> verdict) =>
    verdict.RejectedWith is { } code
        ? $"{code} ({(int)code}): {verdict.RejectionMessage}"
        : "the title is claimed";

/// <summary>A player of the pretend game domain this sample maps failures for.</summary>
internal sealed record Player(string Id, int Level, string Name, Team? Team);

/// <summary>A team a player may belong to.</summary>
internal sealed record Team(string Name);

/// <summary>The roster a match fills up, and the state the enlistment rule produces a new one of.</summary>
internal sealed record Roster(string MatchId, int Enlisted, int Capacity, int LevelFloor);

/// <summary>
/// What a granted enlistment hands back. Two values, one of them an <see langword="int"/>, so they
/// are bundled into a record rather than reached for one at a time: the payload of a
/// <c>Judgement&lt;T, TCode&gt;</c> is a reference type because its null is the enforcement device,
/// and bundling also rules out the half-states a pair of separately nullable fields would allow.
/// </summary>
internal sealed record Enlistment(Roster Roster, int Slot);

/// <summary>
/// The three-line recipe for a domain whose refusals always carry one code. Returning the carrier
/// rather than a judgement is what lets it fit either judgement form, exactly like
/// <c>Judgement.Reject</c> does -- so no library surface is needed for this.
/// </summary>
internal static class TitleJudgements
{
    public static RejectedJudgement<GameStatusCode> NotEarned(string message) =>
        Judgement.Reject(GameStatusCode.TitleNotEarned, message);
}

/// <summary>
/// The domain's error codes. An ordinary enum: the mapping hands one of these back and stops there,
/// so nothing in this library has an opinion about HTTP status codes or gRPC statuses.
/// </summary>
internal enum GameStatusCode
{
    Unspecified = 0,
    NotFound = 1000,
    UserNotFound = 1001,
    InvalidTeam = 1002,
    ServerBusy = 2001,

    // Handed back by the judgement rules rather than thrown. Nothing about a code says which way it
    // travels: one enum serves both halves of the contract.
    LevelTooLow = 3001,
    RosterFull = 3002,
    TitleNotEarned = 3003,

    GuardViolation = 9001,
}

/// <summary>The base of the inheritance pair, with a code of its own.</summary>
[ErrorCode<GameStatusCode>(GameStatusCode.NotFound)]
internal class NotFoundException : ErrorCodedException
{
    public NotFoundException(string? message = null)
        : base(message)
    {
    }
}

/// <summary>The derived half of the pair, with a different code.</summary>
[ErrorCode<GameStatusCode>(GameStatusCode.UserNotFound)]
internal sealed class UserNotFoundException : NotFoundException
{
    public UserNotFoundException(string? message = null)
        : base(message)
    {
    }
}

/// <summary>
/// Declares the <c>(string?, Exception?)</c> constructor, so its generated helpers mirror both
/// parameters rather than only the message.
/// </summary>
[ErrorCode<GameStatusCode>(GameStatusCode.InvalidTeam)]
internal sealed class InvalidTeamException : ErrorCodedException
{
    public InvalidTeamException(string? message = null, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The mapping container. These four lines are the entire declaration: the TryMap/MapOrDefault pair
/// and every factory and throw helper used above are generated into the other half of this class.
/// </summary>
[ErrorCodes<GameStatusCode>]
[ExternalErrorCode<GameStatusCode>(typeof(TimeoutException), GameStatusCode.ServerBusy)]
[ExternalErrorCode<GameStatusCode>(typeof(GuardViolationException), GameStatusCode.GuardViolation)]
internal static partial class GameErrors
{
}
