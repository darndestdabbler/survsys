using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of <c>/api/vaccines</c>: the vaccine
/// catalogue and a case's vaccine records.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about vaccines, and the
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is. The
/// catalogue and a case's records each come back as a plain list, the shape the
/// API returns them in.
/// </remarks>
public sealed class VaccinesClient(HttpClient http)
{
    /// <summary>How a failure to load the catalogue starts.</summary>
    public const string CatalogueFailurePrefix = "The vaccine catalogue could not be loaded";

    /// <summary>How a failure to load a case's records starts.</summary>
    public const string RecordsFailurePrefix = "The case's vaccines could not be loaded";

    /// <summary>How a failure to add or save a record starts.</summary>
    public const string SaveFailurePrefix = "The vaccine record could not be saved";

    private static readonly ApiAsk AskingCatalogue = new(CatalogueFailurePrefix, "the vaccine catalogue");

    private static readonly ApiAsk AskingRecords = new(RecordsFailurePrefix, "the case's vaccine records");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the record as saved");

    /// <summary>The vaccine catalogue, in sort order, or why there is none.</summary>
    public Task<Asked<List<VaccineResponse>>> Catalogue(
        CancellationToken cancellationToken = default) =>
        AskingCatalogue.Get<List<VaccineResponse>>(http, "api/vaccines", cancellationToken);

    /// <summary>A case's vaccine records, newest administered first, or why there is none.</summary>
    public Task<Asked<List<VaccineRecordResponse>>> Records(
        int personConditionId,
        CancellationToken cancellationToken = default) =>
        AskingRecords.Get<List<VaccineRecordResponse>>(
            http,
            $"api/cases/{personConditionId}/vaccines",
            cancellationToken);

    /// <summary>Records a vaccine against the case, answering with it or why it was not.</summary>
    public Task<Asked<VaccineRecordResponse>> Add(
        int personConditionId,
        SaveVaccineRecordRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Post<SaveVaccineRecordRequest, VaccineRecordResponse>(
            http,
            $"api/cases/{personConditionId}/vaccines",
            request,
            cancellationToken);

    /// <summary>Saves over a case's vaccine record, answering with it or why it was not.</summary>
    public Task<Asked<VaccineRecordResponse>> Update(
        int id,
        SaveVaccineRecordRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Put<SaveVaccineRecordRequest, VaccineRecordResponse>(
            http,
            $"api/cases/vaccines/{id}",
            request,
            cancellationToken);

    /// <summary>Removes a case's vaccine record, answering with null on success or why it was not.</summary>
    public Task<string?> Delete(int id, CancellationToken cancellationToken = default) =>
        Saving.Delete(http, $"api/cases/vaccines/{id}", cancellationToken);
}
