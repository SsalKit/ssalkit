namespace SsalKit.Guard.Generator.Models;

/// <summary>
/// Which of the recognised public constructor shapes an <c>[ErrorCode]</c> exception exposes, and
/// therefore what the generated factory and throw helper look like.
/// </summary>
/// <remarks>
/// The values are ordered from narrowest to widest: when an exception declares several of these,
/// the widest one wins, so the generated helper offers the caller everything the exception itself
/// offers.
/// </remarks>
internal enum ConstructorShape
{
    /// <summary>
    /// None of the recognised shapes is available. No helpers are generated (SSALG006); the type
    /// still takes part in the mapping table.
    /// </summary>
    None = 0,

    /// <summary>
    /// <c>()</c> -- the helpers take no parameters.
    /// </summary>
    Parameterless = 1,

    /// <summary>
    /// <c>(string?)</c> -- the helpers take a message.
    /// </summary>
    Message = 2,

    /// <summary>
    /// <c>(string?, System.Exception?)</c> -- the helpers take a message and an inner exception.
    /// </summary>
    MessageAndInner = 3,
}
