namespace Survsys.Data;

/// <summary>
/// A contact of an exposure: one person who was at the exposure and may have
/// caught the condition from it, with the time they were there and the
/// disposition the investigation reached about them.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>exposure_contact</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, down to their snake_case names. The screen is the
/// Contacts tab that
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps: a contact is a full person record linked to an exposure through this
/// table, and the tab's data table shows the contact's type, disposition and
/// disposition date beside the person.
///
/// A contact is a detail of the exposure it was at, so <see cref="ExposureId"/>
/// is required and a contact goes when its exposure does; the person is
/// required too, and kept, as <see cref="Lab.PersonId"/> is — a person with a
/// contact record against them is not deleted out from under it. The
/// disposition is a bare nullable id into the <c>contact_disposition_type</c>
/// reference table and the contact type a plain code string into
/// <c>contact_type</c>, neither of which exists here yet, exactly as the
/// Clinical and Laboratory slices hold their coded fields. All three are set
/// when the contact is recorded with its exposure and not changed through an
/// endpoint of their own. There is no behaviour here and no navigation out.
/// </remarks>
public class ExposureContact
{
    public int Id { get; set; }

    /// <summary>The exposure this contact was at.</summary>
    public int ExposureId { get; set; }

    /// <summary>The person who was the contact.</summary>
    public int PersonId { get; set; }

    /// <summary>The first day this contact was at the exposure, where it is known.</summary>
    /// <remarks>
    /// A <see cref="DateOnly"/> where the legacy column is a <c>TIMESTAMP</c>,
    /// for the reason <see cref="Exposure.ExposureStart"/> is one.
    /// </remarks>
    public DateOnly? ExposureStart { get; set; }

    /// <summary>The last day this contact was at the exposure, where it is known.</summary>
    /// <remarks>
    /// A <see cref="DateOnly"/> where the legacy column is a <c>TIMESTAMP</c>,
    /// for the reason <see cref="Exposure.ExposureEnd"/> is one.
    /// </remarks>
    public DateOnly? ExposureEnd { get; set; }

    /// <summary>
    /// What the investigation decided about this contact, as the legacy
    /// <c>disposition_id</c> — the Contacts tab's "Contact Disposition". A
    /// bare nullable id into the <c>contact_disposition_type</c> reference
    /// table, which does not exist here yet.
    /// </summary>
    public int? DispositionId { get; set; }

    /// <summary>
    /// What kind of contact this was — the Contacts tab's "Contact Type" — as a
    /// code; the legacy column is a plain <c>VARCHAR</c> and the
    /// <c>contact_type</c> reference table does not exist here yet.
    /// </summary>
    public string? ContactType { get; set; }

    /// <summary>
    /// The day the disposition was reached, where it is known — the Contacts
    /// tab's "Disposition date". A <see cref="DateOnly"/> for the legacy
    /// <c>DATE</c>.
    /// </summary>
    public DateOnly? DispositionDate { get; set; }
}
