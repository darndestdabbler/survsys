namespace Survsys.Contracts;

/// <summary>
/// A form assigned to a case, as the case's list of forms to complete shows it:
/// the assignment, the form it is of, and when it was assigned.
/// </summary>
/// <remarks>
/// The completion screen opens a form by its <see cref="Id"/> — the assignment
/// id — through <c>GET /api/cases/forms/{assignmentId}</c>, which answers the
/// form's tree and the case's answers so far.
/// </remarks>
/// <param name="Id">The assignment's identifier.</param>
/// <param name="FormId">The form that was assigned.</param>
/// <param name="FormName">The form's name, as the list shows it.</param>
/// <param name="AssignedAt">When the form was assigned to the case.</param>
public record FormAssignmentResponse(
    int Id,
    int FormId,
    string FormName,
    DateTimeOffset AssignedAt);
