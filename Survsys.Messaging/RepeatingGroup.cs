namespace Survsys.Messaging;

/// <summary>
/// A dataset repeating group: a set of PHIN variables that repeat together,
/// once per row of a named dataset (a lab, vaccine, treatment or travel record),
/// so each appears once per row in the national notification message.
/// </summary>
/// <remarks>
/// This is the dataset repeating group of the legacy NMI XML generation
/// described in <c>../nedss/docs/business-logic/NMI_XML_GENERATION.md</c>, kept
/// to the four datasets the session plan names. It is plain in-memory data the
/// caller fills: each variable carries its loaded ruleset and the formatter its
/// type is evaluated by, so the processor needs no registry and this module
/// keeps its fence against the database.
/// </remarks>
/// <param name="GroupName">The group's name.</param>
/// <param name="DataSet">The dataset it iterates: <c>Lab</c>, <c>Vaccine</c>, <c>Treatment</c> or <c>Travel</c>.</param>
/// <param name="Variables">The PHIN variables that repeat together, once per dataset row.</param>
public sealed record RepeatingGroup(
    string GroupName,
    string DataSet,
    IReadOnlyList<RepeatingGroupVariable> Variables);

/// <summary>
/// A PHIN variable within a repeating group: its identifier, its loaded ruleset,
/// and the formatter its ruleset type is evaluated by.
/// </summary>
/// <param name="PhinVar">The variable's identifier, as the output tags it.</param>
/// <param name="RuleSet">The variable's loaded ruleset.</param>
/// <param name="Formatter">The formatter the ruleset's type is evaluated by.</param>
public sealed record RepeatingGroupVariable(
    string PhinVar,
    LoadedRuleSet RuleSet,
    IRuleSetFormatter Formatter);
