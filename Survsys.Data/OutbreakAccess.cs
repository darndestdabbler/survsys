namespace Survsys.Data;

/// <summary>
/// That an agency other than the owner may see an outbreak: one grant in the
/// multi-agency access an outbreak is shared across.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>outbreak_access</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>: the outbreak, the agency granted sight of it, and a
/// type. An agency is granted once per outbreak — the configuration keys the
/// pair — so a second grant of the same agency is the same row, not another.
/// </remarks>
public class OutbreakAccess
{
    public int Id { get; set; }

    /// <summary>The outbreak the grant is on.</summary>
    public int OutbreakId { get; set; }

    /// <summary>The agency granted sight of the outbreak.</summary>
    public int AgencyId { get; set; }

    /// <summary>
    /// The kind of grant, as the legacy <c>type</c> int; 1 until moved. A
    /// plain coded int, as the legacy holds it, with no reference table behind
    /// it.
    /// </summary>
    public int Type { get; set; } = 1;
}
