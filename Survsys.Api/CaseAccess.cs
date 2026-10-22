using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// Which case morbidity reports a caller may reach: those of the agencies they
/// work for, those shared with one of those agencies, and every one for a
/// caller holding <see cref="Privileges.AllJurisdictions"/>.
/// </summary>
/// <remarks>
/// One rule over every case route rather than a check written into each
/// endpoint, in the way <see cref="PrivilegeRules"/> is one rule over every
/// route: <see cref="UseCaseAccess"/> runs after authorisation, finds the case
/// a request is about from its route pattern -- the case itself, or the case
/// that owns the lab, exposure, note or other record the route names -- and
/// answers 404 for a case the caller may not reach, as though it were not
/// there, so a refusal does not say which cases exist. A record that belongs
/// to no case (an outbreak's attachment, a task of nobody's case) is not a
/// case route and passes. The case search narrows by <see cref="Visible"/>,
/// the same rule as a query.
///
/// The caller's agencies travel on their token, as their privileges do, so
/// a change to them is felt at their next sign-in.
/// </remarks>
public static class CaseAccess
{
    private delegate IQueryable<int?> OwningCase(SurvsysDbContext database, int id);

    /// <summary>
    /// The route patterns that are about one case, by the start of the
    /// pattern: the route parameter that names the record, and the query from
    /// that record to its case, or none where the parameter is the case itself.
    /// </summary>
    private static readonly (string Prefix, string Parameter, OwningCase? Owner)[] Routes =
    [
        ("/api/person-conditions/{id:int}", "id", null),
        ("/api/cases/encounters/{id:int}", "id",
            (db, id) => db.Encounters.Where(row => row.Id == id).Select(row => (int?)row.PersonConditionId)),
        ("/api/cases/treatments/{id:int}", "id",
            (db, id) => db.PersonTreatments.Where(row => row.Id == id).Select(row => (int?)row.PersonConditionId)),
        ("/api/cases/vaccines/{id:int}", "id",
            (db, id) => db.PersonVaccines.Where(row => row.Id == id).Select(row => (int?)row.PersonConditionId)),
        ("/api/cases/forms/{assignmentId:int}", "assignmentId",
            (db, id) => db.FormAssignments.Where(row => row.Id == id).Select(row => (int?)row.PersonConditionId)),
        ("/api/cases/{personConditionId:int}", "personConditionId", null),
        ("/api/notification-queue/{personConditionId:int}", "personConditionId", null),
        ("/api/labs/{id:int}", "id",
            (db, id) => db.Labs.Where(row => row.Id == id).Select(row => row.PersonConditionId)),
        ("/api/lab-tests/{id:int}", "id",
            (db, id) => db.LabTests.Where(row => row.Id == id)
                .Join(db.Labs, test => test.LabId, lab => lab.Id, (_, lab) => lab.PersonConditionId)),
        ("/api/lab-test-results/{id:int}", "id",
            (db, id) => db.LabTestResults.Where(row => row.Id == id)
                .Join(db.LabTests, result => result.LabTestId, test => test.Id, (_, test) => test)
                .Join(db.Labs, test => test.LabId, lab => lab.Id, (_, lab) => lab.PersonConditionId)),
        ("/api/lab-resist-tests/{id:int}", "id",
            (db, id) => db.LabResistTests.Where(row => row.Id == id)
                .Join(db.Labs, test => test.LabId, lab => lab.Id, (_, lab) => lab.PersonConditionId)),
        ("/api/exposures/{id:int}", "id",
            (db, id) => db.Exposures.Where(row => row.Id == id).Select(row => row.PersonConditionId)),
        ("/api/exposure-places/{id:int}", "id",
            (db, id) => db.Exposures.Where(row => row.ExposurePlaceId == id).Select(row => row.PersonConditionId)),
        ("/api/exposure-contacts/{id:int}", "id",
            (db, id) => db.ExposureContacts.Where(row => row.Id == id)
                .Join(db.Exposures, contact => contact.ExposureId, exposure => exposure.Id, (_, exposure) => exposure.PersonConditionId)),
        ("/api/exposure-sources/{id:int}", "id",
            (db, id) => db.ExposureSources.Where(row => row.Id == id)
                .Join(db.Exposures, source => source.ExposureId, exposure => exposure.Id, (_, exposure) => exposure.PersonConditionId)),
        ("/api/notes/{id:int}", "id",
            (db, id) => db.Notes.Where(row => row.Id == id).Select(row => row.PersonConditionId)),
        ("/api/attachments/{id:int}", "id",
            (db, id) => db.Attachments.Where(row => row.Id == id).Select(row => row.PersonConditionId)),
        ("/api/tasks/{id:int}", "id",
            (db, id) => db.Tasks.Where(row => row.Id == id).Select(row => row.PersonConditionId)),
    ];

    /// <summary>
    /// The prefix of the case route a route pattern is, or null where the
    /// pattern is about no one case.
    /// </summary>
    /// <remarks>
    /// By whole segments, and without regard to case: <c>/api/labs/{id:int}/tests</c>
    /// is the lab route, and <c>/api/person-conditions</c>, which creates a
    /// case, is no case route.
    /// </remarks>
    public static string? CaseRouteOf(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        return Find(pattern)?.Prefix;
    }

    /// <summary>
    /// The reports a caller may reach, of those given.
    /// </summary>
    public static IQueryable<PersonCondition> Visible(
        SurvsysDbContext database,
        IQueryable<PersonCondition> reports,
        ClaimsPrincipal caller)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentNullException.ThrowIfNull(caller);

        if (caller.HasClaim(PrivilegeRules.PrivilegeClaim, Privileges.AllJurisdictions))
        {
            return reports;
        }

        var agencyIds = AccountEndpoints.AgencyIdsOf(caller);

        return reports.Where(report =>
            (report.AgencyId != null && agencyIds.Contains(report.AgencyId.Value))
            || database.PersonConditionShares.Any(share =>
                share.PersonConditionId == report.Id && agencyIds.Contains(share.AgencyId)));
    }

    /// <summary>
    /// Refuses, as 404 Problem Details, every request on a case route about a
    /// case the caller may not reach. Registered after authorisation, so the
    /// caller is known.
    /// </summary>
    public static IApplicationBuilder UseCaseAccess(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.GetEndpoint() is RouteEndpoint { RoutePattern.RawText: { } pattern }
                && Find(pattern) is { } route
                && int.TryParse(context.Request.RouteValues[route.Parameter]?.ToString(), out var id))
            {
                var database = context.RequestServices.GetRequiredService<SurvsysDbContext>();
                var caseId = route.Owner is { } owner
                    ? await owner(database, id).FirstOrDefaultAsync(context.RequestAborted)
                    : id;

                // A case nobody has is left to the endpoint, whose own 404 says so.
                var report = database.PersonConditions.Where(row => row.Id == caseId);

                if (await report.AnyAsync(context.RequestAborted)
                    && !await Visible(database, report, context.User).AnyAsync(context.RequestAborted))
                {
                    await Results.Problem(
                            detail: "There is no case morbidity report here that you may see.",
                            statusCode: StatusCodes.Status404NotFound,
                            title: "No such case morbidity report.")
                        .ExecuteAsync(context);

                    return;
                }
            }

            await next(context);
        });

    private static (string Prefix, string Parameter, OwningCase? Owner)? Find(string pattern)
    {
        foreach (var route in Routes)
        {
            if (pattern.StartsWith(route.Prefix, StringComparison.OrdinalIgnoreCase)
                && (pattern.Length == route.Prefix.Length || pattern[route.Prefix.Length] == '/'))
            {
                return route;
            }
        }

        return null;
    }
}
