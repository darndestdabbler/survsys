using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/notification-setup/hep-lab-groups</c>: the hepatitis lab groups,
/// each with its conditions and test types, listed, created, saved whole and
/// deleted.
/// </summary>
/// <remarks>
/// The legacy NMI hepatitis lab setup described in
/// <c>../nedss/docs/business-logic/NMI_EXPORT_PIPELINE.md</c>, <i>Hepatitis
/// Lab Configuration</i>. A group's conditions and test types are saved with
/// it, because a screen edits them together. A condition is in one group, and
/// a condition or test type not on file is a 400 asked before the write. The
/// handlers are handed <see cref="HepLabGroupQueries"/> and not the
/// <c>DbContext</c>.
/// </remarks>
public static class HepLabGroupEndpoints
{
    public const string Path = "/api/notification-setup/hep-lab-groups";

    private const int NameLength = 100;

    /// <summary>Why this is not a hepatitis lab group this tier will save, or null when it is one.</summary>
    public static string? WhyNotAHepLabGroup(HepLabGroupRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var why = VocabularyEndpoints.Required("name", row.Name, NameLength);
        foreach (var testType in row.TestTypes ?? [])
        {
            why ??= testType is null
                ? "a test type is empty."
                : VocabularyEndpoints.Required($"test type {testType.TestTypeId}'s testGroupName", testType.TestGroupName, NameLength);
        }

        return why
            ?? ((row.ConditionIds ?? []).GroupBy(id => id).FirstOrDefault(group => group.Count() > 1) is { } condition
                ? $"condition {condition.Key} is given twice."
                : null)
            ?? ((row.TestTypes ?? []).GroupBy(item => item.TestTypeId).FirstOrDefault(group => group.Count() > 1) is { } testTypeTwice
                ? $"test type {testTypeTwice.Key} is given twice."
                : null);
    }

    /// <summary>Registers what the hepatitis lab group endpoints need of the container.</summary>
    public static IServiceCollection AddHepLabGroups(this IServiceCollection services) =>
        services.AddScoped<HepLabGroupQueries>();

    public static IEndpointRouteBuilder MapHepLabGroups(this IEndpointRouteBuilder routes)
    {
        routes.MapGet(Path, (HepLabGroupQueries groups, CancellationToken cancellationToken) => groups.List(cancellationToken))
            .WithName("ListHepLabGroups")
            .WithSummary("Every hepatitis lab group, with its conditions and test types.");

        routes.MapPost(Path, async Task<Results<Created<HepLabGroupRow>, ProblemHttpResult>> (
                HepLabGroupQueries groups, HepLabGroupRow row, CancellationToken cancellationToken) =>
            {
                if ((WhyNotAHepLabGroup(row) ?? await groups.WhyNotResolved(null, row, cancellationToken)) is { } why)
                {
                    return Refused(why);
                }

                var saved = (await groups.Save(null, row, cancellationToken))!;
                return TypedResults.Created($"{Path}/{saved.Id}", saved);
            })
            .WithName("CreateHepLabGroup")
            .WithSummary("Creates a hepatitis lab group.");

        routes.MapPut($"{Path}/{{id:int}}", async Task<Results<Ok<HepLabGroupRow>, ProblemHttpResult>> (
                HepLabGroupQueries groups, int id, HepLabGroupRow row, CancellationToken cancellationToken) =>
            {
                if (!await groups.Exists(id, cancellationToken))
                {
                    return NoSuch(id);
                }

                if ((WhyNotAHepLabGroup(row) ?? await groups.WhyNotResolved(id, row, cancellationToken)) is { } why)
                {
                    return Refused(why);
                }

                return TypedResults.Ok((await groups.Save(id, row, cancellationToken))!);
            })
            .WithName("UpdateHepLabGroup")
            .WithSummary("Saves a hepatitis lab group whole.");

        routes.MapDelete($"{Path}/{{id:int}}", async Task<Results<NoContent, ProblemHttpResult>> (
                HepLabGroupQueries groups, int id, CancellationToken cancellationToken) =>
            {
                if (!await groups.Exists(id, cancellationToken))
                {
                    return NoSuch(id);
                }

                await groups.Delete(id, cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("DeleteHepLabGroup")
            .WithSummary("Deletes a hepatitis lab group, with its conditions and test types.");

        return routes;
    }

    private static ProblemHttpResult NoSuch(int id) =>
        TypedResults.Problem(
            detail: $"No hepatitis lab group has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such hepatitis lab group.");

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The hepatitis lab group asked for is not one this endpoint can save.");
}

/// <summary>
/// The hepatitis lab group queries, and the only thing in this tier that holds
/// the <c>DbContext</c> for them.
/// </summary>
internal sealed class HepLabGroupQueries(SurvsysDbContext database)
{
    public async Task<IReadOnlyList<HepLabGroupRow>> List(CancellationToken cancellationToken)
    {
        var groups = await database.HepLabGroups.AsNoTracking().ToListAsync(cancellationToken);
        var conditions = (await database.HepLabGroupConditions.AsNoTracking().ToListAsync(cancellationToken))
            .ToLookup(item => item.HepLabGroupId);
        var testTypes = (await database.HepLabGroupTestTypes.AsNoTracking().ToListAsync(cancellationToken))
            .ToLookup(item => item.HepLabGroupId);

        return groups
            .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new HepLabGroupRow(
                group.Id,
                group.Name,
                conditions[group.Id].Select(item => item.ConditionId).Order().ToList(),
                testTypes[group.Id].OrderBy(item => item.SortOrder).ThenBy(item => item.TestTypeId)
                    .Select(item => new HepLabGroupTestTypeRow(item.TestTypeId, item.TestGroupName, item.SortOrder))
                    .ToList()))
            .ToList();
    }

    public Task<bool> Exists(int id, CancellationToken cancellationToken) =>
        database.HepLabGroups.AnyAsync(group => group.Id == id, cancellationToken);

    /// <summary>
    /// Why the group cannot be saved as group <paramref name="id"/>: its name
    /// is another group's, a condition or test type is not on file, or a
    /// condition is in another group. Null where it can.
    /// </summary>
    public async Task<string?> WhyNotResolved(int? id, HepLabGroupRow row, CancellationToken cancellationToken)
    {
        var name = row.Name.Trim();
        if (await database.HepLabGroups.AnyAsync(group => group.Name == name && group.Id != id, cancellationToken))
        {
            return $"another hepatitis lab group is named {name}.";
        }

        var conditionIds = (row.ConditionIds ?? []).ToList();
        var knownConditions = await database.Conditions.Where(item => conditionIds.Contains(item.Id))
            .Select(item => item.Id).ToListAsync(cancellationToken);
        if (conditionIds.Except(knownConditions).Select(item => (int?)item).FirstOrDefault() is { } unknownCondition)
        {
            return $"condition {unknownCondition} is not a condition GET /api/conditions lists.";
        }

        if (await database.HepLabGroupConditions
                .Where(item => conditionIds.Contains(item.ConditionId) && item.HepLabGroupId != id)
                .Select(item => (int?)item.ConditionId).FirstOrDefaultAsync(cancellationToken) is { } elsewhere)
        {
            return $"condition {elsewhere} is already in another hepatitis lab group.";
        }

        var testTypeIds = (row.TestTypes ?? []).Select(item => item.TestTypeId).ToList();
        var knownTestTypes = await database.CodeValues
            .Where(item => item.List == CodeLists.TestType && testTypeIds.Contains(item.Id))
            .Select(item => item.Id).ToListAsync(cancellationToken);
        return testTypeIds.Except(knownTestTypes).Select(item => (int?)item).FirstOrDefault() is { } unknownTestType
            ? $"test type {unknownTestType} is not a {CodeLists.TestType} code GET /api/codes lists."
            : null;
    }

    /// <summary>Creates a group where <paramref name="id"/> is null, or saves the one with this id whole.</summary>
    public async Task<HepLabGroupRow?> Save(int? id, HepLabGroupRow row, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var group = id is { } existing
            ? await database.HepLabGroups.SingleAsync(item => item.Id == existing, cancellationToken)
            : database.HepLabGroups.Add(new HepLabGroup { Name = "" }).Entity;
        group.Name = row.Name.Trim();
        await database.SaveChangesAsync(cancellationToken);

        await database.HepLabGroupConditions.Where(item => item.HepLabGroupId == group.Id).ExecuteDeleteAsync(cancellationToken);
        await database.HepLabGroupTestTypes.Where(item => item.HepLabGroupId == group.Id).ExecuteDeleteAsync(cancellationToken);
        database.HepLabGroupConditions.AddRange((row.ConditionIds ?? [])
            .Select(conditionId => new HepLabGroupCondition { HepLabGroupId = group.Id, ConditionId = conditionId }));
        database.HepLabGroupTestTypes.AddRange((row.TestTypes ?? []).Select(item => new HepLabGroupTestType
        {
            HepLabGroupId = group.Id,
            TestTypeId = item.TestTypeId,
            TestGroupName = item.TestGroupName.Trim(),
            SortOrder = item.SortOrder,
        }));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.ChangeTracker.Clear();

        return (await List(cancellationToken)).SingleOrDefault(item => item.Id == group.Id);
    }

    /// <summary>Deletes the group; its conditions and test types go with it.</summary>
    public Task Delete(int id, CancellationToken cancellationToken) =>
        database.HepLabGroups.Where(group => group.Id == id).ExecuteDeleteAsync(cancellationToken);
}
