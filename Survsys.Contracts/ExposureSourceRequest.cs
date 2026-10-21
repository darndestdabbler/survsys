namespace Survsys.Contracts;

/// <summary>
/// A source to record an exposure as coming from: part of what
/// <c>POST /api/person-conditions/{id}/exposures</c> takes, nested under
/// <see cref="CreateExposureRequest"/>.
/// </summary>
/// <remarks>
/// The writable columns of the legacy <c>exposure_source</c> table, as
/// declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, less the identity and the exposure id, which the
/// server assigns and the nesting carries; the screen is the Investigation
/// tab's "+ Exposure" panel that
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps to <c>exposure</c> and its related <c>exposure_source</c>. A source
/// may be a person or a report on record, or no more than a name and a kind,
/// so every field is nullable; the person and the report are bare ids with no
/// check that they exist, and the source type a bare code.
/// </remarks>
/// <param name="PersonId">The person the condition came from, where the source is a person on record.</param>
/// <param name="PersonConditionId">The case morbidity report the source is known by, where the source is a case on record.</param>
/// <param name="SourceName">What the source is called.</param>
/// <param name="SourceType">What kind of source this is, as a code.</param>
public record ExposureSourceRequest(
    int? PersonId = null,
    int? PersonConditionId = null,
    string? SourceName = null,
    string? SourceType = null);
