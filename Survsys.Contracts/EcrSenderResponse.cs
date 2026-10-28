namespace Survsys.Contracts;

/// <summary>
/// One eCR sender: the organisation an eICR's custodian names, where it is in
/// onboarding, and its settings.
/// </summary>
/// <param name="Id">The sender's identifier.</param>
/// <param name="SenderId">The custodian id its eICRs name, root and extension joined by a caret.</param>
/// <param name="Name">The sender's name.</param>
/// <param name="Stage">Its onboarding stage: <c>test</c>, <c>validation</c> or <c>production</c>.</param>
/// <param name="TakesLabResults">Whether its eICR results are written to a case as labs.</param>
public record EcrSenderResponse(int Id, string SenderId, string Name, string Stage, bool TakesLabResults);
