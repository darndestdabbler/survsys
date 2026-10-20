using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/codes</c>: the reference code lists every coded field is chosen
/// from — one list read, and a code added or saved over.
/// </summary>
/// <remarks>
/// One endpoint for every list rather than one per legacy table, because a
/// list is rows of one table told apart by name (<see cref="CodeLists"/>).
/// A code is never deleted: it is retired by saving it inactive, so a record
/// that names it still does, and the default read leaves it out.
///
/// The test types are the one list mapped to conditions: read with a
/// condition, the list is narrowed to the test types mapped to it, or is the
/// whole list where that condition has none mapped, so a condition nobody has
/// mapped yet still offers every test type.
///
/// The handlers are handed <see cref="CodeQueries"/> and not the
/// <c>DbContext</c>, because the architecture test lets no <c>Survsys.Data</c>
/// type into an endpoint signature.
/// </remarks>
public static class CodeEndpoints
{
    private const int CodeLength = 100;

    private const int DescriptionLength = 400;

    /// <summary>
    /// Why this is not a code this tier will save on that list, as far as can
    /// be said without the database, or null when it is one.
    /// </summary>
    /// <remarks>
    /// A pure function the unit suite proves, with the endpoint turning the
    /// sentence into a 400. Whether the code is taken and whether the
    /// conditions exist need a query and are the endpoint's.
    /// </remarks>
    public static string? WhyNotACode(string list, SaveCodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsAList(list))
        {
            return NotAList(list);
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return "code is required: a coded field stores the code.";
        }

        if (request.ConditionIds is { Count: > 0 } && list != CodeLists.TestType)
        {
            return $"conditionIds are only kept for the {CodeLists.TestType} list, not {list}.";
        }

        return TooLong("code", request.Code, CodeLength)
            ?? TooLong("description", request.Description, DescriptionLength);
    }

    /// <summary>
    /// Registers what the code endpoints need of the container.
    /// </summary>
    public static IServiceCollection AddCodes(this IServiceCollection services) =>
        services.AddScoped<CodeQueries>();

    public static IEndpointRouteBuilder MapCodes(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/codes/{list}", GetCodes)
            .WithName("GetCodes")
            .WithSummary("One code list, by sort order then description.");

        routes.MapPost("/api/codes/{list}", CreateCode)
            .WithName("CreateCode")
            .WithSummary("Adds a code to a list.");

        routes.MapPut("/api/codes/{id:int}", UpdateCode)
            .WithName("UpdateCode")
            .WithSummary("Saves over a code.");

        return routes;
    }

    private static async Task<Results<Ok<IReadOnlyList<CodeResponse>>, ProblemHttpResult>> GetCodes(
        CodeQueries codes,
        string list,
        int? conditionId = null,
        bool inactive = false,
        CancellationToken cancellationToken = default)
    {
        if (!IsAList(list))
        {
            return TypedResults.Problem(
                detail: NotAList(list),
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such code list.");
        }

        return TypedResults.Ok(await codes.List(list, conditionId, inactive, cancellationToken));
    }

    private static async Task<Results<Created<CodeResponse>, ProblemHttpResult>> CreateCode(
        CodeQueries codes,
        string list,
        SaveCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await WhyNotSaved(codes, list, null, request, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var created = await codes.Add(list, request, cancellationToken);

        return TypedResults.Created($"/api/codes/{created.Id}", created);
    }

    private static async Task<Results<Ok<CodeResponse>, ProblemHttpResult>> UpdateCode(
        CodeQueries codes,
        int id,
        SaveCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await codes.ListOf(id, cancellationToken) is not { } list)
        {
            return TypedResults.Problem(
                detail: $"No code has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such code.");
        }

        if (await WhyNotSaved(codes, list, id, request, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        return TypedResults.Ok(await codes.Update(id, request, cancellationToken));
    }

    private static async Task<ProblemHttpResult?> WhyNotSaved(
        CodeQueries codes,
        string list,
        int? id,
        SaveCodeRequest request,
        CancellationToken cancellationToken)
    {
        if (WhyNotACode(list, request) is { } why)
        {
            return Refused(why);
        }

        if (await codes.Taken(list, request.Code!.Trim(), id, cancellationToken))
        {
            return TypedResults.Problem(
                detail: $"The code '{request.Code.Trim()}' is already on the {list} list.",
                statusCode: StatusCodes.Status409Conflict,
                title: "The code is already on the list.");
        }

        if (request.ConditionIds is { Count: > 0 } conditionIds
            && await codes.MissingCondition(conditionIds, cancellationToken) is { } missing)
        {
            return Refused($"No condition has id {missing}. GET /api/conditions lists them.");
        }

        return null;
    }

    private static bool IsAList(string list) => CodeLists.All.Contains(list, StringComparer.Ordinal);

    private static string NotAList(string list) =>
        $"'{list}' is not a code list. The lists are {string.Join(", ", CodeLists.All)}.";

    private static string? TooLong(string field, string? value, int length) =>
        value?.Trim().Length > length ? $"{field} is longer than {length} characters." : null;

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The code asked for is not one this endpoint can save.");
}

/// <summary>
/// The code list queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for the code lists.
/// </summary>
internal sealed class CodeQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One list, active codes only unless asked otherwise, ordered by sort
    /// order, then by what a picker shows, then by id so the order is total;
    /// test types narrowed to a condition's where it has any mapped.
    /// </summary>
    public async Task<IReadOnlyList<CodeResponse>> List(
        string list,
        int? conditionId,
        bool inactive,
        CancellationToken cancellationToken)
    {
        var codes = database.CodeValues.Where(code => code.List == list);

        if (!inactive)
        {
            codes = codes.Where(code => code.Active);
        }

        if (list == CodeLists.TestType && conditionId is { } condition)
        {
            var mapped = database.CodeValueConditions.Where(link => link.ConditionId == condition);

            if (await mapped.AnyAsync(cancellationToken))
            {
                codes = codes.Where(code => mapped.Any(link => link.CodeValueId == code.Id));
            }
        }

        return await codes
            .OrderBy(code => code.SortOrder)
            .ThenBy(code => code.Description ?? code.Code)
            .ThenBy(code => code.Id)
            .Select(code => new CodeResponse(
                code.Id,
                code.List,
                code.Code,
                code.Description,
                code.SortOrder,
                code.Active,
                database.CodeValueConditions
                    .Where(link => link.CodeValueId == code.Id)
                    .Select(link => link.ConditionId)
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    /// <summary>The list the code with that id is on, or null where no code has it.</summary>
    public Task<string?> ListOf(int id, CancellationToken cancellationToken) =>
        database.CodeValues
            .Where(code => code.Id == id)
            .Select(code => code.List)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Whether another code on the list already has this code, compared as
    /// the database compares it.
    /// </summary>
    public Task<bool> Taken(string list, string code, int? exceptId, CancellationToken cancellationToken) =>
        database.CodeValues.AnyAsync(
            existing => existing.List == list && existing.Code == code && existing.Id != exceptId,
            cancellationToken);

    /// <summary>The first of these condition ids no condition has, or null where every one exists.</summary>
    public async Task<int?> MissingCondition(IReadOnlyList<int> conditionIds, CancellationToken cancellationToken)
    {
        var found = await database.Conditions
            .Where(condition => conditionIds.Contains(condition.Id))
            .Select(condition => condition.Id)
            .ToListAsync(cancellationToken);

        return conditionIds.Where(id => !found.Contains(id)).Select(id => (int?)id).FirstOrDefault();
    }

    public async Task<CodeResponse> Add(string list, SaveCodeRequest request, CancellationToken cancellationToken)
    {
        var code = new CodeValue { List = list, Code = request.Code!.Trim() };
        Apply(code, request);
        database.CodeValues.Add(code);
        await database.SaveChangesAsync(cancellationToken);

        return await Link(code, request, cancellationToken);
    }

    /// <summary>Saves over the code, which the caller has found exists.</summary>
    public async Task<CodeResponse> Update(int id, SaveCodeRequest request, CancellationToken cancellationToken)
    {
        var code = await database.CodeValues.SingleAsync(code => code.Id == id, cancellationToken);
        Apply(code, request);

        await database.CodeValueConditions
            .Where(link => link.CodeValueId == id)
            .ExecuteDeleteAsync(cancellationToken);

        return await Link(code, request, cancellationToken);
    }

    /// <summary>
    /// The request's conditions onto the saved code, and the code as answered.
    /// </summary>
    private async Task<CodeResponse> Link(CodeValue code, SaveCodeRequest request, CancellationToken cancellationToken)
    {
        var conditionIds = (request.ConditionIds ?? []).Distinct().ToList();

        database.CodeValueConditions.AddRange(conditionIds.Select(conditionId =>
            new CodeValueCondition { CodeValueId = code.Id, ConditionId = conditionId }));
        await database.SaveChangesAsync(cancellationToken);

        return new CodeResponse(
            code.Id, code.List, code.Code, code.Description, code.SortOrder, code.Active, conditionIds);
    }

    /// <summary>
    /// The request's values onto the code: trimmed, a blank description stored
    /// as null.
    /// </summary>
    private static void Apply(CodeValue code, SaveCodeRequest request)
    {
        code.Code = request.Code!.Trim();
        code.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        code.SortOrder = request.SortOrder;
        code.Active = request.Active;
    }
}
