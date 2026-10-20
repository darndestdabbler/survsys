using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;

namespace Survsys.Api;

/// <summary>
/// <c>/api/vocabulary</c>: the ELR vocabulary intake translates through —
/// master and child LOINC, master organisms, master specimen and result values,
/// and the lab-specific child codes — each listed, created, saved over,
/// deleted, exported as CSV and imported from it; and
/// <c>/api/conditions/{id}/rules</c>, a condition's intake rules read and
/// replaced whole.
/// </summary>
/// <remarks>
/// The five tables differ only in their columns, their key and what they refer
/// to, so each is a <see cref="VocabularyTable{TEntity, TRow}"/> and one set of
/// handlers serves them all. A CSV import is <see cref="ReferenceImport"/>'s,
/// previewed and then confirmed, and upserts by the table's unique key — the
/// LOINC, the SNOMED, the category and concept, or the lab and its code — so an
/// export imported back changes nothing. A child names its master
/// by code, and a master, condition, organism or sending facility not on file is
/// a 400 asked before the write rather than a dangling id. A master a child
/// still names is not deleted. The vocabulary is small reference data, so a
/// table is listed whole rather than paged. The handlers are handed
/// <see cref="VocabularyQueries"/> and not the <c>DbContext</c>, because the
/// architecture test lets no <c>Survsys.Data</c> type into an endpoint
/// signature.
/// </remarks>
public static class VocabularyEndpoints
{
    public const string MasterLoincs = "master-loincs";
    public const string ChildLoincs = "child-loincs";
    public const string Organisms = "organisms";
    public const string MasterValues = "master-values";
    public const string ChildValues = "child-values";

    private const int CodeLength = 20;
    private const int ChildCodeLength = 50;
    private const int TextLength = 200;

    private static readonly string[] MasterCategories = [VocabularyCategory.Specimen, VocabularyCategory.TestResult];

    private static readonly string[] ChildCategories =
        [VocabularyCategory.Specimen, VocabularyCategory.TestResult, VocabularyCategory.Organism];

    private static readonly string[] RuleKinds = [VocabRule.MessageFilterKind, VocabRule.CaseManagementKind];

    private static readonly string[] RuleFields =
    [
        IntakeTranslator.LoincField,
        IntakeTranslator.TestResultField,
        IntakeTranslator.ResultValueField,
        IntakeTranslator.OrganismField,
        IntakeTranslator.SpecimenField,
        IntakeTranslator.DaysSinceCollectionField,
    ];

    private static readonly string[] Operators = ["=", "<>", ">", "<", ">=", "<="];

    /// <summary>Why this is not a master LOINC this tier will save, or null when it is one.</summary>
    public static string? WhyNotAMasterLoinc(VocabMasterLoincRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return Required("loinc", row.Loinc, CodeLength) ?? TooLong("conceptName", row.ConceptName, TextLength);
    }

    /// <summary>Why this is not a child LOINC this tier will save, or null when it is one.</summary>
    public static string? WhyNotAChildLoinc(VocabChildLoincRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return (row.LabId <= 0 ? "labId is required: the sending facility the code belongs to." : null)
            ?? Required("childLoinc", row.ChildLoinc, ChildCodeLength)
            ?? Required("masterLoinc", row.MasterLoinc, CodeLength)
            ?? TooLong("conceptName", row.ConceptName, TextLength);
    }

    /// <summary>Why this is not a master organism this tier will save, or null when it is one.</summary>
    public static string? WhyNotAnOrganism(VocabMasterOrganismRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return Required("snomed", row.Snomed, CodeLength)
            ?? TooLong("snomedAlt", row.SnomedAlt, CodeLength)
            ?? Required("organismName", row.OrganismName, TextLength);
    }

    /// <summary>Why this is not a master value this tier will save, or null when it is one.</summary>
    public static string? WhyNotAMasterValue(VocabMasterValueRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return OneOf("category", row.Category, MasterCategories)
            ?? Required("concept", row.Concept, TextLength)
            ?? TooLong("appValue", row.AppValue, TextLength);
    }

    /// <summary>Why this is not a child code this tier will save, or null when it is one.</summary>
    public static string? WhyNotAChildValue(VocabChildValueRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return (row.LabId <= 0 ? "labId is required: the sending facility the code belongs to." : null)
            ?? OneOf("category", row.Category, ChildCategories)
            ?? Required("childCode", row.ChildCode, TextLength)
            ?? Required("master", row.Master, TextLength);
    }

    /// <summary>Why these are not a condition's rules this tier will save, or null when they are.</summary>
    public static string? WhyNotRules(IReadOnlyList<VocabRuleRow> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        foreach (var (rule, at) in rules.Select((rule, at) => (rule, at + 1)))
        {
            if (rule is null)
            {
                return $"rule {at} is empty.";
            }

            if (OneOf($"rule {at}'s kind", rule.Kind, RuleKinds) is { } kind)
            {
                return kind;
            }

            foreach (var condition in rule.Conditions ?? [])
            {
                var why = OneOf($"rule {at}'s field", condition.Field, RuleFields)
                    ?? OneOf($"rule {at}'s operator", condition.Operator, Operators)
                    ?? TooLong($"rule {at}'s value", condition.Value, TextLength)
                    ?? (string.IsNullOrWhiteSpace(condition.AndOr)
                        ? null
                        : OneOf($"rule {at}'s and/or", condition.AndOr.ToUpperInvariant(), ["AND", "OR"]));
                if (why is not null)
                {
                    return why;
                }
            }
        }

        return null;
    }

    /// <summary>Registers what the vocabulary endpoints need of the container.</summary>
    public static IServiceCollection AddVocabulary(this IServiceCollection services) =>
        services.AddScoped<VocabularyQueries>();

    public static IEndpointRouteBuilder MapVocabulary(this IEndpointRouteBuilder routes)
    {
        MapTable(routes, MasterLoincs, VocabularyQueries.MasterLoincs, "master LOINC");
        MapTable(routes, ChildLoincs, VocabularyQueries.ChildLoincs, "child LOINC");
        MapTable(routes, Organisms, VocabularyQueries.Organisms, "master organism");
        MapTable(routes, MasterValues, VocabularyQueries.MasterValues, "master value");
        MapTable(routes, ChildValues, VocabularyQueries.ChildValues, "child code");

        routes.MapGet("/api/conditions/{id:int}/rules", GetRules)
            .WithName("GetConditionRules")
            .WithSummary("A condition's intake rules, message filter and case management, in execution order.");

        routes.MapPut("/api/conditions/{id:int}/rules", ReplaceRules)
            .WithName("ReplaceConditionRules")
            .WithSummary("Replaces a condition's intake rules whole.");

        return routes;
    }

    private static void MapTable<TEntity, TRow>(
        IEndpointRouteBuilder routes, string table, VocabularyTable<TEntity, TRow> shape, string noun)
        where TEntity : class
        where TRow : class
    {
        var path = $"/api/vocabulary/{table}";

        routes.MapGet(path, (VocabularyQueries vocabulary, CancellationToken cancellationToken) =>
                vocabulary.List(shape, cancellationToken))
            .WithName($"List-{table}")
            .WithSummary($"Every {noun}.");

        routes.MapPost(path, async Task<Results<Created<TRow>, ProblemHttpResult>> (
                VocabularyQueries vocabulary, TRow row, CancellationToken cancellationToken) =>
            {
                var (saved, why) = await vocabulary.Save(shape, null, row, cancellationToken);
                return saved is null ? Refused(noun, why!) : TypedResults.Created(path, saved);
            })
            .WithName($"Create-{table}")
            .WithSummary($"Creates a {noun}.");

        routes.MapPut($"{path}/{{id:int}}", async Task<Results<Ok<TRow>, ProblemHttpResult>> (
                VocabularyQueries vocabulary, int id, TRow row, CancellationToken cancellationToken) =>
            {
                if (!await vocabulary.Exists(shape, id, cancellationToken))
                {
                    return NoSuch(noun, id);
                }

                var (saved, why) = await vocabulary.Save(shape, id, row, cancellationToken);
                return saved is null ? Refused(noun, why!) : TypedResults.Ok(saved);
            })
            .WithName($"Update-{table}")
            .WithSummary($"Saves over a {noun}.");

        routes.MapDelete($"{path}/{{id:int}}", async Task<Results<NoContent, ProblemHttpResult>> (
                VocabularyQueries vocabulary, int id, CancellationToken cancellationToken) =>
            {
                if (!await vocabulary.Exists(shape, id, cancellationToken))
                {
                    return NoSuch(noun, id);
                }

                return await vocabulary.Delete(shape, id, cancellationToken) is { } why
                    ? TypedResults.Problem(
                        detail: why,
                        statusCode: StatusCodes.Status409Conflict,
                        title: $"The {noun} is still in use.")
                    : TypedResults.NoContent();
            })
            .WithName($"Delete-{table}")
            .WithSummary($"Deletes a {noun} nothing names.");

        routes.MapGet($"{path}/csv", async (VocabularyQueries vocabulary, CancellationToken cancellationToken) =>
                TypedResults.Text(await vocabulary.Export(shape, cancellationToken), "text/csv; charset=utf-8"))
            .WithName($"Export-{table}")
            .WithSummary($"Every {noun}, as CSV.");

        routes.MapPost($"{path}/csv", async (
                VocabularyQueries vocabulary,
                HttpRequest request,
                CancellationToken cancellationToken,
                bool confirm = false) =>
            {
                using var reader = new StreamReader(request.Body);
                var csv = await reader.ReadToEndAsync(cancellationToken);
                return TypedResults.Ok(await vocabulary.Import(shape, csv, confirm, cancellationToken));
            })
            .WithName($"Import-{table}")
            .WithSummary($"Previews, or with confirm=true saves, each {noun} of a CSV file, added or saved over by its key.");
    }

    private static async Task<Results<Ok<IReadOnlyList<VocabRuleRow>>, ProblemHttpResult>> GetRules(
        VocabularyQueries vocabulary, int id, CancellationToken cancellationToken = default) =>
        await vocabulary.Rules(id, cancellationToken) is { } rules
            ? TypedResults.Ok(rules)
            : NoSuch("condition", id);

    private static async Task<Results<Ok<IReadOnlyList<VocabRuleRow>>, ProblemHttpResult>> ReplaceRules(
        VocabularyQueries vocabulary,
        int id,
        IReadOnlyList<VocabRuleRow> rules,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotRules(rules) is { } why)
        {
            return Refused("set of rules", why);
        }

        return await vocabulary.ReplaceRules(id, rules, cancellationToken) is { } saved
            ? TypedResults.Ok(saved)
            : NoSuch("condition", id);
    }

    private static string? Required(string field, string? value, int length) =>
        string.IsNullOrWhiteSpace(value) ? $"{field} is required." : TooLong(field, value, length);

    private static string? TooLong(string field, string? value, int length) =>
        value?.Trim().Length > length ? $"{field} is longer than {length} characters." : null;

    private static string? OneOf(string field, string? value, string[] allowed) =>
        allowed.Contains(value?.Trim(), StringComparer.Ordinal)
            ? null
            : $"{field} '{value}' is not one of {string.Join(", ", allowed)}.";

    private static ProblemHttpResult NoSuch(string noun, int id) =>
        TypedResults.Problem(
            detail: $"No {noun} has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: $"There is no such {noun}.");

    private static ProblemHttpResult Refused(string noun, string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: $"The {noun} asked for is not one this endpoint can save.");
}

/// <summary>
/// What distinguishes one vocabulary table from another beyond its CSV form:
/// its id, what it refers to and what refers to it. The handling common to
/// all five is <see cref="VocabularyQueries"/>'s.
/// </summary>
internal abstract class VocabularyTable<TEntity, TRow> : ReferenceTable<TEntity, TRow>
    where TEntity : class
    where TRow : class
{
    public abstract int IdOf(TEntity entity);

    /// <summary>The row with this id, read as the list reads it.</summary>
    public abstract Task<TRow?> Find(SurvsysDbContext database, int id, CancellationToken cancellationToken);

    public override async Task<TRow> RowOf(
        SurvsysDbContext database, TEntity entity, CancellationToken cancellationToken) =>
        (await Find(database, IdOf(entity), cancellationToken))!;

    /// <summary>Why the entry cannot be deleted, because something names it, or null.</summary>
    public abstract Task<string?> WhyNotDeleted(
        SurvsysDbContext database, TEntity entity, CancellationToken cancellationToken);

    protected static async Task<string?> Condition(
        SurvsysDbContext database, int? conditionId, CancellationToken cancellationToken) =>
        conditionId is { } id && !await database.Conditions.AnyAsync(condition => condition.Id == id, cancellationToken)
            ? $"conditionId {id} is not a condition GET /api/conditions lists."
            : null;

    protected static async Task<string?> Facility(
        SurvsysDbContext database, int labId, CancellationToken cancellationToken) =>
        await database.Facilities.AnyAsync(facility => facility.Id == labId, cancellationToken)
            ? null
            : $"labId {labId} is not a facility GET /api/facilities lists.";
}

/// <summary>
/// The vocabulary queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for the vocabulary and the rules.
/// </summary>
internal sealed class VocabularyQueries(SurvsysDbContext database)
{
    public static readonly VocabularyTable<VocabMasterLoinc, VocabMasterLoincRow> MasterLoincs = new MasterLoincTable();
    public static readonly VocabularyTable<VocabChildLoinc, VocabChildLoincRow> ChildLoincs = new ChildLoincTable();
    public static readonly VocabularyTable<VocabMasterOrganism, VocabMasterOrganismRow> Organisms = new OrganismTable();
    public static readonly VocabularyTable<VocabMasterValue, VocabMasterValueRow> MasterValues = new MasterValueTable();
    public static readonly VocabularyTable<VocabChildValue, VocabChildValueRow> ChildValues = new ChildValueTable();

    public async Task<IReadOnlyList<TRow>> List<TEntity, TRow>(
        VocabularyTable<TEntity, TRow> table, CancellationToken cancellationToken)
        where TEntity : class
        where TRow : class =>
        await table.List(database, cancellationToken);

    public async Task<bool> Exists<TEntity, TRow>(
        VocabularyTable<TEntity, TRow> table, int id, CancellationToken cancellationToken)
        where TEntity : class
        where TRow : class =>
        await database.Set<TEntity>().FindAsync([id], cancellationToken) is not null;

    /// <summary>
    /// Saves the row as a new entry, or over the entry <paramref name="id"/>,
    /// answering the saved row or why it was not saved.
    /// </summary>
    public async Task<(TRow? Saved, string? Why)> Save<TEntity, TRow>(
        VocabularyTable<TEntity, TRow> table, int? id, TRow row, CancellationToken cancellationToken)
        where TEntity : class
        where TRow : class
    {
        if (table.WhyNot(row) is { } why)
        {
            return (null, why);
        }

        if (await table.ByKey(database, row, cancellationToken) is { } holder && table.IdOf(holder) != id)
        {
            return (null, $"another entry, id {table.IdOf(holder)}, already has this key; save over that one instead.");
        }

        var entity = id is { } existing
            ? (await database.Set<TEntity>().FindAsync([existing], cancellationToken))!
            : table.New(row);

        if (await table.Apply(database, entity, row, cancellationToken) is { } unresolved)
        {
            return (null, unresolved);
        }

        if (id is null)
        {
            database.Add(entity);
        }

        await database.SaveChangesAsync(cancellationToken);

        return (await table.Find(database, table.IdOf(entity), cancellationToken), null);
    }

    public async Task<string?> Delete<TEntity, TRow>(
        VocabularyTable<TEntity, TRow> table, int id, CancellationToken cancellationToken)
        where TEntity : class
        where TRow : class
    {
        var entity = (await database.Set<TEntity>().FindAsync([id], cancellationToken))!;

        if (await table.WhyNotDeleted(database, entity, cancellationToken) is { } why)
        {
            return why;
        }

        database.Remove(entity);
        await database.SaveChangesAsync(cancellationToken);
        return null;
    }

    public Task<string> Export<TEntity, TRow>(
        VocabularyTable<TEntity, TRow> table, CancellationToken cancellationToken)
        where TEntity : class
        where TRow : class =>
        ReferenceImport.Export(database, table, cancellationToken);

    /// <summary>What importing a CSV file would do, written only where <paramref name="confirm"/> is true.</summary>
    public Task<ReferenceImportResponse> Import<TEntity, TRow>(
        VocabularyTable<TEntity, TRow> table, string? csv, bool confirm, CancellationToken cancellationToken)
        where TEntity : class
        where TRow : class =>
        ReferenceImport.Import(database, table, csv, confirm, cancellationToken);

    /// <summary>A condition's rules in execution order, or null where there is no such condition.</summary>
    public async Task<IReadOnlyList<VocabRuleRow>?> Rules(int conditionId, CancellationToken cancellationToken)
    {
        if (!await database.Conditions.AnyAsync(condition => condition.Id == conditionId, cancellationToken))
        {
            return null;
        }

        var rules = await database.VocabRules
            .Where(rule => rule.ConditionId == conditionId)
            .OrderBy(rule => rule.ExecutionOrder)
            .ThenBy(rule => rule.Id)
            .ToListAsync(cancellationToken);
        var ruleIds = rules.Select(rule => rule.Id).ToList();
        var conditions = (await database.VocabRuleConditions
                .Where(condition => ruleIds.Contains(condition.VocabRuleId))
                .OrderBy(condition => condition.Id)
                .ToListAsync(cancellationToken))
            .ToLookup(condition => condition.VocabRuleId);

        return rules
            .Select(rule => new VocabRuleRow(
                rule.Id,
                rule.Kind,
                rule.ExecutionOrder,
                rule.AllowNewCase,
                rule.AllowUpdateCase,
                rule.IsSurveillance,
                conditions[rule.Id]
                    .Select(condition => new VocabRuleConditionRow(
                        condition.Field, condition.Operator, condition.Value, condition.AndOr))
                    .ToList()))
            .ToList();
    }

    /// <summary>
    /// Replaces a condition's rules whole, in one transaction, or answers null
    /// where there is no such condition.
    /// </summary>
    public async Task<IReadOnlyList<VocabRuleRow>?> ReplaceRules(
        int conditionId, IReadOnlyList<VocabRuleRow> rules, CancellationToken cancellationToken)
    {
        if (!await database.Conditions.AnyAsync(condition => condition.Id == conditionId, cancellationToken))
        {
            return null;
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var oldIds = await database.VocabRules
            .Where(rule => rule.ConditionId == conditionId)
            .Select(rule => rule.Id)
            .ToListAsync(cancellationToken);
        await database.VocabRuleConditions
            .Where(condition => oldIds.Contains(condition.VocabRuleId))
            .ExecuteDeleteAsync(cancellationToken);
        await database.VocabRules.Where(rule => oldIds.Contains(rule.Id)).ExecuteDeleteAsync(cancellationToken);

        foreach (var row in rules)
        {
            var rule = new VocabRule
            {
                ConditionId = conditionId,
                Kind = row.Kind.Trim(),
                ExecutionOrder = row.ExecutionOrder,
                AllowNewCase = row.AllowNewCase,
                AllowUpdateCase = row.AllowUpdateCase,
                IsSurveillance = row.IsSurveillance,
            };
            database.VocabRules.Add(rule);
            await database.SaveChangesAsync(cancellationToken);

            database.VocabRuleConditions.AddRange((row.Conditions ?? []).Select(condition => new VocabRuleCondition
            {
                VocabRuleId = rule.Id,
                Field = condition.Field.Trim(),
                Operator = condition.Operator.Trim(),
                Value = string.IsNullOrWhiteSpace(condition.Value) ? null : condition.Value.Trim(),
                AndOr = string.IsNullOrWhiteSpace(condition.AndOr) ? null : condition.AndOr.Trim().ToUpperInvariant(),
            }));
            await database.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return await Rules(conditionId, cancellationToken);
    }
}

/// <summary>Master LOINC, keyed by the LOINC; a child LOINC naming it keeps it.</summary>
internal sealed class MasterLoincTable : VocabularyTable<VocabMasterLoinc, VocabMasterLoincRow>
{
    public override IReadOnlyList<string> Columns => VocabularyCsv.MasterLoincColumns;

    public override int IdOf(VocabMasterLoinc entity) => entity.Id;

    private static IQueryable<VocabMasterLoincRow> Rows(SurvsysDbContext database, int? id = null) =>
        database.VocabMasterLoincs
            .Where(master => id == null || master.Id == id)
            .Select(master => new VocabMasterLoincRow(
            master.Id, master.Loinc, master.ConceptName, master.ConditionId, master.OrganismId, master.OrganismFromResult));

    public override async Task<List<VocabMasterLoincRow>> List(
        SurvsysDbContext database, CancellationToken cancellationToken) =>
        (await Rows(database).ToListAsync(cancellationToken))
            .OrderBy(row => row.Loinc)
            .ToList();

    public override Task<VocabMasterLoincRow?> Find(SurvsysDbContext database, int id, CancellationToken cancellationToken) =>
        Rows(database, id).SingleOrDefaultAsync(cancellationToken);

    public override IReadOnlyList<string?> Fields(VocabMasterLoincRow row) => VocabularyCsv.Fields(row);

    public override (VocabMasterLoincRow? Row, string? Why) Parse(CsvRow row) => VocabularyCsv.MasterLoinc(row);

    public override string? WhyNot(VocabMasterLoincRow row) => VocabularyEndpoints.WhyNotAMasterLoinc(row);

    public override string KeyOf(VocabMasterLoincRow row) => row.Loinc.Trim();

    public override Task<VocabMasterLoinc?> ByKey(
        SurvsysDbContext database, VocabMasterLoincRow row, CancellationToken cancellationToken) =>
        database.VocabMasterLoincs.SingleOrDefaultAsync(master => master.Loinc == row.Loinc.Trim(), cancellationToken);

    public override VocabMasterLoinc New(VocabMasterLoincRow row) => new() { Loinc = row.Loinc.Trim() };

    public override async Task<string?> Apply(
        SurvsysDbContext database, VocabMasterLoinc entity, VocabMasterLoincRow row, CancellationToken cancellationToken)
    {
        if (await Condition(database, row.ConditionId, cancellationToken) is { } why)
        {
            return why;
        }

        if (row.OrganismId is { } organismId
            && !await database.VocabMasterOrganisms.AnyAsync(organism => organism.Id == organismId, cancellationToken))
        {
            return $"organismId {organismId} is not a master organism GET /api/vocabulary/organisms lists.";
        }

        entity.Loinc = row.Loinc.Trim();
        entity.ConceptName = Blank(row.ConceptName);
        entity.ConditionId = row.ConditionId;
        entity.OrganismId = row.OrganismId;
        entity.OrganismFromResult = row.OrganismFromResult;
        return null;
    }

    public override async Task<string?> WhyNotDeleted(
        SurvsysDbContext database, VocabMasterLoinc entity, CancellationToken cancellationToken) =>
        await database.VocabChildLoincs.AnyAsync(child => child.MasterLoincId == entity.Id, cancellationToken)
            ? $"A child LOINC resolves to {entity.Loinc}; delete or move it first."
            : null;
}

/// <summary>Child LOINC, keyed by the lab and its code; it names its master by LOINC.</summary>
internal sealed class ChildLoincTable : VocabularyTable<VocabChildLoinc, VocabChildLoincRow>
{
    public override IReadOnlyList<string> Columns => VocabularyCsv.ChildLoincColumns;

    public override int IdOf(VocabChildLoinc entity) => entity.Id;

    private static IQueryable<VocabChildLoincRow> Rows(SurvsysDbContext database, int? id = null) =>
        database.VocabChildLoincs
            .Where(child => id == null || child.Id == id)
            .Join(
            database.VocabMasterLoincs,
            child => child.MasterLoincId,
            master => master.Id,
            (child, master) => new VocabChildLoincRow(
                child.Id, child.LabId, child.ChildLoinc, master.Loinc, child.ConceptName, child.Archived));

    public override async Task<List<VocabChildLoincRow>> List(
        SurvsysDbContext database, CancellationToken cancellationToken) =>
        (await Rows(database).ToListAsync(cancellationToken))
            .OrderBy(row => row.LabId)
            .ThenBy(row => row.ChildLoinc)
            .ToList();

    public override Task<VocabChildLoincRow?> Find(SurvsysDbContext database, int id, CancellationToken cancellationToken) =>
        Rows(database, id).SingleOrDefaultAsync(cancellationToken);

    public override IReadOnlyList<string?> Fields(VocabChildLoincRow row) => VocabularyCsv.Fields(row);

    public override (VocabChildLoincRow? Row, string? Why) Parse(CsvRow row) => VocabularyCsv.ChildLoinc(row);

    public override string? WhyNot(VocabChildLoincRow row) => VocabularyEndpoints.WhyNotAChildLoinc(row);

    public override string KeyOf(VocabChildLoincRow row) => $"{row.LabId} {row.ChildLoinc.Trim()}";

    public override Task<VocabChildLoinc?> ByKey(
        SurvsysDbContext database, VocabChildLoincRow row, CancellationToken cancellationToken) =>
        database.VocabChildLoincs.SingleOrDefaultAsync(
            child => child.LabId == row.LabId && child.ChildLoinc == row.ChildLoinc.Trim(), cancellationToken);

    public override VocabChildLoinc New(VocabChildLoincRow row) => new() { ChildLoinc = row.ChildLoinc.Trim() };

    public override async Task<string?> Apply(
        SurvsysDbContext database, VocabChildLoinc entity, VocabChildLoincRow row, CancellationToken cancellationToken)
    {
        if (await Facility(database, row.LabId, cancellationToken) is { } why)
        {
            return why;
        }

        var masterLoinc = row.MasterLoinc.Trim();
        var masterId = await database.VocabMasterLoincs
            .Where(master => master.Loinc == masterLoinc)
            .Select(master => (int?)master.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (masterId is null)
        {
            return $"masterLoinc {masterLoinc} is not a master LOINC GET /api/vocabulary/master-loincs lists.";
        }

        entity.LabId = row.LabId;
        entity.ChildLoinc = row.ChildLoinc.Trim();
        entity.MasterLoincId = masterId.Value;
        entity.ConceptName = Blank(row.ConceptName);
        entity.Archived = row.Archived;
        return null;
    }

    public override Task<string?> WhyNotDeleted(
        SurvsysDbContext database, VocabChildLoinc entity, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}

/// <summary>Master organisms, keyed by the SNOMED; a master LOINC or child code naming one keeps it.</summary>
internal sealed class OrganismTable : VocabularyTable<VocabMasterOrganism, VocabMasterOrganismRow>
{
    public override IReadOnlyList<string> Columns => VocabularyCsv.OrganismColumns;

    public override int IdOf(VocabMasterOrganism entity) => entity.Id;

    private static IQueryable<VocabMasterOrganismRow> Rows(SurvsysDbContext database, int? id = null) =>
        database.VocabMasterOrganisms
            .Where(organism => id == null || organism.Id == id)
            .Select(organism => new VocabMasterOrganismRow(
            organism.Id, organism.Snomed, organism.SnomedAlt, organism.OrganismName, organism.ConditionId));

    public override async Task<List<VocabMasterOrganismRow>> List(
        SurvsysDbContext database, CancellationToken cancellationToken) =>
        (await Rows(database).ToListAsync(cancellationToken))
            .OrderBy(row => row.OrganismName)
            .ThenBy(row => row.Snomed)
            .ToList();

    public override Task<VocabMasterOrganismRow?> Find(
        SurvsysDbContext database, int id, CancellationToken cancellationToken) =>
        Rows(database, id).SingleOrDefaultAsync(cancellationToken);

    public override IReadOnlyList<string?> Fields(VocabMasterOrganismRow row) => VocabularyCsv.Fields(row);

    public override (VocabMasterOrganismRow? Row, string? Why) Parse(CsvRow row) => VocabularyCsv.Organism(row);

    public override string? WhyNot(VocabMasterOrganismRow row) => VocabularyEndpoints.WhyNotAnOrganism(row);

    public override string KeyOf(VocabMasterOrganismRow row) => row.Snomed.Trim();

    public override Task<VocabMasterOrganism?> ByKey(
        SurvsysDbContext database, VocabMasterOrganismRow row, CancellationToken cancellationToken) =>
        database.VocabMasterOrganisms.SingleOrDefaultAsync(
            organism => organism.Snomed == row.Snomed.Trim(), cancellationToken);

    public override VocabMasterOrganism New(VocabMasterOrganismRow row) =>
        new() { Snomed = row.Snomed.Trim(), OrganismName = row.OrganismName.Trim() };

    public override async Task<string?> Apply(
        SurvsysDbContext database,
        VocabMasterOrganism entity,
        VocabMasterOrganismRow row,
        CancellationToken cancellationToken)
    {
        if (await Condition(database, row.ConditionId, cancellationToken) is { } why)
        {
            return why;
        }

        entity.Snomed = row.Snomed.Trim();
        entity.SnomedAlt = Blank(row.SnomedAlt);
        entity.OrganismName = row.OrganismName.Trim();
        entity.ConditionId = row.ConditionId;
        return null;
    }

    public override async Task<string?> WhyNotDeleted(
        SurvsysDbContext database, VocabMasterOrganism entity, CancellationToken cancellationToken)
    {
        if (await database.VocabMasterLoincs.AnyAsync(master => master.OrganismId == entity.Id, cancellationToken))
        {
            return $"A master LOINC maps to {entity.OrganismName}; change it first.";
        }

        return await database.VocabChildValues.AnyAsync(
                child => child.Category == VocabularyCategory.Organism && child.MasterId == entity.Id,
                cancellationToken)
            ? $"A lab's child organism code resolves to {entity.OrganismName}; delete or move it first."
            : null;
    }
}

/// <summary>Master specimen and result values, keyed by category and concept; a child code naming one keeps it.</summary>
internal sealed class MasterValueTable : VocabularyTable<VocabMasterValue, VocabMasterValueRow>
{
    public override IReadOnlyList<string> Columns => VocabularyCsv.MasterValueColumns;

    public override int IdOf(VocabMasterValue entity) => entity.Id;

    private static IQueryable<VocabMasterValueRow> Rows(SurvsysDbContext database, int? id = null) =>
        database.VocabMasterValues
            .Where(master => id == null || master.Id == id)
            .Select(master => new VocabMasterValueRow(
            master.Id, master.Category, master.Concept, master.AppValue));

    public override async Task<List<VocabMasterValueRow>> List(
        SurvsysDbContext database, CancellationToken cancellationToken) =>
        (await Rows(database).ToListAsync(cancellationToken))
            .OrderBy(row => row.Category)
            .ThenBy(row => row.Concept)
            .ToList();

    public override Task<VocabMasterValueRow?> Find(SurvsysDbContext database, int id, CancellationToken cancellationToken) =>
        Rows(database, id).SingleOrDefaultAsync(cancellationToken);

    public override IReadOnlyList<string?> Fields(VocabMasterValueRow row) => VocabularyCsv.Fields(row);

    public override (VocabMasterValueRow? Row, string? Why) Parse(CsvRow row) => VocabularyCsv.MasterValue(row);

    public override string? WhyNot(VocabMasterValueRow row) => VocabularyEndpoints.WhyNotAMasterValue(row);

    public override string KeyOf(VocabMasterValueRow row) => $"{row.Category.Trim()} {row.Concept.Trim()}";

    public override Task<VocabMasterValue?> ByKey(
        SurvsysDbContext database, VocabMasterValueRow row, CancellationToken cancellationToken) =>
        database.VocabMasterValues.SingleOrDefaultAsync(
            master => master.Category == row.Category.Trim() && master.Concept == row.Concept.Trim(),
            cancellationToken);

    public override VocabMasterValue New(VocabMasterValueRow row) =>
        new() { Category = row.Category.Trim(), Concept = row.Concept.Trim() };

    public override async Task<string?> Apply(
        SurvsysDbContext database, VocabMasterValue entity, VocabMasterValueRow row, CancellationToken cancellationToken)
    {
        var category = row.Category.Trim();

        if (entity.Id != 0
            && category != entity.Category
            && await database.VocabChildValues.AnyAsync(
                child => child.Category == entity.Category && child.MasterId == entity.Id, cancellationToken))
        {
            return "a lab's child code resolves to this value, so its category cannot change.";
        }

        entity.Category = category;
        entity.Concept = row.Concept.Trim();
        entity.AppValue = Blank(row.AppValue);
        return null;
    }

    public override async Task<string?> WhyNotDeleted(
        SurvsysDbContext database, VocabMasterValue entity, CancellationToken cancellationToken) =>
        await database.VocabChildValues.AnyAsync(
                child => child.Category == entity.Category && child.MasterId == entity.Id, cancellationToken)
            ? $"A lab's child code resolves to {entity.Concept}; delete or move it first."
            : null;
}

/// <summary>
/// The lab-specific child codes, keyed by the lab, category and code; each
/// names its master by concept, or for an organism by SNOMED.
/// </summary>
internal sealed class ChildValueTable : VocabularyTable<VocabChildValue, VocabChildValueRow>
{
    public override IReadOnlyList<string> Columns => VocabularyCsv.ChildValueColumns;

    public override int IdOf(VocabChildValue entity) => entity.Id;

    private static IQueryable<VocabChildValueRow> Rows(SurvsysDbContext database, int? id = null) =>
        database.VocabChildValues
            .Where(child => id == null || child.Id == id)
            .Select(child => new VocabChildValueRow(
            child.Id,
            child.LabId,
            child.Category,
            child.ChildCode,
            (child.Category == VocabularyCategory.Organism
                ? database.VocabMasterOrganisms
                    .Where(organism => organism.Id == child.MasterId)
                    .Select(organism => organism.Snomed)
                    .FirstOrDefault()
                : database.VocabMasterValues
                    .Where(master => master.Id == child.MasterId)
                    .Select(master => master.Concept)
                    .FirstOrDefault()) ?? ""));

    public override async Task<List<VocabChildValueRow>> List(
        SurvsysDbContext database, CancellationToken cancellationToken) =>
        (await Rows(database).ToListAsync(cancellationToken))
            .OrderBy(row => row.LabId)
            .ThenBy(row => row.Category)
            .ThenBy(row => row.ChildCode)
            .ToList();

    public override Task<VocabChildValueRow?> Find(SurvsysDbContext database, int id, CancellationToken cancellationToken) =>
        Rows(database, id).SingleOrDefaultAsync(cancellationToken);

    public override IReadOnlyList<string?> Fields(VocabChildValueRow row) => VocabularyCsv.Fields(row);

    public override (VocabChildValueRow? Row, string? Why) Parse(CsvRow row) => VocabularyCsv.ChildValue(row);

    public override string? WhyNot(VocabChildValueRow row) => VocabularyEndpoints.WhyNotAChildValue(row);

    public override string KeyOf(VocabChildValueRow row) =>
        $"{row.LabId} {row.Category.Trim()} {row.ChildCode.Trim()}";

    public override Task<VocabChildValue?> ByKey(
        SurvsysDbContext database, VocabChildValueRow row, CancellationToken cancellationToken) =>
        database.VocabChildValues.SingleOrDefaultAsync(
            child => child.LabId == row.LabId
                && child.Category == row.Category.Trim()
                && child.ChildCode == row.ChildCode.Trim(),
            cancellationToken);

    public override VocabChildValue New(VocabChildValueRow row) =>
        new() { Category = row.Category.Trim(), ChildCode = row.ChildCode.Trim() };

    public override async Task<string?> Apply(
        SurvsysDbContext database, VocabChildValue entity, VocabChildValueRow row, CancellationToken cancellationToken)
    {
        if (await Facility(database, row.LabId, cancellationToken) is { } why)
        {
            return why;
        }

        var category = row.Category.Trim();
        var master = row.Master.Trim();
        var masterId = category == VocabularyCategory.Organism
            ? await database.VocabMasterOrganisms
                .Where(organism => organism.Snomed == master)
                .Select(organism => (int?)organism.Id)
                .SingleOrDefaultAsync(cancellationToken)
            : await database.VocabMasterValues
                .Where(value => value.Category == category && value.Concept == master)
                .Select(value => (int?)value.Id)
                .SingleOrDefaultAsync(cancellationToken);

        if (masterId is null)
        {
            return category == VocabularyCategory.Organism
                ? $"master {master} is not a master organism's SNOMED GET /api/vocabulary/organisms lists."
                : $"master {master} is not a {category} concept GET /api/vocabulary/master-values lists.";
        }

        entity.LabId = row.LabId;
        entity.Category = category;
        entity.ChildCode = row.ChildCode.Trim();
        entity.MasterId = masterId.Value;
        return null;
    }

    public override Task<string?> WhyNotDeleted(
        SurvsysDbContext database, VocabChildValue entity, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}
