namespace SsalKit.Guard;

/// <summary>
/// What <see cref="Judgement.Grant()"/> returns: a grant that has not yet been told which code
/// enum it belongs to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Return it, do not keep it.</b> This type has no public members on purpose — it exists only
/// long enough for the implicit conversion on <see cref="Judgement{TCode}"/> to turn it into a
/// judgement, which is what lets the call site write <c>return Judgement.Grant();</c> without
/// naming the code enum.
/// </para>
/// <para>
/// <b>It converts one way only.</b> A payload-free grant becomes a
/// <see cref="Judgement{TCode}"/> and nothing else: there is no state here for a
/// <see cref="Judgement{T, TCode}"/> to carry, and a conversion that invented one would be a
/// conversion that lies.
/// </para>
/// <para>
/// <b><see langword="default"/> is a legal value here.</b> This carrier holds nothing, so a
/// default instance is indistinguishable from one <see cref="Judgement.Grant()"/> produced, and
/// both mean the same thing. <see cref="GrantedJudgement{T}"/> and
/// <see cref="RejectedJudgement{TCode}"/> refuse their default values instead, because those would
/// be missing state the contract requires.
/// </para>
/// </remarks>
public readonly struct GrantedJudgement;

/// <summary>
/// What <see cref="Judgement.Grant{T}(T)"/> returns: the new state a rule produced, not yet told
/// which code enum the rejecting side would use.
/// </summary>
/// <typeparam name="T">The type of the new state.</typeparam>
/// <remarks>
/// <para>
/// <b>Return it, do not keep it.</b> This type has no public members on purpose — it exists only
/// long enough for the implicit conversion on <see cref="Judgement{T, TCode}"/> to turn it into a
/// judgement, which is what lets the call site write <c>return Judgement.Grant(state);</c> without
/// naming the code enum.
/// </para>
/// <para>
/// <b>It converts one way only.</b> A grant carrying state becomes a
/// <see cref="Judgement{T, TCode}"/> and nothing else; converting it to the payload-free
/// <see cref="Judgement{TCode}"/> would silently drop the state.
/// </para>
/// </remarks>
public readonly struct GrantedJudgement<T>
    where T : class
{
    /// <summary>
    /// The new state, or <see langword="null"/> for a <see langword="default"/> instance that never
    /// went through <see cref="Judgement.Grant{T}(T)"/>.
    /// </summary>
    internal readonly T? Payload;

    internal GrantedJudgement(T payload) => Payload = payload;

    /// <summary>
    /// Returns the new state, refusing a carrier that was never produced by
    /// <see cref="Judgement.Grant{T}(T)"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">This carrier is <see langword="default"/>.</exception>
    internal T RequirePayload()
        => Payload ?? throw new InvalidOperationException(
            $"A default GrantedJudgement<{typeof(T).Name}> carries no state and cannot become a "
            + "judgement. Produce granted judgements with Judgement.Grant(state).");
}
