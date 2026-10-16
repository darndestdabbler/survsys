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
            lab.LabFacilityId,
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

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The laboratory specimen asked for is not one this endpoint can record.");
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
    /// <see cref="WhyNotOnFile"/> before this is called.
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

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var specimen = new Lab
        {
            PersonId = personId,
            LabFacilityId = request.LabFacilityId,
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

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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
}
