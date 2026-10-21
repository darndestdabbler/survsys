using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/notification-setup/phin-variables</c>: the PHIN variables a guide
/// can carry, listed, created, saved over and deleted.
/// </summary>
/// <remarks>
/// The legacy NMI PHIN variable setup described in
/// <c>../nedss/docs/functional/nmi-ui-overview.md</c>. A variable is keyed by
/// its identifier, the name a guide and a message use for it, so the
/// identifier is the route's and is not renamed. A rule set type or
/// configuration not on file is a 400 asked before the write, and a variable a
/// guide still carries is not deleted. The catalogue is small reference data,
/// so it is listed whole rather than paged.
/// </remarks>
public static class PhinVariableEndpoints
{
    public const string Path = "/api/notification-setup/phin-variables";

    private const int PhinVarLength = 50;
    private const int DescriptionLength = 500;
    private const int IdentifierLength = 50;
    private const int PriorityLength = 10;
    private const int DataTypeLength = 20;
    private const int StatusLength = 20;
    private const int NoteLength = 1000;
    private const int CodeSystemLength = 50;

    /// <summary>Why this is not a PHIN variable this tier will save, or null when it is one.</summary>
    public static string? WhyNotAPhinVariable(PhinVariableRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return VocabularyEndpoints.Required("phinVar", row.PhinVar, PhinVarLength)
            ?? VocabularyEndpoints.Required("description", row.Description, DescriptionLength)
            ?? VocabularyEndpoints.TooLong("identifier", row.Identifier, IdentifierLength)
            ?? VocabularyEndpoints.TooLong("priority", row.Priority, PriorityLength)
            ?? VocabularyEndpoints.TooLong("hl7DataType", row.Hl7DataType, DataTypeLength)
            ?? VocabularyEndpoints.TooLong("status", row.Status, StatusLength)
            ?? VocabularyEndpoints.TooLong("note", row.Note, NoteLength)
            ?? VocabularyEndpoints.TooLong("codeSystem", row.CodeSystem, CodeSystemLength);
    }

    /// <summary>Registers what the PHIN variable endpoints need of the container.</summary>
    public static IServiceCollection AddPhinVariables(this IServiceCollection services) =>
        services.AddScoped<PhinVariableQueries>();

    public static IEndpointRouteBuilder MapPhinVariables(this IEndpointRouteBuilder routes)
    {
        routes.MapGet(Path, (PhinVariableQueries variables, CancellationToken cancellationToken) =>
                variables.List(cancellationToken))
            .WithName("ListPhinVariables")
            .WithSummary("Every PHIN variable.");

        routes.MapPost(Path, async Task<Results<Created<PhinVariableRow>, ProblemHttpResult>> (
                PhinVariableQueries variables, PhinVariableRow row, CancellationToken cancellationToken) =>
            {
                if ((WhyNotAPhinVariable(row) ?? await variables.WhyNotResolved(row, cancellationToken)) is { } why)
                {
                    return Refused(why);
                }

                if (await variables.Find(row.PhinVar.Trim(), cancellationToken) is not null)
                {
                    return Refused($"the PHIN variable {row.PhinVar.Trim()} is already on file; save over it instead.");
                }

                var saved = await variables.Save(row.PhinVar.Trim(), row, create: true, cancellationToken);
                return TypedResults.Created($"{Path}/{saved.PhinVar}", saved);
            })
            .WithName("CreatePhinVariable")
            .WithSummary("Creates a PHIN variable.");

        routes.MapPut($"{Path}/{{phinVar}}", async Task<Results<Ok<PhinVariableRow>, ProblemHttpResult>> (
                PhinVariableQueries variables, string phinVar, PhinVariableRow row, CancellationToken cancellationToken) =>
            {
                if (await variables.Find(phinVar, cancellationToken) is null)
                {
                    return NoSuch(phinVar);
                }

                if ((WhyNotAPhinVariable(row with { PhinVar = phinVar })
                    ?? await variables.WhyNotResolved(row, cancellationToken)) is { } why)
                {
                    return Refused(why);
                }

                return TypedResults.Ok(await variables.Save(phinVar, row, create: false, cancellationToken));
            })
            .WithName("UpdatePhinVariable")
            .WithSummary("Saves over a PHIN variable.");

        routes.MapDelete($"{Path}/{{phinVar}}", async Task<Results<NoContent, ProblemHttpResult>> (
                PhinVariableQueries variables, string phinVar, CancellationToken cancellationToken) =>
            {
                if (await variables.Find(phinVar, cancellationToken) is null)
                {
                    return NoSuch(phinVar);
                }

                return await variables.Delete(phinVar, cancellationToken) is { } why
                    ? TypedResults.Problem(
                        detail: why, statusCode: StatusCodes.Status409Conflict, title: "The PHIN variable is still in use.")
                    : TypedResults.NoContent();
            })
            .WithName("DeletePhinVariable")
            .WithSummary("Deletes a PHIN variable no guide carries.");

        return routes;
    }

    private static ProblemHttpResult NoSuch(string phinVar) =>
        TypedResults.Problem(
            detail: $"No PHIN variable is named {phinVar}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such PHIN variable.");

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The PHIN variable asked for is not one this endpoint can save.");
}

/// <summary>
/// The PHIN variable queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for the variable catalogue.
/// </summary>
internal sealed class PhinVariableQueries(SurvsysDbContext database)
{
    public async Task<IReadOnlyList<PhinVariableRow>> List(CancellationToken cancellationToken) =>
        (await database.PhinVariables.AsNoTracking().ToListAsync(cancellationToken))
            .OrderBy(variable => variable.PhinVar, StringComparer.Ordinal)
            .Select(Row)
            .ToList();

    public async Task<PhinVariableRow?> Find(string phinVar, CancellationToken cancellationToken) =>
        await database.PhinVariables.AsNoTracking().SingleOrDefaultAsync(item => item.PhinVar == phinVar, cancellationToken)
            is { } variable
            ? Row(variable)
            : null;

    /// <summary>Why the rule set type or configuration the row names is not on file, or null.</summary>
    public async Task<string?> WhyNotResolved(PhinVariableRow row, CancellationToken cancellationToken)
    {
        if (row.RuleSetId is { } type && !await database.RuleSets.AnyAsync(item => item.Id == type, cancellationToken))
        {
            return $"ruleSetId {type} is not a type GET {RuleSetEndpoints.TypesPath} lists.";
        }

        return row.RuleSetConfigId is { } config
            && !await database.RuleSetConfigs.AnyAsync(item => item.Id == config, cancellationToken)
            ? $"ruleSetConfigId {config} is not a rule set configuration on file."
            : null;
    }

    /// <summary>Saves the row as the variable <paramref name="phinVar"/>, adding it where <paramref name="create"/> is set.</summary>
    public async Task<PhinVariableRow> Save(string phinVar, PhinVariableRow row, bool create, CancellationToken cancellationToken)
    {
        var variable = create
            ? database.PhinVariables.Add(new Data.PhinVariable { PhinVar = phinVar, Description = "" }).Entity
            : await database.PhinVariables.SingleAsync(item => item.PhinVar == phinVar, cancellationToken);

        variable.Description = row.Description.Trim();
        variable.Identifier = Blank(row.Identifier);
        variable.Priority = Blank(row.Priority);
        variable.Hl7DataType = Blank(row.Hl7DataType);
        variable.RuleSetId = row.RuleSetId;
        variable.RuleSetConfigId = row.RuleSetConfigId;
        variable.Status = Blank(row.Status);
        variable.Note = Blank(row.Note);
        variable.CodeSystem = Blank(row.CodeSystem);
        await database.SaveChangesAsync(cancellationToken);

        return Row(variable);
    }

    /// <summary>Deletes the variable, or says why not: a guide still carries it.</summary>
    public async Task<string?> Delete(string phinVar, CancellationToken cancellationToken)
    {
        if (await database.GuidePhinVariables.AnyAsync(item => item.PhinVar == phinVar, cancellationToken))
        {
            return $"A guide still carries {phinVar}; take it off the guide first.";
        }

        await database.PhinVariables.Where(item => item.PhinVar == phinVar).ExecuteDeleteAsync(cancellationToken);
        return null;
    }

    private static PhinVariableRow Row(Data.PhinVariable variable) =>
        new(
            variable.PhinVar,
            variable.Description,
            variable.Identifier,
            variable.Priority,
            variable.Hl7DataType,
            variable.RuleSetId,
            variable.RuleSetConfigId,
            variable.Status,
            variable.Note,
            variable.CodeSystem);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
