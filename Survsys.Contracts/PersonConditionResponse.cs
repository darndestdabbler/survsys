namespace Survsys.Contracts;

/// <summary>
/// A case morbidity report, as the API tier returns it.
/// </summary>
/// <remarks>
/// The whole of what this phase stores about a report, which is what the
/// creation flow writes: who it is about, what it is for, the number it is
/// known by, the agency investigating it, the two dates and when it was
/// created. Every other field of the legacy table belongs to a later session,
/// and so does every name behind an identifier here — the person's name and
/// the condition's are read from their own resources, because a report that
/// carried them would be two resources in one and would go stale when either
/// changed.
///
/// <see cref="RecordNumber"/> is the field a user is shown next: the legacy
/// flow ends by naming it, and it is how a report is referred to afterwards.
/// </remarks>
/// <param name="Id">The report's identifier, as every other resource refers to it.</param>
/// <param name="PersonId">The person the report is about.</param>
/// <param name="ConditionId">The disease or condition the report is for.</param>
/// <param name="RecordNumber">
/// The number the report is known by, in the legacy <c>YYYYNNNNN</c> format —
/// the year it was created in and the sequence within that year.
/// </param>
/// <param name="AgencyId">
/// The agency investigating the report, or null where none was chosen.
/// </param>
/// <param name="FirstReportedPhDate">
/// The date the condition was first reported to public health, or null where
/// it is not known.
/// </param>
/// <param name="EventOnsetDate">
/// The date the event began. Set to the creation date when the report is
/// created, which is what the legacy flow does; the Clinical tab is where it
/// is corrected.
/// </param>
/// <param name="CreatedAt">When the report was created.</param>
public record PersonConditionResponse(
    int Id,
    int PersonId,
    int ConditionId,
    string RecordNumber,
    int? AgencyId,
    DateOnly? FirstReportedPhDate,
    DateOnly? EventOnsetDate,
    DateTimeOffset CreatedAt);
