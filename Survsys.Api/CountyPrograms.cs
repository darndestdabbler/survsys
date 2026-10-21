using Microsoft.Extensions.Configuration;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// The <c>CountyPrograms</c> configuration section: which of an address's two
/// county values each program reports, its county or its county equivalent.
/// </summary>
/// <remarks>
/// Since 2022 the Census Bureau has counted Connecticut's planning regions as
/// its county equivalents, and CDC expects the planning region for a
/// Connecticut address; ELR and eCR still carry the historical county. So an
/// address keeps both, and each program names the one it uses:
/// <see cref="CodeValue.CountyKind"/> or
/// <see cref="CodeValue.CountyEquivalentKind"/>.
/// </remarks>
public sealed class CountyPrograms
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "CountyPrograms";

    /// <summary>Electronic laboratory reporting.</summary>
    public const string ElrProgram = "elr";

    /// <summary>Electronic case reporting.</summary>
    public const string EcrProgram = "ecr";

    /// <summary>The notification sent to CDC.</summary>
    public const string CdcNotificationProgram = "cdc-notification";

    [ConfigurationKeyName(ElrProgram)]
    public string Elr { get; set; } = CodeValue.CountyKind;

    [ConfigurationKeyName(EcrProgram)]
    public string Ecr { get; set; } = CodeValue.CountyKind;

    [ConfigurationKeyName(CdcNotificationProgram)]
    public string CdcNotification { get; set; } = CodeValue.CountyEquivalentKind;

    /// <summary>
    /// Whether <paramref name="program"/> uses the county equivalent rather
    /// than the county. Throws <see cref="ArgumentOutOfRangeException"/> for a
    /// program not named here and <see cref="InvalidOperationException"/> for
    /// a setting that is neither kind.
    /// </summary>
    public bool UsesCountyEquivalent(string program)
    {
        var setting = program switch
        {
            ElrProgram => Elr,
            EcrProgram => Ecr,
            CdcNotificationProgram => CdcNotification,
            _ => throw new ArgumentOutOfRangeException(nameof(program), program, "No such program."),
        };

        return setting switch
        {
            CodeValue.CountyKind => false,
            CodeValue.CountyEquivalentKind => true,
            _ => throw new InvalidOperationException(
                $"CountyPrograms:{program} is \"{setting}\", not \"{CodeValue.CountyKind}\" or \"{CodeValue.CountyEquivalentKind}\"."),
        };
    }

    /// <summary>The county value <paramref name="program"/> reports for <paramref name="address"/>.</summary>
    public string? Pick(string program, PersonAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        return UsesCountyEquivalent(program) ? address.CountyEquivalent : address.County;
    }

    /// <summary>
    /// The county value a CDC notification reports for <paramref name="address"/>,
    /// as its DEM165: the one the <c>cdc-notification</c> setting names.
    /// </summary>
    public string? NotificationCounty(PersonAddress address) => Pick(CdcNotificationProgram, address);
}
