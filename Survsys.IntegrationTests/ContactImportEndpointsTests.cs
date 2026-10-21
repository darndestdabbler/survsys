using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The batch contact import against the real database: the rows it accepts
/// saved as new people and contact events under the chosen exposure, the rows
/// it refuses answered with their lines and reasons and written nowhere.
/// </summary>
/// <remarks>
/// Built as <see cref="ContactEventEndpointsTests"/> is: the index case is the
/// seeded report, re-seeded if a report test in this collection has cleared
/// it, and every exposure, contact event and person a test writes is taken out
/// again afterwards.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class ContactImportEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The last name every person these tests import carries, so they can be cleared.</summary>
    private const string ImportedLastName = "Contactimporttest";

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    /// <summary>
    /// Two good rows and two refused — one with no last name, one with a birth
    /// date that is not a date: the good two are saved as two new people, two
    /// contact rows on the exposure and two contact events, and the refused
    /// two come back by line with nothing written for them.
    /// </summary>
    [Fact]
    public async Task GoodRowsAreSavedAsContactEventsAndRefusedRowsAreAnswered()
    {
        var reportId = await EnsureSeededReport();
        var exposureId = await NewExposure(reportId);

        var csv = string.Join("\n",
            "first_name,last_name,birth_date,city,phone",
            $"Ana,{ImportedLastName},1990-04-02,Hartford,860-555-0100",
            ",,1985-01-01,,",
            $"Ben,{ImportedLastName},not-a-date,,",
            $"Cal,{ImportedLastName},2001-12-30,,");

        try
        {
            using var response = await api.CreateClient().PostAsJsonAsync(
                $"/api/person-conditions/{reportId}/contacts/import",
                new ImportContactsRequest(csv, exposureId),
                Json);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var answer = (await response.Content.ReadFromJsonAsync<ContactImportResponse>(Json))!;

            Assert.Equal([2, 5], answer.Accepted.Select(contact => contact.Line).ToList());
            Assert.All(answer.Accepted, contact => Assert.NotNull(contact.PersonConditionId));
            Assert.Equal([3, 4], answer.Rejected.Select(problem => problem.Line).ToList());
            Assert.All(answer.Rejected, problem => Assert.False(string.IsNullOrWhiteSpace(problem.Reason)));

            await using var context = server.NewContext();

            var people = await context.Persons
                .Where(person => person.LastName == ImportedLastName)
                .OrderBy(person => person.FirstName)
                .ToListAsync();
            Assert.Equal(["Ana", "Cal"], people.Select(person => person.FirstName).ToList());
            Assert.Equal("Hartford", (await context.PersonAddresses
                .SingleAsync(address => address.PersonId == people[0].Id)).City);

            var contacts = await context.ExposureContacts
                .Where(contact => contact.ExposureId == exposureId)
                .ToListAsync();
            Assert.Equal(
                people.Select(person => person.Id).Order().ToList(),
                contacts.Select(contact => contact.PersonId).Order().ToList());

            var eventIds = answer.Accepted.Select(contact => contact.PersonConditionId!.Value).ToList();
            Assert.Equal(eventIds.Order().ToList(),
                contacts.Select(contact => contact.PersonConditionId!.Value).Order().ToList());
            Assert.Equal(2, await context.PersonConditions.CountAsync(
                report => eventIds.Contains(report.Id) && report.EventType == EventTypes.Contact));
        }
        finally
        {
            await ClearImportsOf(reportId);
        }
    }

    /// <summary>
    /// An exposure of another report is not one this batch can go under, and
    /// nothing is written.
    /// </summary>
    [Fact]
    public async Task AnExposureOfAnotherReportIsAProblemDetailsAndNothingIsWritten()
    {
        var reportId = await EnsureSeededReport();
        var exposureId = await NewExposure(reportId);
        int? other = null;

        try
        {
            other = await OtherReport(reportId);

            using var refused = await api.CreateClient().PostAsJsonAsync(
                $"/api/person-conditions/{other}/contacts/import",
                new ImportContactsRequest($"last_name,birth_date\n{ImportedLastName},1990-04-02", exposureId),
                Json);

            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Equal("application/problem+json", refused.Content.Headers.ContentType?.MediaType);

            await using var context = server.NewContext();
            Assert.False(await context.Persons.AnyAsync(person => person.LastName == ImportedLastName));
            Assert.False(await context.ExposureContacts.AnyAsync(contact => contact.ExposureId == exposureId));
        }
        finally
        {
            await ClearImportsOf(reportId);

            await using var context = server.NewContext();
            await context.PersonConditions.Where(report => report.Id == other).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task AReportNotOnFileIsNotFound()
    {
        using var response = await api.CreateClient().PostAsJsonAsync(
            "/api/person-conditions/987654/contacts/import",
            new ImportContactsRequest("last_name,birth_date", 1),
            Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<int> NewExposure(int reportId)
    {
        using var response = await api.CreateClient().PostAsJsonAsync(
            $"/api/person-conditions/{reportId}/exposures",
            new CreateExposureRequest(Name: "Church supper"),
            Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<ExposureDetailResponse>(Json))!.Id;
    }

    /// <summary>A second report, on the same person and condition as the seeded one, opened through the API.</summary>
    private async Task<int> OtherReport(int reportId)
    {
        await using var context = server.NewContext();

        var seeded = await context.PersonConditions.SingleAsync(report => report.Id == reportId);

        using var response = await api.CreateClient().PostAsJsonAsync(
            "/api/person-conditions",
            new CreatePersonConditionRequest(seeded.PersonId, seeded.ConditionId),
            Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!.Id;
    }

    /// <summary>
    /// The seeded report's id, re-seeding it if a report test in this
    /// collection has cleared it — the helper <see cref="ExposureEndpointsTests"/>
    /// keeps for the same reason.
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

        report = new PersonCondition
        {
            PersonId = await context.Persons
                .Where(person => person.LastName == "Rodriguez" && person.FirstName == "Maria")
                .Select(person => person.Id)
                .SingleAsync(),
            ConditionId = await context.Conditions
                .Where(condition => condition.Name == "Malaria")
                .Select(condition => condition.Id)
                .SingleAsync(),
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
    /// Takes out everything these tests wrote: the contact rows on the seeded
    /// report's exposures, the contact events they named, the exposures and
    /// the people the import created.
    /// </summary>
    private async Task ClearImportsOf(int reportId)
    {
        await using var context = server.NewContext();

        var exposureIds = await context.Exposures
            .Where(exposure => exposure.PersonConditionId == reportId)
            .Select(exposure => exposure.Id)
            .ToListAsync();

        var eventIds = await context.ExposureContacts
            .Where(contact => exposureIds.Contains(contact.ExposureId) && contact.PersonConditionId != null)
            .Select(contact => contact.PersonConditionId!.Value)
            .ToListAsync();

        await context.ExposureContacts
            .Where(contact => exposureIds.Contains(contact.ExposureId)).ExecuteDeleteAsync();
        await context.PersonConditions
            .Where(report => eventIds.Contains(report.Id)).ExecuteDeleteAsync();
        await context.Exposures
            .Where(exposure => exposureIds.Contains(exposure.Id)).ExecuteDeleteAsync();
        await context.Persons
            .Where(person => person.LastName == ImportedLastName).ExecuteDeleteAsync();
    }
}
