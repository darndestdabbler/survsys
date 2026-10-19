namespace Survsys.Contracts;

/// <summary>
/// One treatment as its editor shows it: the treatment and the conditions it is
/// listed for, each by id and name.
/// </summary>
/// <remarks>
/// The detail adds to <see cref="TreatmentResponse"/> what the editor reads and
/// the list does not: the conditions the treatment is listed for, so the editor
/// shows the set already chosen. A condition is named as well as identified so
/// the editor need not resolve the id itself.
/// </remarks>
/// <param name="Id">The treatment's identifier.</param>
/// <param name="Name">The treatment's name.</param>
/// <param name="Active">Whether the treatment may be listed and chosen.</param>
/// <param name="DefaultTreatment">Whether the treatment is a default for its conditions.</param>
/// <param name="Conditions">The conditions the treatment is listed for, ordered by name.</param>
public record TreatmentDetailResponse(
    int Id,
    string Name,
    bool Active,
    bool DefaultTreatment,
    IReadOnlyList<TreatmentConditionResponse> Conditions);

/// <summary>
/// A condition a treatment is listed for, as the treatment editor shows it: the
/// condition's id and name and nothing more.
/// </summary>
/// <param name="Id">The condition's identifier.</param>
/// <param name="Name">The condition's name.</param>
public record TreatmentConditionResponse(int Id, string Name);
