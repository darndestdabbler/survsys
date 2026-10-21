namespace Survsys.Contracts;

/// <summary>
/// An antimicrobial susceptibility test to record on a specimen: part of what
/// <c>POST /api/person-conditions/{id}/labs</c> takes, nested under its
/// specimen.
/// </summary>
/// <remarks>
/// The writable columns of the legacy <c>lab_resist_test</c> table, as
/// declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, less the identity and the lab id, which the server
/// assigns and the nesting carries. The organism, the agent and the coded
/// outcome are the bare legacy ids, because the reference tables behind them
/// do not exist here yet.
/// </remarks>
/// <param name="OrganismId">The organism challenged, as the legacy <c>organism_id</c>.</param>
/// <param name="ResistTestAgentId">
/// The antimicrobial agent the organism was challenged with, as the legacy
/// <c>resist_test_agent_id</c>.
/// </param>
/// <param name="ResistTestResultId">
/// The coded outcome — resistant, susceptible and the rest — as the legacy
/// <c>resist_test_result_id</c>.
/// </param>
/// <param name="TestDate">The date the susceptibility test was run.</param>
public record CreateLabResistTestRequest(
    int? OrganismId = null,
    int? ResistTestAgentId = null,
    int? ResistTestResultId = null,
    DateOnly? TestDate = null);
