namespace Survsys.Contracts;

/// <summary>
/// An eCR sender to add or to save over one: what <c>POST /api/ecr-senders</c>
/// and <c>PUT /api/ecr-senders/{id}</c> take.
/// </summary>
/// <param name="SenderId">The custodian id its eICRs name, unique. Required.</param>
/// <param name="Name">The sender's name. Required.</param>
/// <param name="Stage">Its onboarding stage: <c>test</c>, <c>validation</c> or <c>production</c>.</param>
/// <param name="TakesLabResults">Whether its eICR results are written to a case as labs.</param>
public record SaveEcrSenderRequest(string SenderId, string Name, string Stage, bool TakesLabResults = false);
