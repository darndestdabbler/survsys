namespace Survsys.Contracts;

/// <summary>
/// One review state a staged message entered, as its audit trail lists it on
/// <see cref="StagedMessageDetailResponse"/>.
/// </summary>
/// <param name="ReviewStatus">The review-state code the message entered.</param>
/// <param name="ExceptionCategory">The exception category it carried there, or null.</param>
/// <param name="ExceptionDetail">The exception detail it carried there, or null.</param>
/// <param name="OccurredAt">When it entered the state, in UTC.</param>
public record StagedMessageAuditResponse(
    string ReviewStatus,
    string? ExceptionCategory,
    string? ExceptionDetail,
    DateTime OccurredAt);
