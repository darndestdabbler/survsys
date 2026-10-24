namespace Survsys.Data;

/// <summary>
/// One eCR sender: a healthcare organisation whose eICRs reach this
/// jurisdiction, known by the custodian id its eICRs name, with where it is
/// in onboarding and its settings.
/// </summary>
/// <remarks>
/// A sender is matched to its documents by <see cref="SenderId"/> against
/// <see cref="EcrDocument.Sender"/>, so it is unique. The stage is a record of
/// onboarding, test then validation then production, and gates nothing.
/// </remarks>
public class EcrSender
{
    /// <summary>The stage of a sender sending test documents.</summary>
    public const string TestStage = "test";

    /// <summary>The stage of a sender whose production documents are being validated.</summary>
    public const string ValidationStage = "validation";

    /// <summary>The stage of a sender in production.</summary>
    public const string ProductionStage = "production";

    /// <summary>The stages, in the order a sender passes through them.</summary>
    public static readonly IReadOnlyList<string> Stages = [TestStage, ValidationStage, ProductionStage];

    public int Id { get; set; }

    /// <summary>The custodian id the sender's eICRs name, root and extension joined by a caret.</summary>
    public string SenderId { get; set; } = string.Empty;

    /// <summary>The sender's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One of <see cref="Stages"/>.</summary>
    public string Stage { get; set; } = TestStage;

    /// <summary>
    /// Whether the results in the sender's eICRs are written to a case as
    /// labs; where not, its labs are expected by ELR.
    /// </summary>
    public bool TakesLabResults { get; set; }
}
