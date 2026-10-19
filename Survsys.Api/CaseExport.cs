using System.Globalization;
using System.Text;
using Survsys.Contracts;

namespace Survsys.Api;

/// <summary>
/// One case morbidity report as an export reads it: every field the
/// <see cref="ExportColumns"/> catalogue names.
/// </summary>
public sealed record CaseExportRow(
    string RecordNumber,
    string LastName,
    string? FirstName,
    DateOnly? BirthDate,
    string Condition,
    string? Agency,
    string WorkflowState,
    string? CaseStatus,
    DateOnly? EventOnsetDate,
    DateOnly? FirstReportedPhDate,
    int? MmwrWeek,
    int? MmwrYear,
    DateTimeOffset CreatedAt);

/// <summary>
/// How a saved view's reports are written as CSV: the chosen columns' values,
/// and the file they make.
/// </summary>
/// <remarks>
/// RFC 4180: a header row of the column names, CRLF line ends, and a value
/// holding a comma, a quote or a line break wrapped in quotes with its quotes
/// doubled. A value a spreadsheet would read as a formula — one starting with
/// <c>=</c>, <c>+</c>, <c>-</c> or <c>@</c> — is written with a leading
/// apostrophe, so opening an export never runs what someone typed into a
/// name. Dates are ISO 8601, so the file reads the same in every locale.
/// </remarks>
public static class CaseExport
{
    /// <summary>The row's values for these columns, in their order.</summary>
    public static IReadOnlyList<string?> Values(CaseExportRow row, IReadOnlyList<string> columns)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(columns);

        return columns.Select(column => column switch
        {
            ExportColumns.RecordNumber => row.RecordNumber,
            ExportColumns.LastName => row.LastName,
            ExportColumns.FirstName => row.FirstName,
            ExportColumns.BirthDate => Date(row.BirthDate),
            ExportColumns.Condition => row.Condition,
            ExportColumns.Agency => row.Agency,
            ExportColumns.WorkflowState => row.WorkflowState,
            ExportColumns.CaseStatus => row.CaseStatus,
            ExportColumns.EventOnsetDate => Date(row.EventOnsetDate),
            ExportColumns.FirstReportedPhDate => Date(row.FirstReportedPhDate),
            ExportColumns.MmwrWeek => row.MmwrWeek?.ToString(CultureInfo.InvariantCulture),
            ExportColumns.MmwrYear => row.MmwrYear?.ToString(CultureInfo.InvariantCulture),
            ExportColumns.CreatedAt => row.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(
                nameof(columns), column, "is not a column ExportColumns lists."),
        }).ToList();
    }

    /// <summary>The CSV text: a header of the columns, then one line per row.</summary>
    public static string Csv(IReadOnlyList<string> columns, IEnumerable<IReadOnlyList<string?>> rows)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        var csv = new StringBuilder();
        Line(csv, columns);

        foreach (var row in rows)
        {
            Line(csv, row);
        }

        return csv.ToString();
    }

    private static void Line(StringBuilder csv, IReadOnlyList<string?> values)
    {
        csv.AppendJoin(',', values.Select(Field));
        csv.Append("\r\n");
    }

    private static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        if (value[0] is '=' or '+' or '-' or '@')
        {
            value = "'" + value;
        }

        return value.AsSpan().IndexOfAny(",\"\r\n") >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }

    private static string? Date(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
