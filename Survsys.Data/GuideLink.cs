namespace Survsys.Data;

/// <summary>
/// A guide a guide pulls in alongside its own: when a case is exported under a
/// guide, the PHIN variables of the guides it links are carried too.
/// </summary>
/// <remarks>
/// The columns are the legacy NMI <c>guide_link</c> table, as described in
/// <c>../nedss/docs/business-logic/NMI_XML_GENERATION.md</c>, where a case
/// processes the variables of its guide "plus linked guides". Keyed by the
/// pair, so a guide links another once.
///
/// <see cref="GuideId"/> and <see cref="LinkedGuideId"/> are bare ids into
/// <c>guide</c>, held as plain ints and not navigations, as the existing
/// reference entities are. There is no behaviour here and no navigation out.
/// </remarks>
public class GuideLink
{
    /// <summary>The guide that does the linking, as a bare id into <c>guide</c>.</summary>
    public int GuideId { get; set; }

    /// <summary>
    /// The guide pulled in alongside, as a bare id into <c>guide</c>.
    /// </summary>
    public int LinkedGuideId { get; set; }
}
