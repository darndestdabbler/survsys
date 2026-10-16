using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's writer of <c>POST /api/person-conditions</c>: the
/// case morbidity report the creation flow opens.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about reports, and the
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is.
///
/// It opens a report and does not read one back. The screen that will show a
/// report is a later session's, and the method it needs —
/// <c>GET /api/person-conditions/{id}</c> — is on the other tier already; a
/// read written here now would be a method with no caller.
/// </remarks>
public sealed class PersonConditionsClient(HttpClient http)
{
    /// <summary>
    /// How every failure message starts. Public because the screens and the
    /// unit suite read it rather than repeating the wording.
    /// </summary>
    public const string FailurePrefix = "The case morbidity report could not be created";

    private static readonly ApiAsk Ask = new(FailurePrefix, "the report created");

    /// <summary>
    /// Opens a case morbidity report and answers with it — its record number
    /// among it, which is what the screen shows next — or with why none was
    /// opened.
    /// </summary>
    public Task<Asked<PersonConditionResponse>> Open(
        CreatePersonConditionRequest report,
        CancellationToken cancellationToken = default) =>
        Ask.Post<CreatePersonConditionRequest, PersonConditionResponse>(
            http, "api/person-conditions", report, cancellationToken);
}
