// SsalKit.Guard sample
//
// Walks through the library in the order the pieces are meant to be met: the guard clauses and the
// expression text they capture for free, the [ErrorCode] declarations at the bottom of this file and
// the factory/throw helpers generated from them, the generated mapping table (including the
// derived-before-base guarantee and externally registered BCL exceptions), and finally the whole
// thing assembled into the service boundary the pattern exists for.
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

/// <summary>A player of the pretend game domain this sample maps failures for.</summary>
internal sealed record Player(string Id, int Level, string Name, Team? Team);

/// <summary>A team a player may belong to.</summary>
internal sealed record Team(string Name);

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
