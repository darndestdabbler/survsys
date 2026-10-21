using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/notification-setup/rule-set-types</c>, the seven rule set types; and
/// <c>/api/notification-setup/rule-sets</c>, a rule set configuration created,
/// read and replaced whole.
/// </summary>
/// <remarks>
/// The legacy NMI rule set configuration described in
/// <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>. A configuration and its
/// fields, parameters, value maps and expressions are one document here, read
/// and written together, because a screen edits them together and the
/// formatters read them together. The handlers are handed
/// <see cref="RuleSetQueries"/> and not the <c>DbContext</c>, as every
/// endpoint here is.
/// </remarks>
public static class RuleSetEndpoints
{
    public const string TypesPath = "/api/notification-setup/rule-set-types";
    public const string Path = "/api/notification-setup/rule-sets";

    private const int FormatTypeLength = 50;
    private const int ValueSetNameLength = 200;
    private const int FieldLength = 200;
    private const int DataNameLength = 100;
    private const int DataValueLength = 1000;
    private const int TextLength = 500;

    private static readonly string[] Operators = ["=", "<>", ">", "<", ">=", "<="];

    /// <summary>Why this is not a rule set configuration this tier will save, or null when it is one.</summary>
    public static string? WhyNotARuleSet(RuleSetConfigRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var why = VocabularyEndpoints.TooLong("formatType", row.FormatType, FormatTypeLength)
            ?? VocabularyEndpoints.TooLong("valueSetName", row.ValueSetName, ValueSetNameLength);

        foreach (var (field, at) in (row.Fields ?? []).Select((field, at) => (field, at + 1)))
        {
            why ??= VocabularyEndpoints.Required($"field {at}", field, FieldLength);
        }

        foreach (var data in row.Data ?? [])
        {
            why ??= VocabularyEndpoints.Required("a parameter's name", data?.Name, DataNameLength)
                ?? VocabularyEndpoints.TooLong($"parameter {data!.Name}", data.Value, DataValueLength);
        }

        foreach (var (map, at) in (row.ValueMaps ?? []).Select((map, at) => (map, at + 1)))
        {
            why ??= map is null
                ? $"value map {at} is empty."
                : VocabularyEndpoints.TooLong($"value map {at}'s from", map.ValueFrom, TextLength)
                    ?? VocabularyEndpoints.TooLong($"value map {at}'s to", map.ValueTo, TextLength)
                    ?? VocabularyEndpoints.TooLong($"value map {at}'s other specified", map.OtherSpecified, TextLength);
        }

        foreach (var (expression, at) in (row.Expressions ?? []).Select((expression, at) => (expression, at + 1)))
        {
            why ??= expression is null
                ? $"expression {at} is empty."
                : VocabularyEndpoints.TooLong($"expression {at}'s value", expression.Value, TextLength)
                    ?? VocabularyEndpoints.TooLong($"expression {at}'s other specified", expression.OtherSpecified, TextLength)
                    ?? WhyNotConditions(at, expression.Conditions ?? []);
        }

        if (why is null && row.Data?.GroupBy(data => data.Name.Trim()).FirstOrDefault(group => group.Count() > 1) is { } twice)
        {
            why = $"the parameter {twice.Key} is given twice.";
        }

        return why;
    }

    private static string? WhyNotConditions(int at, IReadOnlyList<RuleSetExpressionConditionRow> conditions)
    {
        foreach (var condition in conditions)
        {
            var why = condition is null
                ? $"expression {at} has an empty condition."
                : VocabularyEndpoints.TooLong($"expression {at}'s field", condition.Field, FieldLength)
                    ?? (string.IsNullOrWhiteSpace(condition.Operator) || Operators.Contains(condition.Operator.Trim())
                        ? null
                        : $"expression {at}'s operator '{condition.Operator}' is not one of {string.Join(", ", Operators)}.")
                    ?? VocabularyEndpoints.TooLong($"expression {at}'s value", condition.Value, TextLength)
                    ?? (string.IsNullOrWhiteSpace(condition.AndOr)
                        || condition.AndOr.Trim().ToUpperInvariant() is "AND" or "OR"
                        ? null
                        : $"expression {at}'s and/or '{condition.AndOr}' is not AND or OR.");
            if (why is not null)
            {
                return why;
            }
        }

        return null;
    }

    /// <summary>Registers what the rule set endpoints need of the container.</summary>
    public static IServiceCollection AddRuleSets(this IServiceCollection services) =>
        services.AddScoped<RuleSetQueries>();

    public static IEndpointRouteBuilder MapRuleSets(this IEndpointRouteBuilder routes)
    {
        routes.MapGet(TypesPath, (RuleSetQueries ruleSets, CancellationToken cancellationToken) =>
                ruleSets.Types(cancellationToken))
            .WithName("ListRuleSetTypes")
            .WithSummary("The seven rule set types.");

        routes.MapGet($"{Path}/{{id:int}}", async Task<Results<Ok<RuleSetConfigRow>, ProblemHttpResult>> (
                RuleSetQueries ruleSets, int id, CancellationToken cancellationToken) =>
                await ruleSets.Find(id, cancellationToken) is { } row ? TypedResults.Ok(row) : NoSuch(id))
            .WithName("GetRuleSet")
            .WithSummary("A rule set configuration whole.");

        routes.MapPost(Path, async Task<Results<Created<RuleSetConfigRow>, ProblemHttpResult>> (
                RuleSetQueries ruleSets, RuleSetConfigRow row, CancellationToken cancellationToken) =>
            {
                if (WhyNotARuleSet(row) is { } why)
                {
                    return Refused(why);
                }

                var saved = await ruleSets.Save(null, row, cancellationToken);
                return TypedResults.Created($"{Path}/{saved!.Id}", saved);
            })
            .WithName("CreateRuleSet")
            .WithSummary("Creates a rule set configuration.");

        routes.MapPut($"{Path}/{{id:int}}", async Task<Results<Ok<RuleSetConfigRow>, ProblemHttpResult>> (
                RuleSetQueries ruleSets, int id, RuleSetConfigRow row, CancellationToken cancellationToken) =>
            {
                if (WhyNotARuleSet(row) is { } why)
                {
                    return Refused(why);
                }

                return await ruleSets.Save(id, row, cancellationToken) is { } saved ? TypedResults.Ok(saved) : NoSuch(id);
            })
            .WithName("ReplaceRuleSet")
            .WithSummary("Replaces a rule set configuration whole.");

        return routes;
    }

    private static ProblemHttpResult NoSuch(int id) =>
        TypedResults.Problem(
            detail: $"No rule set configuration has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such rule set configuration.");

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The rule set configuration asked for is not one this endpoint can save.");
}

/// <summary>
/// The rule set queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for rule set configurations. A guide's JSON import and
/// export read and write configurations through it too.
/// </summary>
internal sealed class RuleSetQueries(SurvsysDbContext database)
{
    public async Task<IReadOnlyList<RuleSetTypeRow>> Types(CancellationToken cancellationToken) =>
        await database.RuleSets.OrderBy(type => type.Id)
            .Select(type => new RuleSetTypeRow(type.Id, type.Label))
            .ToListAsync(cancellationToken);

    /// <summary>A configuration whole, or null where there is none with this id.</summary>
    public async Task<RuleSetConfigRow?> Find(int id, CancellationToken cancellationToken)
    {
        if (await database.RuleSetConfigs.FindAsync([id], cancellationToken) is not { } config)
        {
            return null;
        }

        var fields = await database.RuleSetFields.Where(field => field.RuleSetConfigId == id)
            .OrderBy(field => field.Id).Select(field => field.Field).ToListAsync(cancellationToken);
        var data = await database.RuleSetData.Where(item => item.RuleSetConfigId == id)
            .OrderBy(item => item.Id).Select(item => new RuleSetDataRow(item.DataName, item.DataValue))
            .ToListAsync(cancellationToken);
        var maps = await database.RuleSetValueMaps.Where(map => map.RuleSetConfigId == id)
            .OrderBy(map => map.Id).Select(map => new RuleSetValueMapRow(map.ValueFrom, map.ValueTo, map.OtherSpecified))
            .ToListAsync(cancellationToken);
        var expressions = await database.RuleSetExpressions.Where(expression => expression.RuleSetConfigId == id)
            .OrderBy(expression => expression.ExecutionOrder).ThenBy(expression => expression.Id)
            .ToListAsync(cancellationToken);
        var expressionIds = expressions.Select(expression => expression.Id).ToList();
        var conditions = (await database.RuleSetExpressionConditions
                .Where(condition => expressionIds.Contains(condition.RuleSetExpressionId))
                .OrderBy(condition => condition.Id).ToListAsync(cancellationToken))
            .ToLookup(condition => condition.RuleSetExpressionId);

        return new RuleSetConfigRow(
            config.Id,
            config.FormatType,
            config.ValueSetName,
            config.Repeating,
            fields,
            data,
            maps,
            expressions.Select(expression => new RuleSetExpressionRow(
                    expression.Value,
                    expression.OtherSpecified,
                    expression.ExecutionOrder,
                    conditions[expression.Id]
                        .Select(condition => new RuleSetExpressionConditionRow(
                            condition.Field, condition.Operator, condition.Value, condition.AndOr, condition.ValueIsField))
                        .ToList()))
                .ToList());
    }

    /// <summary>
    /// Creates a configuration where <paramref name="id"/> is null, or replaces
    /// the one with this id whole, in one transaction; null where there is no
    /// such configuration.
    /// </summary>
    public async Task<RuleSetConfigRow?> Save(int? id, RuleSetConfigRow row, CancellationToken cancellationToken)
    {
        var config = id is { } existing
            ? await database.RuleSetConfigs.FindAsync([existing], cancellationToken)
            : new RuleSetConfig();
        if (config is null)
        {
            return null;
        }

        await using var transaction = database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(cancellationToken)
            : null;

        config.FormatType = Blank(row.FormatType);
        config.ValueSetName = Blank(row.ValueSetName);
        config.Repeating = row.Repeating;
        if (id is null)
        {
            database.RuleSetConfigs.Add(config);
        }

        await database.SaveChangesAsync(cancellationToken);
        await Clear(config.Id, cancellationToken);

        database.RuleSetFields.AddRange((row.Fields ?? [])
            .Select(field => new RuleSetField { RuleSetConfigId = config.Id, Field = field.Trim() }));
        database.RuleSetData.AddRange((row.Data ?? [])
            .Select(item => new RuleSetData { RuleSetConfigId = config.Id, DataName = item.Name.Trim(), DataValue = item.Value }));
        database.RuleSetValueMaps.AddRange((row.ValueMaps ?? [])
            .Select(map => new RuleSetValueMap
            {
                RuleSetConfigId = config.Id, ValueFrom = map.ValueFrom, ValueTo = map.ValueTo, OtherSpecified = Blank(map.OtherSpecified),
            }));
        await database.SaveChangesAsync(cancellationToken);

        foreach (var item in row.Expressions ?? [])
        {
            var expression = new RuleSetExpression
            {
                RuleSetConfigId = config.Id,
                Value = item.Value,
                OtherSpecified = Blank(item.OtherSpecified),
                ExecutionOrder = item.ExecutionOrder,
            };
            database.RuleSetExpressions.Add(expression);
            await database.SaveChangesAsync(cancellationToken);

            database.RuleSetExpressionConditions.AddRange((item.Conditions ?? []).Select(condition => new RuleSetExpressionCondition
            {
                RuleSetExpressionId = expression.Id,
                Field = Blank(condition.Field),
                Operator = Blank(condition.Operator),
                Value = condition.Value,
                AndOr = Blank(condition.AndOr)?.ToUpperInvariant(),
                ValueIsField = condition.ValueIsField,
            }));
            await database.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        database.ChangeTracker.Clear();
        return await Find(config.Id, cancellationToken);
    }

    /// <summary>Whether a configuration with this id is on file.</summary>
    public Task<bool> Exists(int id, CancellationToken cancellationToken) =>
        database.RuleSetConfigs.AnyAsync(config => config.Id == id, cancellationToken);

    private async Task Clear(int configId, CancellationToken cancellationToken)
    {
        var expressionIds = database.RuleSetExpressions
            .Where(expression => expression.RuleSetConfigId == configId).Select(expression => expression.Id);
        await database.RuleSetExpressionConditions
            .Where(condition => expressionIds.Contains(condition.RuleSetExpressionId)).ExecuteDeleteAsync(cancellationToken);
        await database.RuleSetExpressions.Where(item => item.RuleSetConfigId == configId).ExecuteDeleteAsync(cancellationToken);
        await database.RuleSetFields.Where(item => item.RuleSetConfigId == configId).ExecuteDeleteAsync(cancellationToken);
        await database.RuleSetData.Where(item => item.RuleSetConfigId == configId).ExecuteDeleteAsync(cancellationToken);
        await database.RuleSetValueMaps.Where(item => item.RuleSetConfigId == configId).ExecuteDeleteAsync(cancellationToken);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
