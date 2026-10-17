namespace Survsys.Messaging;

/// <summary>
/// The notification-guide catalogue the <see cref="PhinVariableMapper"/> reads,
/// as plain in-memory data: the PHIN variable definitions, the guide each
/// condition is exported under, which variables a guide carries, and the guides
/// a guide pulls in alongside, gathered in one container the caller fills from
/// whatever source it has.
/// </summary>
/// <remarks>
/// None of these is a Survsys.Data entity, and this module reaches for no
/// DbContext: the mapper takes the catalogue as data so the messaging module
/// keeps its fence against the database, the fence the architecture test
/// asserts. Projecting the Survsys.Data reference tables into these records is
/// the caller's to do, in a tier that may see both.
/// </remarks>
/// <param name="Variables">The PHIN variable definitions the mapping returns.</param>
/// <param name="ConditionGuides">The guide each condition is exported under.</param>
/// <param name="GuideVariables">Which variables a guide carries.</param>
/// <param name="GuideLinks">The guides a guide pulls in alongside its own.</param>
public sealed record PhinVariableCatalog(
    IReadOnlyCollection<PhinVariable> Variables,
    IReadOnlyCollection<ConditionGuide> ConditionGuides,
    IReadOnlyCollection<GuideVariable> GuideVariables,
    IReadOnlyCollection<GuideLink> GuideLinks);

/// <summary>
/// A PHIN variable definition: the fields a message carries over for a variable
/// the mapping resolves. Keyed, like the legacy table, by its identifier.
/// </summary>
/// <param name="PhinVar">The variable's identifier, as a guide names it.</param>
/// <param name="Description">The human-readable name of the variable.</param>
/// <param name="Hl7DataType">The HL7 v2 data type the value is formatted as, where set.</param>
/// <param name="Priority">The legacy single-letter processing priority, where set.</param>
/// <param name="CodeSystem">The code system the variable's coded values come from, where set.</param>
public sealed record PhinVariable(
    string PhinVar,
    string Description,
    string? Hl7DataType,
    string? Priority,
    string? CodeSystem);

/// <summary>
/// The guide a condition is exported under: the condition-to-guide assignment.
/// </summary>
/// <param name="ConditionId">The condition.</param>
/// <param name="GuideId">The guide it is exported under.</param>
public sealed record ConditionGuide(int ConditionId, int GuideId);

/// <summary>
/// A PHIN variable a guide carries: the guide-to-variable membership.
/// </summary>
/// <param name="GuideId">The guide.</param>
/// <param name="PhinVar">The identifier of a variable the guide carries.</param>
public sealed record GuideVariable(int GuideId, string PhinVar);

/// <summary>
/// A guide a guide pulls in alongside its own: when a condition is exported
/// under a guide, the variables of the guides it links are carried too.
/// </summary>
/// <param name="GuideId">The guide that does the linking.</param>
/// <param name="LinkedGuideId">The guide pulled in alongside.</param>
public sealed record GuideLink(int GuideId, int LinkedGuideId);
