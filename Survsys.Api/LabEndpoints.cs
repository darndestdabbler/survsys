using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>POST /api/person-conditions/{id}/labs</c>: records a laboratory specimen
/// against a report, with its tests, their results and its antimicrobial
/// susceptibility. <c>GET /api/person-conditions/{id}/labs</c>: that report's
/// specimens, paged. And <c>GET /api/labs/{id}</c>: one specimen whole.
/// </summary>
/// <remarks>
/// The entity-to-wire mapping lives here, as the other slices' does and for the
/// same reason: <c>Survsys.Data</c> may not reference <c>Survsys.Contracts</c>.
/// The handlers are handed <see cref="LabQueries"/> and not the
/// <c>DbContext</c>, because the architecture test lets no <c>Survsys.Data</c>
/// type into an endpoint signature at all.
///
/// A specimen is read whole by composing queries rather than by walking
/// navigations the entities do not have: the specimen, its tests, their
/// results and its susceptibility rows are four reads this tier stitches
/// together, which keeps the entities free of navigation a later session would
/// otherwise inherit unasked.
///
/// The specimen is collected from the report's person, so the person is read
/// from the report in the route and never taken on the wire. Recording a
/// specimen writes across five tables, so it is done inside one transaction:
/// the specimen, its tests and results, its susceptibility and the join to the
/// report either all land or none do.
/// </remarks>
public static class LabEndpoints
{
    /// <summary>The projection from a stored specimen to its wire shape.</summary>
    /// <remarks>
    /// An <see cref="Expression"/>, as the other slices' projections are: EF
    /// Core translates it into the <c>SELECT</c> of the list and the detail's
    /// specimen, and a unit test compiles and invokes it to prove what it
    /// carries. Public because the unit suite proves it.
    /// </remarks>
    public static readonly Expression<Func<Lab, LabResponse>> LabToResponse =
        lab => new LabResponse(
            lab.Id,
            lab.PersonId,
            lab.PersonConditionId,
            lab.LabFacilityId,
            lab.ProviderId,
            lab.DiagnosticFacilityId,
            lab.ReportingFacilityId,
            lab.SpecimenSourceId,
            lab.CollectionDate,
            lab.AccessionNo,
            lab.SpecimenSentToState);

    /// <summary>The projection from a stored result to its wire shape.</summary>
    public static readonly Expression<Func<LabTestResult, LabTestResultResponse>> ResultToResponse =
        result => new LabTestResultResponse(
            result.Id,
            result.LabTestId,
            result.OrganismId,
            result.ResultValue,
            result.TestResult,
            result.Comment,
            result.Units,
            result.PrimaryLabResult);

    /// <summary>
    /// The projection from a stored susceptibility test to its wire shape.
    /// </summary>
    public static readonly Expression<Func<LabResistTest, LabResistTestResponse>> ResistToResponse =
        resist => new LabResistTestResponse(
            resist.Id,
            resist.LabId,
            resist.OrganismId,
            resist.ResistTestAgentId,
            resist.ResistTestResultId,
            resist.TestDate);

    /// <summary>
    /// The projection from a stored test and its already-read results to the
    /// test's wire shape.
    /// </summary>
    /// <remarks>
    /// Two parameters because a test on the wire carries its results, which are
    /// read by their own query and passed in rather than reached through a
    /// navigation: this maps the test's own columns and hangs the results on
    /// them. Applied in memory — compiled into <see cref="RespondTest"/> — and
    /// not translated, because the results it is handed came from the database
    /// already. Public because the unit suite proves it.
    /// </remarks>
    public static readonly Expression<Func<LabTest, IReadOnlyList<LabTestResultResponse>, LabTestResponse>> TestToResponse =
        (test, results) => new LabTestResponse(
            test.Id,
            test.LabId,
            test.TestTypeId,
            test.TestStatusId,
            test.LoincCode,
            test.ReferenceRange,
            test.LabTestDate,
            results);

    /// <summary><see cref="TestToResponse"/> compiled, for the composition.</summary>
    public static readonly Func<LabTest, IReadOnlyList<LabTestResultResponse>, LabTestResponse> RespondTest =
        TestToResponse.Compile();

    /// <summary>
    /// Why the specimen asked to be recorded is not one this endpoint can
    /// record, as far as can be said without the database, or null when nothing
    /// is wrong with it.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="PersonConditionEndpoints.WhyNotAReport"/> has: a
    /// pure function the unit suite proves, with the endpoint turning the
    /// sentence into a 400. Whether the report exists is not here, because that
    /// needs a query — <see cref="LabQueries.WhyNotOnFile"/> is where that
    /// lives. What is here is the one thing about a test that is a
    /// contradiction rather than a missing reference: a test marks at most one
    /// of its results the primary one, because the primary result is the one a
    /// summary shows and there is only one summary.
    ///
    /// Public because the unit suite proves it; nothing outside this file calls
    /// it.
    /// </remarks>
    public static string? WhyNotALab(CreateLabRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (var test in request.Tests ?? [])
        {
            var primaries = (test.Results ?? []).Count(result => result.PrimaryLabResult);

            if (primaries > 1)
            {
                return $"a test has {primaries} results marked the primary one, and a test "
                    + "has at most one: the primary result is the one a summary shows. Mark "
                    + "one result the primary, or none.";
            }
        }

        return null;
    }

    /// <summary>
    /// The sentence a delete refused for its dependents carries: the row named
    /// by what it is and its id, then each kind of row still recorded against
    /// it that has any, counted and pluralised and joined with "and". A delete
    /// is refused rather than cascaded, so the rows hung off a specimen or a
    /// test are never taken out from under a correction by a foreign key.
    /// </summary>
    /// <remarks>
    /// Public because the unit suite proves it; the queries call it with the
    /// counts they have just read. A dependent whose count is zero is left out,
    /// so the sentence names only what actually blocks the delete.
    /// </remarks>
    public static string Still(int id, string what, params (int Count, string Noun)[] dependents)
    {
        ArgumentNullException.ThrowIfNull(dependents);

        var parts = dependents
            .Where(dependent => dependent.Count > 0)
            .Select(dependent => dependent.Count == 1
                ? $"1 {dependent.Noun}"
                : $"{dependent.Count} {dependent.Noun}s");

        return $"{what} {id} still has {string.Join(" and ", parts)} recorded against it and "
            + "cannot be removed. Remove those first.";
    }

    /// <summary>
    /// Registers what the laboratory endpoints need of the container. Kept here
    /// with the endpoints so the slice's wiring is in one place.
    /// </summary>
    public static IServiceCollection AddLabs(this IServiceCollection services) =>
        services.AddScoped<LabQueries>();

    public static IEndpointRouteBuilder MapLabs(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/person-conditions/{id:int}/labs", CreateLab)
            .WithName("CreateLab")
            .WithSummary("Records a laboratory specimen against a case morbidity report.");

        routes.MapGet("/api/person-conditions/{id:int}/labs", GetLabs)
            .WithName("GetLabs")
            .WithSummary("A case morbidity report's laboratory specimens, paged.");

        routes.MapGet("/api/labs/{id:int}", GetLab)
            .WithName("GetLab")
            .WithSummary("One laboratory specimen, with its tests, results and susceptibility.");

        routes.MapPut("/api/labs/{id:int}", UpdateLab)
            .WithName("UpdateLab")
            .WithSummary("Corrects a laboratory specimen's own columns.");

        routes.MapDelete("/api/labs/{id:int}", DeleteLab)
            .WithName("DeleteLab")
            .WithSummary("Removes a laboratory specimen that has no tests or susceptibility rows.");

        routes.MapPut("/api/lab-tests/{id:int}", UpdateLabTest)
            .WithName("UpdateLabTest")
            .WithSummary("Corrects a test's own columns.");

        routes.MapDelete("/api/lab-tests/{id:int}", DeleteLabTest)
            .WithName("DeleteLabTest")
            .WithSummary("Removes a test that has no results.");

        routes.MapPut("/api/lab-test-results/{id:int}", UpdateLabResult)
            .WithName("UpdateLabResult")
            .WithSummary("Corrects a test result.");

        routes.MapDelete("/api/lab-test-results/{id:int}", DeleteLabResult)
            .WithName("DeleteLabResult")
            .WithSummary("Removes a test result.");

        routes.MapPut("/api/lab-resist-tests/{id:int}", UpdateLabResist)
            .WithName("UpdateLabResist")
            .WithSummary("Corrects an antimicrobial susceptibility row.");

        routes.MapDelete("/api/lab-resist-tests/{id:int}", DeleteLabResist)
            .WithName("DeleteLabResist")
            .WithSummary("Removes an antimicrobial susceptibility row.");

        return routes;
    }

    private static async Task<Results<Created<LabDetailResponse>, ProblemHttpResult>> CreateLab(
        LabQueries labs,
        int id,
        CreateLabRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotALab(request) is { } why)
        {
            return Refused(why);
        }

        if (await labs.WhyNotOnFile(id, cancellationToken) is { } missing)
        {
            return Refused(missing);
        }

        var labId = await labs.Add(id, request, cancellationToken);

        var created = await labs.Detail(labId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"The specimen just recorded as id {labId} could not be read back.");

        return TypedResults.Created($"/api/labs/{labId}", created);
    }

    private static async Task<Results<Ok<PagedResult<LabResponse>>, ProblemHttpResult>> GetLabs(
        LabQueries labs,
        int id,
        int page = Paging.DefaultPage,
        int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (Paging.WhyNotAPage(page, pageSize) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The page asked for is not one this endpoint can return.");
        }

        if (await labs.WhyNotOnFile(id, cancellationToken) is { } missing)
        {
            return Refused(missing);
        }

        return TypedResults.Ok(await labs.Page(id, page, pageSize, cancellationToken));
    }

    private static async Task<Results<Ok<LabDetailResponse>, ProblemHttpResult>> GetLab(
        LabQueries labs,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await labs.Detail(id, cancellationToken) is not { } detail)
        {
            return TypedResults.Problem(
                detail: $"No laboratory specimen has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such laboratory specimen.");
        }

        return TypedResults.Ok(detail);
    }

    private static async Task<Results<Ok<LabResponse>, ProblemHttpResult>> UpdateLab(
        LabQueries labs,
        int id,
        UpdateLabRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await labs.UpdateSpecimen(id, request, cancellationToken) is not { } specimen)
        {
            return NoSuchLab(id);
        }

        return TypedResults.Ok(specimen);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteLab(
        LabQueries labs,
        int id,
        CancellationToken cancellationToken = default)
    {
        var (found, blocked) = await labs.DeleteSpecimen(id, cancellationToken);

        if (!found)
        {
            return NoSuchLab(id);
        }

        return blocked is { } why ? Dependent(why) : TypedResults.NoContent();
    }

    private static async Task<Results<Ok<LabTestResponse>, ProblemHttpResult>> UpdateLabTest(
        LabQueries labs,
        int id,
        UpdateLabTestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await labs.UpdateTest(id, request, cancellationToken) is not { } test)
        {
            return NoSuchTest(id);
        }

        return TypedResults.Ok(test);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteLabTest(
        LabQueries labs,
        int id,
        CancellationToken cancellationToken = default)
    {
        var (found, blocked) = await labs.DeleteTest(id, cancellationToken);

        if (!found)
        {
            return NoSuchTest(id);
        }

        return blocked is { } why ? Dependent(why) : TypedResults.NoContent();
    }

    private static async Task<Results<Ok<LabTestResultResponse>, ProblemHttpResult>> UpdateLabResult(
        LabQueries labs,
        int id,
        CreateLabTestResultRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await labs.WhyNotResultEdit(id, request, cancellationToken) is { } why)
        {
            return Refused(why);
        }

        if (await labs.UpdateResult(id, request, cancellationToken) is not { } result)
        {
            return NoSuchResult(id);
        }

        return TypedResults.Ok(result);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteLabResult(
        LabQueries labs,
        int id,
        CancellationToken cancellationToken = default) =>
        await labs.DeleteResult(id, cancellationToken)
            ? TypedResults.NoContent()
            : NoSuchResult(id);

    private static async Task<Results<Ok<LabResistTestResponse>, ProblemHttpResult>> UpdateLabResist(
        LabQueries labs,
        int id,
        CreateLabResistTestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await labs.UpdateResist(id, request, cancellationToken) is not { } resist)
        {
            return NoSuchResist(id);
        }

        return TypedResults.Ok(resist);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteLabResist(
        LabQueries labs,
        int id,
        CancellationToken cancellationToken = default) =>
        await labs.DeleteResist(id, cancellationToken)
            ? TypedResults.NoContent()
            : NoSuchResist(id);

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The laboratory specimen asked for is not one this endpoint can record.");

    private static ProblemHttpResult Dependent(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status409Conflict,
            title: "The row still has rows recorded against it and cannot be removed.");

    private static ProblemHttpResult NoSuchLab(int id) =>
        TypedResults.Problem(
            detail: $"No laboratory specimen has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such laboratory specimen.");

    private static ProblemHttpResult NoSuchTest(int id) =>
        TypedResults.Problem(
            detail: $"No test has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such test.");

    private static ProblemHttpResult NoSuchResult(int id) =>
        TypedResults.Problem(
            detail: $"No test result has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such test result.");

    private static ProblemHttpResult NoSuchResist(int id) =>
        TypedResults.Problem(
            detail: $"No antimicrobial susceptibility row has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such antimicrobial susceptibility row.");
}

/// <summary>
/// The laboratory queries and the recording, and the only thing in this tier
/// that holds the <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>PersonConditionQueries</c> is: it exists only to serve
/// <see cref="LabEndpoints"/>, and it is the named step between an endpoint
/// signature and <c>Survsys.Data</c> that the architecture test requires.
/// </remarks>
internal sealed class LabQueries(SurvsysDbContext database)
{
    /// <summary>
    /// Why the report a specimen would be recorded against is not one to record
    /// against, or null when it is.
    /// </summary>
    public async Task<string?> WhyNotOnFile(int reportId, CancellationToken cancellationToken)
    {
        if (!await database.PersonConditions.AnyAsync(
            report => report.Id == reportId, cancellationToken))
        {
            return $"personConditionId {reportId} is not a case morbidity report on file. A "
                + "specimen is recorded against a report that already exists: open the report "
                + "first, with POST /api/person-conditions.";
        }

        return null;
    }

    /// <summary>
    /// Records the specimen and everything hung off it, links it to the report,
    /// and answers with the new specimen's id.
    /// </summary>
    /// <remarks>
    /// The person is read from the report, because a specimen is collected from
    /// the person the report is about. The writes span five tables and the keys
    /// are database-generated, so they are saved in dependency order inside one
    /// transaction rather than reached through navigations the entities do not
    /// have: the specimen first, then each test and its results, then the
    /// susceptibility rows and the join. The report's existence is confirmed by
    /// <see cref="WhyNotOnFile"/> before this is called. A transaction the
    /// caller already holds is joined rather than a second begun, so a message
    /// written onto a case is written whole or not at all.
    /// </remarks>
    public async Task<int> Add(
        int reportId,
        CreateLabRequest request,
        CancellationToken cancellationToken)
    {
        var personId = await database.PersonConditions
            .Where(report => report.Id == reportId)
            .Select(report => report.PersonId)
            .SingleAsync(cancellationToken);

        await using var transaction = database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var specimen = new Lab
        {
            PersonId = personId,
            PersonConditionId = reportId,
            LabFacilityId = request.LabFacilityId,
            ProviderId = request.ProviderId,
            DiagnosticFacilityId = request.DiagnosticFacilityId,
            ReportingFacilityId = request.ReportingFacilityId,
            SpecimenSourceId = request.SpecimenSourceId,
            CollectionDate = request.CollectionDate,
            AccessionNo = request.AccessionNo,
            SpecimenSentToState = request.SpecimenSentToState,
        };

        database.Labs.Add(specimen);
        await database.SaveChangesAsync(cancellationToken);

        foreach (var testRequest in request.Tests ?? [])
        {
            var test = new LabTest
            {
                LabId = specimen.Id,
                TestTypeId = testRequest.TestTypeId,
                TestStatusId = testRequest.TestStatusId,
                LoincCode = testRequest.LoincCode,
                ReferenceRange = testRequest.ReferenceRange,
                LabTestDate = testRequest.LabTestDate,
            };

            database.LabTests.Add(test);
            await database.SaveChangesAsync(cancellationToken);

            foreach (var resultRequest in testRequest.Results ?? [])
            {
                database.LabTestResults.Add(new LabTestResult
                {
                    LabTestId = test.Id,
                    OrganismId = resultRequest.OrganismId,
                    ResultValue = resultRequest.ResultValue,
                    TestResult = resultRequest.TestResult,
                    Comment = resultRequest.Comment,
                    Units = resultRequest.Units,
                    PrimaryLabResult = resultRequest.PrimaryLabResult,
                });
            }
        }

        foreach (var resistRequest in request.ResistTests ?? [])
        {
            database.LabResistTests.Add(new LabResistTest
            {
                LabId = specimen.Id,
                OrganismId = resistRequest.OrganismId,
                ResistTestAgentId = resistRequest.ResistTestAgentId,
                ResistTestResultId = resistRequest.ResistTestResultId,
                TestDate = resistRequest.TestDate,
            });
        }

        database.PersonConditionLabs.Add(new PersonConditionLab
        {
            PersonConditionId = reportId,
            LabId = specimen.Id,
        });

        await QueueCase(reportId, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return specimen.Id;
    }

    /// <summary>
    /// One page of a report's specimens, ordered by id — a total order, so a
    /// specimen cannot appear on two pages or on neither.
    /// </summary>
    public async Task<PagedResult<LabResponse>> Page(
        int reportId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var specimens = database.PersonConditionLabs
            .Where(link => link.PersonConditionId == reportId)
            .Join(database.Labs, link => link.LabId, lab => lab.Id, (link, lab) => lab);

        var total = await specimens.CountAsync(cancellationToken);

        var rows = await specimens
            .OrderBy(lab => lab.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(LabEndpoints.LabToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<LabResponse>(rows, page, pageSize, total);
    }

    /// <summary>
    /// One specimen whole — the specimen, its tests with their results, and its
    /// susceptibility rows — or null where no specimen has that id.
    /// </summary>
    /// <remarks>
    /// Four reads composed in this tier rather than one query over navigations
    /// the entities do not have: the specimen, the tests, the results of those
    /// tests in one read keyed back to them, and the susceptibility rows. The
    /// results are grouped to their tests in memory, which is a handful of rows
    /// per specimen.
    /// </remarks>
    public async Task<LabDetailResponse?> Detail(int labId, CancellationToken cancellationToken)
    {
        var specimen = await database.Labs
            .Where(lab => lab.Id == labId)
            .Select(LabEndpoints.LabToResponse)
            .SingleOrDefaultAsync(cancellationToken);

        if (specimen is null)
        {
            return null;
        }

        var testEntities = await database.LabTests
            .Where(test => test.LabId == labId)
            .OrderBy(test => test.Id)
            .ToListAsync(cancellationToken);

        var testIds = testEntities.Select(test => test.Id).ToList();

        var resultsByTest = (await database.LabTestResults
                .Where(result => testIds.Contains(result.LabTestId))
                .OrderBy(result => result.Id)
                .Select(LabEndpoints.ResultToResponse)
                .ToListAsync(cancellationToken))
            .GroupBy(result => result.LabTestId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<LabTestResultResponse>)group.ToList());

        var tests = testEntities
            .Select(test => LabEndpoints.RespondTest(
                test, resultsByTest.GetValueOrDefault(test.Id, [])))
            .ToList();

        var resistTests = await database.LabResistTests
            .Where(resist => resist.LabId == labId)
            .OrderBy(resist => resist.Id)
            .Select(LabEndpoints.ResistToResponse)
            .ToListAsync(cancellationToken);

        return new LabDetailResponse(specimen, tests, resistTests);
    }

    /// <summary>
    /// Corrects a specimen's own columns and answers with it as stored, or null
    /// where no specimen has that id. The tests and susceptibility are left as
    /// they are, because they are corrected through their own routes.
    /// </summary>
    public async Task<LabResponse?> UpdateSpecimen(
        int labId,
        UpdateLabRequest request,
        CancellationToken cancellationToken)
    {
        var specimen = await database.Labs
            .SingleOrDefaultAsync(lab => lab.Id == labId, cancellationToken);

        if (specimen is null)
        {
            return null;
        }

        specimen.LabFacilityId = request.LabFacilityId;
        specimen.ProviderId = request.ProviderId;
        specimen.DiagnosticFacilityId = request.DiagnosticFacilityId;
        specimen.ReportingFacilityId = request.ReportingFacilityId;
        specimen.SpecimenSourceId = request.SpecimenSourceId;
        specimen.CollectionDate = request.CollectionDate;
        specimen.AccessionNo = request.AccessionNo;
        specimen.SpecimenSentToState = request.SpecimenSentToState;

        await QueueCase(specimen.PersonConditionId, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        return LabEndpoints.LabToResponse.Compile().Invoke(specimen);
    }

    /// <summary>
    /// Removes a specimen and the join that links it to its report, inside one
    /// transaction. The answer says whether the specimen was found and, where
    /// it was, why it could not be removed — a specimen that still has tests or
    /// susceptibility rows is kept, so that the rows recorded against it are
    /// never orphaned by a foreign key doing the refusing.
    /// </summary>
    public async Task<(bool Found, string? Blocked)> DeleteSpecimen(
        int labId,
        CancellationToken cancellationToken)
    {
        var specimen = await database.Labs
            .SingleOrDefaultAsync(lab => lab.Id == labId, cancellationToken);

        if (specimen is null)
        {
            return (false, null);
        }

        var tests = await database.LabTests.CountAsync(test => test.LabId == labId, cancellationToken);
        var resists = await database.LabResistTests
            .CountAsync(resist => resist.LabId == labId, cancellationToken);

        if (tests > 0 || resists > 0)
        {
            return (true, LabEndpoints.Still(labId, "laboratory specimen", (tests, "test"), (resists, "susceptibility row")));
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        database.PersonConditionLabs.RemoveRange(
            database.PersonConditionLabs.Where(link => link.LabId == labId));
        database.Labs.Remove(specimen);

        await QueueCase(specimen.PersonConditionId, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (true, null);
    }

    /// <summary>
    /// Corrects a test's own columns and answers with the test whole — its own
    /// columns and the results read back — or null where no test has that id.
    /// </summary>
    public async Task<LabTestResponse?> UpdateTest(
        int testId,
        UpdateLabTestRequest request,
        CancellationToken cancellationToken)
    {
        var test = await database.LabTests
            .SingleOrDefaultAsync(candidate => candidate.Id == testId, cancellationToken);

        if (test is null)
        {
            return null;
        }

        test.TestTypeId = request.TestTypeId;
        test.TestStatusId = request.TestStatusId;
        test.LoincCode = request.LoincCode;
        test.ReferenceRange = request.ReferenceRange;
        test.LabTestDate = request.LabTestDate;

        await QueueCaseOfLab(test.LabId, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        var results = await database.LabTestResults
            .Where(result => result.LabTestId == testId)
            .OrderBy(result => result.Id)
            .Select(LabEndpoints.ResultToResponse)
            .ToListAsync(cancellationToken);

        return LabEndpoints.RespondTest(test, results);
    }

    /// <summary>
    /// Removes a test, or keeps it with the reason where it still has results
    /// recorded against it, the answer shaped as <see cref="DeleteSpecimen"/>'s.
    /// </summary>
    public async Task<(bool Found, string? Blocked)> DeleteTest(
        int testId,
        CancellationToken cancellationToken)
    {
        var test = await database.LabTests
            .SingleOrDefaultAsync(candidate => candidate.Id == testId, cancellationToken);

        if (test is null)
        {
            return (false, null);
        }

        var results = await database.LabTestResults
            .CountAsync(result => result.LabTestId == testId, cancellationToken);

        if (results > 0)
        {
            return (true, LabEndpoints.Still(testId, "test", (results, "result")));
        }

        database.LabTests.Remove(test);
        await QueueCaseOfLab(test.LabId, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    /// <summary>
    /// Why a result correction cannot be made, as far as the database can say,
    /// or null when it can. The one database-bound half of
    /// <see cref="LabEndpoints.WhyNotALab"/>'s rule: a test marks at most one of
    /// its results the primary one, so marking a result primary is refused
    /// where another result on the same test already is. A result that is not
    /// on file passes here and is a 404 at the update.
    /// </summary>
    public async Task<string?> WhyNotResultEdit(
        int resultId,
        CreateLabTestResultRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.PrimaryLabResult)
        {
            return null;
        }

        var testId = await database.LabTestResults
            .Where(result => result.Id == resultId)
            .Select(result => (int?)result.LabTestId)
            .SingleOrDefaultAsync(cancellationToken);

        if (testId is null)
        {
            return null;
        }

        var alreadyPrimary = await database.LabTestResults.AnyAsync(
            result => result.LabTestId == testId
                && result.Id != resultId
                && result.PrimaryLabResult,
            cancellationToken);

        return alreadyPrimary
            ? "another result on this test is already the primary one, and a test has at most "
                + "one: the primary result is the one a summary shows. Clear the other result's "
                + "primary mark first, or leave this one unmarked."
            : null;
    }

    /// <summary>
    /// Corrects a result and answers with it as stored, or null where no result
    /// has that id. The primary-result rule is proven by
    /// <see cref="WhyNotResultEdit"/> before this is called.
    /// </summary>
    public async Task<LabTestResultResponse?> UpdateResult(
        int resultId,
        CreateLabTestResultRequest request,
        CancellationToken cancellationToken)
    {
        var result = await database.LabTestResults
            .SingleOrDefaultAsync(candidate => candidate.Id == resultId, cancellationToken);

        if (result is null)
        {
            return null;
        }

        result.OrganismId = request.OrganismId;
        result.ResultValue = request.ResultValue;
        result.TestResult = request.TestResult;
        result.Comment = request.Comment;
        result.Units = request.Units;
        result.PrimaryLabResult = request.PrimaryLabResult;

        await QueueCaseOfTest(result.LabTestId, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        return LabEndpoints.ResultToResponse.Compile().Invoke(result);
    }

    /// <summary>Removes a result — a leaf — and says whether it was there.</summary>
    public async Task<bool> DeleteResult(int resultId, CancellationToken cancellationToken)
    {
        var result = await database.LabTestResults
            .SingleOrDefaultAsync(candidate => candidate.Id == resultId, cancellationToken);

        if (result is null)
        {
            return false;
        }

        database.LabTestResults.Remove(result);
        await QueueCaseOfTest(result.LabTestId, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Corrects a susceptibility row and answers with it as stored, or null
    /// where no such row has that id.
    /// </summary>
    public async Task<LabResistTestResponse?> UpdateResist(
        int resistId,
        CreateLabResistTestRequest request,
        CancellationToken cancellationToken)
    {
        var resist = await database.LabResistTests
            .SingleOrDefaultAsync(candidate => candidate.Id == resistId, cancellationToken);

        if (resist is null)
        {
            return null;
        }

        resist.OrganismId = request.OrganismId;
        resist.ResistTestAgentId = request.ResistTestAgentId;
        resist.ResistTestResultId = request.ResistTestResultId;
        resist.TestDate = request.TestDate;

        await QueueCaseOfLab(resist.LabId, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        return LabEndpoints.ResistToResponse.Compile().Invoke(resist);
    }

    /// <summary>Removes a susceptibility row — a leaf — and says whether it was there.</summary>
    public async Task<bool> DeleteResist(int resistId, CancellationToken cancellationToken)
    {
        var resist = await database.LabResistTests
            .SingleOrDefaultAsync(candidate => candidate.Id == resistId, cancellationToken);

        if (resist is null)
        {
            return false;
        }

        database.LabResistTests.Remove(resist);
        await QueueCaseOfLab(resist.LabId, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Stages the auto-queue of the case a specimen is filed against, so the
    /// lab write and the case's queue entry land in one save; a specimen filed
    /// against no case queues nothing. Gated on the case's print criteria by
    /// <see cref="CaseNotificationQueue.QueueChangeAsync"/>.
    /// </summary>
    private Task QueueCase(int? reportId, CancellationToken cancellationToken) =>
        reportId is { } id
            ? new CaseNotificationQueue(database).QueueChangeAsync(new CaseChanged(id, DateTime.UtcNow), cancellationToken)
            : Task.CompletedTask;

    /// <summary><see cref="QueueCase"/> for the case of the specimen with that id.</summary>
    private async Task QueueCaseOfLab(int labId, CancellationToken cancellationToken) =>
        await QueueCase(
            await database.Labs.Where(lab => lab.Id == labId).Select(lab => lab.PersonConditionId)
                .SingleOrDefaultAsync(cancellationToken),
            cancellationToken);

    /// <summary><see cref="QueueCase"/> for the case of the specimen the test with that id is on.</summary>
    private async Task QueueCaseOfTest(int testId, CancellationToken cancellationToken) =>
        await QueueCaseOfLab(
            await database.LabTests.Where(test => test.Id == testId).Select(test => test.LabId)
                .SingleOrDefaultAsync(cancellationToken),
            cancellationToken);
}
