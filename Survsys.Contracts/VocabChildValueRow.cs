namespace Survsys.Contracts;

/// <summary>
/// A sending lab's own code for a specimen, a test result or an organism, and
/// the master it resolves to for that lab: the lab-specific override, as
/// <c>/api/vocabulary/child-values</c> lists, saves and carries in CSV.
/// </summary>
/// <param name="Id">The entry's identifier; ignored when one is created.</param>
/// <param name="LabId">The sending facility, as <c>GET /api/facilities</c> lists it.</param>
/// <param name="Category">The category: <c>specimen</c>, <c>test_result</c> or <c>organism</c>.</param>
/// <param name="ChildCode">The lab's own code.</param>
/// <param name="Master">
/// The master it resolves to, one on file: a master value's concept in its
/// category, or for <c>organism</c> a master organism's SNOMED.
/// </param>
public record VocabChildValueRow(
    int Id,
    int LabId,
    string Category,
    string ChildCode,
    string Master);
