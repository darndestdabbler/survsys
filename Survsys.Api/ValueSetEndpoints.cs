using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/notification-setup/phin-value-sets</c> and
/// <c>/api/notification-setup/units-of-measure</c>: the concepts a notification
/// rule set's coded output resolves through, each listed, created, saved over,
/// deleted, exported as CSV and imported from it.
/// </summary>
/// <remarks>
/// The legacy NMI value set maintenance described in
/// <c>../nedss/docs/business-logic/NMI_XML_GENERATION.md</c>, <i>Value Set
/// System</i>. Both are <see cref="VocabularyTable{TEntity, TRow}"/>s served by
/// <see cref="VocabularyEndpoints.MapTable"/>, so a CSV import is previewed
/// before it is confirmed and upserts by the value set and concept code; an
/// entry the file leaves out is kept. Nothing refers to a concept by id, so
/// any concept can be deleted.
/// </remarks>
public static class ValueSetEndpoints
{
    public const string Root = "/api/notification-setup";
    public const string PhinValueSets = "phin-value-sets";
    public const string UnitsOfMeasure = "units-of-measure";

    private const int NameLength = 100;
    private const int CodeLength = 50;
    private const int TextLength = 500;
    private const int OidLength = 100;

    /// <summary>Why this is not a PHIN value set concept this tier will save, or null when it is one.</summary>
    public static string? WhyNotAPhinValueSetConcept(PhinValueSetRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return WhyNotAConcept(row.ValueSetName, row.ConceptCode, row.PreferredConceptName, row.Hl7Code, row.CodeSystemOid)
            ?? VocabularyEndpoints.TooLong("cdcName", row.CdcName, TextLength);
    }

    /// <summary>Why this is not a unit of measure this tier will save, or null when it is one.</summary>
    public static string? WhyNotAUnitOfMeasure(PhinUnitOfMeasureRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return WhyNotAConcept(row.ValueSetName, row.ConceptCode, row.PreferredConceptName, row.Hl7Code, row.CodeSystemOid);
    }

    public static IEndpointRouteBuilder MapValueSets(this IEndpointRouteBuilder routes)
    {
        VocabularyEndpoints.MapTable(routes, Root, PhinValueSets, ValueSetTables.PhinValueSets, "PHIN value set concept");
        VocabularyEndpoints.MapTable(routes, Root, UnitsOfMeasure, ValueSetTables.UnitsOfMeasure, "unit of measure");
        return routes;
    }

    private static string? WhyNotAConcept(
        string? valueSetName, string? conceptCode, string? preferredConceptName, string? hl7Code, string? codeSystemOid) =>
        VocabularyEndpoints.Required("valueSetName", valueSetName, NameLength)
        ?? VocabularyEndpoints.Required("conceptCode", conceptCode, CodeLength)
        ?? VocabularyEndpoints.Required("preferredConceptName", preferredConceptName, TextLength)
        ?? VocabularyEndpoints.TooLong("hl7Code", hl7Code, CodeLength)
        ?? VocabularyEndpoints.TooLong("codeSystemOid", codeSystemOid, OidLength);
}

/// <summary>The two value set tables, as <see cref="ValueSetEndpoints"/> maps them.</summary>
internal static class ValueSetTables
{
    public static readonly VocabularyTable<PhinValueSetConcept, PhinValueSetRow> PhinValueSets = new PhinValueSetTable();

    public static readonly VocabularyTable<PhinUnitOfMeasure, PhinUnitOfMeasureRow> UnitsOfMeasure =
        new UnitOfMeasureTable();
}

/// <summary>PHIN value set concepts, keyed by value set and concept code.</summary>
internal sealed class PhinValueSetTable : VocabularyTable<PhinValueSetConcept, PhinValueSetRow>
{
    public override IReadOnlyList<string> Columns { get; } =
        ["value_set_name", "concept_code", "preferred_concept_name", "hl7_code", "code_system_oid", "cdc_name"];

    public override int IdOf(PhinValueSetConcept entity) => entity.Id;

    private static IQueryable<PhinValueSetRow> Rows(SurvsysDbContext database, int? id = null) =>
        database.PhinValueSets
            .Where(concept => id == null || concept.Id == id)
            .Select(concept => new PhinValueSetRow(
                concept.Id, concept.ValueSetName, concept.ConceptCode, concept.PreferredConceptName,
                concept.Hl7Code, concept.CodeSystemOid, concept.CdcName));

    public override async Task<List<PhinValueSetRow>> List(SurvsysDbContext database, CancellationToken cancellationToken) =>
        (await Rows(database).ToListAsync(cancellationToken))
            .OrderBy(row => row.ValueSetName, StringComparer.Ordinal)
            .ThenBy(row => row.ConceptCode, StringComparer.Ordinal)
            .ToList();

    public override Task<PhinValueSetRow?> Find(SurvsysDbContext database, int id, CancellationToken cancellationToken) =>
        Rows(database, id).SingleOrDefaultAsync(cancellationToken);

    public override IReadOnlyList<string?> Fields(PhinValueSetRow row) =>
        [row.ValueSetName, row.ConceptCode, row.PreferredConceptName, row.Hl7Code, row.CodeSystemOid, row.CdcName];

    public override (PhinValueSetRow? Row, string? Why) Parse(CsvRow row) =>
        (new PhinValueSetRow(
            0,
            row.Value("value_set_name") ?? "",
            row.Value("concept_code") ?? "",
            row.Value("preferred_concept_name") ?? "",
            row.Value("hl7_code"),
            row.Value("code_system_oid"),
            row.Value("cdc_name")), null);

    public override string? WhyNot(PhinValueSetRow row) => ValueSetEndpoints.WhyNotAPhinValueSetConcept(row);

    public override string KeyOf(PhinValueSetRow row) => $"{row.ValueSetName.Trim()} {row.ConceptCode.Trim()}";

    public override Task<PhinValueSetConcept?> ByKey(
        SurvsysDbContext database, PhinValueSetRow row, CancellationToken cancellationToken) =>
        database.PhinValueSets.SingleOrDefaultAsync(
            concept => concept.ValueSetName == row.ValueSetName.Trim() && concept.ConceptCode == row.ConceptCode.Trim(),
            cancellationToken);

    public override PhinValueSetConcept New(PhinValueSetRow row) =>
        new() { ValueSetName = row.ValueSetName.Trim(), ConceptCode = row.ConceptCode.Trim(), PreferredConceptName = "" };

    public override Task<string?> Apply(
        SurvsysDbContext database, PhinValueSetConcept entity, PhinValueSetRow row, CancellationToken cancellationToken)
    {
        entity.ValueSetName = row.ValueSetName.Trim();
        entity.ConceptCode = row.ConceptCode.Trim();
        entity.PreferredConceptName = row.PreferredConceptName.Trim();
        entity.Hl7Code = Blank(row.Hl7Code);
        entity.CodeSystemOid = Blank(row.CodeSystemOid);
        entity.CdcName = Blank(row.CdcName);
        return Task.FromResult<string?>(null);
    }

    public override Task<string?> WhyNotDeleted(
        SurvsysDbContext database, PhinValueSetConcept entity, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}

/// <summary>Units of measure, keyed by value set and concept code.</summary>
internal sealed class UnitOfMeasureTable : VocabularyTable<PhinUnitOfMeasure, PhinUnitOfMeasureRow>
{
    public override IReadOnlyList<string> Columns { get; } =
        ["value_set_name", "concept_code", "preferred_concept_name", "hl7_code", "code_system_oid"];

    public override int IdOf(PhinUnitOfMeasure entity) => entity.Id;

    private static IQueryable<PhinUnitOfMeasureRow> Rows(SurvsysDbContext database, int? id = null) =>
        database.PhinUnitsOfMeasure
            .Where(unit => id == null || unit.Id == id)
            .Select(unit => new PhinUnitOfMeasureRow(
                unit.Id, unit.ValueSetName, unit.ConceptCode, unit.PreferredConceptName, unit.Hl7Code, unit.CodeSystemOid));

    public override async Task<List<PhinUnitOfMeasureRow>> List(
        SurvsysDbContext database, CancellationToken cancellationToken) =>
        (await Rows(database).ToListAsync(cancellationToken))
            .OrderBy(row => row.ValueSetName, StringComparer.Ordinal)
            .ThenBy(row => row.ConceptCode, StringComparer.Ordinal)
            .ToList();

    public override Task<PhinUnitOfMeasureRow?> Find(SurvsysDbContext database, int id, CancellationToken cancellationToken) =>
        Rows(database, id).SingleOrDefaultAsync(cancellationToken);

    public override IReadOnlyList<string?> Fields(PhinUnitOfMeasureRow row) =>
        [row.ValueSetName, row.ConceptCode, row.PreferredConceptName, row.Hl7Code, row.CodeSystemOid];

    public override (PhinUnitOfMeasureRow? Row, string? Why) Parse(CsvRow row) =>
        (new PhinUnitOfMeasureRow(
            0,
            row.Value("value_set_name") ?? "",
            row.Value("concept_code") ?? "",
            row.Value("preferred_concept_name") ?? "",
            row.Value("hl7_code"),
            row.Value("code_system_oid")), null);

    public override string? WhyNot(PhinUnitOfMeasureRow row) => ValueSetEndpoints.WhyNotAUnitOfMeasure(row);

    public override string KeyOf(PhinUnitOfMeasureRow row) => $"{row.ValueSetName.Trim()} {row.ConceptCode.Trim()}";

    public override Task<PhinUnitOfMeasure?> ByKey(
        SurvsysDbContext database, PhinUnitOfMeasureRow row, CancellationToken cancellationToken) =>
        database.PhinUnitsOfMeasure.SingleOrDefaultAsync(
            unit => unit.ValueSetName == row.ValueSetName.Trim() && unit.ConceptCode == row.ConceptCode.Trim(),
            cancellationToken);

    public override PhinUnitOfMeasure New(PhinUnitOfMeasureRow row) =>
        new() { ValueSetName = row.ValueSetName.Trim(), ConceptCode = row.ConceptCode.Trim(), PreferredConceptName = "" };

    public override Task<string?> Apply(
        SurvsysDbContext database, PhinUnitOfMeasure entity, PhinUnitOfMeasureRow row, CancellationToken cancellationToken)
    {
        entity.ValueSetName = row.ValueSetName.Trim();
        entity.ConceptCode = row.ConceptCode.Trim();
        entity.PreferredConceptName = row.PreferredConceptName.Trim();
        entity.Hl7Code = Blank(row.Hl7Code);
        entity.CodeSystemOid = Blank(row.CodeSystemOid);
        return Task.FromResult<string?>(null);
    }

    public override Task<string?> WhyNotDeleted(
        SurvsysDbContext database, PhinUnitOfMeasure entity, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}
