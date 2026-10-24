using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// A user's reach over cases against the real database: a case of another
/// agency refused on every case route and missing from the case search and
/// every list, a case of the user's own agency and one shared with it served,
/// and the shared one refused every write.
/// </summary>
/// <remarks>
/// The routes are not listed here but read from the running host's endpoints,
/// every one <see cref="CaseAccess.CaseRouteOf"/> calls a case route, so a case
/// route mapped later is asked the day it is mapped. Each test seeds its own
/// cases and records and removes them after.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public partial class CaseAccessTests(SqlServerFixture server) : IDisposable
{
    private const string UserName = "jurisdiction-user";

    private const string UserPassword = "jurisdiction-user-password";

    private const string RefusedTitle = "No such case morbidity report.";

    private const string ReadOnlyTitle = "The case is shared with your agency read-only.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    /// <summary>
    /// Every method of every case route, about a case of another agency or a
    /// record on one, is answered with the case refusal; and the case search
    /// does not list the case.
    /// </summary>
    [Fact]
    public async Task ACaseOfAnotherAgencyIsRefusedOnEveryCaseRouteAndMissingFromTheSearch()
    {
        var admin = api.CreateClient();
        var (theirs, mine) = await TwoAgencies(admin);
        using var user = await SignedIn(admin, mine);
        var report = await Open(admin, theirs);
        var seeded = await Seed(report);

        try
        {
            var routes = api.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Where(endpoint => endpoint.RoutePattern.RawText is { } pattern && CaseAccess.CaseRouteOf(pattern) is not null)
                .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                    .Select(method => (
                        Method: method,
                        Pattern: endpoint.RoutePattern.RawText!,
                        Multipart: endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes
                            .Contains("multipart/form-data") == true)))
                .ToList();

            Assert.True(routes.Count > 50, $"Only {routes.Count} case routes were found.");

            foreach (var (method, pattern, multipart) in routes)
            {
                var url = UrlFor(pattern, seeded);
                using var request = new HttpRequestMessage(new HttpMethod(method), url);

                // A body of the type the route accepts, so routing reaches the
                // route rather than answering 415 before the case is asked.
                if (multipart)
                {
                    request.Content = new MultipartFormDataContent { { new StringContent("x"), "file", "x.txt" } };
                }
                else if (method is "POST" or "PUT")
                {
                    request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                }

                using var response = await user.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                Assert.True(
                    response.StatusCode == HttpStatusCode.NotFound && body.Contains(RefusedTitle, StringComparison.Ordinal),
                    $"{method} {url} answered {(int)response.StatusCode}: {body}");
            }

            var found = (await user.GetFromJsonAsync<PagedResult<CaseSearchItemResponse>>(
                $"/api/cases?recordNumber={report.RecordNumber}", Json))!;
            Assert.DoesNotContain(found.Items, item => item.Id == report.Id);
        }
        finally
        {
            await Remove(seeded);
        }
    }

    /// <summary>
    /// A case of the user's own agency and a case of another agency shared
    /// with theirs are both served, and both found by the case search.
    /// </summary>
    [Fact]
    public async Task ACaseOfTheUsersAgencyAndOneSharedWithItAreServed()
    {
        var admin = api.CreateClient();
        var (theirs, mine) = await TwoAgencies(admin);
        using var user = await SignedIn(admin, mine);
        var own = await Open(admin, mine);
        var shared = await Open(admin, theirs);

        try
        {
            await using (var context = server.NewContext())
            {
                context.PersonConditionShares.Add(new PersonConditionShare { PersonConditionId = shared.Id, AgencyId = mine });
                await context.SaveChangesAsync();
            }

            foreach (var report in new[] { own, shared })
            {
                using var read = await user.GetAsync($"/api/person-conditions/{report.Id}");
                Assert.Equal(HttpStatusCode.OK, read.StatusCode);

                var found = (await user.GetFromJsonAsync<PagedResult<CaseSearchItemResponse>>(
                    $"/api/cases?recordNumber={report.RecordNumber}", Json))!;
                Assert.Contains(found.Items, item => item.Id == report.Id);
            }
        }
        finally
        {
            await using var context = server.NewContext();
            await context.PersonConditionShares.Where(row => row.PersonConditionId == shared.Id).ExecuteDeleteAsync();
            await context.PersonConditions.Where(row => row.Id == own.Id || row.Id == shared.Id).ExecuteDeleteAsync();
        }
    }

    /// <summary>
    /// The work queue, the task list, a saved view's export and counts and
    /// the case-count report list a case of the user's agency and not one of
    /// another agency; the work queue of another agency lists nothing.
    /// </summary>
    [Fact]
    public async Task EveryListHoldsACaseOfTheUsersAgencyAndNotOneOfAnother()
    {
        var admin = api.CreateClient();
        var (theirs, mine) = await TwoAgencies(admin);
        using var user = await SignedIn(admin, mine);
        var own = await Open(admin, mine);
        var other = await Open(admin, theirs);
        var agencies = (await admin.GetFromJsonAsync<PagedResult<AgencyResponse>>("/api/agencies?pageSize=200", Json))!;
        int? viewId = null;

        try
        {
            // Due first of all tasks, so both sit on the first page.
            await using (var context = server.NewContext())
            {
                context.Tasks.AddRange(
                    new CaseTask { PersonConditionId = own.Id, Name = "Own task", CreatedAt = DateTimeOffset.UtcNow, DueDate = new DateOnly(1900, 1, 1) },
                    new CaseTask { PersonConditionId = other.Id, Name = "Other task", CreatedAt = DateTimeOffset.UtcNow, DueDate = new DateOnly(1900, 1, 1) });
                await context.SaveChangesAsync();
            }

            var queue = (await user.GetFromJsonAsync<PagedResult<WorkQueueItemResponse>>(
                $"/api/work-queue?agencyId={mine}&pageSize=200", Json))!;
            Assert.Contains(queue.Items, item => item.Id == own.Id);
            Assert.DoesNotContain(queue.Items, item => item.Id == other.Id);

            var theirQueue = (await user.GetFromJsonAsync<PagedResult<WorkQueueItemResponse>>(
                $"/api/work-queue?agencyId={theirs}", Json))!;
            Assert.Empty(theirQueue.Items);
            Assert.Equal(0, theirQueue.TotalCount);

            var tasks = (await user.GetFromJsonAsync<PagedResult<TaskResponse>>("/api/tasks?pageSize=200", Json))!;
            Assert.Contains(tasks.Items, task => task.PersonConditionId == own.Id);
            Assert.DoesNotContain(tasks.Items, task => task.PersonConditionId == other.Id);

            var view = await Created<SavedViewResponse>(user, "/api/saved-views",
                new SaveSavedViewRequest($"Access view {Guid.NewGuid():N}", [ExportColumns.RecordNumber], ConditionId: own.ConditionId));
            viewId = view.Id;

            var csv = await user.GetStringAsync($"/api/saved-views/{view.Id}/export");
            Assert.Contains(own.RecordNumber, csv, StringComparison.Ordinal);
            Assert.DoesNotContain(other.RecordNumber, csv, StringComparison.Ordinal);

            var viewCounts = (await user.GetFromJsonAsync<IReadOnlyList<CaseCountResponse>>(
                $"/api/reports/saved-views/{view.Id}/counts?groupBy={ExportColumns.RecordNumber}", Json))!;
            Assert.Contains(viewCounts, row => row.Group == own.RecordNumber);
            Assert.DoesNotContain(viewCounts, row => row.Group == other.RecordNumber);

            var counts = (await user.GetFromJsonAsync<IReadOnlyList<CaseCountResponse>>(
                $"/api/reports/case-counts?groupBy={ReportGroupings.Agency}", Json))!;
            Assert.Contains(counts, row => row.Group == agencies.Items.Single(agency => agency.Id == mine).Name);
            Assert.DoesNotContain(counts, row => row.Group == agencies.Items.Single(agency => agency.Id == theirs).Name);
        }
        finally
        {
            await using var context = server.NewContext();
            await context.SavedViews.Where(row => row.Id == viewId).ExecuteDeleteAsync();
            await context.Tasks.Where(row => row.PersonConditionId == own.Id || row.PersonConditionId == other.Id).ExecuteDeleteAsync();
            await context.PersonConditions.Where(row => row.Id == own.Id || row.Id == other.Id).ExecuteDeleteAsync();
        }
    }

    /// <summary>
    /// Every write on every case route, about a case shared with the user's
    /// agency or a record on one, is refused as read-only; reading the case is
    /// served, and says it is shared read-only where the user's own case does not.
    /// </summary>
    [Fact]
    public async Task ACaseSharedWithTheUsersAgencyIsReadOnlyToThem()
    {
        var admin = api.CreateClient();
        var (theirs, mine) = await TwoAgencies(admin);
        using var user = await SignedIn(admin, mine);
        var own = await Open(admin, mine);
        var shared = await Open(admin, theirs);
        var seeded = await Seed(shared);

        try
        {
            await using (var context = server.NewContext())
            {
                context.PersonConditionShares.Add(new PersonConditionShare { PersonConditionId = shared.Id, AgencyId = mine });
                await context.SaveChangesAsync();
            }

            var writes = api.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Where(endpoint => endpoint.RoutePattern.RawText is { } pattern && CaseAccess.CaseRouteOf(pattern) is not null)
                .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                    .Where(method => method is not "GET" and not "HEAD")
                    .Select(method => (
                        Method: method,
                        Pattern: endpoint.RoutePattern.RawText!,
                        Multipart: endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes
                            .Contains("multipart/form-data") == true)))
                .ToList();

            Assert.Contains(writes, write => write is { Method: "PUT", Pattern: "/api/person-conditions/{id:int}/status" });
            Assert.Contains(writes, write => write.Pattern.StartsWith("/api/notes/{id:int}", StringComparison.Ordinal));

            foreach (var (method, pattern, multipart) in writes)
            {
                var url = UrlFor(pattern, seeded);
                using var request = new HttpRequestMessage(new HttpMethod(method), url);

                if (multipart)
                {
                    request.Content = new MultipartFormDataContent { { new StringContent("x"), "file", "x.txt" } };
                }
                else if (method is "POST" or "PUT" or "PATCH")
                {
                    request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                }

                using var response = await user.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                Assert.True(
                    response.StatusCode == HttpStatusCode.Forbidden && body.Contains(ReadOnlyTitle, StringComparison.Ordinal),
                    $"{method} {url} answered {(int)response.StatusCode}: {body}");
            }

            var read = (await user.GetFromJsonAsync<PersonConditionResponse>($"/api/person-conditions/{shared.Id}", Json))!;
            Assert.True(read.SharedReadOnly);

            var ownRead = (await user.GetFromJsonAsync<PersonConditionResponse>($"/api/person-conditions/{own.Id}", Json))!;
            Assert.False(ownRead.SharedReadOnly);
        }
        finally
        {
            await using (var context = server.NewContext())
            {
                await context.PersonConditionShares.Where(row => row.PersonConditionId == shared.Id).ExecuteDeleteAsync();
                await context.PersonConditions.Where(row => row.Id == own.Id).ExecuteDeleteAsync();
            }

            await Remove(seeded);
        }
    }

    /// <summary>The ids each case route prefix names, for the case and every record seeded on it.</summary>
    private sealed record Seeded(PersonConditionResponse Report, Dictionary<string, int> Ids, int FormId, int TreatmentId, int VaccineId, int AttachmentFileId, int ExposurePlaceId);

    /// <summary>
    /// The URL of a route pattern: its first parameter, the one its case route
    /// prefix names, is the seeded record's id, and any other is the form's.
    /// </summary>
    private static string UrlFor(string pattern, Seeded seeded)
    {
        var id = seeded.Ids[CaseAccess.CaseRouteOf(pattern)!];
        var first = true;

        return Parameter().Replace(pattern, _ =>
        {
            var value = first ? id : seeded.FormId;
            first = false;

            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        });
    }

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex Parameter();

    /// <summary>Two seeded agencies: the case's, and the user's.</summary>
    private static async Task<(int Theirs, int Mine)> TwoAgencies(HttpClient admin)
    {
        var agencies = (await admin.GetFromJsonAsync<PagedResult<AgencyResponse>>("/api/agencies?pageSize=200", Json))!;

        return (agencies.Items[0].Id, agencies.Items[1].Id);
    }

    /// <summary>
    /// A client signed in as a user who views and edits cases, working for the
    /// agency given and for no other.
    /// </summary>
    private async Task<HttpClient> SignedIn(HttpClient admin, int agencyId)
    {
        var roles = (await admin.GetFromJsonAsync<PagedResult<RoleResponse>>("/api/roles", Json))!;
        var saveRole = new SaveRoleRequest("Jurisdiction editor", [Privileges.ViewCases, Privileges.EditCases, Privileges.RunReports]);
        var role = roles.Items.SingleOrDefault(role => role.Name == saveRole.Name)
            ?? await Created<RoleResponse>(admin, "/api/roles", saveRole);

        using (var savedRole = await admin.PutAsJsonAsync($"/api/roles/{role.Id}", saveRole, Json))
        {
            Assert.Equal(HttpStatusCode.OK, savedRole.StatusCode);
        }

        var users = (await admin.GetFromJsonAsync<PagedResult<UserResponse>>("/api/users", Json))!;
        var request = new SaveUserRequest(UserName, "Jurisdiction User", true, [role.Id], UserPassword, [agencyId]);

        if (users.Items.SingleOrDefault(user => user.UserName == UserName) is { } existing)
        {
            using var saved = await admin.PutAsJsonAsync($"/api/users/{existing.Id}", request, Json);
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        }
        else
        {
            await Created<UserResponse>(admin, "/api/users", request);
        }

        var client = api.CreateAnonymousClient();
        await api.SignIn(client, UserName, UserPassword);

        return client;
    }

    /// <summary>Opens a case on a seeded person and Malaria, in the agency given.</summary>
    private async Task<PersonConditionResponse> Open(HttpClient admin, int agencyId)
    {
        var persons = (await admin.GetFromJsonAsync<PagedResult<PersonResponse>>("/api/persons?pageSize=200", Json))!;
        var conditions = (await admin.GetFromJsonAsync<PagedResult<ConditionResponse>>("/api/conditions?pageSize=200", Json))!;

        using var created = await admin.PostAsJsonAsync(
            "/api/person-conditions",
            new CreatePersonConditionRequest(
                persons.Items[0].Id,
                conditions.Items.Single(condition => condition.Name == "Malaria").Id),
            Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var report = (await created.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!;

        await using var context = server.NewContext();
        await context.PersonConditions
            .Where(row => row.Id == report.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.AgencyId, agencyId));

        return report;
    }

    /// <summary>One of every record a case route names, on the case given.</summary>
    private async Task<Seeded> Seed(PersonConditionResponse report)
    {
        await using var context = server.NewContext();
        var now = DateTimeOffset.UtcNow;
        var unique = Guid.NewGuid().ToString("N")[..8];

        var form = new Form { Name = $"Access form {unique}", ConditionId = report.ConditionId };
        var treatment = new Treatment { Name = $"Access treatment {unique}" };
        var vaccine = new Vaccine { Code = $"A{unique}" };
        var file = new AttachmentFile { Data = [1] };
        var place = new ExposurePlace { Name = $"Access place {unique}" };
        context.AddRange(form, treatment, vaccine, file, place);
        await context.SaveChangesAsync();

        var lab = new Lab { PersonId = report.PersonId, PersonConditionId = report.Id };
        var exposure = new Exposure { PersonConditionId = report.Id, ExposurePlaceId = place.Id };
        var note = new Note { PersonConditionId = report.Id, Text = "Access note", CreatedAt = now };
        var attachment = new Attachment { PersonConditionId = report.Id, AttachmentFileId = file.Id, CreatedAt = now, Filename = "access.txt", ContentType = "text/plain", Category = "Other" };
        var task = new CaseTask { PersonConditionId = report.Id, Name = "Access task", CreatedAt = now };
        var encounter = new Encounter { PersonConditionId = report.Id, EncounterDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = now };
        var personTreatment = new PersonTreatment { PersonConditionId = report.Id, TreatmentId = treatment.Id, CreatedAt = now };
        var personVaccine = new PersonVaccine { PersonConditionId = report.Id, VaccineId = vaccine.Id, CreatedAt = now };
        var assignment = new FormAssignment { PersonConditionId = report.Id, FormId = form.Id, AssignedAt = now };
        context.AddRange(lab, exposure, note, attachment, task, encounter, personTreatment, personVaccine, assignment);
        await context.SaveChangesAsync();

        var labTest = new LabTest { LabId = lab.Id };
        var resistTest = new LabResistTest { LabId = lab.Id };
        var contact = new ExposureContact { ExposureId = exposure.Id, PersonId = report.PersonId };
        var source = new ExposureSource { ExposureId = exposure.Id };
        context.AddRange(labTest, resistTest, contact, source);
        await context.SaveChangesAsync();

        var result = new LabTestResult { LabTestId = labTest.Id };
        context.Add(result);
        await context.SaveChangesAsync();

        var ids = new Dictionary<string, int>
        {
            ["/api/person-conditions/{id:int}"] = report.Id,
            ["/api/cases/{personConditionId:int}"] = report.Id,
            ["/api/notification-queue/{personConditionId:int}"] = report.Id,
            ["/api/cases/encounters/{id:int}"] = encounter.Id,
            ["/api/cases/treatments/{id:int}"] = personTreatment.Id,
            ["/api/cases/vaccines/{id:int}"] = personVaccine.Id,
            ["/api/cases/forms/{assignmentId:int}"] = assignment.Id,
            ["/api/labs/{id:int}"] = lab.Id,
            ["/api/lab-tests/{id:int}"] = labTest.Id,
            ["/api/lab-test-results/{id:int}"] = result.Id,
            ["/api/lab-resist-tests/{id:int}"] = resistTest.Id,
            ["/api/exposures/{id:int}"] = exposure.Id,
            ["/api/exposure-places/{id:int}"] = place.Id,
            ["/api/exposure-contacts/{id:int}"] = contact.Id,
            ["/api/exposure-sources/{id:int}"] = source.Id,
            ["/api/notes/{id:int}"] = note.Id,
            ["/api/attachments/{id:int}"] = attachment.Id,
            ["/api/tasks/{id:int}"] = task.Id,
        };

        return new Seeded(report, ids, form.Id, treatment.Id, vaccine.Id, file.Id, place.Id);
    }

    private async Task Remove(Seeded seeded)
    {
        await using var context = server.NewContext();
        var report = seeded.Report.Id;

        var labs = context.Labs.Where(row => row.PersonConditionId == report).Select(row => row.Id);
        var labTests = context.LabTests.Where(row => labs.Contains(row.LabId)).Select(row => row.Id);
        await context.LabTestResults.Where(row => labTests.Contains(row.LabTestId)).ExecuteDeleteAsync();
        await context.LabTests.Where(row => labs.Contains(row.LabId)).ExecuteDeleteAsync();
        await context.LabResistTests.Where(row => labs.Contains(row.LabId)).ExecuteDeleteAsync();
        await context.Labs.Where(row => row.PersonConditionId == report).ExecuteDeleteAsync();

        var exposures = context.Exposures.Where(row => row.PersonConditionId == report).Select(row => row.Id);
        await context.ExposureContacts.Where(row => exposures.Contains(row.ExposureId)).ExecuteDeleteAsync();
        await context.ExposureSources.Where(row => exposures.Contains(row.ExposureId)).ExecuteDeleteAsync();
        await context.Exposures.Where(row => row.PersonConditionId == report).ExecuteDeleteAsync();
        await context.ExposurePlaces.Where(row => row.Id == seeded.ExposurePlaceId).ExecuteDeleteAsync();

        await context.Notes.Where(row => row.PersonConditionId == report).ExecuteDeleteAsync();
        await context.Attachments.Where(row => row.PersonConditionId == report).ExecuteDeleteAsync();
        await context.AttachmentFiles.Where(row => row.Id == seeded.AttachmentFileId).ExecuteDeleteAsync();
        await context.Tasks.Where(row => row.PersonConditionId == report).ExecuteDeleteAsync();
        await context.Encounters.Where(row => row.PersonConditionId == report).ExecuteDeleteAsync();
        await context.PersonTreatments.Where(row => row.PersonConditionId == report).ExecuteDeleteAsync();
        await context.PersonVaccines.Where(row => row.PersonConditionId == report).ExecuteDeleteAsync();
        await context.FormAssignments.Where(row => row.PersonConditionId == report).ExecuteDeleteAsync();
        await context.Forms.Where(row => row.Id == seeded.FormId).ExecuteDeleteAsync();
        await context.Treatments.Where(row => row.Id == seeded.TreatmentId).ExecuteDeleteAsync();
        await context.Vaccines.Where(row => row.Id == seeded.VaccineId).ExecuteDeleteAsync();
        await context.PersonConditions.Where(row => row.Id == report).ExecuteDeleteAsync();
    }

    private static async Task<T> Created<T>(HttpClient client, string url, object body)
    {
        using var response = await client.PostAsJsonAsync(url, body, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
