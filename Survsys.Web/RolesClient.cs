using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of <c>/api/roles</c>, and reader
/// of <c>/api/privileges</c>, the catalogue a role chooses from.
/// </summary>
/// <remarks>
/// The failure handling is <see cref="ApiAsk"/>'s, as every client's is.
/// </remarks>
public sealed class RolesClient(HttpClient http)
{
    /// <summary>How a failure to load the roles or the privileges starts.</summary>
    public const string LoadFailurePrefix = "The roles could not be loaded";

    /// <summary>How a failure to save a role starts.</summary>
    public const string SaveFailurePrefix = "The role could not be saved";

    private static readonly ApiAsk Loading = new(LoadFailurePrefix, "the roles");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the role as saved");

    /// <summary>Every role, or why there are none.</summary>
    public Task<Asked<PagedResult<RoleResponse>>> All(CancellationToken cancellationToken = default) =>
        Loading.Get<PagedResult<RoleResponse>>(http, "api/roles", cancellationToken);

    /// <summary>The privileges a role may hold, or why they could not be read.</summary>
    public Task<Asked<IReadOnlyList<string>>> Privileges(CancellationToken cancellationToken = default) =>
        Loading.Get<IReadOnlyList<string>>(http, "api/privileges", cancellationToken);

    /// <summary>Creates a role, answering with it as saved or why it was not.</summary>
    public Task<Asked<RoleResponse>> Create(SaveRoleRequest request, CancellationToken cancellationToken = default) =>
        Saving.Post<SaveRoleRequest, RoleResponse>(http, "api/roles", request, cancellationToken);

    /// <summary>Saves over a role, answering with it as saved or why it was not.</summary>
    public Task<Asked<RoleResponse>> Update(
        int id,
        SaveRoleRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Put<SaveRoleRequest, RoleResponse>(http, $"api/roles/{id}", request, cancellationToken);
}
