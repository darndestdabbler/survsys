using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of <c>/api/ecr-senders</c>: the
/// organisations whose eICRs reach this jurisdiction, with their onboarding
/// stage and settings.
/// </summary>
/// <remarks>
/// The failure handling is <see cref="ApiAsk"/>'s, as every client's is.
/// </remarks>
public sealed class EcrSendersClient(HttpClient http)
{
    /// <summary>How a failure to load the senders starts.</summary>
    public const string ListFailurePrefix = "The eCR senders could not be loaded";

    /// <summary>How a failure to add, save or delete a sender starts.</summary>
    public const string SaveFailurePrefix = "The eCR sender could not be saved";

    private static readonly ApiAsk Listing = new(ListFailurePrefix, "the eCR senders");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the eCR sender as saved");

    /// <summary>Every sender, by name, or why there is none.</summary>
    public Task<Asked<List<EcrSenderResponse>>> List(CancellationToken cancellationToken = default) =>
        Listing.Get<List<EcrSenderResponse>>(http, "api/ecr-senders", cancellationToken);

    /// <summary>Adds a sender, answering with it or why it was not.</summary>
    public Task<Asked<EcrSenderResponse>> Add(
        SaveEcrSenderRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Post<SaveEcrSenderRequest, EcrSenderResponse>(http, "api/ecr-senders", request, cancellationToken);

    /// <summary>Saves over a sender, answering with it or why it was not.</summary>
    public Task<Asked<EcrSenderResponse>> Save(
        int id,
        SaveEcrSenderRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Put<SaveEcrSenderRequest, EcrSenderResponse>(http, $"api/ecr-senders/{id}", request, cancellationToken);

    /// <summary>Deletes a sender, answering with null on success or why it was not.</summary>
    public Task<string?> Delete(int id, CancellationToken cancellationToken = default) =>
        Saving.Delete(http, $"api/ecr-senders/{id}", cancellationToken);
}
