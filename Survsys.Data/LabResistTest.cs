namespace Survsys.Data;

/// <summary>
/// An antimicrobial susceptibility test on a laboratory specimen: how one
/// organism answered one agent, and whether it resisted.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>lab_resist_test</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, down to their snake_case names.
///
/// A susceptibility test hangs off a specimen rather than off a test, as the
/// legacy table does: <see cref="LabId"/> is where it belongs. The organism,
/// the agent it was challenged with and the coded outcome are bare nullable
/// ids into reference tables that do not exist here yet, exactly as the
/// specimen's facility and source are.
///
/// There is no behaviour here and no navigation out.
/// </remarks>
public class LabResistTest
{
    public int Id { get; set; }

    /// <summary>The specimen this susceptibility test was run on.</summary>
    public int LabId { get; set; }

    /// <summary>
    /// The organism challenged, as the legacy <c>organism_id</c>. A bare
    /// nullable id into the <c>organism</c> reference table, which does not
    /// exist here yet.
    /// </summary>
    public int? OrganismId { get; set; }

    /// <summary>
    /// The antimicrobial agent the organism was challenged with, as the legacy
    /// <c>resist_test_agent_id</c>. A bare nullable id into the reference table
    /// that names the agents, which does not exist here yet.
    /// </summary>
    public int? ResistTestAgentId { get; set; }

    /// <summary>
    /// The coded outcome — resistant, susceptible and the rest — as the legacy
    /// <c>resist_test_result_id</c>. A bare nullable id into the reference
    /// table that names the outcomes, which does not exist here yet.
    /// </summary>
    public int? ResistTestResultId { get; set; }

    /// <summary>
    /// The date the susceptibility test was run, where it is known.
    /// </summary>
    /// <remarks>
    /// A <see cref="DateOnly"/> where the legacy column is a <c>TIMESTAMP</c>,
    /// for the reason <see cref="Lab.CollectionDate"/> is one.
    /// </remarks>
    public DateOnly? TestDate { get; set; }
}
