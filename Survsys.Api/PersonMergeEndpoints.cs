using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;

namespace Survsys.Api;

/// <summary>
/// <c>GET /api/person-merges/candidates</c>: the pairs of people on file the
/// matcher takes for one person, best first. <c>POST /api/person-merges</c>:
/// one of a pair merged into the other.
/// </summary>
/// <remarks>
/// The scoring and the classification are <see cref="PersonMatchRules"/>'s,
/// with the thresholds <see cref="PersonMatchEndpoints"/> binds; this slice
/// pairs the people and moves the records. A merge keeps one person and moves
/// everything the other has onto them, then leaves the other on file as a
/// tombstone (<see cref="Person.MergedIntoPersonId"/>). It is all one
/// transaction, and the <see cref="AuditInterceptor"/> audits every row it
/// changes inside it, so a merge that fails leaves both people as they were
/// and no audit behind. Nothing is merged unless a person asks.
/// </remarks>
public static class PersonMergeEndpoints
{
    /// <summary>The most pairs the candidates route answers with.</summary>
    public const int MaxPairs = 200;

    /// <summary>
    /// Why a merge request cannot be answered, or null where it can: both
    /// people must be named by an id.
    /// </summary>
    public static string? WhyNotAMergeRequest(PersonMergeRequest request) =>
        request.KeepPersonId > 0 && request.MergePersonId > 0
            ? null
            : "A merge needs the id of the person kept and of the person merged away.";

    /// <summary>
    /// Why these two people cannot be merged, or null where they can: they
    /// must be two people, and neither may already have been merged away.
    /// </summary>
    public static string? WhyNotMergeable(Person keep, Person merge)
    {
        if (keep.Id == merge.Id)
        {
            return "A person cannot be merged into themselves.";
        }

        return keep.MergedIntoPersonId is not null || merge.MergedIntoPersonId is not null
            ? "A person already merged away cannot be merged again."
            : null;
    }

    /// <summary>
    /// The pairs among these people the matcher classifies as the same person
    /// or a probable match, best first: the same person before a probable
    /// match, then by score, then by ids. People are
    /// paired only where they share a last name, a date of birth or an
    /// identifier value, and each pair is scored once, the lower id as the
    /// person on file.
    /// </summary>
    public static IReadOnlyList<DuplicatePersonPairResponse> Pairs(
        IEnumerable<Person> persons, PersonMatchThresholds thresholds)
    {
        var blocks = new Dictionary<string, List<Person>>(StringComparer.Ordinal);
        foreach (var person in persons.OrderBy(person => person.Id))
        {
            var keys = person.Identifiers
                .Select(identifier => identifier.Value.Trim())
                .Where(value => value.Length > 0)
                .Select(value => $"identifier:{value}")
                .Append($"last:{person.LastName.Trim().ToUpperInvariant()}");

            if (person.BirthDate is { } birthDate)
            {
                keys = keys.Append($"born:{birthDate:O}");
            }

            foreach (var key in keys.Distinct())
            {
                (blocks.TryGetValue(key, out var block) ? block : blocks[key] = []).Add(person);
            }
        }

        return blocks.Values
            .SelectMany(block => block.SelectMany((first, at) => block.Skip(at + 1).Select(second => (first, second))))
            .DistinctBy(pair => (pair.first.Id, pair.second.Id))
            .Select(pair =>
            {
                var first = PersonMatchEndpoints.ToSubject(pair.first);
                var second = PersonMatchEndpoints.ToSubject(pair.second);
                var scored = PersonMatchRules.Score(second, first);
                var kind = PersonMatchRules.Classify(
                    new PersonMatchCandidate(pair.first.Id, scored.Score, pair.first.BirthDate, first.Identifiers),
                    second.Identifiers,
                    thresholds);

                return new DuplicatePersonPairResponse(
                    PersonEndpoints.Respond(pair.first),
                    PersonEndpoints.Respond(pair.second),
                    scored.Score,
                    PersonMatchEndpoints.Verdict(kind),
                    scored.Reasons);
            })
            .Where(pair => pair.Verdict != PersonMatchCandidateResponse.None)
            .OrderBy(pair => pair.Verdict == PersonMatchCandidateResponse.Exact ? 0 : 1)
            .ThenByDescending(pair => pair.Score)
            .ThenBy(pair => pair.First.Id)
            .ThenBy(pair => pair.Second.Id)
            .Take(MaxPairs)
            .ToList();
    }

    public static IServiceCollection AddPersonMerges(this IServiceCollection services) =>
        services.AddScoped<PersonMergeQueries>();

    public static IEndpointRouteBuilder MapPersonMerges(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/person-merges/candidates", GetCandidates)
            .WithName("GetDuplicatePersons")
            .WithSummary("The pairs of people on file the matcher takes for one person, best first.");

        routes.MapPost("/api/person-merges", MergePersons)
            .WithName("MergePersons")
            .WithSummary("One person merged into another: everything they have moves to the person kept, "
                + "and they stay on file as a tombstone.");

        return routes;
    }

    private static async Task<Ok<IReadOnlyList<DuplicatePersonPairResponse>>> GetCandidates(
        PersonMergeQueries merges,
        CancellationToken cancellationToken = default) =>
        TypedResults.Ok(await merges.Candidates(cancellationToken));

    private static async Task<Results<Ok<PersonResponse>, ProblemHttpResult>> MergePersons(
        PersonMergeQueries merges,
        PersonMergeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAMergeRequest(request) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The merge asked for is not one this endpoint can make.");
        }

        var outcome = await merges.Merge(request, cancellationToken);

        if (outcome.Missing is { } missing)
        {
            return TypedResults.Problem(
                detail: $"No person has id {missing}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "A person asked to be merged does not exist.");
        }

        if (outcome.Conflict is { } conflict)
        {
            return TypedResults.Problem(
                detail: conflict,
                statusCode: StatusCodes.Status409Conflict,
                title: "These people cannot be merged.");
        }

        return TypedResults.Ok(outcome.Kept!);
    }
}

/// <summary>
/// What a merge came to: the person kept, or the id nobody has, or why the two
/// cannot be merged.
/// </summary>
internal sealed record PersonMergeOutcome(PersonResponse? Kept, int? Missing = null, string? Conflict = null);

/// <summary>
/// The reads and the writes behind the merge endpoints.
/// </summary>
internal sealed class PersonMergeQueries(SurvsysDbContext database, PersonMatchThresholds thresholds)
{
    /// <summary>
    /// The candidate pairs among live people: only those who share a last
    /// name, a date of birth or an identifier value with another live person
    /// are loaded, then paired and scored.
    /// </summary>
    public async Task<IReadOnlyList<DuplicatePersonPairResponse>> Candidates(CancellationToken cancellationToken)
    {
        var live = database.Persons.Where(person => person.MergedIntoPersonId == null);

        var sharedLastNames = live
            .GroupBy(person => person.LastName)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        var sharedBirthDates = live
            .Where(person => person.BirthDate != null)
            .GroupBy(person => person.BirthDate)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        var sharedIdentifiers = database.PersonIdentifiers
            .Where(identifier => live.Any(person => person.Id == identifier.PersonId))
            .GroupBy(identifier => identifier.Value)
            .Where(group => group.Select(identifier => identifier.PersonId).Distinct().Count() > 1)
            .Select(group => group.Key);

        var persons = await live
            .AsNoTracking()
            .Include(person => person.Identifiers)
            .Include(person => person.Addresses)
            .Where(person => sharedLastNames.Contains(person.LastName)
                || sharedBirthDates.Contains(person.BirthDate)
                || person.Identifiers.Any(identifier => sharedIdentifiers.Contains(identifier.Value)))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return PersonMergeEndpoints.Pairs(persons, thresholds);
    }

    /// <summary>
    /// Merges one person into another in one transaction: their cases (and so
    /// everything on a case), labs, contact and exposure-source links,
    /// guardian links, identifiers, name history, telephones, emails and
    /// addresses move to the person kept; their aliases and races are added
    /// where the person kept lacks them; their current address is closed today
    /// where the person kept already has one; and they are left a tombstone.
    /// </summary>
    public async Task<PersonMergeOutcome> Merge(PersonMergeRequest request, CancellationToken cancellationToken)
    {
        int keepId = request.KeepPersonId, mergeId = request.MergePersonId;

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var found = await database.Persons
            .Where(person => person.Id == keepId || person.Id == mergeId)
            .ToListAsync(cancellationToken);

        if (found.SingleOrDefault(person => person.Id == keepId) is not { } keep)
        {
            return new PersonMergeOutcome(null, Missing: keepId);
        }

        if (found.SingleOrDefault(person => person.Id == mergeId) is not { } merge)
        {
            return new PersonMergeOutcome(null, Missing: mergeId);
        }

        if (PersonMergeEndpoints.WhyNotMergeable(keep, merge) is { } conflict)
        {
            return new PersonMergeOutcome(null, Conflict: conflict);
        }

        foreach (var report in await database.PersonConditions.Where(row => row.PersonId == mergeId).ToListAsync(cancellationToken))
        {
            report.PersonId = keepId;
        }

        foreach (var lab in await database.Labs.Where(row => row.PersonId == mergeId).ToListAsync(cancellationToken))
        {
            lab.PersonId = keepId;
        }

        foreach (var contact in await database.ExposureContacts.Where(row => row.PersonId == mergeId).ToListAsync(cancellationToken))
        {
            contact.PersonId = keepId;
        }

        foreach (var source in await database.ExposureSources.Where(row => row.PersonId == mergeId).ToListAsync(cancellationToken))
        {
            source.PersonId = keepId;
        }

        foreach (var link in await database.ParentGuardians
            .Where(row => row.ChildId == mergeId || row.ParentId == mergeId)
            .ToListAsync(cancellationToken))
        {
            link.ChildId = link.ChildId == mergeId ? keepId : link.ChildId;
            link.ParentId = link.ParentId == mergeId ? keepId : link.ParentId;
        }

        foreach (var identifier in await database.PersonIdentifiers.Where(row => row.PersonId == mergeId).ToListAsync(cancellationToken))
        {
            identifier.PersonId = keepId;
        }

        foreach (var name in await database.PersonNameHistory.Where(row => row.PersonId == mergeId).ToListAsync(cancellationToken))
        {
            name.PersonId = keepId;
        }

        foreach (var telephone in await database.PersonTelephones.Where(row => row.PersonId == mergeId).ToListAsync(cancellationToken))
        {
            telephone.PersonId = keepId;
        }

        foreach (var email in await database.PersonEmails.Where(row => row.PersonId == mergeId).ToListAsync(cancellationToken))
        {
            email.PersonId = keepId;
        }

        var keepHasCurrentAddress = await database.PersonAddresses
            .AnyAsync(row => row.PersonId == keepId && row.Ending == null, cancellationToken);
        var today = DateOnly.FromDateTime(DateTimeOffset.Now.Date);
        foreach (var address in await database.PersonAddresses.Where(row => row.PersonId == mergeId).ToListAsync(cancellationToken))
        {
            address.PersonId = keepId;
            if (keepHasCurrentAddress && address.Ending is null)
            {
                address.Ending = today;
            }
        }

        // Aliases and races are keyed by the person, so they are re-added under
        // the person kept rather than moved, and only where not already held.
        var keptAliases = (await database.PersonAliases.Where(row => row.PersonId == keepId)
            .Select(row => row.Alias).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in await database.PersonAliases.Where(row => row.PersonId == mergeId).ToListAsync(cancellationToken))
        {
            database.PersonAliases.Remove(alias);
            if (keptAliases.Add(alias.Alias))
            {
                database.PersonAliases.Add(new PersonAlias { PersonId = keepId, Alias = alias.Alias });
            }
        }

        var keptRaces = (await database.PersonRaces.Where(row => row.PersonId == keepId)
            .Select(row => row.Race).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var race in await database.PersonRaces.Where(row => row.PersonId == mergeId).ToListAsync(cancellationToken))
        {
            database.PersonRaces.Remove(race);
            if (keptRaces.Add(race.Race))
            {
                database.PersonRaces.Add(new PersonRace { PersonId = keepId, Race = race.Race });
            }
        }

        await database.SaveChangesAsync(cancellationToken);

        merge.MergedIntoPersonId = keepId;
        merge.MergedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new PersonMergeOutcome(PersonEndpoints.Respond(keep));
    }
}
