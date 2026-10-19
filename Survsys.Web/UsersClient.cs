using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of <c>/api/users</c>.
/// </summary>
/// <remarks>
/// The failure handling is <see cref="ApiAsk"/>'s, as every client's is.
/// </remarks>
public sealed class UsersClient(HttpClient http)
{
    /// <summary>How a failure to load the users starts.</summary>
    public const string LoadFailurePrefix = "The users could not be loaded";

    /// <summary>How a failure to save a user starts.</summary>
    public const string SaveFailurePrefix = "The user could not be saved";

    private static readonly ApiAsk Loading = new(LoadFailurePrefix, "a page of users");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the user as saved");

    /// <summary>Every user, or why there are none.</summary>
    public Task<Asked<PagedResult<UserResponse>>> All(CancellationToken cancellationToken = default) =>
        Loading.Get<PagedResult<UserResponse>>(http, "api/users", cancellationToken);

    /// <summary>Creates a user, answering with them as saved or why they were not.</summary>
    public Task<Asked<UserResponse>> Create(SaveUserRequest request, CancellationToken cancellationToken = default) =>
        Saving.Post<SaveUserRequest, UserResponse>(http, "api/users", request, cancellationToken);

    /// <summary>Saves over a user, answering with them as saved or why they were not.</summary>
    public Task<Asked<UserResponse>> Update(
        int id,
        SaveUserRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Put<SaveUserRequest, UserResponse>(http, $"api/users/{id}", request, cancellationToken);

    /// <summary>How a failure to load the investigators starts.</summary>
    public const string InvestigatorsFailurePrefix = "The investigators could not be loaded";

    private static readonly ApiAsk LoadingInvestigators = new(InvestigatorsFailurePrefix, "the investigators");

    /// <summary>
    /// The active users an investigation may be assigned to, from
    /// <c>/api/investigators</c>, which a case reader may ask where
    /// <c>/api/users</c> needs user administration — or why they could not be read.
    /// </summary>
    public Task<Asked<IReadOnlyList<InvestigatorResponse>>> Investigators(CancellationToken cancellationToken = default) =>
        LoadingInvestigators.Get<IReadOnlyList<InvestigatorResponse>>(http, "api/investigators", cancellationToken);
}
