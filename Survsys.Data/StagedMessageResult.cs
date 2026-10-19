namespace Survsys.Data;

/// <summary>
/// One result of a staged message's order — an OBX segment as the parser read
/// it, kept as the text the message gave until vocabulary translates it.
/// </summary>
public class StagedMessageResult
{
    public int Id { get; set; }

    /// <summary>The staged message whose order this result belongs to.</summary>
    public int StagedMessageId { get; set; }

    /// <summary>Where this result sits among its order's results, counting from zero.</summary>
    public int Sequence { get; set; }

    /// <summary>What was observed, as its code (OBX-3.1).</summary>
    public string? ObservationCode { get; set; }

    /// <summary>What was observed, as its name (OBX-3.2).</summary>
    public string? ObservationName { get; set; }

    /// <summary>The value (OBX-5), or a coded value's code.</summary>
    public string? ResultValue { get; set; }

    /// <summary>A coded value's text (OBX-5.2).</summary>
    public string? CodedResult { get; set; }

    /// <summary>The units (OBX-6).</summary>
    public string? Units { get; set; }

    /// <summary>The reference range (OBX-7).</summary>
    public string? ReferenceRange { get; set; }

    /// <summary>The first abnormal flag (OBX-8).</summary>
    public string? AbnormalFlag { get; set; }

    /// <summary>The result status (OBX-11).</summary>
    public string? ResultStatus { get; set; }

    /// <summary>
    /// The master LOINC the observation code resolved to, through the lab's
    /// child LOINC where it has one, or null where it resolved to none.
    /// </summary>
    public string? MasterLoinc { get; set; }

    /// <summary>The condition the result resolved to, as a bare id into <c>conditions</c>.</summary>
    public int? ConditionId { get; set; }

    /// <summary>The organism the result resolved to, as a bare id into <c>vocab_master_organism</c>.</summary>
    public int? OrganismId { get; set; }

    /// <summary>
    /// The result's interpretation (for example <c>Positive</c>): the
    /// application value its result code translates to, or null where it
    /// translates to none.
    /// </summary>
    public string? Interpretation { get; set; }
}
