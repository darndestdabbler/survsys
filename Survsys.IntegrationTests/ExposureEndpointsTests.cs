using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The exposure slice's corrections and removals, against the real database: an
/// exposure, its place, its contacts and its sources edited and deleted through
/// their own routes, each change audited, and a delete refused while rows still
/// depend on it.
/// </summary>
/// <remarks>
/// The sibling of <see cref="LabEndpointsTests"/>, and built the same way: the
/// report is <c>Fixtures/person-conditions.sql</c>'s, re-seeded if a report
/// test in this collection has cleared it, and every exposure this test records
/// is cleared afterwards so a report with exposures against it never refuses
/// those tests' delete. The contacts and sources name people
/// <c>Fixtures/persons.sql</c> seeds. The suite does not skip when SQL Server
/// is absent; <see cref="SqlServerFixture"/> fails it loudly instead.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class ExposureEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task AnExposureEditChangesItsColumnsAndIsAudited()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var exposureId = (await Record(client, reportId,
                new CreateExposureRequest(Name: "Church supper"))).Id;

            using var put = await client.PutAsJsonAsync($"/api/exposures/{exposureId}",
                new UpdateExposureRequest(
                    Name: "Parish hall potluck",
                    Description: "Corrected name",
                    ExposureStart: new DateOnly(2026, 3, 8),
                    ExposureEnd: new DateOnly(2026, 3, 10)),
                Json);

            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var updated = (await put.Content.ReadFromJsonAsync<ExposureResponse>(Json))!;
            Assert.Equal("Parish hall potluck", updated.Name);
            Assert.Equal("Corrected name", updated.Description);
            Assert.Equal(new DateOnly(2026, 3, 10), updated.ExposureEnd);

            using var read = await client.GetAsync($"/api/exposures/{exposureId}");
            read.EnsureSuccessStatusCode();
            var round = (await read.Content.ReadFromJsonAsync<ExposureDetailResponse>(Json))!;
            Assert.Equal("Parish hall potluck", round.Name);

            Assert.Contains("Update", await AuditOperations("exposures", exposureId));
        }
        finally
        {
            await ClearExposuresOf(reportId);
        }
    }

    [Fact]
    public async Task AnExposureEditRefusesAnEndBeforeItsStart()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var exposureId = (await Record(client, reportId, new CreateExposureRequest(Name: "Trip"))).Id;

            using var put = await client.PutAsJsonAsync($"/api/exposures/{exposureId}",
                new UpdateExposureRequest(
                    ExposureStart: new DateOnly(2026, 3, 10), ExposureEnd: new DateOnly(2026, 3, 8)),
                Json);

            Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
            Assert.Equal("application/problem+json", put.Content.Headers.ContentType?.MediaType);
            using var problem = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
            Assert.Contains("is before exposureStart",
                problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            await ClearExposuresOf(reportId);
        }
    }

    [Fact]
    public async Task APlaceEditChangesItAndIsAudited()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var detail = await Record(client, reportId, new CreateExposureRequest(
                Name: "Church supper",
                Place: new ExposurePlaceRequest(Name: "St Mark's", City: "Hartford", State: "CT")));

            var placeId = detail.Place!.Id;

            using var put = await client.PutAsJsonAsync($"/api/exposure-places/{placeId}",
                new ExposurePlaceRequest(
                    ExposurePlaceTypeId: 3, Name: "St Mark's parish hall",
                    Street: "12 Chapel St", City: "Hartford", State: "CT"),
                Json);

            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var updated = (await put.Content.ReadFromJsonAsync<ExposurePlaceResponse>(Json))!;
            Assert.Equal("St Mark's parish hall", updated.Name);
            Assert.Equal("12 Chapel St", updated.Street);
            Assert.Equal(3, updated.ExposurePlaceTypeId);

            Assert.Contains("Update", await AuditOperations("exposure_places", placeId));
        }
        finally
        {
            await ClearExposuresOf(reportId);
        }
    }

    [Fact]
    public async Task AContactEditSetsItsDispositionAndIsAudited()
    {
        var reportId = await EnsureSeededReport();
        var personId = await PersonId("Nguyen", "An");
        var client = api.CreateClient();

        try
        {
            var detail = await Record(client, reportId, new CreateExposureRequest(
                Name: "Church supper",
                Contacts: [new ExposureContactRequest(PersonId: personId, ContactType: "household")]));

            var contactId = detail.Contacts.Single().Id;

            using var put = await client.PutAsJsonAsync($"/api/exposure-contacts/{contactId}",
                new ExposureContactRequest(
                    PersonId: personId,
                    ExposureStart: new DateOnly(2026, 3, 8),
                    ExposureEnd: new DateOnly(2026, 3, 9),
                    DispositionId: 4,
                    ContactType: "household",
                    DispositionDate: new DateOnly(2026, 3, 12)),
                Json);

            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var updated = (await put.Content.ReadFromJsonAsync<ExposureContactResponse>(Json))!;
            Assert.Equal(personId, updated.PersonId);
            Assert.Equal(4, updated.DispositionId);
            Assert.Equal(new DateOnly(2026, 3, 12), updated.DispositionDate);

            Assert.Contains("Update", await AuditOperations("exposure_contacts", contactId));
        }
        finally
        {
            await ClearExposuresOf(reportId);
        }
    }

    [Fact]
    public async Task AContactEditRefusesAPersonNotOnFile()
    {
        var reportId = await EnsureSeededReport();
        var personId = await PersonId("Nguyen", "An");
        var client = api.CreateClient();

        try
        {
            var detail = await Record(client, reportId, new CreateExposureRequest(
                Name: "Church supper",
                Contacts: [new ExposureContactRequest(PersonId: personId)]));

            var contactId = detail.Contacts.Single().Id;

            using var put = await client.PutAsJsonAsync($"/api/exposure-contacts/{contactId}",
                new ExposureContactRequest(PersonId: 987654),
                Json);

            Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
            using var problem = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
            Assert.Contains("personId 987654 is not a person on file",
                problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            await ClearExposuresOf(reportId);
        }
    }

    [Fact]
    public async Task ASourceEditChangesItAndIsAudited()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var detail = await Record(client, reportId, new CreateExposureRequest(
                Name: "Church supper",
                Sources: [new ExposureSourceRequest(SourceName: "Well", SourceType: "environmental")]));

            var sourceId = detail.Sources.Single().Id;

            using var put = await client.PutAsJsonAsync($"/api/exposure-sources/{sourceId}",
                new ExposureSourceRequest(SourceName: "Well water", SourceType: "environmental"),
                Json);

            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var updated = (await put.Content.ReadFromJsonAsync<ExposureSourceResponse>(Json))!;
            Assert.Equal("Well water", updated.SourceName);

            Assert.Contains("Update", await AuditOperations("exposure_sources", sourceId));
        }
        finally
        {
            await ClearExposuresOf(reportId);
        }
    }

    [Fact]
    public async Task LeafContactAndSourceDeleteAndAreAudited()
    {
        var reportId = await EnsureSeededReport();
        var personId = await PersonId("Okafor", "Chidi");
        var client = api.CreateClient();

        try
        {
            var detail = await Record(client, reportId, new CreateExposureRequest(
                Name: "Church supper",
                Contacts: [new ExposureContactRequest(PersonId: personId)],
                Sources: [new ExposureSourceRequest(SourceName: "Well water")]));

            var contactId = detail.Contacts.Single().Id;
            var sourceId = detail.Sources.Single().Id;

            using var deleteContact = await client.DeleteAsync($"/api/exposure-contacts/{contactId}");
            Assert.Equal(HttpStatusCode.NoContent, deleteContact.StatusCode);

            using var deleteSource = await client.DeleteAsync($"/api/exposure-sources/{sourceId}");
            Assert.Equal(HttpStatusCode.NoContent, deleteSource.StatusCode);

            using var read = await client.GetAsync($"/api/exposures/{detail.Id}");
            read.EnsureSuccessStatusCode();
            var round = (await read.Content.ReadFromJsonAsync<ExposureDetailResponse>(Json))!;
            Assert.Empty(round.Contacts);
            Assert.Empty(round.Sources);

            Assert.Contains("Delete", await AuditOperations("exposure_contacts", contactId));
            Assert.Contains("Delete", await AuditOperations("exposure_sources", sourceId));
        }
        finally
        {
            await ClearExposuresOf(reportId);
        }
    }

    [Fact]
    public async Task AnExposureWithNothingLeftDeletesAndIsAudited()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        var exposureId = (await Record(client, reportId, new CreateExposureRequest(Name: "Church supper"))).Id;

        using var delete = await client.DeleteAsync($"/api/exposures/{exposureId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        using var readGone = await client.GetAsync($"/api/exposures/{exposureId}");
        Assert.Equal(HttpStatusCode.NotFound, readGone.StatusCode);

        Assert.Contains("Delete", await AuditOperations("exposures", exposureId));
    }

    [Fact]
    public async Task ADeleteIsRefusedWhileRowsDependOnIt()
    {
        var reportId = await EnsureSeededReport();
        var personId = await PersonId("Okafor", "Chidi");
        var client = api.CreateClient();

        try
        {
            var detail = await Record(client, reportId, new CreateExposureRequest(
                Name: "Church supper",
                Place: new ExposurePlaceRequest(Name: "St Mark's"),
                Contacts: [new ExposureContactRequest(PersonId: personId)]));

            var exposureId = detail.Id;
            var placeId = detail.Place!.Id;

            // The exposure still has a contact against it.
            using var deleteExposure = await client.DeleteAsync($"/api/exposures/{exposureId}");
            Assert.Equal(HttpStatusCode.Conflict, deleteExposure.StatusCode);
            Assert.Equal("application/problem+json", deleteExposure.Content.Headers.ContentType?.MediaType);
            using (var problem = JsonDocument.Parse(await deleteExposure.Content.ReadAsStringAsync()))
            {
                Assert.Contains("still has 1 contact",
                    problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
            }

            // The place is still pointed at by the exposure.
            using var deletePlace = await client.DeleteAsync($"/api/exposure-places/{placeId}");
            Assert.Equal(HttpStatusCode.Conflict, deletePlace.StatusCode);
            using (var problem = JsonDocument.Parse(await deletePlace.Content.ReadAsStringAsync()))
            {
                Assert.Contains("pointed at by 1 exposure",
                    problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
            }

            // Both refused, so both are still there.
            using var read = await client.GetAsync($"/api/exposures/{exposureId}");
            read.EnsureSuccessStatusCode();
        }
        finally
        {
            await ClearExposuresOf(reportId);
        }
    }

    /// <summary>
    /// A place is removed once no exposure points at it: the exposure is
    /// pointed at no place through its edit, and the now-unreferenced place is
    /// then deleted with a 204 and the delete audited. This is how the place
    /// delete route succeeds, the dependent check having refused it while the
    /// exposure still pointed at it.
    /// </summary>
    [Fact]
    public async Task AnExposurePlaceIsDetachedThenDeletedAndAudited()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var detail = await Record(client, reportId, new CreateExposureRequest(
                Name: "Church supper",
                Place: new ExposurePlaceRequest(Name: "St Mark's")));

            var exposureId = detail.Id;
            var placeId = detail.Place!.Id;

            // While the exposure points at it, the place cannot be removed.
            using (var refused = await client.DeleteAsync($"/api/exposure-places/{placeId}"))
            {
                Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            }

            // Point the exposure at no place, and the place is then removable.
            using (var detach = await client.PutAsJsonAsync($"/api/exposures/{exposureId}",
                new UpdateExposureRequest(Name: "Church supper", ExposurePlaceId: null), Json))
            {
                Assert.Equal(HttpStatusCode.OK, detach.StatusCode);
            }

            using (var read = await client.GetAsync($"/api/exposures/{exposureId}"))
            {
                read.EnsureSuccessStatusCode();
                var round = (await read.Content.ReadFromJsonAsync<ExposureDetailResponse>(Json))!;
                Assert.Null(round.Place);
            }

            using var delete = await client.DeleteAsync($"/api/exposure-places/{placeId}");
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

            await using (var context = server.NewContext())
            {
                Assert.False(await context.ExposurePlaces.AnyAsync(place => place.Id == placeId));
            }

            Assert.Contains("Delete", await AuditOperations("exposure_places", placeId));
        }
        finally
        {
            await ClearExposuresOf(reportId);
        }
    }

    [Fact]
    public async Task AnEditOrDeleteOfARowNobodyHasIsA404()
    {
        var client = api.CreateClient();

        using var put = await client.PutAsJsonAsync("/api/exposures/987654",
            new UpdateExposureRequest(Name: "Nothing"), Json);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);

        using var delete = await client.DeleteAsync("/api/exposure-sources/987654");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    /// <summary>Records an exposure that is expected to succeed, and answers with it.</summary>
    private async Task<ExposureDetailResponse> Record(
        HttpClient client, int reportId, CreateExposureRequest request)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/person-conditions/{reportId}/exposures", request, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<ExposureDetailResponse>(Json))!;
    }

    /// <summary>
    /// The operations audited against a table for one single-column key, in the
    /// order they were written, read back through a fresh context.
    /// </summary>
    private async Task<List<string>> AuditOperations(string table, int key)
    {
        await using var context = server.NewContext();

        return await context.Set<AuditEntry>()
            .Where(entry => entry.TableName == table && entry.KeyValues == key.ToString())
            .OrderBy(entry => entry.Id)
            .Select(entry => entry.Operation)
            .ToListAsync();
    }

    /// <summary>A seeded person's id, by the name <c>Fixtures/persons.sql</c> writes.</summary>
    private async Task<int> PersonId(string lastName, string firstName)
    {
        await using var context = server.NewContext();

        return await context.Persons
            .Where(person => person.LastName == lastName && person.FirstName == firstName)
            .Select(person => person.Id)
            .SingleAsync();
    }

    /// <summary>
    /// The seeded report's id, re-seeding the report if a report test in this
    /// collection has cleared it since the fixture was built — the helper
    /// <see cref="LabEndpointsTests"/> keeps for the same reason.
    /// </summary>
    private async Task<int> EnsureSeededReport()
    {
        await using var context = server.NewContext();

        var report = await context.PersonConditions.SingleOrDefaultAsync(
            report => report.RecordNumber == SqlServerFixture.SeededReportRecordNumber);

        if (report is not null)
        {
            return report.Id;
        }

        var personId = await context.Persons
            .Where(person => person.LastName == "Rodriguez" && person.FirstName == "Maria")
            .Select(person => person.Id)
            .SingleAsync();

        var conditionId = await context.Conditions
            .Where(condition => condition.Name == "Malaria")
            .Select(condition => condition.Id)
            .SingleAsync();

        report = new PersonCondition
        {
            PersonId = personId,
            ConditionId = conditionId,
            RecordNumber = SqlServerFixture.SeededReportRecordNumber,
            FirstReportedPhDate = new DateOnly(2026, 3, 12),
            EventOnsetDate = new DateOnly(2026, 3, 8),
            CreatedAt = DateTimeOffset.Now,
        };

        context.PersonConditions.Add(report);
        await context.SaveChangesAsync();

        return report.Id;
    }

    /// <summary>
    /// Takes every exposure this test recorded against the report out of the
    /// database — the contacts and sources first, then the exposures, then the
    /// places they pointed at — and leaves the report, so a report test's clear
    /// is not refused by an exposure still scoped to it.
    /// </summary>
    private async Task ClearExposuresOf(int reportId)
    {
        await using var context = server.NewContext();

        var exposures = await context.Exposures
            .Where(exposure => exposure.PersonConditionId == reportId)
            .Select(exposure => new { exposure.Id, exposure.ExposurePlaceId })
            .ToListAsync();

        if (exposures.Count == 0)
        {
            return;
        }

        var exposureIds = exposures.Select(exposure => exposure.Id).ToList();

        await context.ExposureContacts
            .Where(contact => exposureIds.Contains(contact.ExposureId)).ExecuteDeleteAsync();
        await context.ExposureSources
            .Where(source => exposureIds.Contains(source.ExposureId)).ExecuteDeleteAsync();
        await context.Exposures
            .Where(exposure => exposureIds.Contains(exposure.Id)).ExecuteDeleteAsync();

        var placeIds = exposures
            .Where(exposure => exposure.ExposurePlaceId is not null)
            .Select(exposure => exposure.ExposurePlaceId!.Value)
            .Distinct()
            .ToList();

        if (placeIds.Count > 0)
        {
            await context.ExposurePlaces
                .Where(place => placeIds.Contains(place.Id)).ExecuteDeleteAsync();
        }
    }
}
