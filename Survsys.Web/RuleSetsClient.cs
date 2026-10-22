using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of the rule set types
/// (<c>/api/notification-setup/rule-set-types</c>) and of rule set
/// configurations (<c>/api/notification-setup/rule-sets</c>), each read and
/// replaced whole.
/// </summary>
/// <remarks>The failure handling is <see cref="ApiAsk"/>'s, as every client's is.</remarks>
public sealed class RuleSetsClient(HttpClient http)
{
    /// <summary>How a failure to load the types or a configuration starts.</summary>
    public const string LoadFailurePrefix = "The rule set could not be loaded";

    /// <summary>How a failure to save a configuration starts.</summary>
    public const string SaveFailurePrefix = "The rule set could not be saved";

    private const string Path = "api/notification-setup/rule-sets";

    private static readonly ApiAsk Loading = new(LoadFailurePrefix, "the rule set");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the rule set as saved");

    /// <summary>The seven rule set types, or why there are none.</summary>
    public Task<Asked<IReadOnlyList<RuleSetTypeRow>>> Types(CancellationToken cancellationToken = default) =>
        Loading.Get<IReadOnlyList<RuleSetTypeRow>>(http, "api/notification-setup/rule-set-types", cancellationToken);

    /// <summary>A configuration whole, or why there is none.</summary>
    public Task<Asked<RuleSetConfigRow>> Get(int id, CancellationToken cancellationToken = default) =>
        Loading.Get<RuleSetConfigRow>(http, $"{Path}/{id}", cancellationToken);

    /// <summary>Creates a configuration, answering with it as created or why it was not.</summary>
    public Task<Asked<RuleSetConfigRow>> Create(RuleSetConfigRow row, CancellationToken cancellationToken = default) =>
        Saving.Post<RuleSetConfigRow, RuleSetConfigRow>(http, Path, row, cancellationToken);

    /// <summary>Replaces a configuration whole, answering with it as saved or why it was not.</summary>
    public Task<Asked<RuleSetConfigRow>> Save(int id, RuleSetConfigRow row, CancellationToken cancellationToken = default) =>
        Saving.Put<RuleSetConfigRow, RuleSetConfigRow>(http, $"{Path}/{id}", row, cancellationToken);
}
