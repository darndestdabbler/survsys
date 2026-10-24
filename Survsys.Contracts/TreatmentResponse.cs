namespace Survsys.Contracts;

/// <summary>
/// A treatment, as the treatments list and a condition's treatment list show
/// it: the treatment's own columns and how many conditions it is listed for.
/// </summary>
/// <remarks>
/// The list row carries the count and not the conditions themselves, as
/// <see cref="OutbreakResponse"/> carries what a list needs and no more; the
/// conditions a treatment is listed for, by id and name, are in
/// <see cref="TreatmentDetailResponse"/>, which the one-treatment editor reads.
/// </remarks>
/// <param name="Id">The treatment's identifier, as a screen refers to it.</param>
/// <param name="Name">The treatment's name, as a list and a picker show it.</param>
/// <param name="Active">Whether the treatment may be listed and chosen.</param>
/// <param name="DefaultTreatment">Whether the treatment is a default for its conditions.</param>
/// <param name="ConditionCount">How many conditions the treatment is listed for.</param>
public record TreatmentResponse(
    int Id,
    string Name,
    bool Active,
    bool DefaultTreatment,
    int ConditionCount);
