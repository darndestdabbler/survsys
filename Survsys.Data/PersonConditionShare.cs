namespace Survsys.Data;

/// <summary>
/// A read-only share of a case morbidity report with an agency: the row that
/// puts one agency's report into another agency's work queue to be seen, not
/// edited.
/// </summary>
/// <remarks>
/// This is this schema's own join and not a legacy table carried across: the
/// legacy has its own cross-agency visibility, and what is modelled here is the
/// read-only sharing the session plan names — a report, and an agency it is
/// shared with. A report shared with several agencies is several rows, one per
/// agency, which is what "multi-agency" means of it.
///
/// The agency is the <see cref="AgencyId"/> the report's
/// <see cref="PersonCondition.AgencyId"/> is, a reference into the stand-in
/// agency list the API tier serves rather than a foreign key to a table this
/// schema has — there is no <c>agency</c> table here yet, so there is no key to
/// point at. The report is a real foreign key, because
/// <see cref="PersonCondition"/> is a real table.
///
/// "Read-only" is a fact about what this row is for and not a rule this row
/// enforces: it carries no permission, and nothing in this schema stops a write
/// against a shared report. The queue surface offers no editing of a shared-in
/// report, and enforced per-agency authorization is a later concern. There is
/// an identity of its own and no navigation out; the pair of the report and the
/// agency is unique by the configuration, and the session that walks from a
/// share to its report or its agency adds the navigation it needs.
/// </remarks>
public class PersonConditionShare
{
    public int Id { get; set; }

    /// <summary>The report that is shared.</summary>
    public int PersonConditionId { get; set; }

    /// <summary>
    /// The agency the report is shared read-only with, as the stand-in agency
    /// list the API tier serves identifies it.
    /// </summary>
    public int AgencyId { get; set; }
}
