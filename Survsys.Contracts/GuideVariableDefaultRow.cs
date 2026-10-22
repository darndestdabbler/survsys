namespace Survsys.Contracts;

/// <summary>
/// A header variable and the value every guide uses unless it sets its own,
/// as <c>/api/notification-setup/guide-variable-defaults</c> lists, saves and
/// carries in CSV. An application or facility is
/// <c>namespace^universal id^type</c>.
/// </summary>
/// <param name="Id">The variable's id; ignored when one is created.</param>
/// <param name="Name">The variable's name (for example <c>sending_app</c>), unique.</param>
/// <param name="DefaultValue">Its default value, or null.</param>
public record GuideVariableDefaultRow(int Id, string Name, string? DefaultValue);
