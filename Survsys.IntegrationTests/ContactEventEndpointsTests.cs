using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// Contact events against the real database: a contact added to an exposure
/// as a case of their own — the person on file or created new — and that case
/// promoted to a morbidity report.
/// </summary>
/// <remarks>
/// Built as <see cref="ExposureEndpointsTests"/> is: the index case is
/// <c>Fixtures/person-conditions.sql</c>'s seeded report, re-seeded if a report
/// test in this collection has cleared it, and the existing contact is a
/// person <c>Fixtures/persons.sql</c> seeds. Every exposure, contact event,
/// queue entry and person a test writes is taken out again afterwards, so the
/// report tests' clear of the reports table is never refused by a contact row
/// that names a contact event.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class ContactEventEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The last name every person these tests create carries, so they can be cleared.</summary>
    private const string NewLastName = "Contacteventtest";

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    /// <summary>
    /// A contact added with a person on file and one added as a new person
    /// each open a contact event of their own, with the index case's condition
    /// and agency and a record number of their own, and the exposure lists
    /// both contacts naming those events.
    /// </summary>
    [Fact]
    public async Task AContactAddedOpensAContactEventOfTheirOwn()
    {
        var (reportId, exposureId) = await SeededExposure();
        var client = api.CreateClient();

        try
        {
            var existingPerson = await PersonId("Zimmerman", "Paul");

            var onFile = await Added(client, exposureId,
                new CreateContactEventRequest(PersonId: existingPerson, ContactType: "household"));
            var created = await Added(client, exposureId,
                new CreateContactEventRequest(NewPerson: new CreatePersonRequest(NewLastName, "Ana")));

            await using var context = server.NewContext();

            var index = await context.PersonConditions.SingleAsync(report => report.Id == reportId);
            var newPerson = await context.Persons.SingleAsync(person => person.LastName == NewLastName);

            Assert.Equal(existingPerson, onFile.PersonId);
            Assert.Equal("household", onFile.ContactType);
            Assert.Equal(newPerson.Id, created.PersonId);

            var events = await context.PersonConditions
                .Where(report => report.Id == onFile.PersonConditionId
                    || report.Id == created.PersonConditionId)
                .ToListAsync();

            Assert.Equal(2, events.Count);
            Assert.All(events, contactEvent =>
            {
                Assert.Equal(EventTypes.Contact, contactEvent.EventType);
                Assert.Equal(index.ConditionId, contactEvent.ConditionId);
                Assert.Equal(index.AgencyId, contactEvent.AgencyId);
                Assert.NotEqual(index.RecordNumber, contactEvent.RecordNumber);
            });
            Assert.NotEqual(events[0].RecordNumber, events[1].RecordNumber);
            Assert.Equal(onFile.PersonId,
                events.Single(contactEvent => contactEvent.Id == onFile.PersonConditionId).PersonId);

            var exposure = await client.GetFromJsonAsync<ExposureDetailResponse>(
                $"/api/exposures/{exposureId}", Json);

            Assert.Equal(
                [onFile.PersonConditionId, created.PersonConditionId],
                exposure!.Contacts.Select(contact => contact.PersonConditionId).ToList());
        }
        finally
        {
            await ClearContactEventsOf(reportId);
        }
    }

    [Fact]
    public async Task AContactNamedBothWaysOrNeitherIsAProblemDetails()
    {
        var (reportId, exposureId) = await SeededExposure();
        var client = api.CreateClient();

        try
        {
            using var both = await client.PostAsJsonAsync($"/api/exposures/{exposureId}/contact-events",
                new CreateContactEventRequest(
                    PersonId: await PersonId("Zimmerman", "Paul"),
                    NewPerson: new CreatePersonRequest(NewLastName)),
                Json);
            using var neither = await client.PostAsJsonAsync($"/api/exposures/{exposureId}/contact-events",
                new CreateContactEventRequest(ContactType: "household"), Json);

            Assert.Equal(HttpStatusCode.BadRequest, both.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, neither.StatusCode);
            Assert.Equal("application/problem+json", both.Content.Headers.ContentType?.MediaType);

            await using var context = server.NewContext();
            Assert.False(await context.ExposureContacts.AnyAsync(contact => contact.ExposureId == exposureId));
        }
        finally
        {
            await ClearContactEventsOf(reportId);
        }
    }

    [Fact]
    public async Task AContactAddedToAnExposureNotOnFileIsNotFound()
    {
        using var response = await api.CreateClient().PostAsJsonAsync(
            "/api/exposures/987654/contact-events",
            new CreateContactEventRequest(PersonId: await PersonId("Zimmerman", "Paul")),
            Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The contact event promoted is a morbidity report from then on, read
    /// back as one, and queued for notification, its confirmed status meeting
    /// its condition's print criteria.
    /// </summary>
    [Fact]
    public async Task AContactEventPromotedIsAMorbidityReportAndIsQueued()
    {
        var (reportId, exposureId) = await SeededExposure();
        var client = api.CreateClient();

        try
        {
            var contact = await Added(client, exposureId,
                new CreateContactEventRequest(NewPerson: new CreatePersonRequest(NewLastName, "Ben")));
            var eventId = contact.PersonConditionId!.Value;

            await using (var before = server.NewContext())
            {
                Assert.False(await before.NotificationQueue.AnyAsync(entry => entry.PersonConditionId == eventId));

                // Confirmed, so the seeded print criteria notify it once it is
                // a morbidity report.
                await before.PersonConditions.Where(report => report.Id == eventId)
                    .ExecuteUpdateAsync(set => set.SetProperty(report => report.CaseStatus, CaseStatusRules.ConfirmedCode));
            }

            using var promoted = await client.PostAsync($"/api/person-conditions/{eventId}/promote", null);

            Assert.Equal(HttpStatusCode.OK, promoted.StatusCode);
            Assert.Equal(EventTypes.Morbidity,
                (await promoted.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!.EventType);

            var read = await client.GetFromJsonAsync<PersonConditionResponse>(
                $"/api/person-conditions/{eventId}", Json);
            Assert.Equal(EventTypes.Morbidity, read!.EventType);

            await using var context = server.NewContext();
            Assert.True(await context.NotificationQueue.AnyAsync(entry => entry.PersonConditionId == eventId));
        }
        finally
        {
            await ClearContactEventsOf(reportId);
        }
    }

    [Fact]
    public async Task PromotingAMorbidityReportIsAProblemDetails()
    {
        var reportId = await EnsureSeededReport();

        using var response = await api.CreateClient()
            .PostAsync($"/api/person-conditions/{reportId}/promote", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("only a contact event is promoted", await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PromotingAReportNotOnFileIsNotFound()
    {
        using var response = await api.CreateClient()
            .PostAsync("/api/person-conditions/987654/promote", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<ExposureContactResponse> Added(
        HttpClient client, int exposureId, CreateContactEventRequest request)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/exposures/{exposureId}/contact-events", request, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var contact = (await response.Content.ReadFromJsonAsync<ExposureContactResponse>(Json))!;
        Assert.NotNull(contact.PersonConditionId);

        return contact;
    }

    /// <summary>The seeded report's id and a new exposure recorded against it.</summary>
    private async Task<(int ReportId, int ExposureId)> SeededExposure()
    {
        var reportId = await EnsureSeededReport();

        using var response = await api.CreateClient().PostAsJsonAsync(
            $"/api/person-conditions/{reportId}/exposures",
            new CreateExposureRequest(Name: "Church supper"),
            Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (reportId, (await response.Content.ReadFromJsonAsync<ExposureDetailResponse>(Json))!.Id);
    }

    private async Task<int> PersonId(string lastName, string firstName)
    {
        await using var context = server.NewContext();

        return await context.Persons
            .Where(person => person.LastName == lastName && person.FirstName == firstName)
            .Select(person => person.Id)
            .SingleAsync();
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
            PersonId = await PersonId("Rodriguez", "Maria"),
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
    /// Takes out everything these tests wrote against the report: the contact
    /// rows, then the contact events they named and those events' queue
    /// entries, then the exposures, then the people the tests created.
    /// </summary>
    private async Task ClearContactEventsOf(int reportId)
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
        await context.NotificationQueue
            .Where(entry => eventIds.Contains(entry.PersonConditionId)).ExecuteDeleteAsync();
        await context.PersonConditions
            .Where(report => eventIds.Contains(report.Id)).ExecuteDeleteAsync();
        await context.Exposures
            .Where(exposure => exposureIds.Contains(exposure.Id)).ExecuteDeleteAsync();
        await context.Persons
            .Where(person => person.LastName == NewLastName).ExecuteDeleteAsync();
    }
}
