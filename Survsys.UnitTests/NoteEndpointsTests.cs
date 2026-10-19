using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the note endpoints make of a stored note, and what they refuse before
/// asking the database anything.
/// </summary>
/// <remarks>
/// No database and no host: that a note records and reads back, that it is
/// scoped to its report, that a struck note stays on the list and that a
/// missing report or note is a 404 are the queries' to answer against a real
/// SQL Server, which no session-012 snapshot seeds a row this session writes.
/// The values here are made up in the shape the legacy <c>note</c> table
/// takes, as the session-012 dump shows it: an administrative note written in
/// the second week of March.
/// </remarks>
public class NoteEndpointsTests
{
    private static readonly DateTimeOffset Written =
        new(2026, 3, 13, 0, 13, 17, TimeSpan.FromHours(-4));

    private static Note Recorded() => new()
    {
        Id = 11,
        PersonConditionId = 14,
        ExposureId = 31,
        FacilityId = 5,
        UserId = 11,
        NoteType = NoteEndpoints.AdministrativeNoteType,
        Text = "Event Created",
        Struckthrough = false,
        CreatedAt = Written,
    };

    [Fact]
    public void NoteToResponseCarriesTheNotesColumns()
    {
        var response = NoteEndpoints.Respond(Recorded());

        Assert.Equal(
            new NoteResponse(
                11,
                14,
                31,
                5,
                11,
                NoteEndpoints.AdministrativeNoteType,
                "Event Created",
                false,
                Written),
            response);
    }

    /// <summary>
    /// A note with no exposure, no facility and nobody named: every one of
    /// those columns is nullable, and a note that has none comes back with
    /// nulls rather than with zeros.
    /// </summary>
    [Fact]
    public void NoteToResponseCarriesABareNote()
    {
        var response = NoteEndpoints.Respond(new Note { Id = 12, CreatedAt = Written });

        Assert.Equal(
            new NoteResponse(12, null, null, null, null, null, null, false, Written),
            response);
    }

    /// <summary>
    /// A struck-through note projects as struck. The soft delete is a column
    /// and not a disappearance: the list returns it, and the screen shows it
    /// struck.
    /// </summary>
    [Fact]
    public void NoteToResponseCarriesAStruckThroughNoteAsStruck()
    {
        var note = Recorded();
        note.Struckthrough = true;

        Assert.True(NoteEndpoints.Respond(note).Struckthrough);
    }

    /// <summary>A private note projects as private, and a note by default as not.</summary>
    [Fact]
    public void NoteToResponseCarriesWhetherTheNoteIsPrivate()
    {
        var note = Recorded();
        Assert.False(NoteEndpoints.Respond(note).Private);

        note.Private = true;

        Assert.True(NoteEndpoints.Respond(note).Private);
    }

    [Theory]
    [InlineData(NoteEndpoints.ClinicalNoteType)]
    [InlineData(NoteEndpoints.AdministrativeNoteType)]
    public void ANoteOfEitherKindIsNotRefused(string noteType)
    {
        Assert.Null(NoteEndpoints.WhyNotANote(
            new CreateNoteRequest(Text: "Spoke to the patient", NoteType: noteType)));
    }

    /// <summary>
    /// A note needs nothing but its text and its type: the user and the
    /// exposure are optional, and a note that names neither is recorded.
    /// </summary>
    [Fact]
    public void ANoteNamingNeitherAUserNorAnExposureIsNotRefused()
    {
        Assert.Null(NoteEndpoints.WhyNotANote(
            new CreateNoteRequest(
                Text: "Left a voicemail",
                NoteType: NoteEndpoints.ClinicalNoteType,
                UserId: null,
                ExposureId: null)));
    }

    /// <summary>
    /// A note with nothing in it records nothing, so it is refused with the
    /// field named — whitespace included, because a text area that was tabbed
    /// through is as empty as one that was not typed in.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void ANoteWithNoTextIsRefused(string? text)
    {
        var why = NoteEndpoints.WhyNotANote(
            new CreateNoteRequest(Text: text, NoteType: NoteEndpoints.ClinicalNoteType));

        Assert.NotNull(why);
        Assert.Contains("text", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// Each shape of note type the endpoint refuses: absent, empty, a legacy
    /// kind this tier does not record, and one in the wrong case — the codes
    /// are legacy codes and not display text. The sentence names the field and
    /// the two codes, so a caller knows what to send instead.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("investigation")]
    [InlineData("Clinical")]
    public void ANoteOfAnUnknownKindIsRefused(string? noteType)
    {
        var why = NoteEndpoints.WhyNotANote(
            new CreateNoteRequest(Text: "Spoke to the patient", NoteType: noteType));

        Assert.NotNull(why);
        Assert.Contains("noteType", why, StringComparison.Ordinal);
        Assert.Contains(NoteEndpoints.ClinicalNoteType, why, StringComparison.Ordinal);
        Assert.Contains(NoteEndpoints.AdministrativeNoteType, why, StringComparison.Ordinal);
    }

    /// <summary>
    /// The missing text is named before the missing type, because a caller
    /// fixing one field at a time should be told about the one that makes the
    /// note a note.
    /// </summary>
    [Fact]
    public void TheMissingTextIsNamedBeforeTheMissingType()
    {
        var why = NoteEndpoints.WhyNotANote(new CreateNoteRequest());

        Assert.NotNull(why);
        Assert.Contains("text", why, StringComparison.Ordinal);
        Assert.DoesNotContain("noteType", why, StringComparison.Ordinal);
    }
}
