namespace Survsys.Contracts;

/// <summary>
/// One laboratory specimen whole: the specimen, its tests with their results,
/// and its antimicrobial susceptibility rows — the shape
/// <c>GET /api/labs/{id}</c> returns.
/// </summary>
/// <remarks>
/// The specimen is a <see cref="LabResponse"/>, the same shape a list of
/// specimens returns, so one specimen reads the same whether it is read in a
/// list or on its own. The tests and susceptibility rows are what a list has
/// no use for and a reader of one specimen does: each test carries its own
/// results, and the susceptibility rows hang off the specimen as the legacy
/// <c>lab_resist_test</c> table does.
///
/// Both collections are <see cref="IReadOnlyList{T}"/>, as a response is read
/// and not added to, and are present but empty where a specimen has no tests
/// or no susceptibility rows.
/// </remarks>
/// <param name="Specimen">The specimen itself.</param>
/// <param name="Tests">The tests run on the specimen, each with its results.</param>
/// <param name="ResistTests">
/// The antimicrobial susceptibility tests run on the specimen.
/// </param>
public record LabDetailResponse(
    LabResponse Specimen,
    IReadOnlyList<LabTestResponse> Tests,
    IReadOnlyList<LabResistTestResponse> ResistTests);
