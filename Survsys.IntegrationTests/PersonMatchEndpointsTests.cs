using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The person-match endpoints against a real database: the verdict on the
/// legacy match cases <c>EMSA_PERSON_MATCHING.md</c> sets out — a score at the
/// ceiling, a score in the fuzzy band, that band settled up by a shared
/// medical record number and down by a missing date of birth, and a score
/// below the floor — the thresholds taken from configuration, and a staged
/// message's patient matched to the person on file.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class PersonMatchEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly DateOnly BirthDate = new(1931, 7, 4);

    private static readonly PersonMatchRequest Ingrid = new(
        "Matchwell", "Ingrid", BirthDate, "F", ["MRN-77"], "12 Elm Street", "06101");

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task TheLegacyMatchCasesGetTheLegacyVerdicts()
    {
        var everything = Matchwell("Ingrid", BirthDate, "F", address: true);
        var probable = Matchwell("Ingrid", BirthDate, "F");
        var sharedIdentifier = Matchwell("Ingrid", BirthDate, "F", identifier: "mrn-77");
        var noBirthDate = Matchwell("Ingrid", null, "F", address: true);
        var lastNameOnly = Matchwell("Bjorn", new DateOnly(1950, 1, 1), "M");

        await Add(everything, probable, sharedIdentifier, noBirthDate, lastNameOnly);

        try
        {
            var candidates = await Match(api.CreateClient(), Ingrid);
            var verdicts = candidates.ToDictionary(candidate => candidate.Person.Id);

            Assert.Equal(everything.Id, candidates[0].Person.Id);
            Assert.Equal(100.0, verdicts[everything.Id].Score);
            Assert.Equal(PersonMatchCandidateResponse.Exact, verdicts[everything.Id].Verdict);

            Assert.Equal(85.0, verdicts[probable.Id].Score);
            Assert.Equal(PersonMatchCandidateResponse.Probable, verdicts[probable.Id].Verdict);

            Assert.Equal(PersonMatchCandidateResponse.Exact, verdicts[sharedIdentifier.Id].Verdict);
            Assert.Contains("Shares identifier mrn-77", verdicts[sharedIdentifier.Id].Reasons);

            Assert.Equal(70.0, verdicts[noBirthDate.Id].Score);
            Assert.Equal(PersonMatchCandidateResponse.None, verdicts[noBirthDate.Id].Verdict);

            Assert.Equal(30.0, verdicts[lastNameOnly.Id].Score);
            Assert.Equal(PersonMatchCandidateResponse.None, verdicts[lastNameOnly.Id].Verdict);
        }
        finally
        {
            await Remove(everything, probable, sharedIdentifier, noBirthDate, lastNameOnly);
        }
    }

    [Fact]
    public async Task TheThresholdsAreReadFromConfiguration()
    {
        var probable = Matchwell("Ingrid", BirthDate, "F");
        await Add(probable);

        Environment.SetEnvironmentVariable("PersonMatching__Exact", "85");

        try
        {
            using var configured = new SurvsysApi(server.ConnectionString);

            var candidates = await Match(
                configured.CreateClient(), Ingrid with { PersonIds = [probable.Id] });

            Assert.Equal(PersonMatchCandidateResponse.Exact, Assert.Single(candidates).Verdict);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PersonMatching__Exact", null);
            await Remove(probable);
        }
    }

    [Fact]
    public async Task AMatchWithNothingToFindCandidatesByIsRefused()
    {
        using var response = await api.CreateClient().PostAsJsonAsync(
            "/api/persons/match", new PersonMatchRequest(null, FirstName: "Ingrid"), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AStagedMessagesPatientIsMatchedToThePersonOnFile()
    {
        var john = new Person
        {
            LastName = "Doe", FirstName = "John", BirthDate = new DateOnly(1980, 1, 15), BirthGender = "M",
        };
        await Add(john);

        var staged = new StagedMessage
        {
            RawMessage = await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hl7", "oru-r01-chlamydia.hl7")),
            Source = "person-match-test.hl7",
            ReceivedAt = DateTime.UtcNow,
            ReviewStatus = MessageReviewStatusRules.EntryCode,
        };

        await using (var context = server.NewContext())
        {
            context.StagedMessages.Add(staged);
            await context.SaveChangesAsync();
        }

        try
        {
            var client = api.CreateClient();

            var proposal = await client.GetFromJsonAsync<StagedMessagePersonMatchResponse>(
                $"/api/staged-messages/{staged.Id}/person-matches", Json);

            Assert.Equal("DOE", proposal!.Patient?.LastName);
            var best = proposal.Candidates[0];
            Assert.Equal(john.Id, best.Person.Id);
            Assert.Equal(85.0, best.Score);
            Assert.Equal(PersonMatchCandidateResponse.Probable, best.Verdict);

            using var missing = await client.GetAsync("/api/staged-messages/0/person-matches");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
        finally
        {
            await using var context = server.NewContext();
            await context.StagedMessages.Where(message => message.Id == staged.Id).ExecuteDeleteAsync();
            await Remove(john);
        }
    }

    private static Person Matchwell(
        string firstName, DateOnly? birthDate, string birthGender, bool address = false, string? identifier = null)
    {
        var person = new Person
        {
            LastName = "Matchwell", FirstName = firstName, BirthDate = birthDate, BirthGender = birthGender,
        };

        if (address)
        {
            person.Addresses.Add(new PersonAddress { Street = "12 Elm Street", PostalCode = "06101" });
        }

        if (identifier is not null)
        {
            person.Identifiers.Add(new PersonIdentifier { Value = identifier, IdentifierType = "MR" });
        }

        return person;
    }

    private static async Task<IReadOnlyList<PersonMatchCandidateResponse>> Match(
        HttpClient client, PersonMatchRequest request)
    {
        using var response = await client.PostAsJsonAsync("/api/persons/match", request, Json);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<List<PersonMatchCandidateResponse>>(Json))!;
    }

    private async Task Add(params Person[] persons)
    {
        await using var context = server.NewContext();
        context.Persons.AddRange(persons);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Takes the people a test added out again, so the seeded count other
    /// tests assert stays what the fixture wrote.
    /// </summary>
    private async Task Remove(params Person[] persons)
    {
        var ids = persons.Select(person => person.Id).ToList();

        await using var context = server.NewContext();
        await context.Persons.Where(person => ids.Contains(person.Id)).ExecuteDeleteAsync();
    }
}
