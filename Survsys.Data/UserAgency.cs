namespace Survsys.Data;

/// <summary>
/// An agency a user works for: the cases in its jurisdiction, and the cases
/// shared with it, are the cases the user may open.
/// </summary>
/// <remarks>
/// A user may work for several agencies, and a user who works for none sees
/// no case unless a role gives them every agency's.
/// </remarks>
public class UserAgency
{
    public int UserId { get; set; }

    public int AgencyId { get; set; }
}
