using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the batch contact import makes of a CSV: the contacts a well-formed
/// batch yields with their fields parsed, and the rows a malformed line, a
/// missing or unparseable required field and a duplicate are rejected as,
/// each named by its line.
/// </summary>
/// <remarks>
/// No database and no host: the parser is pure, and that a missing report is
/// a 404 is the endpoint's query's to answer. The lines here are made up in
/// the shape the legacy <c>contact_import_row</c> columns take.
/// </remarks>
public class ContactImportParserTests
{
    private const string Header =
        "first_name,middle_name,last_name,birth_date,sex,street,unit,city,state,zip,county,phone,email";

    [Fact]
    public void ParseAcceptsEveryRowOfAWellFormedBatchWithItsFieldsParsed()
    {
        var csv = Header + "\n"
            + "Ada,M,Lovelace,1990-12-10,F,12 Chapel St,4B,Hartford,CT,06103,003,860-555-0100,ada@example.test\n"
            + "Charles,,Babbage,1991-12-26,M,,,Hartford,CT,06103,,,\n";

        var response = ContactImportParser.Parse(csv);

        Assert.Empty(response.Rejected);
        Assert.Collection(
            response.Accepted,
            first => Assert.Equal(
                new ImportedContact(
                    2, "Ada", "M", "Lovelace", new DateOnly(1990, 12, 10), "F", "12 Chapel St", "4B",
                    "Hartford", "CT", "06103", "003", "860-555-0100", "ada@example.test"),
                first),
            second => Assert.Equal(
                new ImportedContact(
                    3, "Charles", null, "Babbage", new DateOnly(1991, 12, 26), "M", null, null,
                    "Hartford", "CT", "06103", null, null, null),
                second));
    }

    /// <summary>
    /// The header says which column is which: the legacy names with their
    /// <c>contact_</c> prefix, in any order, read the same as the bare ones.
    /// </summary>
    [Fact]
    public void ParseReadsTheColumnsByTheHeaderInAnyOrderWithOrWithoutTheLegacyPrefix()
    {
        var csv = "contact_last_name,Contact_Birth_Date,first_name\n"
            + "Lovelace,1990-12-10,Ada\n";

        var response = ContactImportParser.Parse(csv);

        Assert.Empty(response.Rejected);
        var contact = Assert.Single(response.Accepted);
        Assert.Equal("Lovelace", contact.LastName);
        Assert.Equal("Ada", contact.FirstName);
        Assert.Equal(new DateOnly(1990, 12, 10), contact.BirthDate);
        Assert.Null(contact.City);
    }

    /// <summary>
    /// A quoted field holds a comma, and a doubled quote inside it is one
    /// quote: the street "Flat 2, Chapel St" is one column, not two.
    /// </summary>
    [Fact]
    public void ParseReadsAQuotedFieldWithACommaAsOneField()
    {
        var csv = "last_name,birth_date,street,city\n"
            + "Lovelace,1990-12-10,\"Flat 2, \"\"The Chapel\"\"\",Hartford\n";

        var response = ContactImportParser.Parse(csv);

        Assert.Empty(response.Rejected);
        var contact = Assert.Single(response.Accepted);
        Assert.Equal("Flat 2, \"The Chapel\"", contact.Street);
        Assert.Equal("Hartford", contact.City);
    }

    /// <summary>
    /// A row with the wrong number of columns is rejected by its line, and
    /// the rows around it are read as they stand.
    /// </summary>
    [Fact]
    public void ParseRejectsARowWhoseColumnCountDoesNotMatchTheHeader()
    {
        var csv = "last_name,birth_date,city\n"
            + "Lovelace,1990-12-10,Hartford\n"
            + "Babbage,1991-12-26\n"
            + "Hopper,1906-12-09,Arlington\n";

        var response = ContactImportParser.Parse(csv);

        var problem = Assert.Single(response.Rejected);
        Assert.Equal(3, problem.Line);
        Assert.Contains("2 column(s) where the header names 3", problem.Reason, StringComparison.Ordinal);
        Assert.Equal([2, 4], response.Accepted.Select(contact => contact.Line));
    }

    [Fact]
    public void ParseRejectsARowWithNoLastName()
    {
        var csv = "first_name,last_name,birth_date\n"
            + "Ada,,1990-12-10\n";

        var response = ContactImportParser.Parse(csv);

        Assert.Empty(response.Accepted);
        var problem = Assert.Single(response.Rejected);
        Assert.Equal(2, problem.Line);
        Assert.StartsWith("last_name is empty", problem.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseRejectsARowWithAnEmptyOrUnparseableBirthDate()
    {
        var csv = "last_name,birth_date\n"
            + "Lovelace,\n"
            + "Babbage,26/12/1991\n";

        var response = ContactImportParser.Parse(csv);

        Assert.Empty(response.Accepted);
        Assert.Collection(
            response.Rejected,
            empty =>
            {
                Assert.Equal(2, empty.Line);
                Assert.StartsWith("birth_date is empty", empty.Reason, StringComparison.Ordinal);
            },
            unparseable =>
            {
                Assert.Equal(3, unparseable.Line);
                Assert.Contains("'26/12/1991' is not a date in the form yyyy-MM-dd", unparseable.Reason, StringComparison.Ordinal);
            });
    }

    /// <summary>
    /// The same name and birth date twice is one contact: the first row is
    /// accepted and the second rejected as a duplicate naming it, the
    /// comparison ignoring case and surrounding space.
    /// </summary>
    [Fact]
    public void ParseRejectsADuplicateOfAnEarlierRow()
    {
        var csv = "first_name,last_name,birth_date,city\n"
            + "Ada,Lovelace,1990-12-10,Hartford\n"
            + " ada , LOVELACE ,1990-12-10,New Haven\n"
            + "Ada,Lovelace,1990-12-11,Hartford\n";

        var response = ContactImportParser.Parse(csv);

        var problem = Assert.Single(response.Rejected);
        Assert.Equal(3, problem.Line);
        Assert.Contains("ada LOVELACE born 1990-12-10 is already in this batch", problem.Reason, StringComparison.Ordinal);

        // A different birth date is a different contact.
        Assert.Equal([2, 4], response.Accepted.Select(contact => contact.Line));
    }

    /// <summary>
    /// A blank line is neither a contact nor a fault, and the line numbers
    /// after it still count it, so a reason points at the line in the file.
    /// </summary>
    [Fact]
    public void ParseSkipsBlankLinesWithoutLosingCount()
    {
        var csv = "last_name,birth_date\r\n"
            + "\r\n"
            + "Lovelace,1990-12-10\r\n"
            + "\r\n";

        var response = ContactImportParser.Parse(csv);

        Assert.Empty(response.Rejected);
        var contact = Assert.Single(response.Accepted);
        Assert.Equal(3, contact.Line);
    }

    [Fact]
    public void ParseRejectsAHeaderThatNamesNoRequiredColumn()
    {
        var response = ContactImportParser.Parse("first_name,city\nAda,Hartford\n");

        Assert.Empty(response.Accepted);
        var problem = Assert.Single(response.Rejected);
        Assert.Equal(1, problem.Line);
        Assert.Contains("does not name last_name or birth_date", problem.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseRejectsAFileWithNoHeaderRow()
    {
        foreach (var empty in new[] { null, "", "\n\n" })
        {
            var response = ContactImportParser.Parse(empty);

            Assert.Empty(response.Accepted);
            var problem = Assert.Single(response.Rejected);
            Assert.Equal(1, problem.Line);
            Assert.StartsWith("the file has no header row", problem.Reason, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A header alone is a batch of nothing: nothing accepted and nothing
    /// rejected, because there was no row to read.
    /// </summary>
    [Fact]
    public void ParseAcceptsAHeaderWithNoRowsAsAnEmptyBatch()
    {
        var response = ContactImportParser.Parse(Header + "\n");

        Assert.Empty(response.Accepted);
        Assert.Empty(response.Rejected);
    }
}
