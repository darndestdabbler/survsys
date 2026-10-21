namespace Survsys.Contracts;

/// <summary>
/// What processing the notification queue now came to: how many released
/// cases were taken off the queue, each built, sent and logged.
/// </summary>
/// <param name="Processed">How many queue entries were processed.</param>
public record NotificationQueueProcessResponse(int Processed);
