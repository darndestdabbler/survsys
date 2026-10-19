namespace Survsys.Contracts;

/// <summary>
/// An antimicrobial susceptibility test on a laboratory specimen, as the API
/// tier returns it.
/// </summary>
/// <remarks>
/// The legacy <c>lab_resist_test</c> table's columns, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>. The organism, the agent and the coded outcome are
/// the bare legacy ids, because the reference tables behind them do not exist
/// here yet.
/// </remarks>
/// <param name="Id">The susceptibility test's identifier.</param>
/// <param name="LabId">The specimen this susceptibility test was run on.</param>
/// <param name="OrganismId">
/// The organism challenged, as the legacy <c>organism_id</c>, or null.
/// </param>
/// <param name="ResistTestAgentId">
/// The antimicrobial agent the organism was challenged with, as the legacy
/// <c>resist_test_agent_id</c>, or null.
/// </param>
/// <param name="ResistTestResultId">
/// The coded outcome — resistant, susceptible and the rest — as the legacy
/// <c>resist_test_result_id</c>, or null.
/// </param>
/// <param name="TestDate">
/// The date the susceptibility test was run, or null where it is not known.
/// </param>
public record LabResistTestResponse(
    int Id,
    int LabId,
    int? OrganismId,
    int? ResistTestAgentId,
    int? ResistTestResultId,
    DateOnly? TestDate);
