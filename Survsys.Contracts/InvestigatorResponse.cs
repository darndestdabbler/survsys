namespace Survsys.Contracts;

/// <summary>
/// A user an investigation may be assigned to, as <c>GET /api/investigators</c>
/// lists them: the identifier a workflow assignment names and the name a
/// picker shows, and nothing else of the user.
/// </summary>
/// <remarks>
/// Every active user, not a role's members or an agency's staff: who may
/// assign is a privilege's business, and who may be assigned is anyone who can
/// sign in. A case read is what this list needs, where <c>/api/users</c> needs
/// user administration, which is why it is a resource of its own.
/// </remarks>
/// <param name="Id">The user's identifier.</param>
/// <param name="DisplayName">The name screens show for the user.</param>
public record InvestigatorResponse(int Id, string DisplayName);
