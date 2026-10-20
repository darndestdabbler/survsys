using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of <c>/api/codes</c>, the
/// reference code lists every coded field is chosen from.
/// </summary>
/// <remarks>
/// The only thing in this tier that speaks HTTP about the code lists, and the
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is.
/// </remarks>
public sealed class CodesClient(HttpClient http)
{
    /// <summary>How a failure to load a list starts.</summary>
    public const string ListFailurePrefix = "The code list could not be loaded";

    /// <summary>How a failure to add or save a code starts.</summary>
    public const string SaveFailurePrefix = "The code could not be saved";

    private static readonly ApiAsk Listing = new(ListFailurePrefix, "a code list");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the code as saved");

    /// <summary>
    /// One list's codes, or why there are none: the active ones unless
    /// <paramref name="inactive"/> asks for the retired too, and the test types
    /// narrowed to a condition's where one is given.
    /// </summary>
    public Task<Asked<IReadOnlyList<CodeResponse>>> List(
        string list,
        int? conditionId = null,
        bool inactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();

        if (conditionId is { } condition)
        {
            query.Add($"conditionId={condition}");
        }

        if (inactive)
        {
            query.Add("inactive=true");
        }

        var path = $"api/codes/{Uri.EscapeDataString(list)}"
            + (query.Count == 0 ? string.Empty : "?" + string.Join("&", query));

        return Listing.Get<IReadOnlyList<CodeResponse>>(http, path, cancellationToken);
    }

    /// <summary>Adds a code to a list, answering with it as added or why it was not.</summary>
    public Task<Asked<CodeResponse>> Create(
        string list,
        SaveCodeRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Post<SaveCodeRequest, CodeResponse>(
            http, $"api/codes/{Uri.EscapeDataString(list)}", request, cancellationToken);

    /// <summary>Saves over a code, answering with it as saved or why it was not.</summary>
    public Task<Asked<CodeResponse>> Update(
        int id,
        SaveCodeRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Put<SaveCodeRequest, CodeResponse>(http, $"api/codes/{id}", request, cancellationToken);
}
