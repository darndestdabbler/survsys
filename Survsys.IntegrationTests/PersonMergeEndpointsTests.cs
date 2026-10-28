using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The person-merge endpoints against a real database: two matching people
/// paired as duplicates and a dissimilar one not; a merge moving every kind of
/// row the merged person has onto the person kept, leaving a tombstone and the
/// audit behind it; the refusals; and a merge that fails partway leaving both
/// people exactly as they were.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class PersonMergeEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly DateOnly BirthDate = new(1962, 3, 9);

    private const string UserPassword = "person-merge-test-password";

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task TheCandidatesPairTwoMatchingPeopleAndNotADissimilarOne()
    {
        var keep = Mergewell("Hedda", BirthDate);
        var merge = Mergewell("Hedda", BirthDate);
        var other = Mergewell("Olaf", new DateOnly(1990, 1, 1));
        await Add(keep, merge, other);

        try
        {
            var pairs = (await api.CreateClient().GetFromJsonAsync<List<DuplicatePersonPairResponse>>(
                "/api/person-merges/candidates", Json))!;

            var pair = Assert.Single(pairs, pair => pair.First.Id == keep.Id);
            Assert.Equal(merge.Id, pair.Second.Id);
            Assert.Equal(PersonMatchCandidateResponse.Probable, pair.Verdict);
            Assert.DoesNotContain(pairs, pair => pair.First.Id == other.Id || pair.Second.Id == other.Id);
        }
        finally
        {
            await Remove(keep, merge, other);
        }
    }

    [Fact]
    public async Task AMergeMovesEveryRowOntoThePersonKeptAndLeavesATombstone()
    {
        var keep = Mergewell("Hedda", BirthDate);
        keep.Aliases.Add(new PersonAlias { Alias = "Heddy" });
        keep.Races.Add(new PersonRace { Race = "W" });
        keep.Addresses.Add(new PersonAddress { Street = "1 Kept Road", PostalCode = "06101" });
        var merge = Mergewell("Hedda", BirthDate);
        await Add(keep, merge);
        var rows = await Seed(merge.Id);

        try
        {
            using var response = await api.CreateClient().PostAsJsonAsync(
                "/api/person-merges", new PersonMergeRequest(keep.Id, merge.Id), Json);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(keep.Id, (await response.Content.ReadFromJsonAsync<PersonResponse>(Json))!.Id);

            await using var context = server.NewContext();

            Assert.Equal(keep.Id, (await context.PersonConditions.SingleAsync(row => row.Id == rows.CaseId)).PersonId);
            Assert.True(await context.PersonVaccines.AnyAsync(row => row.Id == rows.VaccineId && row.PersonConditionId == rows.CaseId));
            Assert.Equal(keep.Id, (await context.Labs.SingleAsync(row => row.Id == rows.LabId)).PersonId);
            Assert.Equal(keep.Id, (await context.ExposureContacts.SingleAsync(row => row.Id == rows.ContactId)).PersonId);
            Assert.Equal(keep.Id, (await context.ExposureSources.SingleAsync(row => row.Id == rows.SourceId)).PersonId);
            Assert.Equal(keep.Id, (await context.ParentGuardians.SingleAsync(row => row.Id == rows.GuardianId)).ChildId);
            Assert.Equal(keep.Id, (await context.PersonIdentifiers.SingleAsync(row => row.Id == rows.IdentifierId)).PersonId);
            Assert.Equal(keep.Id, (await context.PersonNameHistory.SingleAsync(row => row.Id == rows.FormerNameId)).PersonId);
            Assert.Equal(keep.Id, (await context.PersonTelephones.SingleAsync(row => row.Id == rows.TelephoneId)).PersonId);
            Assert.Equal(keep.Id, (await context.PersonEmails.SingleAsync(row => row.Id == rows.EmailId)).PersonId);

            Assert.Equal(
                ["Heddy", "Hedwig"],
                await context.PersonAliases.Where(row => row.PersonId == keep.Id).Select(row => row.Alias).OrderBy(alias => alias).ToListAsync());
            Assert.Equal(
                ["A", "W"],
                await context.PersonRaces.Where(row => row.PersonId == keep.Id).Select(row => row.Race).OrderBy(race => race).ToListAsync());

            var moved = await context.PersonAddresses.SingleAsync(row => row.Id == rows.AddressId);
            Assert.Equal(keep.Id, moved.PersonId);
            Assert.NotNull(moved.Ending);
            Assert.Single(await context.PersonAddresses.Where(row => row.PersonId == keep.Id && row.Ending == null).ToListAsync());

            Assert.Empty(await context.PersonAliases.Where(row => row.PersonId == merge.Id).ToListAsync());
            Assert.Empty(await context.PersonRaces.Where(row => row.PersonId == merge.Id).ToListAsync());

            var tombstone = await context.Persons.SingleAsync(person => person.Id == merge.Id);
            Assert.Equal(keep.Id, tombstone.MergedIntoPersonId);
            Assert.NotNull(tombstone.MergedAt);

            Assert.Contains("Update", await AuditOperations(context, "person_conditions", rows.CaseId));
            Assert.Contains("Update", await AuditOperations(context, "labs", rows.LabId));
            Assert.Contains("Update", await AuditOperations(context, "persons", merge.Id));

            var found = (await api.CreateClient().GetFromJsonAsync<PagedResult<PersonResponse>>(
                "/api/persons?lastName=Mergewell", Json))!;
            Assert.Equal([keep.Id], found.Items.Select(person => person.Id));
        }
        finally
        {
            await Remove(keep, merge);
        }
    }

    [Fact]
    public async Task ASelfMergeOrATombstoneIsAConflict()
    {
        var keep = Mergewell("Hedda", BirthDate);
        var merge = Mergewell("Hedda", BirthDate);
        var tombstone = Mergewell("Hedda", BirthDate);
        await Add(keep, merge);
        tombstone.MergedIntoPersonId = keep.Id;
        tombstone.MergedAt = DateTime.UtcNow;
        await Add(tombstone);

        try
        {
            var client = api.CreateClient();

            using var self = await client.PostAsJsonAsync("/api/person-merges", new PersonMergeRequest(keep.Id, keep.Id), Json);
            Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);

            using var again = await client.PostAsJsonAsync("/api/person-merges", new PersonMergeRequest(merge.Id, tombstone.Id), Json);
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

            using var missing = await client.PostAsJsonAsync("/api/person-merges", new PersonMergeRequest(keep.Id, int.MaxValue), Json);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
        finally
        {
            await Remove(keep, merge, tombstone);
        }
    }

    [Fact]
    public async Task ACallerWithoutMergePersonsIsAnswered403()
    {
        // A user of its own: PersonConditionEndpointsTests creates "case-editor"
        // with another password, and whichever class ran first would decide it.
        using var editor = await SignedIn(
            "Merge case editor", "merge-case-editor", [Privileges.ViewCases, Privileges.EditCases, Privileges.AllJurisdictions]);

        using var candidates = await editor.GetAsync("/api/person-merges/candidates");
        Assert.Equal(HttpStatusCode.Forbidden, candidates.StatusCode);

        using var person = await editor.GetAsync("/api/person-merges/persons/1");
        Assert.Equal(HttpStatusCode.Forbidden, person.StatusCode);

        using var merged = await editor.PostAsJsonAsync("/api/person-merges", new PersonMergeRequest(1, 2), Json);
        Assert.Equal(HttpStatusCode.Forbidden, merged.StatusCode);
    }

    [Fact]
    public async Task ACallerHoldingOnlyMergePersonsCanCompareAndMerge()
    {
        var keep = Mergewell("Hedda", BirthDate);
        var merge = Mergewell("Hedda", BirthDate);
        await Add(keep, merge);

        try
        {
            using var merger = await SignedIn("Person merger", "person-merger", [Privileges.MergePersons]);

            var record = await merger.GetFromJsonAsync<PersonDetailResponse>(
                $"/api/person-merges/persons/{merge.Id}", Json);
            Assert.Equal("Mergewell", record!.LastName);

            using var missing = await merger.GetAsync($"/api/person-merges/persons/{int.MaxValue}");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

            using var merged = await merger.PostAsJsonAsync(
                "/api/person-merges", new PersonMergeRequest(keep.Id, merge.Id), Json);
            Assert.Equal(HttpStatusCode.OK, merged.StatusCode);
        }
        finally
        {
            await Remove(keep, merge);
        }
    }

    [Fact]
    public async Task AMergeThatFailsPartwayLeavesBothPeopleUnchanged()
    {
        var keep = Mergewell("Hedda", BirthDate);
        keep.Addresses.Add(new PersonAddress { Street = "1 Kept Road", PostalCode = "06101" });
        var merge = Mergewell("Hedda", BirthDate);
        await Add(keep, merge);
        var rows = await Seed(merge.Id);

        // The rows are moved by the first save inside the merge; the tombstone
        // is marked by the second, which this throws on.
        using var failing = api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<SurvsysDbContext>(options => options.AddInterceptors(new FailOnTombstone()))));

        // Signed in before the count, because a sign-in is itself audited.
        using var client = failing.CreateClient();
        var auditsBefore = await AuditCount();

        try
        {
            using var response = await client.PostAsJsonAsync(
                "/api/person-merges", new PersonMergeRequest(keep.Id, merge.Id), Json);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

            await using var context = server.NewContext();

            Assert.Null((await context.Persons.SingleAsync(person => person.Id == merge.Id)).MergedIntoPersonId);
            Assert.Equal(merge.Id, (await context.PersonConditions.SingleAsync(row => row.Id == rows.CaseId)).PersonId);
            Assert.Equal(merge.Id, (await context.Labs.SingleAsync(row => row.Id == rows.LabId)).PersonId);
            Assert.Equal(merge.Id, (await context.ExposureContacts.SingleAsync(row => row.Id == rows.ContactId)).PersonId);
            Assert.Equal(merge.Id, (await context.ExposureSources.SingleAsync(row => row.Id == rows.SourceId)).PersonId);
            Assert.Equal(merge.Id, (await context.ParentGuardians.SingleAsync(row => row.Id == rows.GuardianId)).ChildId);
            Assert.Equal(merge.Id, (await context.PersonIdentifiers.SingleAsync(row => row.Id == rows.IdentifierId)).PersonId);
            Assert.Equal(merge.Id, (await context.PersonNameHistory.SingleAsync(row => row.Id == rows.FormerNameId)).PersonId);
            Assert.Equal(merge.Id, (await context.PersonTelephones.SingleAsync(row => row.Id == rows.TelephoneId)).PersonId);
            Assert.Equal(merge.Id, (await context.PersonEmails.SingleAsync(row => row.Id == rows.EmailId)).PersonId);
            Assert.True(await context.PersonAliases.AnyAsync(row => row.PersonId == merge.Id && row.Alias == "Hedwig"));
            Assert.True(await context.PersonRaces.AnyAsync(row => row.PersonId == merge.Id && row.Race == "A"));

            var address = await context.PersonAddresses.SingleAsync(row => row.Id == rows.AddressId);
            Assert.Equal(merge.Id, address.PersonId);
            Assert.Null(address.Ending);

            Assert.Equal(auditsBefore, await AuditCount());
        }
        finally
        {
            await Remove(keep, merge);
        }
    }

    /// <summary>Throws on the save that would mark a person merged away.</summary>
    private sealed class FailOnTombstone : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            eventData.Context!.ChangeTracker.Entries<Person>().Any(entry => entry.Entity.MergedIntoPersonId is not null)
                ? throw new InvalidOperationException("The merge was made to fail partway.")
                : base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>The ids of the rows <see cref="Seed"/> gave the person merged away.</summary>
    private sealed record SeededRows(
        int CaseId, int VaccineId, int LabId, int ContactId, int SourceId, int GuardianId,
        int IdentifierId, int FormerNameId, int TelephoneId, int EmailId, int AddressId);

    /// <summary>One row of every kind a merge moves, for the person given.</summary>
    private async Task<SeededRows> Seed(int personId)
    {
        await using var context = server.NewContext();

        var conditionId = await context.Conditions.OrderBy(condition => condition.Id).Select(condition => condition.Id).FirstAsync();
        var vaccine = await context.Vaccines.FirstOrDefaultAsync(row => row.Code == "MERGE-TEST")
            ?? context.Vaccines.Add(new Vaccine { Code = "MERGE-TEST" }).Entity;

        var report = new PersonCondition
        {
            PersonId = personId,
            ConditionId = conditionId,
            RecordNumber = $"M71{personId:D9}",
            CreatedAt = DateTimeOffset.Now,
        };
        context.PersonConditions.Add(report);
        await context.SaveChangesAsync();

        var record = new PersonVaccine { PersonConditionId = report.Id, VaccineId = vaccine.Id, CreatedAt = DateTimeOffset.Now };
        var lab = new Lab { PersonId = personId, PersonConditionId = report.Id };
        var exposure = new Exposure { Name = "Merge test exposure" };
        var guardian = new ParentGuardian { ChildId = personId, Name = "Guardian" };
        var identifier = new PersonIdentifier { PersonId = personId, Value = "MERGE-MRN-1", IdentifierType = "MR" };
        var formerName = new PersonNameHistory { PersonId = personId, LastName = "Oldname", ChangedOn = new DateOnly(2020, 1, 1) };
        var telephone = new PersonTelephone { PersonId = personId, AreaCode = "860", PhoneNumber = "5550101" };
        var email = new PersonEmail { PersonId = personId, EmailAddress = "hedda@example.test" };
        var address = new PersonAddress { PersonId = personId, Street = "2 Merged Lane", PostalCode = "06102" };
        context.AddRange(record, lab, exposure, guardian, identifier, formerName, telephone, email, address);
        context.PersonAliases.Add(new PersonAlias { PersonId = personId, Alias = "Heddy" });
        context.PersonAliases.Add(new PersonAlias { PersonId = personId, Alias = "Hedwig" });
        context.PersonRaces.Add(new PersonRace { PersonId = personId, Race = "W" });
        context.PersonRaces.Add(new PersonRace { PersonId = personId, Race = "A" });
        await context.SaveChangesAsync();

        var contact = new ExposureContact { ExposureId = exposure.Id, PersonId = personId };
        var source = new ExposureSource { ExposureId = exposure.Id, PersonId = personId };
        context.AddRange(contact, source);
        await context.SaveChangesAsync();

        return new SeededRows(
            report.Id, record.Id, lab.Id, contact.Id, source.Id, guardian.Id,
            identifier.Id, formerName.Id, telephone.Id, email.Id, address.Id);
    }

    private static Person Mergewell(string firstName, DateOnly birthDate) => new()
    {
        LastName = "Mergewell", FirstName = firstName, BirthDate = birthDate, BirthGender = "F",
    };

    private async Task Add(params Person[] persons)
    {
        await using var context = server.NewContext();
        context.Persons.AddRange(persons);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Takes the people a test added out again with everything hung on them,
    /// tombstones first, so the seeded count other tests assert stays what the
    /// fixture wrote.
    /// </summary>
    private async Task Remove(params Person[] persons)
    {
        var ids = persons.Select(person => person.Id).ToList();

        await using var context = server.NewContext();
        var reports = context.PersonConditions.Where(row => ids.Contains(row.PersonId)).Select(row => row.Id);

        await context.ExposureContacts.Where(row => ids.Contains(row.PersonId)).ExecuteDeleteAsync();
        await context.ExposureSources.Where(row => row.PersonId != null && ids.Contains(row.PersonId.Value)).ExecuteDeleteAsync();
        await context.Exposures.Where(row => row.Name == "Merge test exposure").ExecuteDeleteAsync();
        await context.Labs.Where(row => ids.Contains(row.PersonId)).ExecuteDeleteAsync();
        await context.PersonVaccines.Where(row => reports.Contains(row.PersonConditionId)).ExecuteDeleteAsync();
        await context.PersonConditions.Where(row => ids.Contains(row.PersonId)).ExecuteDeleteAsync();
        await context.ParentGuardians.Where(row => ids.Contains(row.ChildId)).ExecuteDeleteAsync();
        await context.Persons.Where(person => ids.Contains(person.Id) && person.MergedIntoPersonId != null).ExecuteDeleteAsync();
        await context.Persons.Where(person => ids.Contains(person.Id)).ExecuteDeleteAsync();
    }

    private async Task<int> AuditCount()
    {
        await using var context = server.NewContext();

        return await context.AuditEntries.CountAsync();
    }

    private static Task<List<string>> AuditOperations(SurvsysDbContext context, string table, int key) =>
        context.AuditEntries
            .Where(entry => entry.TableName == table && entry.KeyValues == key.ToString())
            .OrderBy(entry => entry.Id)
            .Select(entry => entry.Operation)
            .ToListAsync();

    /// <summary>
    /// A client signed in as a user whose one role holds exactly these
    /// privileges, the role and the user created by the administrator over the
    /// wire and found again on a later run rather than created twice.
    /// </summary>
    private async Task<HttpClient> SignedIn(string roleName, string userName, string[] privileges)
    {
        var admin = api.CreateClient();
        var roles = (await admin.GetFromJsonAsync<PagedResult<RoleResponse>>("/api/roles", Json))!;
        var role = roles.Items.SingleOrDefault(role => role.Name == roleName)
            ?? await Created<RoleResponse>(admin, "/api/roles", new SaveRoleRequest(roleName, privileges));

        var users = (await admin.GetFromJsonAsync<PagedResult<UserResponse>>("/api/users", Json))!;
        if (users.Items.All(user => user.UserName != userName))
        {
            await Created<UserResponse>(admin, "/api/users",
                new SaveUserRequest(userName, roleName, true, [role.Id], UserPassword));
        }

        var client = api.CreateAnonymousClient();
        await api.SignIn(client, userName, UserPassword);

        return client;
    }

    private static async Task<T> Created<T>(HttpClient client, string url, object body)
    {
        using var response = await client.PostAsJsonAsync(url, body, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
