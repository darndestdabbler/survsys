using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>GET /api/persons</c>: the people on file, paged. And
/// <c>GET /api/persons/{id}</c>: one person's whole demographic record.
/// </summary>
/// <remarks>
/// The entity-to-wire mapping lives here, as the conditions slice's does and
/// for the same reason: <c>Survsys.Data</c> may not reference
/// <c>Survsys.Contracts</c>, because persistence must not know the wire
/// shapes, which leaves the translation to the tier that owns the wire.
///
/// The handlers are handed <see cref="PersonQueries"/> and not the
/// <c>DbContext</c>, because the architecture test in <c>Survsys.UnitTests</c>
/// lets no <c>Survsys.Data</c> type into an endpoint signature at all.
///
/// Two endpoints and two mappings, rather than one of each: a list of people
/// has no use for an address, and reading every person's addresses,
/// telephones, emails, races, aliases and guardians to show a page of names
/// would be fifty people's worth of rows for a screen that shows none of them.
/// </remarks>
public static class PersonEndpoints
{
    /// <summary>
    /// The projection from the stored person to the list shape — the seven
    /// fields a list of people shows.
    /// </summary>
    /// <remarks>
    /// An <see cref="Expression"/> rather than a method, so that it is the one
    /// mapping in both directions it has to work: EF Core translates it into
    /// the <c>SELECT</c> list, so a list request reads seven columns and no
    /// child row at all, and a unit test compiles and invokes the same
    /// expression to prove what it carries.
    ///
    /// Public because the unit suite proves it. Nothing else calls it.
    /// </remarks>
    public static readonly Expression<Func<Person, PersonResponse>> ToResponse =
        person => new PersonResponse(
            person.Id,
            person.LastName,
            person.FirstName,
            person.MiddleName,
            person.BirthDate,
            person.BirthGender,
            person.CurrentGender);

    /// <summary>
    /// The projection from the stored person to the whole demographic record —
    /// the person's own columns and every row that hangs off them.
    /// </summary>
    /// <remarks>
    /// An <see cref="Expression"/> for the same reason
    /// <see cref="ToResponse"/> is one, and it earns it twice over here: EF
    /// Core translates the nested collections into the same query, so one
    /// person's addresses, telephones, emails, races, aliases and guardians
    /// arrive without the graph being loaded and walked, and without a
    /// round trip apiece.
    ///
    /// Each child collection is ordered, because an unordered one comes back
    /// in whatever order SQL Server found it and a screen would show the same
    /// person's addresses in a different order on two requests. The order is
    /// by identifier — the order the rows were written in — and not by date:
    /// which address is the current one is what
    /// <see cref="PersonAddressResponse.Ending"/> being null says, and it is
    /// not the list's job to say it again by sorting.
    ///
    /// A guardian row with no name carries only a link to another person, and
    /// a list of names has nothing to show for it; those rows are left out
    /// here, which is what <see cref="PersonDetailResponse.ParentGuardians"/>
    /// says of itself.
    ///
    /// Public because the unit suite proves it. Nothing else calls it.
    /// </remarks>
    public static readonly Expression<Func<Person, PersonDetailResponse>> ToDetail =
        person => new PersonDetailResponse(
            person.Id,
            person.LastName,
            person.FirstName,
            person.MiddleName,
            person.BirthDate,
            person.DaysOldNoBirthday,
            person.BirthGender,
            person.CurrentGender,
            person.Ethnicity,
            person.Language,
            person.CountryOfBirth,
            person.Races
                .OrderBy(race => race.Race)
                .Select(race => race.Race)
                .ToList(),
            person.Aliases
                .OrderBy(alias => alias.Alias)
                .Select(alias => alias.Alias)
                .ToList(),
            person.Guardians
                .Where(guardian => guardian.Name != null)
                .OrderBy(guardian => guardian.Id)
                .Select(guardian => guardian.Name!)
                .ToList(),
            person.Addresses
                .OrderBy(address => address.Id)
                .Select(address => new PersonAddressResponse(
                    address.Id,
                    address.Street,
                    address.UnitNumber,
                    address.City,
                    address.State,
                    address.PostalCode,
                    address.County,
                    address.Beginning,
                    address.Ending,
                    address.Note,
                    address.ResidenceTypeId))
                .ToList(),
            person.Telephones
                .OrderBy(telephone => telephone.Id)
                .Select(telephone => new PersonTelephoneResponse(
                    telephone.Id,
                    telephone.TelephoneType,
                    telephone.CountryCode,
                    telephone.AreaCode,
                    telephone.PhoneNumber,
                    telephone.Extension,
                    telephone.Beginning,
                    telephone.Note))
                .ToList(),
            person.Emails
                .OrderBy(email => email.Id)
                .Select(email => new PersonEmailResponse(
                    email.Id,
                    email.EmailAddress,
                    email.CurrentEmail))
                .ToList());

    /// <summary>
    /// Registers what the persons endpoints need of the container. Kept here
    /// with the endpoints so the slice's wiring is in one place, and so that
    /// <c>Program.cs</c> need not name an internal type.
    /// </summary>
    public static IServiceCollection AddPersons(this IServiceCollection services) =>
        services.AddScoped<PersonQueries>();

    public static IEndpointRouteBuilder MapPersons(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/persons", GetPersons)
            .WithName("GetPersons")
            .WithSummary("The people on file, paged and ordered by name.");

        routes.MapGet("/api/persons/{id:int}", GetPerson)
            .WithName("GetPerson")
            .WithSummary("One person's whole demographic record.");

        return routes;
    }

    private static async Task<Results<Ok<PagedResult<PersonResponse>>, ProblemHttpResult>> GetPersons(
        PersonQueries persons,
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

        return TypedResults.Ok(await persons.Page(page, pageSize, cancellationToken));
    }

    private static async Task<Results<Ok<PersonDetailResponse>, ProblemHttpResult>> GetPerson(
        PersonQueries persons,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await persons.Find(id, cancellationToken) is not { } person)
        {
            return TypedResults.Problem(
                detail: $"No person has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such person.");
        }

        return TypedResults.Ok(person);
    }
}

/// <summary>
/// The person queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, because it exists only to
/// serve <see cref="PersonEndpoints"/>: it is the named step between an
/// endpoint signature and <c>Survsys.Data</c> that the architecture test
/// requires, and it keeps the ordering and the paging arithmetic in one place
/// instead of in a route registration.
///
/// Nothing here translates a database failure into an HTTP concern. An EF Core
/// or SQL Server exception surfaces unwrapped and the exception handler turns
/// it into a 500 with Problem Details and a correlation id.
/// </remarks>
internal sealed class PersonQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One page of people, ordered by last name, then first name, then id — by
    /// id as well because names are not unique, and an order that is not total
    /// lets a row appear on two pages or on neither.
    /// </summary>
    public async Task<PagedResult<PersonResponse>> Page(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var total = await database.Persons.CountAsync(cancellationToken);

        var rows = await database.Persons
            .OrderBy(person => person.LastName)
            .ThenBy(person => person.FirstName)
            .ThenBy(person => person.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(PersonEndpoints.ToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<PersonResponse>(rows, page, pageSize, total);
    }

    /// <summary>
    /// One person's whole demographic record, or null where no person has that
    /// id. Null is the answer and not an exception: an id nobody has is a
    /// question this tier can answer, and the endpoint turns it into a 404.
    /// </summary>
    public Task<PersonDetailResponse?> Find(int id, CancellationToken cancellationToken) =>
        database.Persons
            .Where(person => person.Id == id)
            .Select(PersonEndpoints.ToDetail)
            .SingleOrDefaultAsync(cancellationToken);
}
