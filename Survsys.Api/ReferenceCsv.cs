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
/// The CSV form every reference table shares: a file written from columns and
/// rows, read back into rows by header column, and the fields of a row read as
/// numbers and flags or written from them.
/// </summary>
/// <remarks>
/// Writing is <see cref="CaseExport.Csv"/>'s, guard against spreadsheet
/// formulas and all; reading undoes that guard, so a code that begins with
/// <c>-</c> survives the round trip. A field is quoted where it holds a comma,
/// a quote or a line break, and a row is one line but for the line breaks
/// inside its quoted fields. Pure and static, so the unit suite
/// proves it. Each table's columns and rows are its own, as
/// <see cref="VocabularyCsv"/>'s are.
/// </remarks>
public static class ReferenceCsv
{
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

        var lines = Records(csv ?? string.Empty);
        var headerAt = lines.FindIndex(line => !string.IsNullOrWhiteSpace(line.Text));

        if (headerAt < 0)
        {
            return ([], [new ContactImportProblem(1, "the file has no header row: the first line names the columns, "
                + $"{string.Join(",", columns)}.")]);
        }

        var header = Fields(lines[headerAt].Text).Select(name => (name ?? string.Empty).ToLowerInvariant()).ToList();

        if (header.GroupBy(name => name).FirstOrDefault(names => names.Count() > 1) is { } repeated)
        {
            return ([], [new ContactImportProblem(lines[headerAt].Line, $"the header row names "
                + $"'{repeated.Key}' more than once: each column is named once.")]);
        }

        var missing = columns.Where(column => !header.Contains(column)).ToList();

        if (missing.Count > 0)
        {
            return ([], [new ContactImportProblem(lines[headerAt].Line, $"the header row does not name "
                + $"{string.Join(", ", missing)}: it must name {string.Join(",", columns)}.")]);
        }

        var rows = new List<CsvRow>();
        var refused = new List<ContactImportProblem>();

        foreach (var (line, text) in lines.Skip(headerAt + 1))
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var fields = Fields(text);

            if (fields.Count != header.Count)
            {
                refused.Add(new ContactImportProblem(line, $"the row has {fields.Count} field(s) where the header "
                    + $"names {header.Count}: a field holding a comma is quoted."));
                continue;
            }

            rows.Add(new CsvRow(
                line,
                header.Zip(fields).ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.Ordinal)));
        }

        return (rows, refused);
    }

    /// <summary>
    /// The file's records, each with the line it starts on: split at line
    /// breaks outside quotes, so a quoted field keeps the line breaks
    /// <see cref="Write"/> wrote into it.
    /// </summary>
    private static List<(int Line, string Text)> Records(string csv)
    {
        var records = new List<(int Line, string Text)>();
        var record = new StringBuilder();
        var quoted = false;
        var line = 1;
        var startsOn = 1;

        for (var at = 0; at < csv.Length; at++)
        {
            var character = csv[at];

            if (character == '"')
            {
                quoted = !quoted;
            }

            if (!quoted && (character == '\n' || (character == '\r' && at + 1 < csv.Length && csv[at + 1] == '\n')))
            {
                records.Add((startsOn, record.ToString()));
                record.Clear();
                at += character == '\r' ? 1 : 0;
                startsOn = ++line;
                continue;
            }

            if (character == '\n')
            {
                line++;
            }

            record.Append(character);
        }

        records.Add((startsOn, record.ToString()));
        return records;
    }

    /// <summary>
    /// Reads a column as a whole number, null where it is blank, or says why
    /// it is not one.
    /// </summary>
    public static string? Optional(CsvRow row, string column, out int? value)
    {
        ArgumentNullException.ThrowIfNull(row);

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

    /// <summary>Reads a column as a whole number, or says why it is blank or not one.</summary>
    public static string? Required(CsvRow row, string column, out int value)
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

    /// <summary>
    /// Reads a column as yes or no — <c>yes</c>, <c>true</c>, <c>y</c> or
    /// <c>1</c> and their opposites, a blank being no — or says why it is not.
    /// </summary>
    public static string? Flag(CsvRow row, string column, out bool value)
    {
        ArgumentNullException.ThrowIfNull(row);

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

    /// <summary>A number as its field, blank where there is none.</summary>
    public static string? Number(int? value) => value?.ToString(CultureInfo.InvariantCulture);

    /// <summary>A flag as its field.</summary>
    public static string YesNo(bool value) => value ? "yes" : "no";

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
