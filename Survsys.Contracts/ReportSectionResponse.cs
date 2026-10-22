namespace Survsys.Contracts;

/// <summary>
/// One section of an outbreak report, as a report editor's list of headings
/// shows it.
/// </summary>
/// <remarks>
/// The seven report sections are a fixed reference set, so this is the whole of
/// what an editor needs to lay the report out: the section's id, which the
/// text is saved against, and its heading, which the editor shows above the
/// text.
/// </remarks>
/// <param name="Id">The section's identifier, as the report text is saved against it.</param>
/// <param name="Description">The section's heading, as the editor shows it.</param>
public record ReportSectionResponse(int Id, string Description);
