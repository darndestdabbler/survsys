namespace Survsys.Data;

/// <summary>
/// A person: the human a case morbidity report is about, and what the
/// Demographic tab of that report shows and saves.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>person</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c> — with <c>current_gender</c> as <c>V0023</c> added it
/// and <c>country_of_birth</c> as <c>V0158</c> did — down to their snake_case
/// names, so that a later migration of the legacy data reads straight across
/// rather than through a column-name translation nobody kept.
///
/// <see cref="LastName"/> is the one required field, because
/// <c>V0081__alter_person_lastname_required.sql</c> made it <c>NOT NULL</c> in
/// the legacy schema. Everything else is nullable, and deliberately: the
/// legacy data leaves it so, and the Demographic tab saves a person before it
/// knows their ethnicity, their language or where they were born.
///
/// The demographic values are the legacy codes themselves — <c>F</c> for a
/// gender, <c>H</c> for an ethnicity, <c>es</c> for a language, <c>MEX</c> for
/// a country — held as plain strings. The legacy schema has a reference table
/// behind each of them; none of those tables exists here yet, and a navigation
/// to a table that does not exist is not a thing to write early.
///
/// The mortality columns (<c>date_of_death</c>, <c>death_status</c>,
/// <c>live</c>, <c>fatal_condition_id</c>), the pregnancy link
/// (<c>from_pregnant_id</c>) and the audit timestamp (<c>updated_at</c>) are
/// not here. The first are the Clinical tab's fields rather than the
/// Demographic tab's, and an audit column nothing in this schema writes would
/// be a half-feature.
///
/// There is no behaviour here. The ages the Demographic tab shows — current
/// age, age at onset, age at event date — are computed by the screen that
/// shows them, from <see cref="BirthDate"/> and the date it is comparing
/// against; none of them is stored.
/// </remarks>
public class Person
{
    public int Id { get; set; }

    /// <summary>
    /// The person's last name: the one name part the legacy schema requires,
    /// and the one a list orders by.
    /// </summary>
    public required string LastName { get; set; }

    /// <summary>The person's first name, where it is known.</summary>
    public string? FirstName { get; set; }

    /// <summary>The person's middle name, where it is known.</summary>
    public string? MiddleName { get; set; }

    /// <summary>
    /// The person's date of birth, where it is known.
    /// </summary>
    /// <remarks>
    /// A <see cref="DateOnly"/> where the legacy column is a
    /// <c>TIMESTAMP</c>: every person row in
    /// <c>../nedss/docs/db-snapshots/session-010/</c> holds midnight, and a
    /// date of birth has no time of day to lose. This is the one translation
    /// this entity makes from the legacy type, and it is on the record here.
    /// </remarks>
    public DateOnly? BirthDate { get; set; }

    /// <summary>
    /// The person's age in days, for a person whose age is known but whose
    /// birthday is not. The legacy fallback for an infant reported without a
    /// date of birth, and null for everyone else.
    /// </summary>
    public int? DaysOldNoBirthday { get; set; }

    /// <summary>The sex recorded at birth, as the legacy gender code.</summary>
    public string? BirthGender { get; set; }

    /// <summary>
    /// The person's current gender, as the legacy gender code. A separate
    /// column from <see cref="BirthGender"/> in the legacy schema and a
    /// separate field on the Demographic tab, so it stays separate here.
    /// </summary>
    public string? CurrentGender { get; set; }

    /// <summary>The person's ethnicity, as the legacy ethnicity code.</summary>
    public string? Ethnicity { get; set; }

    /// <summary>
    /// The person's primary language, as the legacy language code.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// The country the person was born in, as the legacy country code.
    /// </summary>
    public string? CountryOfBirth { get; set; }

    /// <summary>
    /// Every address this person has had, the current one included: the one
    /// with no <see cref="PersonAddress.Ending"/> is where they live now.
    /// </summary>
    public ICollection<PersonAddress> Addresses { get; } = [];

    /// <summary>Every telephone number recorded for this person.</summary>
    public ICollection<PersonTelephone> Telephones { get; } = [];

    /// <summary>Every email address recorded for this person.</summary>
    public ICollection<PersonEmail> Emails { get; } = [];

    /// <summary>
    /// Every race this person reported — as many as apply, which is what the
    /// Demographic tab's multi-select means.
    /// </summary>
    public ICollection<PersonRace> Races { get; } = [];

    /// <summary>Every other name this person is known by.</summary>
    public ICollection<PersonAlias> Aliases { get; } = [];

    /// <summary>
    /// This person's parents or guardians. The navigation is over
    /// <see cref="ParentGuardian.ChildId"/>, so these are the guardians
    /// <em>of</em> this person and not the people they are guardian to.
    /// </summary>
    public ICollection<ParentGuardian> Guardians { get; } = [];
}
