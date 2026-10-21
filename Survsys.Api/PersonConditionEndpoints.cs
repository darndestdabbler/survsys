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
            report.WorkflowState,
            report.InvestigatorId,
            report.EventType,
            report.BriefNote,
            report.LhdCaseStatusId,
            report.StateCaseStatusId,
            report.DeletedAt);

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

        // A contact event is opened from the exposure the contact was at, so
        // the one route that opens one by hand offers the other two kinds.
        if (request.EventType is { } eventType
            && eventType != EventTypes.Morbidity
            && eventType != EventTypes.Assessment)
        {
            return $"eventType '{eventType}' cannot be opened here: a report is opened as "
                + $"'{EventTypes.Morbidity}' or '{EventTypes.Assessment}', and a contact event "
                + "is added from the exposure the contact was at.";
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

    /// <summary>Room for the report header's brief note.</summary>
    public const int BriefNoteLength = 500;

    /// <summary>
    /// Why this is not a brief note this tier will save, or null when it is
    /// one: a note longer than the column is refused, and a blank one clears it.
    /// </summary>
    /// <remarks>
    /// Public because the unit suite proves it. Nothing outside this file calls
    /// it.
    /// </remarks>
    public static string? WhyNotABriefNote(UpdateBriefNoteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.BriefNote?.Trim().Length > BriefNoteLength
            ? $"briefNote is longer than {BriefNoteLength} characters."
            : null;
    }

    /// <summary>Room for the note a workflow move carries.</summary>
    public const int NoteLength = 1000;

    /// <summary>
    /// Why this is not a workflow move this tier will make, as far as can be
    /// said without the database, or null when it is one.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="WhyNotAReport"/> and <see cref="WhyNotAStatus"/>
    /// have: a pure function the unit suite proves, with the endpoint turning
    /// the sentence into a 400. A transition code the vocabulary does not know,
    /// a route with no agency or an assignment with no investigator, and a note
    /// longer than the column are refused here; whether the report's state
    /// allows the move, whether the caller may make it, and whether the agency
    /// or the investigator is on file need the caller's claims or a query.
    ///
    /// Public because the unit suite proves it. Nothing outside this file calls
    /// it.
    /// </remarks>
    public static string? WhyNotATransition(WorkflowTransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!WorkflowRules.TryParseTransition(request.Transition, out var transition))
        {
            return $"transition '{request.Transition}' is not a workflow move this tier knows. "
                + $"A transition is one of {TransitionCodes}.";
        }

        if (transition == WorkflowTransition.Route && request.AgencyId is not > 0)
        {
            return "agencyId is required for a route: the report is routed to an agency, and "
                + $"{request.AgencyId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "nothing"} "
                + "is not an identifier any agency has.";
        }

        if (transition == WorkflowTransition.AssignInvestigator && request.InvestigatorId is not > 0)
        {
            return "investigatorId is required to assign an investigator, and "
                + $"{request.InvestigatorId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "nothing"} "
                + "is not an identifier any user has.";
        }

        if (request.Note is { Length: > NoteLength })
        {
            return $"note is longer than {NoteLength} characters.";
        }

        return null;
    }

    /// <summary>
    /// The privilege a transition needs: the investigating department's own
    /// moves are a case edit; routing, assigning an investigator and the state's
    /// review each have a privilege of their own.
    /// </summary>
    /// <remarks>
    /// Here and not in <see cref="WorkflowRules"/> because the privilege names
    /// are <c>Survsys.Contracts</c>', which <c>Survsys.Data</c> may not
    /// reference. Public because the unit suite proves it.
    /// </remarks>
    public static string PrivilegeFor(WorkflowTransition transition) => transition switch
    {
        WorkflowTransition.Accept => Privileges.EditCases,
        WorkflowTransition.Investigate => Privileges.EditCases,
        WorkflowTransition.Close => Privileges.EditCases,
        WorkflowTransition.DoNotInvestigate => Privileges.EditCases,
        WorkflowTransition.Route => Privileges.RouteCases,
        WorkflowTransition.AssignInvestigator => Privileges.AssignInvestigators,
        WorkflowTransition.Reopen => Privileges.ApproveCases,
        WorkflowTransition.Approve => Privileges.ApproveCases,
        WorkflowTransition.Reject => Privileges.ApproveCases,
        _ => throw new ArgumentOutOfRangeException(
            nameof(transition), transition, "is not a workflow transition this rule knows."),
    };

    /// <summary>
    /// The transitions a caller holding these privileges may make from a state,
    /// as their codes and in the workflow's order: what a Workflow Options menu
    /// offers, already narrowed to the person looking at it.
    /// </summary>
    /// <remarks>Public because the unit suite proves it.</remarks>
    public static IReadOnlyList<string> OptionsFor(WorkflowState state, IEnumerable<string> heldPrivileges)
    {
        ArgumentNullException.ThrowIfNull(heldPrivileges);

        var held = heldPrivileges.ToHashSet(StringComparer.Ordinal);

        return WorkflowRules.AllowedFrom(state)
            .Where(transition => held.Contains(PrivilegeFor(transition)))
            .Select(WorkflowRules.CodeOf)
            .ToList();
    }

    private static string TransitionCodes => string.Join(", ",
        Enum.GetValues<WorkflowTransition>().Select(WorkflowRules.CodeOf));

    /// <summary>
    /// Why a case with this event type cannot be promoted to a morbidity
    /// report, or null when it can.
    /// </summary>
    /// <remarks>
    /// Only a contact event is promoted: a morbidity report is one already,
    /// and an assessment is opened by hand and becomes a case by being opened
    /// as one. Public because the unit suite proves it.
    /// </remarks>
    public static string? WhyNotAPromotion(string eventType) =>
        eventType == EventTypes.Contact
            ? null
            : $"This case is a {eventType} event, and only a contact event is promoted "
                + "to a morbidity report.";

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

        routes.MapPut("/api/person-conditions/{id:int}/brief-note", SetBriefNote)
            .WithName("SetPersonConditionBriefNote")
            .WithSummary("Saves a case morbidity report's brief note.");

        routes.MapGet("/api/person-conditions/{id:int}/workflow", GetWorkflow)
            .WithName("GetPersonConditionWorkflow")
            .WithSummary("A case morbidity report's workflow state, the moves the caller may "
                + "make from it, and its history.");

        routes.MapPut("/api/person-conditions/{id:int}/workflow", MakeTransition)
            .WithName("SetPersonConditionWorkflow")
            .WithSummary("Makes a workflow move on a case morbidity report, where its state "
                + "allows the move and the caller holds the privilege it needs.");

        routes.MapPost("/api/person-conditions/{id:int}/promote", Promote)
            .WithName("PromotePersonCondition")
            .WithSummary("Promotes a contact event to a morbidity report.");

        routes.MapDelete("/api/person-conditions/{id:int}", DeletePersonCondition)
            .WithName("DeletePersonCondition")
            .WithSummary("Deletes a case morbidity report softly: it is kept, marked deleted "
                + "and left out of the case search.");

        routes.MapPost("/api/person-conditions/{id:int}/restore", RestorePersonCondition)
            .WithName("RestorePersonCondition")
            .WithSummary("Restores a deleted case morbidity report.");

        return routes;
    }

    /// <remarks>
    /// The soft delete: the report is stamped and kept, so a restore brings it
    /// back whole. Deleting a report already deleted keeps its first stamp and
    /// answers the same 204, as a repeated DELETE should.
    /// </remarks>
    private static async Task<Results<NoContent, ProblemHttpResult>> DeletePersonCondition(
        PersonConditionQueries reports,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await reports.SetDeleted(id, DateTimeOffset.UtcNow, cancellationToken) is null)
        {
            return NoSuchReport(id);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<PersonConditionResponse>, ProblemHttpResult>> RestorePersonCondition(
        PersonConditionQueries reports,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await reports.SetDeleted(id, null, cancellationToken) is not { } report)
        {
            return NoSuchReport(id);
        }

        return TypedResults.Ok(report);
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
            await audit.Record(userId, id, "View", null, cancellationToken);
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

    private static async Task<Results<Ok<PersonConditionResponse>, ProblemHttpResult>> SetBriefNote(
        PersonConditionQueries reports,
        int id,
        UpdateBriefNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotABriefNote(request) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The brief note asked for is not one this endpoint can save.");
        }

        if (await reports.SetBriefNote(id, request, cancellationToken) is not { } report)
        {
            return TypedResults.Problem(
                detail: $"No case morbidity report has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such case morbidity report.");
        }

        return TypedResults.Ok(report);
    }

    /// <remarks>
    /// The report's state, the moves this caller may make from it and its
    /// history, in one answer: a screen offers exactly the options listed and
    /// repeats no rule of its own. The options are the transition table's moves
    /// from the state, narrowed by the privileges the caller's claims carry. A
    /// report id that names no report is a 404.
    /// </remarks>
    private static async Task<Results<Ok<WorkflowResponse>, ProblemHttpResult>> GetWorkflow(
        PersonConditionQueries reports,
        ClaimsPrincipal user,
        int id,
        CancellationToken cancellationToken = default)
    {
        var held = user.FindAll(PrivilegeRules.PrivilegeClaim).Select(claim => claim.Value).ToList();

        if (await reports.Workflow(id, held, cancellationToken) is not { } workflow)
        {
            return NoSuchReport(id);
        }

        return TypedResults.Ok(workflow);
    }

    /// <remarks>
    /// A move is refused in the order the questions can be answered: what is
    /// wrong with the request itself is a 400 before anything is read; a caller
    /// whose claims lack the transition's privilege is a 403, so a department
    /// user cannot route or approve whatever state the report is in; a report
    /// id that names no report is a 404; a move the report's state does not
    /// allow is a 400 that names the state and the moves it does allow; and a
    /// route to an agency not on file or an assignment to a user not on file or
    /// not active is a 400. Only then is the move made, which writes the state,
    /// the agency or investigator, the history row and the notification queue
    /// entry in one save.
    ///
    /// The clock is read here, once, for the history row, as the creation reads
    /// it once for the record number. The <see cref="WorkflowTransition"/> enum
    /// stays out of this signature and in the body, as the architecture test
    /// requires of an endpoint; the query holds the <c>DbContext</c>.
    /// </remarks>
    private static async Task<Results<Ok<PersonConditionResponse>, ProblemHttpResult>> MakeTransition(
        PersonConditionQueries reports,
        AgencyQueries agencies,
        ClaimsPrincipal user,
        int id,
        WorkflowTransitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (WhyNotATransition(request) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The workflow move asked for is not one this endpoint can make.");
        }

        WorkflowRules.TryParseTransition(request.Transition, out var transition);
        var required = PrivilegeFor(transition);

        if (!user.HasClaim(PrivilegeRules.PrivilegeClaim, required))
        {
            return TypedResults.Problem(
                detail: $"{required} is required to {WorkflowRules.CodeOf(transition)} a case morbidity report.",
                statusCode: StatusCodes.Status403Forbidden,
                title: "The caller does not hold the privilege this workflow move needs.");
        }

        if (AccountEndpoints.UserIdOf(user) is not { } userId)
        {
            return TypedResults.Problem(
                detail: "The caller's token names no user this tier knows, and a workflow move is recorded against the user who made it.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "The caller is not a signed-in user.");
        }

        if (await reports.WorkflowCodeOf(id, cancellationToken) is not { } currentCode)
        {
            return NoSuchReport(id);
        }

        var current = WorkflowRules.Read(currentCode);

        if (!WorkflowRules.IsAllowed(current, transition))
        {
            var allowed = WorkflowRules.AllowedFrom(current).Select(WorkflowRules.CodeOf).ToList();

            return TypedResults.Problem(
                detail: $"A report at {WorkflowRules.CodeOf(current)} may not {WorkflowRules.CodeOf(transition)}. "
                    + (allowed.Count == 0
                        ? "No move is allowed from there."
                        : $"From there it may {string.Join(", ", allowed)}."),
                statusCode: StatusCodes.Status400BadRequest,
                title: "The workflow move asked for is not one the transition table allows.");
        }

        if (transition == WorkflowTransition.Route
            && await agencies.Find(request.AgencyId!.Value, cancellationToken) is null)
        {
            return TypedResults.Problem(
                detail: AgencyEndpoints.NotAnAgency(request.AgencyId.Value),
                statusCode: StatusCodes.Status400BadRequest,
                title: "The agency the report is to be routed to is not one this endpoint knows.");
        }

        if (transition == WorkflowTransition.AssignInvestigator
            && !await reports.IsAnInvestigator(request.InvestigatorId!.Value, cancellationToken))
        {
            return TypedResults.Problem(
                detail: $"investigatorId {request.InvestigatorId.Value} is not an active user. The "
                    + "investigators are the ones GET /api/investigators lists.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "The investigator named is not one this endpoint knows.");
        }

        if (await reports.Transition(id, userId, transition, request, DateTimeOffset.UtcNow, cancellationToken)
            is not { } report)
        {
            return NoSuchReport(id);
        }

        return TypedResults.Ok(report);
    }

    private static async Task<Results<Ok<PersonConditionResponse>, ProblemHttpResult>> Promote(
        PersonConditionQueries reports,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await reports.EventTypeOf(id, cancellationToken) is not { } eventType)
        {
            return NoSuchReport(id);
        }

        if (WhyNotAPromotion(eventType) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The case is not one this endpoint can promote.");
        }

        return TypedResults.Ok(await reports.Promote(id, cancellationToken));
    }

    private static ProblemHttpResult NoSuchReport(int id) =>
        TypedResults.Problem(
            detail: $"No case morbidity report has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such case morbidity report.");

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
        notifications.QueueChangeAsync(new CaseChanged(reportId, DateTime.UtcNow), cancellationToken);

    /// <summary>
    /// Stages the assignment of the new case's condition's auto-assign forms on
    /// the context, so the next <c>SaveChangesAsync</c> writes them alongside
    /// the case's queue entry.
    /// </summary>
    /// <remarks>
    /// A supplemental form marked <see cref="Form.AutoAssign"/> for the case's
    /// condition is given to the case as it is opened — the one place
    /// auto-assignment runs. Forms already assigned to the case are skipped, so
    /// a case that is somehow created against an id that already holds the form
    /// is not given it twice; a brand-new case holds none, so every auto-assign
    /// form for its condition is assigned. The assignment time is read once
    /// here, as the record number's year is.
    /// </remarks>
    private async Task AutoAssignForms(PersonCondition report, CancellationToken cancellationToken)
    {
        var formIds = await database.Forms
            .Where(form => form.ConditionId == report.ConditionId && form.AutoAssign)
            .Select(form => form.Id)
            .ToListAsync(cancellationToken);

        if (formIds.Count == 0)
        {
            return;
        }

        var alreadyAssigned = await database.FormAssignments
            .Where(assignment => assignment.PersonConditionId == report.Id
                && formIds.Contains(assignment.FormId))
            .Select(assignment => assignment.FormId)
            .ToListAsync(cancellationToken);

        var assignedAt = DateTimeOffset.UtcNow;

        database.FormAssignments.AddRange(formIds
            .Except(alreadyAssigned)
            .Select(formId => new FormAssignment
            {
                FormId = formId,
                PersonConditionId = report.Id,
                AssignedAt = assignedAt,
            }));
    }

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
            EventType = request.EventType ?? EventTypes.Morbidity,
        };

        database.PersonConditions.Add(report);
        await database.SaveChangesAsync(cancellationToken);

        // A new case has no id until it is saved, so the auto-assigned forms
        // and the notification queue entry are added after the insert and in a
        // second save rather than the one above.
        await AutoAssignForms(report, cancellationToken);
        await QueueForNotification(report.Id, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        return PersonConditionEndpoints.Respond(report);
    }

    /// <summary>The event type of the report with that id, or null where there is none.</summary>
    public Task<string?> EventTypeOf(int id, CancellationToken cancellationToken) =>
        database.PersonConditions
            .Where(report => report.Id == id)
            .Select(report => report.EventType)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Makes a contact event a morbidity report in place, and answers with it
    /// as stored.
    /// </summary>
    /// <remarks>
    /// The endpoint has confirmed the report is a contact event. A contact
    /// event is not queued for notification when it is opened, so it is queued
    /// here, in the same save, as a newly opened report is.
    /// </remarks>
    public async Task<PersonConditionResponse> Promote(int id, CancellationToken cancellationToken)
    {
        var report = await database.PersonConditions
            .SingleAsync(report => report.Id == id, cancellationToken);

        report.EventType = EventTypes.Morbidity;
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
    /// Saves the report's brief note, trimmed and with a blank one stored as
    /// null, and answers the report; null where no report has that id.
    /// </summary>
    public async Task<PersonConditionResponse?> SetBriefNote(
        int id,
        UpdateBriefNoteRequest request,
        CancellationToken cancellationToken)
    {
        var report = await database.PersonConditions
            .SingleOrDefaultAsync(report => report.Id == id, cancellationToken);

        if (report is null)
        {
            return null;
        }

        report.BriefNote = string.IsNullOrWhiteSpace(request.BriefNote)
            ? null
            : request.BriefNote.Trim();
        await database.SaveChangesAsync(cancellationToken);

        return PersonConditionEndpoints.Respond(report);
    }

    /// <summary>
    /// Deletes the report softly, or restores it where <paramref name="deletedAt"/>
    /// is null, and answers the report; null where no report has that id.
    /// </summary>
    /// <remarks>
    /// A report already deleted keeps the moment it was first deleted.
    /// </remarks>
    public async Task<PersonConditionResponse?> SetDeleted(
        int id,
        DateTimeOffset? deletedAt,
        CancellationToken cancellationToken)
    {
        var report = await database.PersonConditions
            .SingleOrDefaultAsync(report => report.Id == id, cancellationToken);

        if (report is null)
        {
            return null;
        }

        report.DeletedAt = deletedAt is null ? null : report.DeletedAt ?? deletedAt;
        await database.SaveChangesAsync(cancellationToken);

        return PersonConditionEndpoints.Respond(report);
    }

    /// <summary>Whether a user with that id is on file and active, and so may be assigned a case.</summary>
    public Task<bool> IsAnInvestigator(int userId, CancellationToken cancellationToken) =>
        database.Users.AnyAsync(user => user.Id == userId && user.Active, cancellationToken);

    /// <summary>
    /// The report's workflow as the caller sees it: its state, the moves a
    /// caller holding these privileges may make from it, and every move it has
    /// made, newest first — or null where no report has that id.
    /// </summary>
    public async Task<WorkflowResponse?> Workflow(
        int id,
        IReadOnlyList<string> heldPrivileges,
        CancellationToken cancellationToken)
    {
        var found = await database.PersonConditions
            .Where(report => report.Id == id)
            .Select(report => new { report.WorkflowState, report.InvestigatorId })
            .SingleOrDefaultAsync(cancellationToken);

        if (found is null)
        {
            return null;
        }

        var history = await database.WorkflowHistory
            .Where(entry => entry.PersonConditionId == id)
            .Join(database.Users, entry => entry.UserId, user => user.Id, (entry, user) => new { entry, user })
            .OrderByDescending(row => row.entry.OccurredAt)
            .ThenByDescending(row => row.entry.Id)
            .Select(row => new WorkflowHistoryEntryResponse(
                row.entry.OccurredAt,
                row.entry.UserId,
                row.user.DisplayName,
                row.entry.Transition,
                row.entry.FromState,
                row.entry.ToState,
                row.entry.Note))
            .ToListAsync(cancellationToken);

        var state = WorkflowRules.Read(found.WorkflowState);

        return new WorkflowResponse(
            id,
            WorkflowRules.CodeOf(state),
            found.InvestigatorId,
            PersonConditionEndpoints.OptionsFor(state, heldPrivileges),
            history);
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
    /// Makes a workflow move on a report and answers with the report as stored,
    /// or null where no report has that id.
    /// </summary>
    /// <remarks>
    /// The move is validated before this is called — the transition is one the
    /// table allows from the report's state, the caller may make it, and the
    /// agency or investigator it names is on file — so here is only the write,
    /// and it is one save: the state moves to the transition's target, a route
    /// sets the agency and an assignment the investigator, the history row
    /// records who, when, which move, from where to where and the note, and the
    /// case is queued for notification as every change to it is.
    /// </remarks>
    public async Task<PersonConditionResponse?> Transition(
        int id,
        int userId,
        WorkflowTransition transition,
        WorkflowTransitionRequest request,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var report = await database.PersonConditions
            .SingleOrDefaultAsync(report => report.Id == id, cancellationToken);

        if (report is null)
        {
            return null;
        }

        var from = WorkflowRules.Read(report.WorkflowState);
        var to = WorkflowRules.TargetOf(transition);

        report.WorkflowState = WorkflowRules.CodeOf(to);

        if (transition == WorkflowTransition.Route)
        {
            report.AgencyId = request.AgencyId;
        }

        if (transition == WorkflowTransition.AssignInvestigator)
        {
            report.InvestigatorId = request.InvestigatorId;
        }

        database.WorkflowHistory.Add(new WorkflowHistoryEntry
        {
            PersonConditionId = report.Id,
            UserId = userId,
            OccurredAt = occurredAt,
            Transition = WorkflowRules.CodeOf(transition),
            FromState = WorkflowRules.CodeOf(from),
            ToState = WorkflowRules.CodeOf(to),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
        });

        await QueueForNotification(report.Id, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        return PersonConditionEndpoints.Respond(report);
    }
}
