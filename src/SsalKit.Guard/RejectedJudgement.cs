namespace SsalKit.Guard;

/// <summary>
/// What <see cref="Judgement.Reject{TCode}(TCode, string)"/> returns: one code and one message,
/// not yet told what the granting side of the rule would have produced.
/// </summary>
/// <typeparam name="TCode">The error-code enum the rejection is expressed in.</typeparam>
/// <remarks>
/// <para>
/// <b>Return it, do not keep it.</b> This type has no public members on purpose — it exists only
/// long enough for an implicit conversion to turn it into a judgement, which is what lets the call
/// site write <c>return Judgement.Reject(code, message);</c> without naming the payload type.
/// </para>
/// <para>
/// <b>It converts both ways.</b> A rejection carries no payload, so the same carrier becomes
/// either a <see cref="Judgement{TCode}"/> or a <see cref="Judgement{T, TCode}"/>, whichever the
/// target type asks for. That is what removes the payload type from the refusing branch of a rule
/// — the branch that, in practice, is written far more often than the granting one.
/// </para>
/// <para>
/// <b>A rule whose rejections always use one code</b> needs nothing from this library beyond a
/// three-line helper that returns this carrier; see the example.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// internal static class TitleJudgements
/// {
///     // Every refusal in this domain is the same code, so only the reason varies.
///     public static RejectedJudgement&lt;ErrorCode&gt; NotEarned(string message) =&gt;
///         Judgement.Reject(ErrorCode.TitleNotEarned, message);
/// }
///
/// // Fits either return type, exactly like Judgement.Reject does.
/// return TitleJudgements.NotEarned($"Take part in defeating {target} to earn this title.");
/// </code>
/// </example>
public readonly struct RejectedJudgement<TCode>
    where TCode : struct, Enum
{
    /// <summary>
    /// The code the rule rejected with.
    /// </summary>
    internal readonly TCode Code;

    /// <summary>
    /// Why the rule refused, or <see langword="null"/> for a <see langword="default"/> instance
    /// that never went through <see cref="Judgement.Reject{TCode}(TCode, string)"/>.
    /// </summary>
    internal readonly string? Message;

    internal RejectedJudgement(TCode code, string message)
    {
        Code = code;
        Message = message;
    }

    /// <summary>
    /// Returns the rejection message, refusing a carrier that was never produced by
    /// <see cref="Judgement.Reject{TCode}(TCode, string)"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">This carrier is <see langword="default"/>.</exception>
    internal string RequireMessage()
        => Message ?? throw new InvalidOperationException(
            $"A default RejectedJudgement<{typeof(TCode).Name}> carries no message and cannot "
            + "become a judgement. Produce rejections with Judgement.Reject(code, message).");
}
