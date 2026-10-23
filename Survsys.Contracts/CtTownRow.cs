namespace Survsys.Contracts;

/// <summary>
/// One Connecticut town, with the historical county and the planning region it
/// lies in: what fills a Connecticut address's two county values when it is
/// saved with its town alone.
/// </summary>
/// <param name="Town">The town's name, its key; a town is not renamed, a new name is a new town.</param>
/// <param name="County">The historical county's code on the county list, 09001 to 09015.</param>
/// <param name="PlanningRegion">The planning region's code on the county list, 09110 to 09190.</param>
public record CtTownRow(string Town, string County, string PlanningRegion);
