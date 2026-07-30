namespace SsalKit.StableHashing.Generator.Models;

/// <summary>
/// One successfully-validated <c>[StableHashMember]</c>, reduced to what
/// <see cref="Emission.StableHashEmitter"/> needs: the id it is encoded under, how to read it off
/// the <c>value</c> parameter, and its resolved <see cref="TypeShape"/>.
/// </summary>
/// <param name="Id">
/// The member's stable id (see <c>StableHashMemberAttribute.Id</c>). Members are emitted in
/// ascending id order, not declaration order (design §3.1/§4.1).
/// </param>
/// <param name="AccessExpression">
/// How to read this member off a value of the contract type, already <c>@</c>-escaped if it
/// collides with a keyword, e.g. <c>"Weight"</c> for a member read as <c>value.Weight</c>.
/// </param>
/// <param name="Shape">The member's resolved encoding shape.</param>
internal sealed record MemberModel(int Id, string AccessExpression, TypeShape Shape);
