using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;

namespace Survsys.Api;

/// <summary>
/// <c>POST /api/persons/match</c>: the people on file scored against a person
/// a match is sought for, best first. <c>GET
/// /api/staged-messages/{id}/person-matches</c>: the same, for a staged
/// message's patient.
/// </summary>
/// <remarks>
/// The scoring and the classification are <see cref="PersonMatchRules"/>'s;
/// this slice finds the candidates, projects them to plain subjects and turns
/// the outcome into the wire shape. The thresholds come from the
/// <c>PersonMatching</c> configuration section. Nothing is assigned: a person
/// reads the verdicts and decides.
/// </remarks>
public static class PersonMatchEndpoints
{
    /// <summary>The configuration section the thresholds are read from.</summary>
    public const string SectionName = "PersonMatching";

    /// <summary>The most people a request may name to be scored.</summary>
    public const int MaxPersonIds = 100;

    /// <summary>The most candidates a search loads to score.</summary>
    public const int MaxCandidates = 200;

    /// <summary>
    /// Why a match request cannot be answered, or null where it can: it must
    /// give something to find candidates by, and name no more than
    /// <see cref="MaxPersonIds"/> people.
    /// </summary>
    public static string? WhyNotAMatchRequest(PersonMatchRequest request)
    {
        if (request.PersonIds is { Count: > MaxPersonIds })
        {
            return $"No more than {MaxPersonIds} people can be scored at once.";
        }

        var findable = !string.IsNullOrWhiteSpace(request.LastName)
            || request.BirthDate is not null
            || Values(request.Identifiers).Count > 0
            || request.PersonIds is { Count: > 0 };

        return findable
            ? null
            : "A match needs a last name, a date of birth, an identifier or the people to score.";
    }

    /// <summary>The person a match is sought for, as the rules compare them.</summary>
    public static PersonMatchSubject ToSubject(PersonMatchRequest request) => new(
        request.LastName,
        request.FirstName,
        request.BirthDate,
        request.BirthGender,
        Values(request.Identifiers),
        PersonMatchRules.AddressKey(request.Street, request.PostalCode) is { } key ? [key] : []);

    /// <summary>
    /// A person on file, as the rules compare them: their identifier values
    /// and their current addresses (those with no ending date).
    /// </summary>
    public static PersonMatchSubject ToSubject(Person person) => new(
        person.LastName,
        person.FirstName,
        person.BirthDate,
        person.BirthGender,
        person.Identifiers.Select(identifier => identifier.Value).ToList(),
        person.Addresses
            .Where(address => address.Ending is null)
            .Select(address => PersonMatchRules.AddressKey(address.Street, address.PostalCode))
            .OfType<string>()
            .ToList());

    /// <summary>The match request a staged message's parsed patient makes.</summary>
    public static PersonMatchRequest ToRequest(CreatePersonRequest patient) => new(
        patient.LastName, patient.FirstName, patient.BirthDate, patient.BirthGender);

    /// <summary>
    /// Score and classify each candidate against the request and answer them
    /// best first, by name where scores tie and by id where names do too.
    /// </summary>
    public static IReadOnlyList<PersonMatchCandidateResponse> Rank(
        PersonMatchRequest request, IEnumerable<Person> candidates, PersonMatchThresholds thresholds)
    {
        var incoming = ToSubject(request);

        return candidates
            .Select(person =>
            {
                var subject = ToSubject(person);
                var scored = PersonMatchRules.Score(incoming, subject);
                var kind = PersonMatchRules.Classify(
                    new PersonMatchCandidate(person.Id, scored.Score, person.BirthDate, subject.Identifiers),
                    incoming.Identifiers,
                    thresholds);

                return new PersonMatchCandidateResponse(
                    PersonEndpoints.Respond(person), scored.Score, Verdict(kind), scored.Reasons);
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Person.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Person.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Person.Id)
            .ToList();
    }

    /// <summary>The wire name of a classification.</summary>
    public static string Verdict(PersonMatchKind kind) => kind switch
    {
        PersonMatchKind.Exact => PersonMatchCandidateResponse.Exact,
        PersonMatchKind.Fuzzy => PersonMatchCandidateResponse.Probable,
        _ => PersonMatchCandidateResponse.None,
    };

    /// <summary>
    /// Registers what the match endpoints need: the queries and the thresholds
    /// bound from configuration, the defaults where the section is absent.
    /// </summary>
    public static IServiceCollection AddPersonMatches(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddScoped<PersonMatchQueries>()
            .AddSingleton(configuration.GetSection(SectionName).Get<PersonMatchThresholds>()
                ?? new PersonMatchThresholds());

    public static IEndpointRouteBuilder MapPersonMatches(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/persons/match", MatchPersons)
            .WithName("MatchPersons")
            .WithSummary("The people on file scored against the person given, best first, "
                + "each with a verdict and the reasons for it.");

        routes.MapGet("/api/staged-messages/{id:int}/person-matches", MatchStagedMessage)
            .WithName("MatchStagedMessagePerson")
            .WithSummary("A staged message's patient and the people on file who may be them, best first.");

        return routes;
    }

    private static async Task<Results<Ok<IReadOnlyList<PersonMatchCandidateResponse>>, ProblemHttpResult>> MatchPersons(
        PersonMatchQueries matches,
        PersonMatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAMatchRequest(request) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The match asked for is not one this endpoint can make.");
        }

        return TypedResults.Ok(await matches.Match(request, cancellationToken));
    }

    private static async Task<Results<Ok<StagedMessagePersonMatchResponse>, ProblemHttpResult>> MatchStagedMessage(
        PersonMatchQueries matches,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await matches.ForStagedMessage(id, cancellationToken) is not { } found)
        {
            return TypedResults.Problem(
                detail: $"No staged message has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "The staged message asked for does not exist.");
        }

        return TypedResults.Ok(found);
    }

    private static List<string> Values(IReadOnlyList<string>? values) =>
        values?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToList() ?? [];
}

/// <summary>
/// The reads behind the match endpoints: the candidates, loaded with their
/// identifiers and addresses, and a staged message's raw text.
/// </summary>
internal sealed class PersonMatchQueries(SurvsysDbContext database, PersonMatchThresholds thresholds)
{
    /// <summary>
    /// The candidates for a request, scored and best first: the people it
    /// names, or else up to <see cref="PersonMatchEndpoints.MaxCandidates"/>
    /// sharing its last name, its date of birth or one of its identifier values,
    /// those sharing more of them kept first.
    /// </summary>
    public async Task<IReadOnlyList<PersonMatchCandidateResponse>> Match(
        PersonMatchRequest request, CancellationToken cancellationToken)
    {
        IQueryable<Person> persons = database.Persons
            .AsNoTracking()
            .Include(person => person.Identifiers)
            .Include(person => person.Addresses);

        if (request.PersonIds is { Count: > 0 } ids)
        {
            persons = persons.Where(person => ids.Contains(person.Id));
        }
        else
        {
            var lastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();
            var birthDate = request.BirthDate;
            var identifiers = PersonMatchEndpoints.ToSubject(request).Identifiers.ToList();

            // Those sharing the most of the three come first, so the cap drops
            // the weakest candidates and never one sharing more than a kept one.
            persons = persons
                .Where(person => (lastName != null && person.LastName == lastName)
                    || (birthDate != null && person.BirthDate == birthDate)
                    || person.Identifiers.Any(identifier => identifiers.Contains(identifier.Value)))
                .OrderByDescending(person =>
                    (lastName != null && person.LastName == lastName ? 1 : 0)
                    + (birthDate != null && person.BirthDate == birthDate ? 1 : 0)
                    + (person.Identifiers.Any(identifier => identifiers.Contains(identifier.Value)) ? 1 : 0))
                .ThenBy(person => person.Id)
                .Take(PersonMatchEndpoints.MaxCandidates);
        }

        var candidates = await persons.AsSplitQuery().ToListAsync(cancellationToken);
        return PersonMatchEndpoints.Rank(request, candidates, thresholds);
    }

    /// <summary>
    /// A staged message's patient and their candidates, or null where no
    /// message has that id. A message that does not parse, or whose patient
    /// gives nothing to find candidates by, has none.
    /// </summary>
    public async Task<StagedMessagePersonMatchResponse?> ForStagedMessage(int id, CancellationToken cancellationToken)
    {
        var raw = await database.StagedMessages
            .Where(message => message.Id == id)
            .Select(message => message.RawMessage)
            .SingleOrDefaultAsync(cancellationToken);

        if (raw is null)
        {
            return null;
        }

        CreatePersonRequest patient;
        try
        {
            patient = new OruR01Parser().Parse(raw).Person;
        }
        catch (Hl7ParseException)
        {
            return new StagedMessagePersonMatchResponse(null, []);
        }

        var request = PersonMatchEndpoints.ToRequest(patient);
        var candidates = PersonMatchEndpoints.WhyNotAMatchRequest(request) is null
            ? await Match(request, cancellationToken)
            : [];

        return new StagedMessagePersonMatchResponse(patient, candidates);
    }
}
