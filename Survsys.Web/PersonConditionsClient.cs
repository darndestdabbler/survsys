using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's writer and reader of <c>/api/person-conditions</c>:
/// the case morbidity report the creation flow opens, and the one the report's
/// own form shows.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about reports, and the
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is.
///
/// Session 14 wrote it to open a report and not to read one back, because the
/// screen that shows a report was this session's and a read written then
/// would have been a method with no caller. The report shell is that caller,
/// and <see cref="Find"/> is the read: the two asks carry two prefixes, since
/// a user whose report was not created and one whose report could not be
/// shown are being told two different things.
/// </remarks>
public sealed class PersonConditionsClient(HttpClient http)
{
    /// <summary>
    /// How a failure to open a report starts. Public because the screens and
    /// the unit suite read it rather than repeating the wording.
    /// </summary>
    public const string FailurePrefix = "The case morbidity report could not be created";

    /// <summary>How a failure to read one report starts.</summary>
    public const string FindFailurePrefix = "The case morbidity report could not be loaded";

    private static readonly ApiAsk Opening = new(FailurePrefix, "the report created");

    private static readonly ApiAsk Finding = new(FindFailurePrefix, "the report");

    /// <summary>
    /// Opens a case morbidity report and answers with it — its record number
    /// among it, which is what the screen shows next — or with why none was
    /// opened.
    /// </summary>
    public Task<Asked<PersonConditionResponse>> Open(
        CreatePersonConditionRequest report,
        CancellationToken cancellationToken = default) =>
        Opening.Post<CreatePersonConditionRequest, PersonConditionResponse>(
            http, "api/person-conditions", report, cancellationToken);

    /// <summary>
    /// One case morbidity report, or why there is none — an identifier
    /// nobody has among the reasons, which the API tier answers with a 404.
    /// </summary>
    /// <remarks>
    /// What the report shell needs first: the report says who it is about
    /// and what it is for, and the shell reads those from their own
    /// resources once it has this.
    /// </remarks>
    public Task<Asked<PersonConditionResponse>> Find(
        int id,
        CancellationToken cancellationToken = default) =>
        Finding.Get<PersonConditionResponse>(http, $"api/person-conditions/{id}", cancellationToken);
}
