using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/notification-setup/guides</c>: the notification guides, listed,
/// created, saved and deleted; a guide's PHIN variables, linked guides and
/// header variables replaced whole; a guide exported as JSON and imported
/// from it; and <c>/api/notification-setup/guide-variable-defaults</c>, the
/// header variables' defaults, as a reference table.
/// </summary>
/// <remarks>
/// The legacy NMI guide maintenance described in
/// <c>../nedss/docs/business-logic/NMI_XML_GENERATION.md</c>, <i>Guide
/// System</i>. A guide's JSON carries its PHIN variables with the rule set
/// that formats each, by type label and configuration, never by this
/// system's ids. An import matches the guide by name and version — creating
/// it where there is none — saves each variable's definition and rule set
/// over what is on file, and replaces the guide's variables with the file's,
/// all in one transaction; so an export imported back changes nothing. A
/// guide a condition is exported under is not deleted.
/// </remarks>
public static class GuideEndpoints
{
    public const string Root = "/api/notification-setup";
    public const string Path = $"{Root}/guides";
    public const string VariableDefaults = "guide-variable-defaults";

    private const int NameLength = 200;
    private const int VersionLength = 100;
    private const int VariableNameLength = 100;
    private const int ValueLength = 500;

    /// <summary>Why this is not a guide this tier will save, or null when it is one.</summary>
    public static string? WhyNotAGuide(GuideRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return VocabularyEndpoints.Required("name", row.Name, NameLength)
            ?? VocabularyEndpoints.TooLong("version", row.Version, VersionLength);
    }

    /// <summary>Why these are not a guide's header variables this tier will save, or null when they are.</summary>
    public static string? WhyNotGuideVariables(IReadOnlyList<GuideVariableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return rows.Any(row => row is null) ? "a variable is empty."
            : rows.GroupBy(row => row.GuideVariableDefaultId).FirstOrDefault(group => group.Count() > 1) is { } twice
                ? $"the variable {twice.Key} is given twice."
            : rows.Select(row => VocabularyEndpoints.TooLong("a variable's value", row.Value, ValueLength))
                .FirstOrDefault(why => why is not null);
    }

    /// <summary>Why this is not a header variable default this tier will save, or null when it is one.</summary>
    public static string? WhyNotAGuideVariableDefault(GuideVariableDefaultRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return VocabularyEndpoints.Required("name", row.Name, VariableNameLength)
            ?? VocabularyEndpoints.TooLong("defaultValue", row.DefaultValue, ValueLength);
    }

    /// <summary>Why this is not a guide's JSON this tier will import, or null when it is one.</summary>
    public static string? WhyNotAGuideExport(GuideExport export)
    {
        ArgumentNullException.ThrowIfNull(export);

        var why = WhyNotAGuide(new GuideRow(0, export.Name, export.Version));
        foreach (var item in export.PhinVars ?? [])
        {
            why ??= item?.Variable is null
                ? "a PHIN variable is empty."
                : PhinVariableEndpoints.WhyNotAPhinVariable(item.Variable)
                    ?? (item.RuleSet is null ? null : RuleSetEndpoints.WhyNotARuleSet(item.RuleSet))
                    ?? (item.RuleSet is not null && string.IsNullOrWhiteSpace(item.RuleSetType)
                        ? $"{item.Variable.PhinVar} has a rule set but no rule set type."
                        : null);
        }

        if (why is null
            && (export.PhinVars ?? []).GroupBy(item => item.Variable.PhinVar.Trim()).FirstOrDefault(group => group.Count() > 1)
                is { } twice)
        {
            why = $"the PHIN variable {twice.Key} is given twice.";
        }

        return why;
    }

    /// <summary>Registers what the guide endpoints need of the container.</summary>
    public static IServiceCollection AddGuides(this IServiceCollection services) =>
        services.AddScoped<GuideQueries>();

    public static IEndpointRouteBuilder MapGuides(this IEndpointRouteBuilder routes)
    {
        VocabularyEndpoints.MapTable(routes, Root, VariableDefaults, GuideTables.VariableDefaults, "guide variable default");

        routes.MapGet(Path, (GuideQueries guides, CancellationToken cancellationToken) => guides.List(cancellationToken))
            .WithName("ListGuides")
            .WithSummary("Every notification guide.");

        routes.MapGet($"{Path}/{{id:int}}", async Task<Results<Ok<GuideDetailResponse>, ProblemHttpResult>> (
                GuideQueries guides, int id, CancellationToken cancellationToken) =>
                await guides.Detail(id, cancellationToken) is { } guide ? TypedResults.Ok(guide) : NoSuch(id))
            .WithName("GetGuide")
            .WithSummary("A notification guide whole.");

        routes.MapPost(Path, async Task<Results<Created<GuideDetailResponse>, ProblemHttpResult>> (
                GuideQueries guides, GuideRow row, CancellationToken cancellationToken) =>
            {
                if ((WhyNotAGuide(row) ?? await guides.WhyNotUnique(null, row, cancellationToken)) is { } why)
                {
                    return Refused(why);
                }

                var created = await guides.Save(null, row, cancellationToken);
                return TypedResults.Created($"{Path}/{created!.Id}", created);
            })
            .WithName("CreateGuide")
            .WithSummary("Creates a notification guide.");

        routes.MapPut($"{Path}/{{id:int}}", async Task<Results<Ok<GuideDetailResponse>, ProblemHttpResult>> (
                GuideQueries guides, int id, GuideRow row, CancellationToken cancellationToken) =>
            {
                if ((WhyNotAGuide(row) ?? await guides.WhyNotUnique(id, row, cancellationToken)) is { } why)
                {
                    return Refused(why);
                }

                return await guides.Save(id, row, cancellationToken) is { } saved ? TypedResults.Ok(saved) : NoSuch(id);
            })
            .WithName("UpdateGuide")
            .WithSummary("Saves a notification guide's name and version.");

        routes.MapDelete($"{Path}/{{id:int}}", async Task<Results<NoContent, ProblemHttpResult>> (
                GuideQueries guides, int id, CancellationToken cancellationToken) =>
            {
                if (await guides.Detail(id, cancellationToken) is null)
                {
                    return NoSuch(id);
                }

                return await guides.Delete(id, cancellationToken) is { } why
                    ? TypedResults.Problem(
                        detail: why, statusCode: StatusCodes.Status409Conflict, title: "The guide is still in use.")
                    : TypedResults.NoContent();
            })
            .WithName("DeleteGuide")
            .WithSummary("Deletes a notification guide no condition is exported under.");

        routes.MapPut($"{Path}/{{id:int}}/phin-variables", async Task<Results<Ok<GuideDetailResponse>, ProblemHttpResult>> (
                GuideQueries guides, int id, IReadOnlyList<string> phinVars, CancellationToken cancellationToken) =>
                await guides.Detail(id, cancellationToken) is null ? NoSuch(id)
                : await guides.ReplacePhinVariables(id, phinVars, cancellationToken) is { } why ? Refused(why)
                : TypedResults.Ok((await guides.Detail(id, cancellationToken))!))
            .WithName("ReplaceGuidePhinVariables")
            .WithSummary("Replaces the PHIN variables a guide carries.");

        routes.MapPut($"{Path}/{{id:int}}/linked-guides", async Task<Results<Ok<GuideDetailResponse>, ProblemHttpResult>> (
                GuideQueries guides, int id, IReadOnlyList<int> linkedGuideIds, CancellationToken cancellationToken) =>
                await guides.Detail(id, cancellationToken) is null ? NoSuch(id)
                : await guides.ReplaceLinks(id, linkedGuideIds, cancellationToken) is { } why ? Refused(why)
                : TypedResults.Ok((await guides.Detail(id, cancellationToken))!))
            .WithName("ReplaceGuideLinks")
            .WithSummary("Replaces the guides a guide links.");

        routes.MapPut($"{Path}/{{id:int}}/variables", async Task<Results<Ok<GuideDetailResponse>, ProblemHttpResult>> (
                GuideQueries guides, int id, IReadOnlyList<GuideVariableRow> variables, CancellationToken cancellationToken) =>
                await guides.Detail(id, cancellationToken) is null ? NoSuch(id)
                : (WhyNotGuideVariables(variables) ?? await guides.ReplaceVariables(id, variables, cancellationToken)) is { } why
                    ? Refused(why)
                : TypedResults.Ok((await guides.Detail(id, cancellationToken))!))
            .WithName("ReplaceGuideVariables")
            .WithSummary("Replaces a guide's own header variable values.");

        routes.MapGet($"{Path}/{{id:int}}/json", async Task<Results<Ok<GuideExport>, ProblemHttpResult>> (
                GuideQueries guides, int id, CancellationToken cancellationToken) =>
                await guides.Export(id, cancellationToken) is { } export ? TypedResults.Ok(export) : NoSuch(id))
            .WithName("ExportGuide")
            .WithSummary("A notification guide as JSON, with its PHIN variables and their rule sets.");

        routes.MapPost($"{Path}/json", async Task<Results<Ok<GuideDetailResponse>, ProblemHttpResult>> (
                GuideQueries guides, GuideExport export, CancellationToken cancellationToken) =>
            {
                if (WhyNotAGuideExport(export) is { } why)
                {
                    return Refused(why);
                }

                var (imported, unresolved) = await guides.Import(export, cancellationToken);
                return imported is null ? Refused(unresolved!) : TypedResults.Ok(imported);
            })
            .WithName("ImportGuide")
            .WithSummary("Imports a notification guide's JSON, matched by name and version.");

        return routes;
    }

    private static ProblemHttpResult NoSuch(int id) =>
        TypedResults.Problem(
            detail: $"No notification guide has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such notification guide.");

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The notification guide asked for is not one this endpoint can save.");
}

/// <summary>
/// The guide queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for the notification guides.
/// </summary>
internal sealed class GuideQueries(SurvsysDbContext database, RuleSetQueries ruleSets)
{
    public async Task<IReadOnlyList<GuideRow>> List(CancellationToken cancellationToken) =>
        (await database.NotificationGuides.AsNoTracking()
            .Select(guide => new GuideRow(guide.Id, guide.Name, guide.Version))
            .ToListAsync(cancellationToken))
        .OrderBy(guide => guide.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(guide => guide.Version, StringComparer.Ordinal)
        .ToList();

    /// <summary>The guide whole, or null where there is none with this id.</summary>
    public async Task<GuideDetailResponse?> Detail(int id, CancellationToken cancellationToken)
    {
        if (await database.NotificationGuides.AsNoTracking().SingleOrDefaultAsync(guide => guide.Id == id, cancellationToken)
            is not { } guide)
        {
            return null;
        }

        var phinVars = (await database.GuidePhinVariables.AsNoTracking()
                .Where(item => item.GuideId == id).Select(item => item.PhinVar).ToListAsync(cancellationToken))
            .Order(StringComparer.Ordinal).ToList();
        var links = await database.GuideLinks.AsNoTracking()
            .Where(item => item.GuideId == id).OrderBy(item => item.LinkedGuideId).Select(item => item.LinkedGuideId)
            .ToListAsync(cancellationToken);
        var own = await database.GuideVariables.AsNoTracking()
            .Where(item => item.GuideId == id)
            .ToDictionaryAsync(item => item.GuideVariableDefaultId, item => item.Value, cancellationToken);
        var variables = await database.GuideVariableDefaults.AsNoTracking().OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return new GuideDetailResponse(
            guide.Id,
            guide.Name,
            guide.Version,
            phinVars,
            links,
            variables.Select(item => new GuideVariableRow(item.Id, item.Name, own.GetValueOrDefault(item.Id), item.DefaultValue))
                .ToList());
    }

    /// <summary>Why another guide already has this name and version, or null.</summary>
    public async Task<string?> WhyNotUnique(int? id, GuideRow row, CancellationToken cancellationToken) =>
        await Matching(row.Name, row.Version, cancellationToken) is { } holder && holder.Id != id
            ? $"another guide, id {holder.Id}, already has this name and version."
            : null;

    /// <summary>Creates a guide where <paramref name="id"/> is null, or saves the one with this id; null where there is none.</summary>
    public async Task<GuideDetailResponse?> Save(int? id, GuideRow row, CancellationToken cancellationToken)
    {
        var guide = id is { } existing
            ? await database.NotificationGuides.FindAsync([existing], cancellationToken)
            : database.NotificationGuides.Add(new NotificationGuide { Name = "" }).Entity;
        if (guide is null)
        {
            return null;
        }

        guide.Name = row.Name.Trim();
        guide.Version = Blank(row.Version);
        await database.SaveChangesAsync(cancellationToken);
        database.ChangeTracker.Clear();

        return await Detail(guide.Id, cancellationToken);
    }

    /// <summary>Deletes the guide with what hangs off it, or says why not: a condition is exported under it.</summary>
    public async Task<string?> Delete(int id, CancellationToken cancellationToken)
    {
        if (await database.ConditionDetails.AnyAsync(item => item.GuideId == id, cancellationToken))
        {
            return "A condition is exported under this guide; move it to another guide first.";
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.GuidePhinVariables.Where(item => item.GuideId == id).ExecuteDeleteAsync(cancellationToken);
        await database.GuideLinks.Where(item => item.GuideId == id || item.LinkedGuideId == id)
            .ExecuteDeleteAsync(cancellationToken);
        await database.NotificationGuides.Where(item => item.Id == id).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    /// <summary>Replaces the guide's PHIN variables, or says which is not on file.</summary>
    public async Task<string?> ReplacePhinVariables(int id, IReadOnlyList<string> phinVars, CancellationToken cancellationToken)
    {
        var wanted = phinVars.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal).ToList();
        var known = await database.PhinVariables.Where(item => wanted.Contains(item.PhinVar))
            .Select(item => item.PhinVar).ToListAsync(cancellationToken);
        if (wanted.Except(known, StringComparer.Ordinal).FirstOrDefault() is { } unknown)
        {
            return $"{unknown} is not a PHIN variable GET {PhinVariableEndpoints.Path} lists.";
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.GuidePhinVariables.Where(item => item.GuideId == id).ExecuteDeleteAsync(cancellationToken);
        database.GuidePhinVariables.AddRange(wanted.Select(phinVar => new GuidePhinVariable { GuideId = id, PhinVar = phinVar }));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.ChangeTracker.Clear();
        return null;
    }

    /// <summary>Replaces the guides the guide links, or says why not.</summary>
    public async Task<string?> ReplaceLinks(int id, IReadOnlyList<int> linkedGuideIds, CancellationToken cancellationToken)
    {
        var wanted = linkedGuideIds.Distinct().ToList();
        if (wanted.Contains(id))
        {
            return "a guide does not link itself.";
        }

        var known = await database.NotificationGuides.Where(item => wanted.Contains(item.Id))
            .Select(item => item.Id).ToListAsync(cancellationToken);
        if (wanted.Except(known).Select(item => (int?)item).FirstOrDefault() is { } unknown)
        {
            return $"{unknown} is not a guide GET {GuideEndpoints.Path} lists.";
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.GuideLinks.Where(item => item.GuideId == id).ExecuteDeleteAsync(cancellationToken);
        database.GuideLinks.AddRange(wanted.Select(linked => new GuideLink { GuideId = id, LinkedGuideId = linked }));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.ChangeTracker.Clear();
        return null;
    }

    /// <summary>Replaces the guide's own header variable values, or says which variable is not on file.</summary>
    public async Task<string?> ReplaceVariables(int id, IReadOnlyList<GuideVariableRow> variables, CancellationToken cancellationToken)
    {
        var defaults = await database.GuideVariableDefaults.Select(item => item.Id).ToListAsync(cancellationToken);
        if (variables.FirstOrDefault(item => !defaults.Contains(item.GuideVariableDefaultId)) is { } unknown)
        {
            return $"{unknown.GuideVariableDefaultId} is not a variable GET {GuideEndpoints.Root}/{GuideEndpoints.VariableDefaults} lists.";
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.GuideVariables.Where(item => item.GuideId == id).ExecuteDeleteAsync(cancellationToken);
        database.GuideVariables.AddRange(variables
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => new GuideVariableValue
            {
                GuideId = id, GuideVariableDefaultId = item.GuideVariableDefaultId, Value = item.Value!.Trim(),
            }));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.ChangeTracker.Clear();
        return null;
    }

    /// <summary>The guide as JSON, or null where there is none with this id.</summary>
    public async Task<GuideExport?> Export(int id, CancellationToken cancellationToken)
    {
        if (await Detail(id, cancellationToken) is not { } guide)
        {
            return null;
        }

        var labels = await database.RuleSets.ToDictionaryAsync(item => item.Id, item => item.Label, cancellationToken);
        var variables = await database.PhinVariables.AsNoTracking()
            .Where(item => guide.PhinVars.Contains(item.PhinVar)).ToListAsync(cancellationToken);
        var exported = new List<GuideExportVariable>();
        foreach (var variable in variables.OrderBy(item => item.PhinVar, StringComparer.Ordinal))
        {
            exported.Add(new GuideExportVariable(
                new PhinVariableRow(
                    variable.PhinVar, variable.Description, variable.Identifier, variable.Priority, variable.Hl7DataType,
                    null, null, variable.Status, variable.Note, variable.CodeSystem),
                variable.RuleSetId is { } type ? labels.GetValueOrDefault(type) : null,
                variable.RuleSetConfigId is { } config ? await ruleSets.Find(config, cancellationToken) is { } found
                    ? found with { Id = 0 }
                    : null
                : null));
        }

        return new GuideExport(guide.Name, guide.Version, exported);
    }

    /// <summary>
    /// Imports a guide's JSON in one transaction, matching it by name and
    /// version; or says which rule set type is not on file.
    /// </summary>
    public async Task<(GuideDetailResponse? Imported, string? Why)> Import(GuideExport export, CancellationToken cancellationToken)
    {
        var labels = await database.RuleSets.ToDictionaryAsync(item => item.Label, item => item.Id, cancellationToken);
        if (export.PhinVars.FirstOrDefault(item => item.RuleSetType is { } type && !labels.ContainsKey(type.Trim())) is { } unknown)
        {
            return (null, $"{unknown.RuleSetType} is not a rule set type GET {RuleSetEndpoints.TypesPath} lists.");
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var guide = await Matching(export.Name, export.Version, cancellationToken)
            ?? database.NotificationGuides.Add(new NotificationGuide { Name = export.Name.Trim(), Version = Blank(export.Version) }).Entity;
        await database.SaveChangesAsync(cancellationToken);
        var guideId = guide.Id;

        foreach (var item in export.PhinVars)
        {
            var phinVar = item.Variable.PhinVar.Trim();
            var variable = await database.PhinVariables.SingleOrDefaultAsync(entry => entry.PhinVar == phinVar, cancellationToken)
                ?? database.PhinVariables.Add(new Data.PhinVariable { PhinVar = phinVar, Description = "" }).Entity;

            int? configId = null;
            if (item.RuleSet is { } ruleSet)
            {
                var existing = variable.RuleSetConfigId is { } id && await ruleSets.Exists(id, cancellationToken) ? id : (int?)null;
                configId = (await ruleSets.Save(existing, ruleSet, cancellationToken))!.Id;
                variable = await database.PhinVariables.SingleOrDefaultAsync(entry => entry.PhinVar == phinVar, cancellationToken)
                    ?? database.PhinVariables.Add(new Data.PhinVariable { PhinVar = phinVar, Description = "" }).Entity;
            }

            var row = item.Variable;
            variable.Description = row.Description.Trim();
            variable.Identifier = Blank(row.Identifier);
            variable.Priority = Blank(row.Priority);
            variable.Hl7DataType = Blank(row.Hl7DataType);
            variable.RuleSetId = item.RuleSetType is { } label ? labels[label.Trim()] : null;
            variable.RuleSetConfigId = configId;
            variable.Status = Blank(row.Status);
            variable.Note = Blank(row.Note);
            variable.CodeSystem = Blank(row.CodeSystem);
            await database.SaveChangesAsync(cancellationToken);
        }

        await database.GuidePhinVariables.Where(item => item.GuideId == guideId).ExecuteDeleteAsync(cancellationToken);
        database.GuidePhinVariables.AddRange(export.PhinVars
            .Select(item => new GuidePhinVariable { GuideId = guideId, PhinVar = item.Variable.PhinVar.Trim() }));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.ChangeTracker.Clear();

        return (await Detail(guideId, cancellationToken), null);
    }

    private Task<NotificationGuide?> Matching(string name, string? version, CancellationToken cancellationToken)
    {
        var trimmedName = name.Trim();
        var trimmedVersion = Blank(version);
        return database.NotificationGuides.FirstOrDefaultAsync(
            guide => guide.Name == trimmedName && guide.Version == trimmedVersion, cancellationToken);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>The header variable defaults, as <see cref="GuideEndpoints"/> maps them.</summary>
internal static class GuideTables
{
    public static readonly VocabularyTable<GuideVariableDefault, GuideVariableDefaultRow> VariableDefaults =
        new GuideVariableDefaultTable();
}

/// <summary>Header variable defaults, keyed by name; one a guide sets is kept.</summary>
internal sealed class GuideVariableDefaultTable : VocabularyTable<GuideVariableDefault, GuideVariableDefaultRow>
{
    public override IReadOnlyList<string> Columns { get; } = ["name", "default_value"];

    public override int IdOf(GuideVariableDefault entity) => entity.Id;

    private static IQueryable<GuideVariableDefaultRow> Rows(SurvsysDbContext database, int? id = null) =>
        database.GuideVariableDefaults
            .Where(item => id == null || item.Id == id)
            .Select(item => new GuideVariableDefaultRow(item.Id, item.Name, item.DefaultValue));

    public override async Task<List<GuideVariableDefaultRow>> List(SurvsysDbContext database, CancellationToken cancellationToken) =>
        (await Rows(database).ToListAsync(cancellationToken)).OrderBy(row => row.Name, StringComparer.Ordinal).ToList();

    public override Task<GuideVariableDefaultRow?> Find(SurvsysDbContext database, int id, CancellationToken cancellationToken) =>
        Rows(database, id).SingleOrDefaultAsync(cancellationToken);

    public override IReadOnlyList<string?> Fields(GuideVariableDefaultRow row) => [row.Name, row.DefaultValue];

    public override (GuideVariableDefaultRow? Row, string? Why) Parse(CsvRow row) =>
        (new GuideVariableDefaultRow(0, row.Value("name") ?? "", row.Value("default_value")), null);

    public override string? WhyNot(GuideVariableDefaultRow row) => GuideEndpoints.WhyNotAGuideVariableDefault(row);

    public override string KeyOf(GuideVariableDefaultRow row) => row.Name.Trim();

    public override Task<GuideVariableDefault?> ByKey(
        SurvsysDbContext database, GuideVariableDefaultRow row, CancellationToken cancellationToken) =>
        database.GuideVariableDefaults.SingleOrDefaultAsync(item => item.Name == row.Name.Trim(), cancellationToken);

    public override GuideVariableDefault New(GuideVariableDefaultRow row) => new() { Name = row.Name.Trim() };

    public override Task<string?> Apply(
        SurvsysDbContext database, GuideVariableDefault entity, GuideVariableDefaultRow row, CancellationToken cancellationToken)
    {
        entity.Name = row.Name.Trim();
        entity.DefaultValue = Blank(row.DefaultValue);
        return Task.FromResult<string?>(null);
    }

    public override async Task<string?> WhyNotDeleted(
        SurvsysDbContext database, GuideVariableDefault entity, CancellationToken cancellationToken) =>
        await database.GuideVariables.AnyAsync(item => item.GuideVariableDefaultId == entity.Id, cancellationToken)
            ? $"A guide sets its own {entity.Name}; clear it on the guide first."
            : null;
}
