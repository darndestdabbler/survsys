using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of a case's healthcare encounters,
/// under <c>/api/cases</c>.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about encounters, and the
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is. Shaped as
/// <see cref="VaccinesClient"/>'s case records are.
/// </remarks>
public sealed class EncountersClient(HttpClient http)
{
    /// <summary>How a failure to load a case's encounters starts.</summary>
    public const string RecordsFailurePrefix = "The case's encounters could not be loaded";

    /// <summary>How a failure to add, save or remove an encounter starts.</summary>
    public const string SaveFailurePrefix = "The encounter could not be saved";

    private static readonly ApiAsk AskingRecords = new(RecordsFailurePrefix, "the case's encounters");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the encounter as saved");

    /// <summary>A case's encounters, newest first, or why there are none.</summary>
    public Task<Asked<List<EncounterResponse>>> Records(
        int personConditionId,
        CancellationToken cancellationToken = default) =>
        AskingRecords.Get<List<EncounterResponse>>(
            http,
            $"api/cases/{personConditionId}/encounters",
            cancellationToken);

    /// <summary>Records an encounter on the case, answering with it or why it was not.</summary>
    public Task<Asked<EncounterResponse>> Add(
        int personConditionId,
        SaveEncounterRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Post<SaveEncounterRequest, EncounterResponse>(
            http,
            $"api/cases/{personConditionId}/encounters",
            request,
            cancellationToken);

    /// <summary>Saves over an encounter, answering with it or why it was not.</summary>
    public Task<Asked<EncounterResponse>> Update(
        int id,
        SaveEncounterRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Put<SaveEncounterRequest, EncounterResponse>(
            http,
            $"api/cases/encounters/{id}",
            request,
            cancellationToken);

    /// <summary>Removes an encounter, answering with null on success or why it was not.</summary>
    public Task<string?> Delete(int id, CancellationToken cancellationToken = default) =>
        Saving.Delete(http, $"api/cases/encounters/{id}", cancellationToken);
}
