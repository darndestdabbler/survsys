namespace Survsys.Data;

/// <summary>
/// One system configuration setting: a value kept under a name, edited on the
/// System settings screen rather than in a deployment's configuration files.
/// </summary>
/// <remarks>
/// The legacy systems' key-value configuration tables — NMI's <c>nmi.config</c>
/// among them — as one table, <c>system_setting</c>. A setting is chosen by its
/// name, so the name is the key. What reads a setting is the code that needs
/// it; this is only where it is kept.
/// </remarks>
public class SystemSetting
{
    /// <summary>The setting's name, such as <c>cdc_batch_file_prefix</c>. The key.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The setting's value, as text; empty where it is set to nothing.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>What the setting is for, as the screen shows it, or null.</summary>
    public string? Description { get; set; }
}
