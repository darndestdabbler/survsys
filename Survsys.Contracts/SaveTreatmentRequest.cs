namespace Survsys.Contracts;

/// <summary>
/// A treatment to create or to save over an existing one, together with the
/// conditions it is listed for: what <c>POST /api/treatments</c> and
/// <c>PUT /api/treatments/{id}</c> take.
/// </summary>
/// <remarks>
/// The name is required; the two flags default to a treatment that is active
/// and not a default. The conditions are the whole set the treatment is listed
/// for — a save replaces the set rather than adding to it — and each id, where
/// given, must be a condition this tier knows, which the endpoint refuses where
/// it is not. A null list is the empty set: the treatment is listed for no
/// condition.
/// </remarks>
/// <param name="Name">The treatment's name. Required.</param>
/// <param name="Active">Whether the treatment may be listed and chosen. Active by default.</param>
/// <param name="DefaultTreatment">Whether the treatment is a default for its conditions. Not by default.</param>
/// <param name="ConditionIds">The conditions the treatment is listed for, or null for none.</param>
public record SaveTreatmentRequest(
    string Name,
    bool Active = true,
    bool DefaultTreatment = false,
    IReadOnlyList<int>? ConditionIds = null);
