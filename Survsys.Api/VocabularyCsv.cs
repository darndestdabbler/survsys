using Survsys.Contracts;
using static Survsys.Api.ReferenceCsv;

namespace Survsys.Api;

/// <summary>
/// The CSV form of each vocabulary table: its columns, a row's fields, and a
/// row read back from them.
/// </summary>
/// <remarks>
/// The columns are the row records' members, in their order, so an export
/// imports back unchanged. A master is named in a child's row by its code
/// rather than its id, so a file reads as what it maps and moves between
/// databases. The reading and writing of the file itself is
/// <see cref="ReferenceCsv"/>'s. Pure and static, so the unit suite proves
/// every table's round trip.
/// </remarks>
public static class VocabularyCsv
{
    public static readonly IReadOnlyList<string> MasterLoincColumns =
        ["loinc", "concept_name", "condition_id", "organism_id", "organism_from_result"];

    public static readonly IReadOnlyList<string> ChildLoincColumns =
        ["lab_id", "child_loinc", "master_loinc", "concept_name", "archived"];

    public static readonly IReadOnlyList<string> OrganismColumns =
        ["snomed", "snomed_alt", "organism", "condition_id"];

    public static readonly IReadOnlyList<string> MasterValueColumns =
        ["category", "concept", "app_value"];

    public static readonly IReadOnlyList<string> ChildValueColumns =
        ["lab_id", "category", "child_code", "master"];

    public static IReadOnlyList<string?> Fields(VocabMasterLoincRow row) =>
        [row.Loinc, row.ConceptName, Number(row.ConditionId), Number(row.OrganismId), YesNo(row.OrganismFromResult)];

    public static IReadOnlyList<string?> Fields(VocabChildLoincRow row) =>
        [Number(row.LabId), row.ChildLoinc, row.MasterLoinc, row.ConceptName, YesNo(row.Archived)];

    public static IReadOnlyList<string?> Fields(VocabMasterOrganismRow row) =>
        [row.Snomed, row.SnomedAlt, row.OrganismName, Number(row.ConditionId)];

    public static IReadOnlyList<string?> Fields(VocabMasterValueRow row) =>
        [row.Category, row.Concept, row.AppValue];

    public static IReadOnlyList<string?> Fields(VocabChildValueRow row) =>
        [Number(row.LabId), row.Category, row.ChildCode, row.Master];

    /// <summary>A master LOINC read from its CSV row, or why the row is not one.</summary>
    public static (VocabMasterLoincRow? Row, string? Why) MasterLoinc(CsvRow row) =>
        Optional(row, "condition_id", out var condition) is { } why1 ? (null, why1)
        : Optional(row, "organism_id", out var organism) is { } why2 ? (null, why2)
        : Flag(row, "organism_from_result", out var fromResult) is { } why3 ? (null, why3)
        : (new VocabMasterLoincRow(0, row.Value("loinc") ?? "", row.Value("concept_name"), condition, organism, fromResult),
            null);

    /// <summary>A child LOINC read from its CSV row, or why the row is not one.</summary>
    public static (VocabChildLoincRow? Row, string? Why) ChildLoinc(CsvRow row) =>
        Required(row, "lab_id", out var lab) is { } why1 ? (null, why1)
        : Flag(row, "archived", out var archived) is { } why2 ? (null, why2)
        : (new VocabChildLoincRow(
            0, lab, row.Value("child_loinc") ?? "", row.Value("master_loinc") ?? "", row.Value("concept_name"), archived),
            null);

    /// <summary>A master organism read from its CSV row, or why the row is not one.</summary>
    public static (VocabMasterOrganismRow? Row, string? Why) Organism(CsvRow row) =>
        Optional(row, "condition_id", out var condition) is { } why
            ? (null, why)
            : (new VocabMasterOrganismRow(
                0, row.Value("snomed") ?? "", row.Value("snomed_alt"), row.Value("organism") ?? "", condition),
                null);

    /// <summary>A master value read from its CSV row.</summary>
    public static (VocabMasterValueRow? Row, string? Why) MasterValue(CsvRow row) =>
        (new VocabMasterValueRow(0, row.Value("category") ?? "", row.Value("concept") ?? "", row.Value("app_value")),
            null);

    /// <summary>A child code read from its CSV row, or why the row is not one.</summary>
    public static (VocabChildValueRow? Row, string? Why) ChildValue(CsvRow row) =>
        Required(row, "lab_id", out var lab) is { } why
            ? (null, why)
            : (new VocabChildValueRow(
                0, lab, row.Value("category") ?? "", row.Value("child_code") ?? "", row.Value("master") ?? ""),
                null);
}
