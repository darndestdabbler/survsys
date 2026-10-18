using System.Globalization;
using System.Text;
using Survsys.Contracts;

namespace Survsys.Api;

/// <summary>
/// The batch contact import's parser: a CSV of contacts in, and the contacts
/// it would accept with the rows it rejects out.
/// </summary>
/// <remarks>
/// The legacy Contacts tab's "Import" button, as
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// lists it, takes a file of contacts into the <c>contact_import_batch</c>
/// and <c>contact_import_row</c> tables that
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c> declares. This is the reading of such a file and
/// nothing after it: the parser answers with what it would accept and what it
/// would not, and writes nothing — no person, no contact, no import row.
///
/// Pure, and public because the unit suite proves it without starting a host,
/// the shape <see cref="RecordNumber"/> and <see cref="Paging"/> have in this
/// tier. It touches no database and names nothing from <c>Survsys.Data</c>;
/// whether the report the batch is posted against exists is the endpoint's to
/// ask.
///
/// The file is a header row naming the columns, then one contact per row. The
/// column names are the legacy <c>contact_import_row</c> fields, with or
/// without their <c>contact_</c> prefix and in any order: <c>first_name</c>,
/// <c>middle_name</c>, <c>last_name</c>, <c>birth_date</c>, <c>sex</c>,
/// <c>street</c>, <c>unit</c>, <c>city</c>, <c>state</c>, <c>zip</c>,
/// <c>county</c>, <c>phone</c> and <c>email</c>. A header is required to name
/// <c>last_name</c> and <c>birth_date</c>, because those are the required
/// fields; a column the parser does not know is read past. A field may be
/// quoted, and a quoted field may hold a comma or a doubled quote.
///
/// A row is rejected as malformed when its column count does not match the
/// header's or when a required field is missing or unparseable — a last name
/// that is empty, a birth date that is empty or not <c>yyyy-MM-dd</c> — and
/// as a duplicate when a contact of the same name and birth date appeared
/// earlier in the batch, the comparison ignoring case and surrounding space.
/// Every rejection carries the one-based line it was read from, the header
/// being line 1, and a reason naming what was wrong. A blank line is neither
/// accepted nor rejected.
/// </remarks>
public static class ContactImportParser
{
    /// <summary>The form a birth date takes in the file.</summary>
    public const string DateForm = "yyyy-MM-dd";

    /// <summary>The prefix the legacy columns carry, read past where it is given.</summary>
    private const string LegacyPrefix = "contact_";

    private const string FirstName = "first_name";
    private const string MiddleName = "middle_name";
    private const string LastName = "last_name";
    private const string BirthDate = "birth_date";
    private const string Sex = "sex";
    private const string Street = "street";
    private const string Unit = "unit";
    private const string City = "city";
    private const string State = "state";
    private const string Zip = "zip";
    private const string County = "county";
    private const string Phone = "phone";
    private const string Email = "email";

    /// <summary>
    /// Reads a CSV of contacts and answers with the contacts it would accept
    /// and the rows it rejects.
    /// </summary>
    /// <remarks>
    /// A file with no header row, or a header that does not name both
    /// required columns, is answered as one rejection at line 1 and nothing
    /// accepted: there is no way to read a row without knowing which column
    /// holds the last name.
    /// </remarks>
    public static ContactImportResponse Parse(string? csv)
    {
        var lines = (csv ?? string.Empty).Split(["\r\n", "\n"], StringSplitOptions.None);

        var headerAt = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));

        if (headerAt < 0)
        {
            return new ContactImportResponse(
                [],
                [new ContactImportProblem(1, "the file has no header row: the first line names the columns, "
                    + $"and must name at least {LastName} and {BirthDate}.")]);
        }

        var header = Fields(lines[headerAt]).Select(Column).ToList();

        var missing = new[] { LastName, BirthDate }.Where(required => !header.Contains(required)).ToList();

        if (missing.Count > 0)
        {
            return new ContactImportResponse(
                [],
                [new ContactImportProblem(headerAt + 1, $"the header row does not name {string.Join(" or ", missing)}: "
                    + "a contact is imported by last name and birth date, and the header must say "
                    + "which columns hold them.")]);
        }

        var accepted = new List<ImportedContact>();
        var rejected = new List<ContactImportProblem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var at = headerAt + 1; at < lines.Length; at++)
        {
            var lineNumber = at + 1;
            var raw = lines[at];

            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var fields = Fields(raw);

            if (fields.Count != header.Count)
            {
                rejected.Add(new ContactImportProblem(
                    lineNumber,
                    $"the row has {fields.Count} column(s) where the header names {header.Count}: "
                    + "every row carries one field per header column, quoted where a field holds a comma."));

                continue;
            }

            var row = new Dictionary<string, string?>(StringComparer.Ordinal);

            for (var column = 0; column < header.Count; column++)
            {
                row[header[column]] = Blank(fields[column]);
            }

            var lastName = row.GetValueOrDefault(LastName);

            if (lastName is null)
            {
                rejected.Add(new ContactImportProblem(
                    lineNumber,
                    $"{LastName} is empty, and a contact is imported by last name and birth date."));

                continue;
            }

            var birthDateText = row.GetValueOrDefault(BirthDate);

            if (birthDateText is null)
            {
                rejected.Add(new ContactImportProblem(
                    lineNumber,
                    $"{BirthDate} is empty, and a contact is imported by last name and birth date."));

                continue;
            }

            if (!DateOnly.TryParseExact(birthDateText, DateForm, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var birthDate))
            {
                rejected.Add(new ContactImportProblem(
                    lineNumber,
                    $"{BirthDate} '{birthDateText}' is not a date in the form {DateForm}."));

                continue;
            }

            var firstName = row.GetValueOrDefault(FirstName);
            var middleName = row.GetValueOrDefault(MiddleName);

            var key = string.Join("|",
                lastName, firstName ?? string.Empty, middleName ?? string.Empty,
                birthDate.ToString(DateForm, CultureInfo.InvariantCulture));

            if (!seen.Add(key))
            {
                rejected.Add(new ContactImportProblem(
                    lineNumber,
                    $"{Named(firstName, middleName, lastName)} born {birthDate.ToString(DateForm, CultureInfo.InvariantCulture)} "
                    + "is already in this batch on an earlier line: a contact is imported once."));

                continue;
            }

            accepted.Add(new ImportedContact(
                lineNumber,
                firstName,
                middleName,
                lastName,
                birthDate,
                row.GetValueOrDefault(Sex),
                row.GetValueOrDefault(Street),
                row.GetValueOrDefault(Unit),
                row.GetValueOrDefault(City),
                row.GetValueOrDefault(State),
                row.GetValueOrDefault(Zip),
                row.GetValueOrDefault(County),
                row.GetValueOrDefault(Phone),
                row.GetValueOrDefault(Email)));
        }

        return new ContactImportResponse(accepted, rejected);
    }

    /// <summary>
    /// A header cell as a column name: trimmed, lower-cased, and with the
    /// legacy <c>contact_</c> prefix read past where it is given.
    /// </summary>
    private static string Column(string cell)
    {
        var name = cell.Trim().ToLowerInvariant();

        return name.StartsWith(LegacyPrefix, StringComparison.Ordinal)
            ? name[LegacyPrefix.Length..]
            : name;
    }

    /// <summary>A contact's name as a rejection names it: the parts given, in speaking order.</summary>
    private static string Named(string? firstName, string? middleName, string? lastName) =>
        string.Join(" ", new[] { firstName, middleName, lastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>A field's content, trimmed, or null where it holds nothing.</summary>
    private static string? Blank(string field) =>
        string.IsNullOrWhiteSpace(field) ? null : field.Trim();

    /// <summary>
    /// One line split into its fields: comma-separated, with a field quoted in
    /// double quotes allowed to hold a comma, and a doubled quote inside a
    /// quoted field read as one quote.
    /// </summary>
    private static List<string> Fields(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var at = 0; at < line.Length; at++)
        {
            var character = line[at];

            if (quoted)
            {
                if (character == '"')
                {
                    if (at + 1 < line.Length && line[at + 1] == '"')
                    {
                        field.Append('"');
                        at++;
                    }
                    else
                    {
                        quoted = false;
                    }
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
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        fields.Add(field.ToString());

        return fields;
    }
}
