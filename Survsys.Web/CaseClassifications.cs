using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The last classification the API tier answered this circuit with, and word
/// of each one as it arrives.
/// </summary>
/// <remarks>
/// Scoped, so a circuit has one: <see cref="CaseClassificationHandler"/>
/// records what a save answered here, the report page listens to re-read the
/// case's status, and <c>ClassificationFindings</c> shows the findings beside
/// the save. One is enough, because a circuit shows one case at a time.
/// </remarks>
public sealed class CaseClassifications
{
    /// <summary>Raised with each classification as it is recorded.</summary>
    public event Action<CaseClassificationResponse>? Classified;

    /// <summary>The last classification recorded, or null before any.</summary>
    public CaseClassificationResponse? Last { get; private set; }

    /// <summary>Records a classification a save answered with, and says so.</summary>
    public void Record(CaseClassificationResponse classification)
    {
        ArgumentNullException.ThrowIfNull(classification);

        Last = classification;
        Classified?.Invoke(classification);
    }
}
