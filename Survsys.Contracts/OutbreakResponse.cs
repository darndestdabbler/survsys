namespace Survsys.Contracts;

/// <summary>
/// An outbreak, as the outbreaks list shows it: the outbreak's own columns,
/// with the condition and the owning agency as ids a screen resolves to names
/// from the lists it already holds.
/// </summary>
/// <remarks>
/// The list row carries ids and not names, as <see cref="AgencyResponse"/>
/// carries what a dropdown needs and no more: the owning agency's and the
/// condition's names are joined in <see cref="OutbreakDetailResponse"/>, which
/// is what the one-outbreak screen reads. The status is the legacy
/// <c>outbreak.status</c> int — 0 is Active and 1 is Completed, the two values
/// of the legacy <c>OutbreakStatus</c> enum.
/// </remarks>
/// <param name="Id">The outbreak's identifier, as a case and a screen refer to it.</param>
/// <param name="Number">The outbreak's number, or null where it has none.</param>
/// <param name="Name">The outbreak's name, as a list and a dropdown show it.</param>
/// <param name="OutbreakDate">The day the outbreak is dated to, or null where unset.</param>
/// <param name="Description">What the outbreak is, or null where unwritten.</param>
/// <param name="ConditionId">The condition the outbreak is of, or null where unset.</param>
/// <param name="AgencyId">The agency that owns the outbreak, or null where unset.</param>
/// <param name="Status">The outbreak's status: 0 Active, 1 Completed.</param>
/// <param name="CreatedAt">When the outbreak was opened.</param>
public record OutbreakResponse(
    int Id,
    string? Number,
    string Name,
    DateOnly? OutbreakDate,
    string? Description,
    int? ConditionId,
    int? AgencyId,
    int Status,
    DateTimeOffset CreatedAt);
