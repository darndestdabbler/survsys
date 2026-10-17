namespace Survsys.Contracts;

/// <summary>
/// A source of an exposure, as the API tier returns it inside
/// <see cref="ExposureDetailResponse"/>.
/// </summary>
/// <remarks>
/// The legacy <c>exposure_source</c> table's columns, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, the row the Investigation tab's "+ Exposure" panel
/// records with the exposure as
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps it. <see cref="ExposureSourceRequest"/> with the identifier and the
/// exposure the server assigned: a recorded source reads back whole. The
/// person and the report are bare ids and the source type a bare code.
/// </remarks>
/// <param name="Id">The source row's identifier.</param>
/// <param name="ExposureId">The exposure this is a source of.</param>
/// <param name="PersonId">The person the condition came from, or null where the source is not a person on record.</param>
/// <param name="PersonConditionId">The case morbidity report the source is known by, or null where the source is not a case on record.</param>
/// <param name="SourceName">What the source is called, or null.</param>
/// <param name="SourceType">What kind of source this is, as a code, or null.</param>
public record ExposureSourceResponse(
    int Id,
    int ExposureId,
    int? PersonId,
    int? PersonConditionId,
    string? SourceName,
    string? SourceType);
