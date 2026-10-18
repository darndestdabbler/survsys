namespace Survsys.Contracts;

/// <summary>
/// One outbreak as its detail screen shows it: the outbreak, the condition and
/// owning agency by name as well as by id, and the agencies granted sight of
/// it.
/// </summary>
/// <remarks>
/// The detail adds to <see cref="OutbreakResponse"/> what the one-outbreak
/// screen reads and the list does not: the condition's and the owning agency's
/// names, joined by the query, and the multi-agency access as a list of
/// <see cref="OutbreakAccessResponse"/>. A name is null where its id is.
/// </remarks>
/// <param name="Id">The outbreak's identifier.</param>
/// <param name="Number">The outbreak's number, or null where it has none.</param>
/// <param name="Name">The outbreak's name.</param>
/// <param name="OutbreakDate">The day the outbreak is dated to, or null where unset.</param>
/// <param name="Description">What the outbreak is, or null where unwritten.</param>
/// <param name="ConditionId">The condition the outbreak is of, or null where unset.</param>
/// <param name="ConditionName">The condition's name, or null where none is set.</param>
/// <param name="AgencyId">The agency that owns the outbreak, or null where unset.</param>
/// <param name="AgencyName">The owning agency's name, or null where none is set.</param>
/// <param name="Status">The outbreak's status: 0 Active, 1 Completed.</param>
/// <param name="CreatedAt">When the outbreak was opened.</param>
/// <param name="Access">The agencies granted sight of the outbreak, ordered by name.</param>
public record OutbreakDetailResponse(
    int Id,
    string? Number,
    string Name,
    DateOnly? OutbreakDate,
    string? Description,
    int? ConditionId,
    string? ConditionName,
    int? AgencyId,
    string? AgencyName,
    int Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OutbreakAccessResponse> Access);
