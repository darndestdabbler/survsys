namespace Survsys.Contracts;

/// <summary>
/// An outbreak to create or to save over an existing one: what
/// <c>POST /api/outbreaks</c> and <c>PUT /api/outbreaks/{id}</c> take.
/// </summary>
/// <remarks>
/// The name is required; everything else an outbreak can be opened without is
/// optional and may be left null. The condition and the owning agency, where
/// given, must be ones this tier knows, which the endpoint refuses where they
/// are not. The status is the legacy <c>outbreak.status</c> int — 0 Active, 1
/// Completed — and defaults to Active.
/// </remarks>
/// <param name="Name">The outbreak's name. Required.</param>
/// <param name="Number">The outbreak's number, or null for none.</param>
/// <param name="OutbreakDate">The day the outbreak is dated to, or null for none.</param>
/// <param name="Description">What the outbreak is, or null for none.</param>
/// <param name="ConditionId">The condition the outbreak is of, or null for none.</param>
/// <param name="AgencyId">The agency that owns the outbreak, or null for none.</param>
/// <param name="Status">The outbreak's status: 0 Active, 1 Completed.</param>
public record SaveOutbreakRequest(
    string Name,
    string? Number = null,
    DateOnly? OutbreakDate = null,
    string? Description = null,
    int? ConditionId = null,
    int? AgencyId = null,
    int Status = 0);
