namespace Survsys.Data;

/// <summary>
/// One of Connecticut's 169 towns, with the historical county and the planning
/// region it lies in: what fills a Connecticut address's two county values.
/// </summary>
/// <remarks>
/// A town lies wholly in one county and one planning region, where a zip code
/// can cross a region's boundary, so the town is what a region is read from.
/// Both are codes on the <c>county</c> list, their FIPS codes. The rows are
/// <see cref="CtTownSeed"/>.
/// </remarks>
public class CtTown
{
    /// <summary>The town's name, as the CT ELR guide's table of town names spells it.</summary>
    public required string Town { get; set; }

    /// <summary>The historical county's code, 09001 to 09015.</summary>
    public required string County { get; set; }

    /// <summary>The planning region's code, 09110 to 09190.</summary>
    public required string PlanningRegion { get; set; }
}
