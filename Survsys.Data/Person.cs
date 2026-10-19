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
/// Two mortality columns are here as of session 18:
/// <see cref="DeathStatus"/> and <see cref="DateOfDeath"/>, which the legacy
/// Clinical tab writes — <c>../nedss/docs/functional/epitrax-cmr-deep-editing.md</c>
/// section 3 maps the Died? control to <c>death_status</c> and the date beside
/// it to <c>date_of_death</c>. They are Clinical-tab fields, not the
/// Demographic tab's, and session 13 deferred them here. The rest of the
/// legacy mortality columns stay out: <c>live</c> is the Died? answer
/// <see cref="DeathStatus"/> already carries and would duplicate it,
/// <c>fatal_condition_id</c> and the pregnancy link <c>from_pregnant_id</c> are
/// a later session's, and the audit timestamp <c>updated_at</c> is not here
/// because a column nothing in this schema writes would be a half-feature.
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
    /// Whether the person has died, as the legacy <c>death_status</c> code —
    /// the Clinical tab's Died? answer, one of <c>Yes</c>, <c>No</c> or
    /// <c>Unknown</c>, and null until it has been answered.
    /// </summary>
    /// <remarks>
    /// A Clinical-tab field, not the Demographic tab's, added in session 18:
    /// <c>../nedss/docs/functional/epitrax-cmr-deep-editing.md</c> section 3
    /// maps the Died? control to this column. A plain string holding the legacy
    /// code, as the other demographic codes are held, because the reference
    /// table behind it does not exist here yet.
    /// </remarks>
    public string? DeathStatus { get; set; }

    /// <summary>
    /// The date the person died, where it is known; null otherwise, and blank
    /// whenever <see cref="DeathStatus"/> is not <c>Yes</c>.
    /// </summary>
    /// <remarks>
    /// A Clinical-tab field added in session 18, paired with
    /// <see cref="DeathStatus"/> and mapped by
    /// <c>../nedss/docs/functional/epitrax-cmr-deep-editing.md</c> section 3. A
    /// <see cref="DateOnly"/> where the legacy column is a <c>TIMESTAMP</c>,
    /// for the reason <see cref="BirthDate"/> is one: a date of death has no
    /// time of day to lose and the Clinical tab asks for it with a date picker.
    /// </remarks>
    public DateOnly? DateOfDeath { get; set; }

    /// <summary>
    /// Every address this person has had, the current one included: the one
    /// with no <see cref="PersonAddress.Ending"/> is where they live now, and
    /// there is at most one such, which <see cref="AddressRules.CloseHistoric"/>
    /// keeps and a filtered unique index enforces.
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

    /// <summary>Every identifier other systems know this person by.</summary>
    public ICollection<PersonIdentifier> Identifiers { get; } = [];

    /// <summary>Every name this person was on file under before the current one.</summary>
    public ICollection<PersonNameHistory> NameHistory { get; } = [];

    /// <summary>
    /// This person's parents or guardians. The navigation is over
    /// <see cref="ParentGuardian.ChildId"/>, so these are the guardians
    /// <em>of</em> this person and not the people they are guardian to.
    /// </summary>
    public ICollection<ParentGuardian> Guardians { get; } = [];
}
