namespace Survsys.Contracts;

/// <summary>
/// One field of a case morbidity report that core element configuration can
/// show or hide, with whether it is hidden by default.
/// </summary>
/// <param name="Id">The element's identifier.</param>
/// <param name="Uid">The identifier a report tab names the field by.</param>
/// <param name="Label">The field's label.</param>
/// <param name="Tab">The report tab the field is on.</param>
/// <param name="Hidden">Whether the field is hidden where no override says otherwise.</param>
public record CoreElementResponse(int Id, string Uid, string Label, string Tab, bool Hidden);
