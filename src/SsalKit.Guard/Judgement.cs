using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SsalKit.Guard;

/// <summary>
/// The only way to build a <see cref="Judgement{TCode}"/> or a <see cref="Judgement{T, TCode}"/>:
/// a rule either grants (<see cref="Grant()"/>, <see cref="Grant{T}(T)"/>) or rejects
/// (<see cref="Reject{TCode}(TCode, string)"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The non-throwing half of the guard contract.</b> <see cref="Guard"/> throws an
/// <see cref="ErrorCodedException"/> that a boundary later maps to a code; a judgement hands the
/// very same code back instead of throwing it. That is what makes it usable where an exception is
/// not an option — inside an actor's message loop, or in a rule whose whole job is to be allowed
/// to say no.
/// </para>
/// <para>
/// <b>Two outcomes, no third.</b> These factories are the only entry points and the judgement
/// types have no public constructor, so "granted and rejected at once" and "neither" are states
/// that cannot be built.
/// </para>
/// <para>
/// <b>The factories return carriers, not judgements.</b> C# infers all of a method's type
/// arguments or none of them, so a factory producing a <see cref="Judgement{T, TCode}"/> directly
/// would force every call site to spell both type arguments out. Instead each factory returns a
/// small opaque carrier holding only what its arguments imply, and an implicit conversion turns
/// that carrier into whichever judgement the target type asks for. Written in a <c>return</c>
/// statement — or any other position that supplies a target type, including both branches of a
/// conditional expression — the type arguments never appear at the call site.
/// </para>
/// <para>
/// <b>A carrier is a return value, not a value to keep.</b> Carriers deliberately have no public
/// members, so <c>var pending = Judgement.Grant(state);</c> compiles into something nothing can be
/// done with. Convert where the judgement is produced.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // With a payload: the rule hands back the new state, or the one reason it refused.
/// public static Judgement&lt;Inventory, ShopCode&gt; Buy(Inventory inventory, ItemId item)
/// {
///     if (!inventory.Sells(item))
///     {
///         return Judgement.Reject(ShopCode.NotSold, $"The shop does not sell {item}.");
///     }
///
///     return Judgement.Grant(inventory.WithPurchase(item));
/// }
///
/// // Without one: the rule only answers yes or no.
/// public static Judgement&lt;ShopCode&gt; CanTrade(Player player) =&gt;
///     player.IsBanned
///         ? Judgement.Reject(ShopCode.Banned, "Trading is suspended for this account.")
///         : Judgement.Grant();
/// </code>
/// </example>
public static class Judgement
{
    /// <summary>
    /// Grants a judgement that carries no new state.
    /// </summary>
    /// <returns>
    /// A carrier that converts to a granted <see cref="Judgement{TCode}"/> for whichever code enum
    /// the target type names.
    /// </returns>
    /// <remarks>
    /// A grant without a payload has nothing to carry, so the carrier holds no state and every
    /// call hands back the same value.
    /// </remarks>
    public static GrantedJudgement Grant() => default;

    /// <summary>
    /// Grants a judgement carrying <paramref name="granted"/> as the new state.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the new state, inferred from <paramref name="granted"/>.
    /// </typeparam>
    /// <param name="granted">
    /// The new state the rule produced. Bundle several values into a <c>record</c> rather than
    /// reaching for a tuple — see the remarks on <see cref="Judgement{T, TCode}"/>.
    /// </param>
    /// <returns>
    /// A carrier that converts to a granted <see cref="Judgement{T, TCode}"/> for whichever code
    /// enum the target type names.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="granted"/> is <see langword="null"/>. A null payload is what a rejection
    /// looks like, so it may not be produced by a grant.
    /// </exception>
    public static GrantedJudgement<T> Grant<T>(T granted)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(granted);

        return new GrantedJudgement<T>(granted);
    }

    /// <summary>
    /// Rejects with one code and one human-readable reason.
    /// </summary>
    /// <typeparam name="TCode">
    /// The error-code enum, inferred from <paramref name="code"/>.
    /// </typeparam>
    /// <param name="code">The code the caller is expected to act on.</param>
    /// <param name="message">
    /// Why the rule refused, for a human reading a log or a client message. The empty string is
    /// allowed; <see langword="null"/> is not.
    /// </param>
    /// <returns>
    /// A carrier that converts to a rejected <see cref="Judgement{TCode}"/> or a rejected
    /// <see cref="Judgement{T, TCode}"/> — a rejection carries no payload, so it fits either
    /// return type, which is what keeps the payload type off the call site.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="message"/> is <see langword="null"/>.
    /// </exception>
    public static RejectedJudgement<TCode> Reject<TCode>(TCode code, string message)
        where TCode : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(message);

        return new RejectedJudgement<TCode>(code, message);
    }
}

/// <summary>
/// The outcome of one rule check that produces no new state: granted, or rejected with one code
/// and one message.
/// </summary>
/// <typeparam name="TCode">
/// The enum a rejection is expressed in — the same code vocabulary
/// <c>[ErrorCode&lt;TCode&gt;]</c> attaches to exception types.
/// </typeparam>
/// <remarks>
/// <para>
/// <b>This form cannot make the caller look.</b> There is no payload to unwrap, so a caller that
/// forgets to check simply carries on. That is a real limitation, not an oversight: when a missed
/// check must stop the build, model the rule so that it returns the new state and use
/// <see cref="Judgement{T, TCode}"/>, whose payload cannot be reached without ruling the rejection
/// out first.
/// </para>
/// <para>
/// <b>Instances come from the factory.</b> The constructor is private; a value arrives either as
/// <see cref="Granted"/> or by implicit conversion from what
/// <see cref="Judgement.Grant()"/> and <see cref="Judgement.Reject{TCode}(TCode, string)"/>
/// returned. Equality is the record default over the code and the message.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var verdict = Quest.CanAccept(player, questId);
/// if (verdict.TryGetRejection(out var code, out var message))
/// {
///     Sender.Tell(new RequestRejected(code, message));
///     return;
/// }
/// </code>
/// </example>
public sealed record Judgement<TCode>
    where TCode : struct, Enum
{
    private Judgement(TCode? rejectedWith, string rejectionMessage)
    {
        RejectedWith = rejectedWith;
        RejectionMessage = rejectionMessage;
    }

    /// <summary>
    /// Gets the granted judgement for <typeparamref name="TCode"/>.
    /// </summary>
    /// <remarks>
    /// A granted judgement holds nothing that varies, so one instance is allocated per closed
    /// <typeparamref name="TCode"/> and handed out to every caller — including the conversion from
    /// <see cref="Judgement.Grant()"/>.
    /// </remarks>
    public static Judgement<TCode> Granted { get; } = new(null, string.Empty);

    /// <summary>
    /// Gets the code this judgement was rejected with, or <see langword="null"/> when the rule
    /// passed.
    /// </summary>
    public TCode? RejectedWith { get; }

    /// <summary>
    /// Gets why the rule refused, or the empty string when it passed.
    /// </summary>
    public string RejectionMessage { get; }

    /// <summary>
    /// Gets a value indicating whether the rule passed.
    /// </summary>
    public bool IsGranted => RejectedWith is null;

    /// <summary>
    /// Reads the rejection out in one step: <see langword="true"/> along with the code and message
    /// when the rule refused, <see langword="false"/> when it passed.
    /// </summary>
    /// <param name="code">
    /// The code the rule rejected with. Set to <see langword="default"/> when this method returns
    /// <see langword="false"/>.
    /// </param>
    /// <param name="message">
    /// Why the rule refused. Set to the empty string when this method returns
    /// <see langword="false"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when this judgement is a rejection; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// The outputs are non-nullable so that the rejection branch needs no <c>??</c> fallback for a
    /// code that is always there. The price is that they are only meaningful once the return value
    /// has been read: reading the outputs while ignoring the result is outside the contract, and
    /// nothing in the compiler catches it.
    /// </remarks>
    public bool TryGetRejection(out TCode code, out string message)
    {
        if (RejectedWith is { } rejected)
        {
            code = rejected;
            message = RejectionMessage;
            return true;
        }

        code = default;
        message = string.Empty;
        return false;
    }

    /// <summary>
    /// Converts what <see cref="Judgement.Grant()"/> returned into a granted judgement.
    /// </summary>
    /// <param name="granted">The carrier to convert. It holds no state.</param>
    public static implicit operator Judgement<TCode>(GrantedJudgement granted) => Granted;

    /// <summary>
    /// Converts what <see cref="Judgement.Reject{TCode}(TCode, string)"/> returned into a rejected
    /// judgement.
    /// </summary>
    /// <param name="rejected">The carrier holding the code and the message.</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="rejected"/> is <see langword="default"/>, so it never went through
    /// <see cref="Judgement.Reject{TCode}(TCode, string)"/> and has no message.
    /// </exception>
    public static implicit operator Judgement<TCode>(RejectedJudgement<TCode> rejected)
        => new(rejected.Code, rejected.RequireMessage());

    /// <summary>
    /// Returns <c>Granted</c>, or <c>Rejected(Code): message</c>.
    /// </summary>
    /// <returns>A short, stable rendering meant for logs.</returns>
    public override string ToString()
        => RejectedWith is { } rejected ? $"Rejected({rejected}): {RejectionMessage}" : "Granted";
}

/// <summary>
/// The outcome of one rule check that produces new state: the new state when the rule passed, or
/// one code and one message when it did not.
/// </summary>
/// <typeparam name="T">
/// The new state a granted rule produces. Reference types only — see the remarks.
/// </typeparam>
/// <typeparam name="TCode">
/// The enum a rejection is expressed in — the same code vocabulary
/// <c>[ErrorCode&lt;TCode&gt;]</c> attaches to exception types.
/// </typeparam>
/// <remarks>
/// <para>
/// <b>Forgetting the rejection check stops the build.</b> The two outcomes do not overlap:
/// <see cref="Granted"/> is non-<see langword="null"/> exactly when <see cref="RejectedWith"/> is
/// <see langword="null"/>. Since the new state is reachable only through a nullable reference, code
/// that uses it without ruling the rejection out first is dereferencing a maybe-null value.
/// </para>
/// <para>
/// <b>How hard that stops depends on the consuming project.</b> It is a build error only where
/// <c>Nullable</c> is enabled and warnings are errors; elsewhere the same mistake is a warning, or
/// nothing at all. This is a helpful nudge in the right direction, not a guarantee.
/// </para>
/// <para>
/// <b><c>where T : class</c> is the device, not a restriction.</b> The null check is the whole
/// enforcement mechanism, so a payload that cannot be null would remove the reason this type
/// exists. When the new state is several values — including value types — bundle them into a
/// <c>sealed record</c> and use that as <typeparamref name="T"/>. That also rules out the illegal
/// half-states a bag of individually nullable fields would allow.
/// </para>
/// <para>
/// <b>Instances come from the factory.</b> The constructor is private; a value arrives by implicit
/// conversion from what <see cref="Judgement.Grant{T}(T)"/> or
/// <see cref="Judgement.Reject{TCode}(TCode, string)"/> returned. Equality is the record default,
/// which compares the payload with <typeparamref name="T"/>'s own equality.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var verdict = Shop.Buy(inventory, itemId);
/// if (verdict.TryGetRejection(out var code, out var message))
/// {
///     Sender.Tell(new RequestRejected(code, message));
///     return;
/// }
///
/// // No '!' and no null check: the false branch narrowed Granted to non-null.
/// inventory = verdict.Granted;
/// </code>
/// </example>
public sealed record Judgement<T, TCode>
    where T : class
    where TCode : struct, Enum
{
    private Judgement(T? granted, TCode? rejectedWith, string rejectionMessage)
    {
        Granted = granted;
        RejectedWith = rejectedWith;
        RejectionMessage = rejectionMessage;
    }

    /// <summary>
    /// Gets the new state the rule produced, or <see langword="null"/> when it refused — in which
    /// case the state the caller already had still stands.
    /// </summary>
    public T? Granted { get; }

    /// <summary>
    /// Gets the code this judgement was rejected with, or <see langword="null"/> when the rule
    /// passed.
    /// </summary>
    public TCode? RejectedWith { get; }

    /// <summary>
    /// Gets why the rule refused, or the empty string when it passed.
    /// </summary>
    public string RejectionMessage { get; }

    /// <summary>
    /// Gets a value indicating whether the rule passed, narrowing <see cref="Granted"/> to
    /// non-<see langword="null"/> when it did.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Granted))]
    public bool IsGranted => Granted is not null;

    /// <summary>
    /// Reads the rejection out in one step: <see langword="true"/> along with the code and message
    /// when the rule refused, <see langword="false"/> — narrowing <see cref="Granted"/> to
    /// non-<see langword="null"/> — when it passed.
    /// </summary>
    /// <param name="code">
    /// The code the rule rejected with. Set to <see langword="default"/> when this method returns
    /// <see langword="false"/>.
    /// </param>
    /// <param name="message">
    /// Why the rule refused. Set to the empty string when this method returns
    /// <see langword="false"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when this judgement is a rejection; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// One call narrows both branches: the rejection branch gets a non-nullable code, so it needs
    /// no <c>??</c> fallback, and the branch after it gets a non-nullable
    /// <see cref="Granted"/>, so it needs no <c>!</c>. The outputs are only meaningful once the
    /// return value has been read; reading them while ignoring the result is outside the contract,
    /// and nothing in the compiler catches it.
    /// </remarks>
    [MemberNotNullWhen(false, nameof(Granted))]
    public bool TryGetRejection(out TCode code, out string message)
    {
        if (Granted is not null)
        {
            code = default;
            message = string.Empty;
            return false;
        }

        // The two outcomes are set together by the private constructors, so a missing payload
        // always means a code is present.
        Debug.Assert(RejectedWith is not null, "A judgement without a payload must carry a code.");

        code = RejectedWith.GetValueOrDefault();
        message = RejectionMessage;
        return true;
    }

    /// <summary>
    /// Converts what <see cref="Judgement.Grant{T}(T)"/> returned into a granted judgement.
    /// </summary>
    /// <param name="granted">The carrier holding the new state.</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="granted"/> is <see langword="default"/>, so it never went through
    /// <see cref="Judgement.Grant{T}(T)"/> and has no state to grant.
    /// </exception>
    public static implicit operator Judgement<T, TCode>(GrantedJudgement<T> granted)
        => new(granted.RequirePayload(), null, string.Empty);

    /// <summary>
    /// Converts what <see cref="Judgement.Reject{TCode}(TCode, string)"/> returned into a rejected
    /// judgement.
    /// </summary>
    /// <param name="rejected">The carrier holding the code and the message.</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="rejected"/> is <see langword="default"/>, so it never went through
    /// <see cref="Judgement.Reject{TCode}(TCode, string)"/> and has no message.
    /// </exception>
    public static implicit operator Judgement<T, TCode>(RejectedJudgement<TCode> rejected)
        => new(null, rejected.Code, rejected.RequireMessage());

    /// <summary>
    /// Returns <c>Granted(state)</c>, or <c>Rejected(Code): message</c>.
    /// </summary>
    /// <returns>
    /// A short, stable rendering meant for logs — deliberately not the record default, which would
    /// dump the whole payload.
    /// </returns>
    public override string ToString()
        => Granted is { } granted
            ? $"Granted({granted})"
            : $"Rejected({RejectedWith}): {RejectionMessage}";
}
