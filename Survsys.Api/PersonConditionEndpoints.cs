using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>POST /api/person-conditions</c>: opens a case morbidity report, which is
/// the end of the legacy two-step creation flow. And
/// <c>GET /api/person-conditions/{id}</c>: reads one back.
/// </summary>
/// <remarks>
/// The entity-to-wire mapping lives here, as the other slices' do and for the
/// same reason: <c>Survsys.Data</c> may not reference
/// <c>Survsys.Contracts</c>, which leaves the translation to the tier that
/// owns the wire.
///
/// The read endpoint exists in this session for one reason, and it is worth
/// saying plainly: it is what makes the creation's <c>Location</c> header
/// name something a caller can read. The screen that will <em>show</em> a
/// report — the shell with its tab strip — is a later session's, and this is
/// the endpoint it will read.
///
/// The handlers are handed <see cref="PersonConditionQueries"/> and not the
/// <c>DbContext</c>, because the architecture test in <c>Survsys.UnitTests</c>
/// lets no <c>Survsys.Data</c> type into an endpoint signature at all.
///
/// Nothing here translates a database failure into an HTTP concern. Two
/// reports created in the same instant can read the same highest record
/// number and ask for the same next one; the unique index refuses the second
/// insert, and that surfaces as a 500 carrying a correlation id rather than as
/// two reports sharing a number. There is no retry and no sequence table in
/// front of it, which is a decision this session made and recorded rather than
/// one it overlooked.
/// </remarks>
public static class PersonConditionEndpoints
{
    /// <summary>
    /// The projection from the stored report to the wire shape — every column
    /// this phase stores.
    /// </summary>
    /// <remarks>
    /// An <see cref="Expression"/> rather than a method, as the other slices'
    /// mappings are: EF Core translates it into the <c>SELECT</c> list, and a
    /// unit test compiles and invokes the same expression to prove what it
    /// carries.
    ///
    /// Public because the unit suite proves it. Nothing else calls it.
    /// </remarks>
    public static readonly Expression<Func<PersonCondition, PersonConditionResponse>> ToResponse =
        report => new PersonConditionResponse(
            report.Id,
            report.PersonId,
            report.ConditionId,
            report.RecordNumber,
            report.AgencyId,
            report.FirstReportedPhDate,
            report.EventOnsetDate,
            report.CreatedAt);

    /// <summary>
    /// <see cref="ToResponse"/> compiled, for the one caller that has a report
    /// in hand rather than a query to translate: the report just created.
    /// </summary>
    /// <remarks>
    /// The same expression, so a created report and a read one are described
    /// the same way. Public because the unit suite proves that.
    /// </remarks>
    public static readonly Func<PersonCondition, PersonConditionResponse> Respond =
        ToResponse.Compile();

    /// <summary>
    /// Why this is not a report this tier will open, as far as can be said
    /// without asking the database, or null when nothing is wrong with it.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="Paging.WhyNotAPage"/> and
    /// <see cref="PersonEndpoints.WhyNotAPerson"/> have: a pure function the
    /// unit suite proves, with the endpoint turning the sentence into a 400.
    ///
    /// Whether the person and the condition exist is not here, because that
    /// cannot be answered without a query —
    /// <see cref="PersonConditionQueries.WhyNotOnFile"/> is where that lives.
    /// The agency <em>is</em> here, because the agency list is a static one in
    /// this tier and asking it costs nothing.
    ///
    /// Public because the unit suite proves it. Nothing outside this file
    /// calls it.
    /// </remarks>
    public static string? WhyNotAReport(CreatePersonConditionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PersonId <= 0)
        {
            return "personId is required: a case morbidity report is a report about a "
                + $"person, and {request.PersonId} is not an identifier anyone has.";
        }

        if (request.ConditionId <= 0)
        {
            return "conditionId is required: a case morbidity report is a report of a "
                + $"condition, and {request.ConditionId} is not an identifier anything has.";
        }

        if (request.AgencyId is { } agencyId && AgencyEndpoints.Find(agencyId) is null)
        {
            return $"agencyId {agencyId} is not an agency this tier knows. The agencies a "
                + "report may be assigned to are the ones GET /api/agencies lists; leave "
                + "agencyId out to create a report with no investigating agency.";
        }

        return null;
    }

    /// <summary>
    /// Registers what the person-conditions endpoints need of the container.
    /// Kept here with the endpoints so the slice's wiring is in one place, and
    /// so that <c>Program.cs</c> need not name an internal type.
    /// </summary>
    public static IServiceCollection AddPersonConditions(this IServiceCollection services) =>
        services.AddScoped<PersonConditionQueries>();

    public static IEndpointRouteBuilder MapPersonConditions(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/person-conditions/{id:int}", GetPersonCondition)
            .WithName("GetPersonCondition")
            .WithSummary("One case morbidity report.");

        routes.MapPost("/api/person-conditions", CreatePersonCondition)
            .WithName("CreatePersonCondition")
            .WithSummary("Opens a case morbidity report about a person, for a condition, "
                + "and assigns its record number.");

        return routes;
    }

    private static async Task<Results<Ok<PersonConditionResponse>, ProblemHttpResult>> GetPersonCondition(
        PersonConditionQueries reports,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await reports.Find(id, cancellationToken) is not { } report)
        {
            return TypedResults.Problem(
                detail: $"No case morbidity report has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such case morbidity report.");
        }

        return TypedResults.Ok(report);
    }

    /// <remarks>
    /// A person or a condition that does not exist is a 400 and not a 404: the
    /// resource addressed is the collection, which is there, and what is wrong
    /// is the body — so the Problem Details says which of the two it was.
    ///
    /// <c>DateTimeOffset.Now</c> is read here, and this is the one line in
    /// this slice that depends on the clock. Local and not UTC, deliberately:
    /// the record number carries the year the report was created in, and the
    /// year a user in Utah is in on the evening of the 31st of December is the
    /// year the legacy application would have given them. Passing it in rather
    /// than reading it inside the query keeps the dependency to this one
    /// visible line.
    /// </remarks>
    private static async Task<Results<Created<PersonConditionResponse>, ProblemHttpResult>> CreatePersonCondition(
        PersonConditionQueries reports,
        CreatePersonConditionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAReport(request) is { } why)
        {
            return Refused(why);
        }

        if (await reports.WhyNotOnFile(request, cancellationToken) is { } missing)
        {
            return Refused(missing);
        }

        var created = await reports.Add(request, DateTimeOffset.Now, cancellationToken);

        return TypedResults.Created($"/api/person-conditions/{created.Id}", created);
    }

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The case morbidity report asked for is not one this endpoint can open.");
}

/// <summary>
/// The case morbidity report queries, and the only thing in this tier that
/// holds the <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>PersonQueries</c> is: it exists only to serve
/// <see cref="PersonConditionEndpoints"/>, and it is the named step between an
/// endpoint signature and <c>Survsys.Data</c> that the architecture test
/// requires.
/// </remarks>
internal sealed class PersonConditionQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One report, or null where no report has that id — the answer the
    /// endpoint turns into a 404.
    /// </summary>
    public Task<PersonConditionResponse?> Find(int id, CancellationToken cancellationToken) =>
        database.PersonConditions
            .Where(report => report.Id == id)
            .Select(PersonConditionEndpoints.ToResponse)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Why the person or the condition this report would be about is not one
    /// to open a report against, or null when both are.
    /// </summary>
    /// <remarks>
    /// Two questions and two queries rather than one cleverer one: this runs
    /// once per report created, and a message that names which of the two was
    /// wrong is worth more than a round trip saved.
    ///
    /// The condition has to be one that may be <em>chosen</em>, which is
    /// stricter than existing and is what <c>Condition.Active</c> means of
    /// itself. The legacy creation screen only offers active conditions, and a
    /// report opened for an inactive one by a caller that went around the
    /// screen would be a case of a disease this installation has retired.
    /// </remarks>
    public async Task<string?> WhyNotOnFile(
        CreatePersonConditionRequest request,
        CancellationToken cancellationToken)
    {
        if (!await database.Persons.AnyAsync(
            person => person.Id == request.PersonId, cancellationToken))
        {
            return $"personId {request.PersonId} is not a person on file. A case morbidity "
                + "report is opened about a person who already exists: create the person "
                + "first, with POST /api/persons.";
        }

        var condition = await database.Conditions
            .Where(condition => condition.Id == request.ConditionId)
            .Select(condition => new { condition.Active })
            .SingleOrDefaultAsync(cancellationToken);

        if (condition is null)
        {
            return $"conditionId {request.ConditionId} is not a condition on file. The "
                + "conditions a report may be opened for are the ones "
                + "GET /api/conditions lists.";
        }

        if (!condition.Active)
        {
            return $"conditionId {request.ConditionId} is a condition that may not be "
                + "chosen for a new report. The conditions a report may be opened for are "
                + "the active ones GET /api/conditions lists.";
        }

        return null;
    }

    /// <summary>
    /// Opens the report, assigning its record number, and answers with it as
    /// it is stored.
    /// </summary>
    /// <remarks>
    /// The record number is the year of <paramref name="createdAt"/> and the
    /// sequence after the highest already issued in that year, which is one
    /// query: the numbers of a year are fixed-width and zero-padded, so the
    /// highest of them as text is the highest sequence issued — which is what
    /// <see cref="RecordNumber"/> says of itself, and why no arithmetic is
    /// needed in the database to find it.
    ///
    /// The event onset date is set to the creation date, which is what the
    /// legacy creation flow does and what
    /// <c>../nedss/docs/db-snapshots/session-010/02-after-cmr-creation.txt</c>
    /// shows: <c>event_onset_date</c> and <c>created_at</c> are the same day
    /// there. The Clinical tab is where an investigator corrects it.
    ///
    /// The request is validated before this is called, and a database failure
    /// surfaces unwrapped — the unique-index collision two simultaneous
    /// creations can cause among them.
    /// </remarks>
    public async Task<PersonConditionResponse> Add(
        CreatePersonConditionRequest request,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var year = createdAt.Year;
        var issued = year.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);

        var highest = await database.PersonConditions
            .Where(report => report.RecordNumber.StartsWith(issued))
            .OrderByDescending(report => report.RecordNumber)
            .Select(report => report.RecordNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var report = new PersonCondition
        {
            PersonId = request.PersonId,
            ConditionId = request.ConditionId,
            RecordNumber = RecordNumber.Next(year, highest),
            AgencyId = request.AgencyId,
            FirstReportedPhDate = request.FirstReportedPhDate,
            EventOnsetDate = DateOnly.FromDateTime(createdAt.Date),
            CreatedAt = createdAt,
        };

        database.PersonConditions.Add(report);
        await database.SaveChangesAsync(cancellationToken);

        return PersonConditionEndpoints.Respond(report);
    }
}
