namespace Survsys.Contracts;

/// <summary>
/// The names of the reference code lists <c>/api/codes/{list}</c> serves, one
/// per legacy reference table a coded field is chosen from.
/// </summary>
/// <remarks>
/// The names are the snake_case the lists are stored under. A list is rows,
/// not schema, so these are every list the seed fills and the screens read;
/// a name not here is not a list.
/// </remarks>
public static class CodeLists
{
    public const string Gender = "gender";

    public const string Ethnicity = "ethnicity";

    public const string Race = "race";

    public const string Language = "language";

    public const string Country = "country";

    public const string State = "state";

    public const string County = "county";

    public const string ResidenceType = "residence_type";

    public const string TelephoneType = "telephone_type";

    public const string CaseStatus = "case_status";

    public const string ImportedFrom = "imported_from";

    public const string TransmissionMode = "transmission_mode";

    public const string Disposition = "disposition";

    public const string ContactType = "contact_type";

    public const string ExposurePlaceType = "exposure_place_type";

    public const string SourceType = "source_type";

    public const string SpecimenSource = "specimen_source";

    /// <summary>The test types, the one list whose codes are mapped to conditions.</summary>
    public const string TestType = "test_type";

    public const string TestStatus = "test_status";

    public const string Organism = "organism";

    public const string TestResult = "test_result";

    public const string ResistTestAgent = "resist_test_agent";

    public const string ResistTestResult = "resist_test_result";

    public const string TaskCategory = "task_category";

    public const string TaskPriority = "task_priority";

    public const string EncounterType = "encounter_type";

    public const string VaccineManufacturer = "vaccine_manufacturer";

    public const string VaccineInformationSource = "vaccine_information_source";

    public const string AttachmentCategory = "attachment_category";

    public const string ProviderType = "provider_type";

    /// <summary>Every list, in the order the administration screen offers them.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Gender,
        Ethnicity,
        Race,
        Language,
        Country,
        State,
        County,
        ResidenceType,
        TelephoneType,
        CaseStatus,
        ImportedFrom,
        TransmissionMode,
        Disposition,
        ContactType,
        ExposurePlaceType,
        SourceType,
        SpecimenSource,
        TestType,
        TestStatus,
        Organism,
        TestResult,
        ResistTestAgent,
        ResistTestResult,
        TaskCategory,
        TaskPriority,
        EncounterType,
        VaccineManufacturer,
        VaccineInformationSource,
        AttachmentCategory,
        ProviderType,
    ];
}
