using System.Globalization;
using System.Text;
using Survsys.Contracts;

namespace Survsys.Api;

/// <summary>
/// One data row of a CSV file: the line it was read from, the header being
/// line 1, and its fields by header column, a blank field as null.
/// </summary>
public sealed record CsvRow(int Line, IReadOnlyDictionary<string, string?> Fields)
{
    /// <summary>The row's field for a column, or null where it is blank or absent.</summary>
    public string? Value(string column) => Fields.GetValueOrDefault(column);
}

/// <summary>
/// The CSV form of each vocabulary table: its columns, a row's fields, and a
/// row read back from them — and the reading of a CSV file into rows.
/// </summary>
/// <remarks>
/// The columns are the row records' members, in their order, so an export
/// imports back unchanged. A master is named in a child's row by its code
/// rather than its id, so a file reads as what it maps and moves between
/// databases. Writing is <see cref="CaseExport.Csv"/>'s, guard against
/// spreadsheet formulas and all; reading undoes that guard, so a code that
/// begins with <c>-</c> survives the round trip. A field is quoted where it
/// holds a comma or a quote, and a row is one line. Pure and static, so the
/// unit suite proves every table's round trip.
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

    /// <summary>The CSV text: a header of the columns, then one line per row.</summary>
    public static string Write(IReadOnlyList<string> columns, IEnumerable<IReadOnlyList<string?>> rows) =>
        CaseExport.Csv(columns, rows);

    /// <summary>
    /// The data rows of a CSV file, or why it cannot be read: a file with no
    /// header row, or a header missing one of <paramref name="columns"/>, is
    /// refused at the line it is on. A blank line is skipped, and a row with
    /// more or fewer fields than the header is refused as a row.
    /// </summary>
    public static (IReadOnlyList<CsvRow> Rows, IReadOnlyList<ContactImportProblem> Refused) Read(
        string? csv, IReadOnlyList<string> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var lines = (csv ?? string.Empty).Split(["\r\n", "\n"], StringSplitOptions.None);
        var headerAt = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));

        if (headerAt < 0)
        {
            return ([], [new ContactImportProblem(1, "the file has no header row: the first line names the columns, "
                + $"{string.Join(",", columns)}.")]);
        }

        var header = Fields(lines[headerAt]).Select(name => (name ?? string.Empty).ToLowerInvariant()).ToList();
        var missing = columns.Where(column => !header.Contains(column)).ToList();

        if (missing.Count > 0)
        {
            return ([], [new ContactImportProblem(headerAt + 1, $"the header row does not name "
                + $"{string.Join(", ", missing)}: it must name {string.Join(",", columns)}.")]);
        }

        var rows = new List<CsvRow>();
        var refused = new List<ContactImportProblem>();

        for (var at = headerAt + 1; at < lines.Length; at++)
        {
            if (string.IsNullOrWhiteSpace(lines[at]))
            {
                continue;
            }

            var fields = Fields(lines[at]);

            if (fields.Count != header.Count)
            {
                refused.Add(new ContactImportProblem(at + 1, $"the row has {fields.Count} field(s) where the header "
                    + $"names {header.Count}: a field holding a comma is quoted."));
                continue;
            }

            rows.Add(new CsvRow(
                at + 1,
                header.Zip(fields).ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.Ordinal)));
        }

        return (rows, refused);
    }

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

    private static string? Optional(CsvRow row, string column, out int? value)
    {
        value = null;

        if (row.Value(column) is not { } text)
        {
            return null;
        }

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return $"{column} '{text}' is not a whole number.";
        }

        value = number;
        return null;
    }

    private static string? Required(CsvRow row, string column, out int value)
    {
        value = 0;

        if (Optional(row, column, out var number) is { } why)
        {
            return why;
        }

        if (number is null)
        {
            return $"{column} is required.";
        }

        value = number.Value;
        return null;
    }

    private static string? Flag(CsvRow row, string column, out bool value)
    {
        value = false;

        switch (row.Value(column)?.ToLowerInvariant())
        {
            case null or "no" or "false" or "n" or "0":
                return null;
            case "yes" or "true" or "y" or "1":
                value = true;
                return null;
            default:
                return $"{column} '{row.Value(column)}' is not yes or no.";
        }
    }

    private static string? Number(int? value) => value?.ToString(CultureInfo.InvariantCulture);

    private static string YesNo(bool value) => value ? "yes" : "no";

    /// <summary>
    /// The fields of one line: split at commas outside quotes, a doubled quote
    /// inside quotes read as one, trimmed, blank as null, and the formula guard
    /// <see cref="CaseExport.Csv"/> writes taken off again.
    /// </summary>
    private static List<string?> Fields(string line)
    {
        var fields = new List<string?>();
        var field = new StringBuilder();
        var quoted = false;

        for (var at = 0; at < line.Length; at++)
        {
            var character = line[at];

            if (quoted)
            {
                if (character == '"' && at + 1 < line.Length && line[at + 1] == '"')
                {
                    field.Append('"');
                    at++;
                }
                else if (character == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(character);
                }
            }
            else if (character == '"')
            {
                quoted = true;
            }
            else if (character == ',')
            {
                fields.Add(Finish(field));
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        fields.Add(Finish(field));
        return fields;
    }

    private static string? Finish(StringBuilder field)
    {
        var value = field.ToString().Trim();

        if (value.Length > 1 && value[0] == '\'' && value[1] is '=' or '+' or '-' or '@')
        {
            value = value[1..];
        }

        return value.Length == 0 ? null : value;
    }
}
