using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the attachment endpoints make of a stored attachment, and what they
/// refuse before asking the database anything.
/// </summary>
/// <remarks>
/// No database and no host: that a file stores and reads back, that it is
/// scoped to its report, that the bytes come out of the download under the
/// content type they went in as and that a missing report or attachment is a
/// 404 are the queries' to answer against a real SQL Server, which no
/// session-012 snapshot seeds a row this session writes — the legacy
/// <c>attachment</c> and <c>attachment_file</c> tables are empty in every
/// dump. What is proved here is the whole of the size limit and the
/// content-type check, which is pure, and the projection the list is built
/// from.
///
/// The values are made up in the shape the legacy <c>attachment</c> table
/// takes: a scanned laboratory result filed against a report in the second
/// week of March.
/// </remarks>
public class AttachmentEndpointsTests
{
    private const string Pdf = "application/pdf";

    private const string LabCategory = "lab";

    private static readonly string[] Categories = ["correspondence", LabCategory, "other"];

    private static readonly DateTimeOffset Attached =
        new(2026, 3, 13, 9, 41, 2, TimeSpan.FromHours(-6));

    private static Attachment Stored() => new()
    {
        Id = 7,
        PersonConditionId = 14,
        AttachmentFileId = 3,
        Filename = "culture-result.pdf",
        ContentType = Pdf,
        Size = 184_320,
        Category = LabCategory,
        CreatedAt = Attached,
    };

    [Fact]
    public void AttachmentToResponseCarriesTheAttachmentsColumns()
    {
        var response = AttachmentEndpoints.Respond(Stored());

        Assert.Equal(
            new AttachmentResponse(
                7,
                14,
                "culture-result.pdf",
                Pdf,
                184_320,
                LabCategory,
                Attached),
            response);
    }

    /// <summary>A file filed against an outbreak carries the outbreak and no report.</summary>
    [Fact]
    public void AttachmentToResponseCarriesAnOutbreaksAttachment()
    {
        var attachment = Stored();
        attachment.PersonConditionId = null;
        attachment.OutbreakId = 9;

        var response = AttachmentEndpoints.Respond(attachment);

        Assert.Null(response.PersonConditionId);
        Assert.Equal(9, response.OutbreakId);
    }

    [Fact]
    public void AnAttachmentThisTierStoresIsNotRefused() =>
        Assert.Null(WhyNot(size: 184_320));

    [Fact]
    public void AFileWithNoNameIsRefused()
    {
        var why = WhyNot(filename: "   ");

        Assert.NotNull(why);
        Assert.Contains("filename", why, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyFileIsRefused()
    {
        var why = WhyNot(size: 0);

        Assert.NotNull(why);
        Assert.Contains("empty", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// The limit is the legacy's own 30 MB, and it is a limit rather than a
    /// bound to stay under: a file of exactly that size is stored.
    /// </summary>
    [Fact]
    public void AFileOfExactlyTheLimitIsStored() =>
        Assert.Null(WhyNot(size: AttachmentEndpoints.MaxBytes));

    [Fact]
    public void AFileOverTheLimitIsRefused()
    {
        var why = WhyNot(size: AttachmentEndpoints.MaxBytes + 1);

        Assert.NotNull(why);
        Assert.Contains(
            AttachmentEndpoints.MaxBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            why,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AKindOfFileThisTierDoesNotStoreIsRefused()
    {
        var why = WhyNot(contentType: "application/x-msdownload");

        Assert.NotNull(why);
        Assert.Contains("contentType", why, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileDeclaringNoKindAtAllIsRefused()
    {
        var why = WhyNot(contentType: null);

        Assert.NotNull(why);
        Assert.Contains("contentType", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// A caller that names the media type correctly and says what encoding it
    /// is in has not named a different type: the parameters after the
    /// <c>;</c> are not part of what the allowlist holds.
    /// </summary>
    [Fact]
    public void AKindOfFileCarryingItsEncodingIsStored() =>
        Assert.Null(WhyNot(contentType: "text/plain; charset=utf-8"));

    /// <summary>
    /// Media types are case-insensitive, and a client that shouts one has
    /// still named the type this tier stores.
    /// </summary>
    [Fact]
    public void AKindOfFileInAnyCaseIsStored() =>
        Assert.Null(WhyNot(contentType: "APPLICATION/PDF"));

    /// <summary>
    /// A category is accepted when it is one of the categories on the list as
    /// read, whichever they are.
    /// </summary>
    [Theory]
    [InlineData("correspondence")]
    [InlineData("lab")]
    [InlineData("other")]
    public void WhyNotACategoryAcceptsEachCategoryOnTheList(string category) =>
        Assert.Null(AttachmentEndpoints.WhyNotACategory(category, Categories));

    [Fact]
    public void WhyNotACategoryAcceptsACategoryAddedToTheList() =>
        Assert.Null(AttachmentEndpoints.WhyNotACategory("photo", [.. Categories, "photo"]));

    /// <summary>
    /// A category that is not on the list, or none, is refused with a sentence
    /// naming the ones that are.
    /// </summary>
    [Fact]
    public void WhyNotACategoryRefusesACategoryNotOnTheList()
    {
        var why = AttachmentEndpoints.WhyNotACategory("radiology", Categories);

        Assert.NotNull(why);
        Assert.Contains("category 'radiology' is not one", why, StringComparison.Ordinal);
        Assert.Contains("correspondence, lab, other", why, StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotACategoryRefusesNoCategory()
    {
        var why = AttachmentEndpoints.WhyNotACategory(null, Categories);

        Assert.NotNull(why);
        Assert.Contains("is not one an attachment is filed under", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// One attachment this tier stores, with whichever part of it the case
    /// under test replaces.
    /// </summary>
    private static string? WhyNot(
        string? filename = "culture-result.pdf",
        string? contentType = Pdf,
        long size = 184_320) =>
        AttachmentEndpoints.WhyNotAnAttachment(filename, contentType, size);
}
