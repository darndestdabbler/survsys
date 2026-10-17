namespace Survsys.Data;

/// <summary>
/// A privilege a role holds, by its name in the catalogue
/// <c>Survsys.Contracts.Privileges</c> keeps.
/// </summary>
/// <remarks>
/// A name and not a key into a privilege table, because the catalogue is code:
/// the API tier refuses to save a name the catalogue does not hold.
/// </remarks>
public class RolePrivilege
{
    public int RoleId { get; set; }

    public string Privilege { get; set; } = "";
}
