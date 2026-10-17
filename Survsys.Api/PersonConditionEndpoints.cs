using System.Linq.Expressions;
using System.Security.Claims;
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
            report.CreatedAt,
            report.CaseStatus,
            report.MmwrWeek,
            report.MmwrYear,
            report.WorkflowState);

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
    /// Nor whether the agency exists, which <see cref="AgencyQueries"/>
    /// answers; only an agency identifier nothing can have is refused here.
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

        if (request.AgencyId is { } agencyId && agencyId <= 0)
        {
            return AgencyEndpoints.NotAnAgency(agencyId)
                + " Leave agencyId out to create a report with no investigating agency.";
        }

        return null;
    }

    /// <summary>
    /// Why this is not a case status this tier will set, as far as can be said
    /// without the database, or null when it is one.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="WhyNotAReport"/> has: a pure function the unit
    /// suite proves, with the endpoint turning the sentence into a 400. The
    /// vocabulary is <see cref="CaseStatusRules"/>'s, so a code it does not
    /// know is refused rather than written; null is a status too — it clears
    /// the status to Unknown — and so passes. Whether the report exists is not
    /// here, because that needs a query.
    ///
    /// Public because the unit suite proves it. Nothing outside this file calls
    /// it.
    /// </remarks>
    public static string? WhyNotAStatus(UpdateCaseStatusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!CaseStatusRules.IsAssignable(request.CaseStatus))
        {
            return $"caseStatus '{request.CaseStatus}' is not a status a report may be set "
                + $"to. A case status is one of {CaseStatusRules.ConfirmedCode}, "
                + $"{CaseStatusRules.ProbableCode}, {CaseStatusRules.SuspectedCode} or "
                + $"{CaseStatusRules.NotACaseCode}, or left out to clear the status to "
                + "Unknown.";
        }

        return null;
    }

    /// <summary>
    /// Why this is not an agency a report may be assigned to, or null when it
    /// is one.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="WhyNotAReport"/> and <see cref="WhyNotAStatus"/>
    /// have: a pure function the unit suite proves, with the endpoint turning
    /// the sentence into a 400. Only an agency identifier nothing can have is
    /// refused here; whether the agency and the report exist needs a query.
    ///
    /// Public because the unit suite proves it. Nothing outside this file calls
    /// it.
    /// </remarks>
    public static string? WhyNotAnAssignment(AssignReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AgencyId <= 0)
        {
            return AgencyEndpoints.NotAnAgency(request.AgencyId);
        }

        return null;
    }

    /// <summary>
    /// Registers what the person-conditions endpoints need of the container.
    /// Kept here with the endpoints so the slice's wiring is in one place, and
    /// so that <c>Program.cs</c> need not name an internal type.
    /// </summary>
    public static IServiceCollection AddPersonConditions(this IServiceCollection services) =>
        services
            .AddScoped<PersonConditionQueries>()
            .AddScoped<CaseNotificationQueue>();

    public static IEndpointRouteBuilder MapPersonConditions(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/person-conditions/{id:int}", GetPersonCondition)
            .WithName("GetPersonCondition")
            .WithSummary("One case morbidity report.");

        routes.MapPost("/api/person-conditions", CreatePersonCondition)
            .WithName("CreatePersonCondition")
            .WithSummary("Opens a case morbidity report about a person, for a condition, "
                + "and assigns its record number.");

        routes.MapPut("/api/person-conditions/{id:int}/status", SetStatus)
            .WithName("SetPersonConditionStatus")
            .WithSummary("Sets a case morbidity report's case status.");

        routes.MapPut("/api/person-conditions/{id:int}/assignment", Assign)
            .WithName("AssignPersonCondition")
            .WithSummary("Assigns a case morbidity report to an investigating agency and "
                + "returns it to the assigned-to-LHD start of the workflow.");

        routes.MapPut("/api/person-conditions/{id:int}/workflow", AdvanceWorkflow)
            .WithName("SetPersonConditionWorkflow")
            .WithSummary("Moves a case morbidity report to the next workflow state the "
                + "transition table allows.");

        return routes;
    }

    /// <remarks>
    /// Serving a report is what the access audit records: who it was served
    /// to, and when. A report that is not there was not opened, so a 404
    /// records nothing.
    /// </remarks>
    private static async Task<Results<Ok<PersonConditionResponse>, ProblemHttpResult>> GetPersonCondition(
        PersonConditionQueries reports,
        AccessAuditQueries audit,
        ClaimsPrincipal user,
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

        if (AccountEndpoints.UserIdOf(user) is { } userId)
        {
            await audit.Record(userId, id, cancellationToken);
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
        AgencyQueries agencies,
        CreatePersonConditionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAReport(request) is { } why)
        {
            return Refused(why);
        }

        if (request.AgencyId is { } agencyId && await agencies.Find(agencyId, cancellationToken) is null)
        {
            return Refused(AgencyEndpoints.NotAnAgency(agencyId));
        }

        if (await reports.WhyNotOnFile(request, cancellationToken) is { } missing)
        {
            return Refused(missing);
        }

        var created = await reports.Add(request, DateTimeOffset.Now, cancellationToken);

        return TypedResults.Created($"/api/person-conditions/{created.Id}", created);
    }

    /// <remarks>
    /// The status is set directly and not derived: the disease-specific
    /// auto-classification is deferred, so a caller chooses the status, and
    /// <see cref="WhyNotAStatus"/> refuses a code the vocabulary does not know
    /// before the database is asked. A report id that names no report is a 404,
    /// the resource addressed being the report itself.
    /// </remarks>
    private static async Task<Results<Ok<PersonConditionResponse>, ProblemHttpResult>> SetStatus(
        PersonConditionQueries reports,
        int id,
        UpdateCaseStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAStatus(request) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The case status asked for is not one this endpoint can set.");
        }

        if (await reports.SetStatus(id, request, cancellationToken) is not { } report)
        {
            return TypedResults.Problem(
                detail: $"No case morbidity report has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such case morbidity report.");
        }

        return TypedResults.Ok(report);
    }

    /// <remarks>
    /// Assigning a report names its investigating agency and returns it to the
    /// assigned-to-LHD start of the workflow, as the legacy enters a case into
    /// the Local Health Department's queue. An agency no agency on file has is
    /// a 400, as it is on creation; a report id that names no report is a 404,
    /// the resource addressed being the report itself.
    /// </remarks>
    private static async Task<Results<Ok<PersonConditionResponse>, ProblemHttpResult>> Assign(
        PersonConditionQueries reports,
        AgencyQueries agencies,
        int id,
        AssignReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var why = WhyNotAnAssignment(request)
            ?? (await agencies.Find(request.AgencyId, cancellationToken) is null
                ? AgencyEndpoints.NotAnAgency(request.AgencyId)
                : null);

        if (why is not null)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The agency the report is to be assigned to is not one this endpoint knows.");
        }

        if (await reports.Assign(id, request.AgencyId, cancellationToken) is not { } report)
        {
            return TypedResults.Problem(
                detail: $"No case morbidity report has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such case morbidity report.");
        }

        return TypedResults.Ok(report);
    }

    /// <remarks>
    /// The move is gated by the pure transition table and nothing else: a
    /// target code the vocabulary does not know is a 400 before the database is
    /// asked, a report id that names no report is a 404, and a move
    /// <see cref="WorkflowRules.IsAllowed"/> rejects — a skip, a backward move,
    /// or a reopening — is a 400 whose message names where the report is and
    /// where it was asked to go. The current state is read from the report and
    /// the target written back; no labs or classification move it, because the
    /// classification-gated transitions are the deferred, Utah-specific work.
    ///
    /// The <see cref="WorkflowState"/> enum stays out of this signature and in
    /// the body, as the architecture test requires of an endpoint; the query
    /// holds the <c>DbContext</c>.
    /// </remarks>
    private static async Task<Results<Ok<PersonConditionResponse>, ProblemHttpResult>> AdvanceWorkflow(
        PersonConditionQueries reports,
        int id,
        UpdateWorkflowStateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!WorkflowRules.TryParse(request.WorkflowState, out var target))
        {
            return TypedResults.Problem(
                detail: $"workflowState '{request.WorkflowState}' is not a workflow state a "
                    + $"report may be moved to. A workflow state is one of "
                    + $"{WorkflowRules.AssignedToLhdCode}, {WorkflowRules.AcceptedCode}, "
                    + $"{WorkflowRules.UnderInvestigationCode} or {WorkflowRules.ClosedCode}.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "The workflow state asked for is not one this endpoint can set.");
        }

        if (await reports.WorkflowCodeOf(id, cancellationToken) is not { } currentCode)
        {
            return TypedResults.Problem(
                detail: $"No case morbidity report has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such case morbidity report.");
        }

        var current = WorkflowRules.Read(currentCode);

        if (!WorkflowRules.IsAllowed(current, target))
        {
            return TypedResults.Problem(
                detail: $"A report at {WorkflowRules.CodeOf(current)} may not be moved to "
                    + $"{WorkflowRules.CodeOf(target)}. The workflow runs "
                    + $"{WorkflowRules.AssignedToLhdCode} to {WorkflowRules.AcceptedCode} to "
                    + $"{WorkflowRules.UnderInvestigationCode} to {WorkflowRules.ClosedCode}, "
                    + "one step at a time and forward only.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "The workflow move asked for is not one the transition table allows.");
        }

        if (await reports.SetWorkflow(id, WorkflowRules.CodeOf(target), cancellationToken) is not { } report)
        {
            return TypedResults.Problem(
                detail: $"No case morbidity report has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such case morbidity report.");
        }

        return TypedResults.Ok(report);
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
internal sealed class PersonConditionQueries(
    SurvsysDbContext database,
    CaseNotificationQueue notifications)
{
    /// <summary>
    /// Stages the auto-queue of a changed case on the context, so the next
    /// <c>SaveChangesAsync</c> writes the case's queue entry alongside the
    /// change. The one place the clock is read for the queued time, as
    /// <c>Add</c> reads it once for the record number.
    /// </summary>
    private Task QueueForNotification(int reportId, CancellationToken cancellationToken) =>
        notifications.EnqueueAsync(new CaseChanged(reportId, DateTime.UtcNow), cancellationToken);

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

        // The event onset date is the creation date, as the legacy flow sets
        // it, and the MMWR week and year are read off it here and stored, as
        // the record number is: no column computes them on the fly, and the
        // case status stays unset — null, which is Unknown — until one is set.
        // The workflow starts at assigned_to_lhd, as the legacy opens a case at
        // the Local Health Department.
        var onset = DateOnly.FromDateTime(createdAt.Date);
        var mmwr = MmwrWeek.Of(onset);

        var report = new PersonCondition
        {
            PersonId = request.PersonId,
            ConditionId = request.ConditionId,
            RecordNumber = RecordNumber.Next(year, highest),
            AgencyId = request.AgencyId,
            FirstReportedPhDate = request.FirstReportedPhDate,
            EventOnsetDate = onset,
            CreatedAt = createdAt,
            MmwrWeek = mmwr.Week,
            MmwrYear = mmwr.Year,
            WorkflowState = WorkflowRules.InitialCode,
        };

        database.PersonConditions.Add(report);
        await database.SaveChangesAsync(cancellationToken);

        // A new case has no id until it is saved, so it is queued after the
        // insert and in a second save rather than the one above.
        await QueueForNotification(report.Id, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        return PersonConditionEndpoints.Respond(report);
    }

    /// <summary>
    /// Sets a report's case status and answers with the report as stored, or
    /// null where no report has that id — the answer the endpoint turns into a
    /// 404.
    /// </summary>
    /// <remarks>
    /// The request is validated before this is called, so the code is one the
    /// vocabulary knows; here is only the write. The status is stored as it was
    /// offered, as the clinical save stores its coded fields — null clears it
    /// to Unknown. The MMWR fields are untouched, because the event onset date
    /// they are read from does not change here.
    /// </remarks>
    public async Task<PersonConditionResponse?> SetStatus(
        int id,
        UpdateCaseStatusRequest request,
        CancellationToken cancellationToken)
    {
        var report = await database.PersonConditions
            .SingleOrDefaultAsync(report => report.Id == id, cancellationToken);

        if (report is null)
        {
            return null;
        }

        report.CaseStatus = request.CaseStatus;
        await QueueForNotification(report.Id, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        return PersonConditionEndpoints.Respond(report);
    }

    /// <summary>
    /// Assigns a report to an agency and returns its workflow to the
    /// assigned-to-LHD start, answering the report as stored, or null where no
    /// report has that id — the answer the endpoint turns into a 404.
    /// </summary>
    /// <remarks>
    /// The agency is validated before this is called; here is only the write.
    /// Both columns move together: assigning a report is entering it into an
    /// agency's queue, which is the start of the workflow, so the state returns
    /// to <see cref="WorkflowRules.InitialCode"/> whatever it was.
    /// </remarks>
    public async Task<PersonConditionResponse?> Assign(
        int id,
        int agencyId,
        CancellationToken cancellationToken)
    {
        var report = await database.PersonConditions
            .SingleOrDefaultAsync(report => report.Id == id, cancellationToken);

        if (report is null)
        {
            return null;
        }

        report.AgencyId = agencyId;
        report.WorkflowState = WorkflowRules.InitialCode;
        await QueueForNotification(report.Id, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        return PersonConditionEndpoints.Respond(report);
    }

    /// <summary>
    /// The report's current workflow code, read as the assigned-to-LHD default
    /// where the stored column is empty, or null where no report has that id —
    /// which the endpoint turns into a 404.
    /// </summary>
    /// <remarks>
    /// Null means no report, not an empty column: a report that exists with no
    /// stored code is at <see cref="WorkflowRules.InitialCode"/>, as
    /// <see cref="WorkflowRules.Read(string?)"/> reads it, so the endpoint can
    /// tell "no such report" from "a report at the default start". The endpoint
    /// reads the state and checks the move; the write is <see cref="SetWorkflow"/>.
    /// </remarks>
    public async Task<string?> WorkflowCodeOf(int id, CancellationToken cancellationToken)
    {
        var found = await database.PersonConditions
            .Where(report => report.Id == id)
            .Select(report => new { report.WorkflowState })
            .SingleOrDefaultAsync(cancellationToken);

        if (found is null)
        {
            return null;
        }

        return found.WorkflowState ?? WorkflowRules.InitialCode;
    }

    /// <summary>
    /// Writes a report's workflow code and answers with the report as stored,
    /// or null where no report has that id.
    /// </summary>
    /// <remarks>
    /// The move is validated before this is called — the code is one the
    /// vocabulary knows and the transition one the table allows — so here is
    /// only the write. Nothing else on the report moves with it.
    /// </remarks>
    public async Task<PersonConditionResponse?> SetWorkflow(
        int id,
        string workflowCode,
        CancellationToken cancellationToken)
    {
        var report = await database.PersonConditions
            .SingleOrDefaultAsync(report => report.Id == id, cancellationToken);

        if (report is null)
        {
            return null;
        }

        report.WorkflowState = workflowCode;
        await QueueForNotification(report.Id, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        return PersonConditionEndpoints.Respond(report);
    }
}
