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
    /// <see cref="ToResponse"/> compiled, for the one caller that has a person
    /// in hand rather than a query to translate: the person just created.
    /// </summary>
    /// <remarks>
    /// The same expression and so the same mapping, compiled once rather than
    /// per request. A method written beside it for the created-person case
    /// would be a second mapping that agreed with the list's until one of them
    /// was changed.
    ///
    /// Public because the unit suite proves it, as
    /// <see cref="ToResponse"/> is. Nothing outside this file calls it.
    /// </remarks>
    public static readonly Func<Person, PersonResponse> Respond = ToResponse.Compile();

    /// <summary>
    /// Why this is not a person this tier will put on file, or null when it
    /// is.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="Paging.WhyNotAPage"/> has, and here for the same
    /// reason: the rule is then a pure function the unit suite proves without
    /// a database or a host, and the endpoint's part is turning the sentence
    /// into a 400.
    ///
    /// There is one rule, because the schema has one requirement: a person has
    /// a last name. Everything else about a person is nullable in the legacy
    /// data and nullable here, and this tier does not invent a validation the
    /// schema does not have — it does not check that a date of birth is in the
    /// past, or that a gender code is one of a set, because the reference
    /// tables those codes belong to do not exist yet and a rule written
    /// against a guess at them would be a rule to unpick later.
    ///
    /// Public because the unit suite proves it. Nothing outside this file
    /// calls it.
    /// </remarks>
    public static string? WhyNotAPerson(CreatePersonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return string.IsNullOrWhiteSpace(request.LastName)
            ? "lastName is required: a person's last name is the one name part this "
                + "schema requires, and a person without one could not be listed or "
                + "searched for."
            : null;
    }

    /// <summary>
    /// The person a creation request describes.
    /// </summary>
    /// <remarks>
    /// Two normalisations, and both are what a form in front of a user makes
    /// necessary. A value is trimmed, because a surname typed with a trailing
    /// space is the same surname. A value that is blank becomes null, because
    /// a column here means "nobody has said" with NULL — which is what
    /// <c>Fixtures/persons.sql</c> says of the legacy data's empty strings —
    /// and a first name stored as an empty string would be a person who has a
    /// first name that is nothing.
    ///
    /// <see cref="CreatePersonRequest.LastName"/> is read with a null-forgiving
    /// <c>!</c> because <see cref="WhyNotAPerson"/> has already refused a
    /// request without one; this is called after it and never instead of it.
    ///
    /// Public because the unit suite proves it. Nothing outside this file
    /// calls it.
    /// </remarks>
    public static Person ToPerson(CreatePersonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new Person
        {
            LastName = Said(request.LastName)!,
            FirstName = Said(request.FirstName),
            MiddleName = Said(request.MiddleName),
            BirthDate = request.BirthDate,
            BirthGender = Said(request.BirthGender),
        };
    }

    /// <summary>
    /// A field trimmed, or null where nothing was said in it.
    /// </summary>
    private static string? Said(string? field) =>
        string.IsNullOrWhiteSpace(field) ? null : field.Trim();

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
            .WithSummary("The people on file, paged and ordered by name, filtered by the "
                + "search criteria given.");

        routes.MapGet("/api/persons/{id:int}", GetPerson)
            .WithName("GetPerson")
            .WithSummary("One person's whole demographic record.");

        routes.MapPost("/api/persons", CreatePerson)
            .WithName("CreatePerson")
            .WithSummary("Puts a person on file, from the name, date of birth and birth "
                + "gender a search was made with.");

        return routes;
    }

    /// <remarks>
    /// The six search criteria arrive as one <see cref="PersonSearch"/> rather
    /// than as six more parameters here, which is what <c>[AsParameters]</c>
    /// is for: they are one thing a caller is asking by, and the rule for what
    /// each of them means belongs with them and not in a route registration.
    /// </remarks>
    private static async Task<Results<Ok<PagedResult<PersonResponse>>, ProblemHttpResult>> GetPersons(
        PersonQueries persons,
        [AsParameters] PersonSearch search,
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

        return TypedResults.Ok(await persons.Page(search, page, pageSize, cancellationToken));
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

    /// <remarks>
    /// This is the endpoint the legacy flow's "Create Person and CMR" button
    /// needs: a search that found nobody is followed by the person being put
    /// on file, and then by the report about them. It creates the person and
    /// nothing else — no report, because the two are separate steps in that
    /// flow and separate requests here.
    ///
    /// The 201 carries the created person and a <c>Location</c> naming
    /// <c>GET /api/persons/{id}</c>, which is an endpoint that exists: a
    /// caller can read back what it just created.
    /// </remarks>
    private static async Task<Results<Created<PersonResponse>, ProblemHttpResult>> CreatePerson(
        PersonQueries persons,
        CreatePersonRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAPerson(request) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The person asked for is not one this endpoint can create.");
        }

        var created = await persons.Add(request, cancellationToken);

        return TypedResults.Created($"/api/persons/{created.Id}", created);
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
    /// One page of the people matching the criteria, ordered by last name,
    /// then first name, then id — by id as well because names are not unique,
    /// and an order that is not total lets a row appear on two pages or on
    /// neither.
    /// </summary>
    /// <remarks>
    /// The count is taken over the same filtered query as the rows, so the
    /// total a caller is told is the total of what it searched for and not of
    /// the table.
    /// </remarks>
    public async Task<PagedResult<PersonResponse>> Page(
        PersonSearch search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var matching = Matching(database.Persons, search);

        var total = await matching.CountAsync(cancellationToken);

        var rows = await matching
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

    /// <summary>
    /// Puts a person on file and answers with them as a list shows them.
    /// </summary>
    /// <remarks>
    /// One <c>INSERT</c> and no read after it: the identifier the database
    /// assigned is on the tracked entity once the write returns, and the rest
    /// of what comes back is what went in. The mapping is
    /// <see cref="PersonEndpoints.Respond"/> — the list's own projection,
    /// compiled — so a created person and a listed one are described the same
    /// way.
    ///
    /// The request is validated before this is called, and a database failure
    /// surfaces unwrapped, as everything else in this class does.
    /// </remarks>
    public async Task<PersonResponse> Add(
        CreatePersonRequest request,
        CancellationToken cancellationToken)
    {
        var person = PersonEndpoints.ToPerson(request);

        database.Persons.Add(person);
        await database.SaveChangesAsync(cancellationToken);

        return PersonEndpoints.Respond(person);
    }

    /// <summary>
    /// The query narrowed by each criterion that was given, and left alone by
    /// each that was not.
    /// </summary>
    /// <remarks>
    /// A clause at a time rather than one expression built up out of several,
    /// because this is what EF Core translates into a <c>WHERE</c> and what a
    /// reader can check against <see cref="PersonSearch"/>'s own account of
    /// what each criterion means.
    ///
    /// The prefix and substring matches are case- and accent-insensitive here,
    /// and that is SQL Server's doing rather than this code's: the comparison
    /// is made by the database under the column's collation, which is
    /// case-insensitive by default, so a search for <c>rodriguez</c> finds
    /// Rodriguez. The same expression compiled and run in memory would be
    /// case-sensitive, which is why the integration suite is where the
    /// insensitivity is proved and the unit suite does not claim it.
    ///
    /// The telephone match is over the digits of all three columns a number is
    /// stored in — the country code, the area code and the number, in that
    /// order, which is the order they are dialled in. A number typed whole, or
    /// typed without its country code, or typed without its area code, finds
    /// the person every way: each of those is a trailing part of the same
    /// string of digits, which is what matching as a substring is for. A
    /// criterion typed with a country code finds the rows that store one, and a
    /// row that stores none is found by the number without it — which is the
    /// one asymmetry here, and it is the stored row's silence about its country
    /// rather than this query's doing.
    ///
    /// It assumes the stored columns hold digits, which is what the legacy data
    /// holds: every telephone in
    /// <c>../nedss/docs/db-snapshots/session-010/</c> is digits in separate
    /// columns, as the schema's three columns intend. A stored number someone
    /// had typed punctuation into would not be found by a search for its
    /// digits, and the alternative — a chain of <c>REPLACE</c>s over the stored
    /// side — would scan every row of every telephone to answer one search.
    /// Cleaning stored data is a migration's job rather than a query's.
    /// </remarks>
    private static IQueryable<Person> Matching(IQueryable<Person> persons, PersonSearch criteria)
    {
        var search = criteria.Trimmed();

        if (search.LastName is { Length: > 0 } lastName)
        {
            persons = persons.Where(person => person.LastName.StartsWith(lastName));
        }

        if (search.FirstName is { Length: > 0 } firstName)
        {
            persons = persons.Where(person =>
                person.FirstName != null && person.FirstName.StartsWith(firstName));
        }

        if (search.MiddleName is { Length: > 0 } middleName)
        {
            persons = persons.Where(person =>
                person.MiddleName != null && person.MiddleName.StartsWith(middleName));
        }

        if (search.BirthDate is { } birthDate)
        {
            persons = persons.Where(person => person.BirthDate == birthDate);
        }

        if (search.BirthGender is { Length: > 0 } birthGender)
        {
            persons = persons.Where(person => person.BirthGender == birthGender);
        }

        if (search.PhoneDigits is { Length: > 0 } phone)
        {
            persons = persons.Where(person => person.Telephones.Any(telephone =>
                ((telephone.CountryCode ?? string.Empty)
                    + (telephone.AreaCode ?? string.Empty)
                    + (telephone.PhoneNumber ?? string.Empty))
                    .Contains(phone)));
        }

        return persons;
    }
}
