namespace Survsys.Data;

/// <summary>
/// A provider: a clinician on the registry an administrator keeps, such as a
/// lab's ordering clinician.
/// </summary>
/// <remarks>
/// The columns are the legacy <c>provider</c> table's, as
/// <c>../nedss/legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// creates it, with an active flag the legacy does not have: a provider is
/// retired rather than deleted, so a record that names one still does. The
/// legacy <c>provider_type</c> table is the <c>provider_type</c> code list
/// here, and <see cref="ProviderTypeId"/> a code's id on it.
/// </remarks>
public class Provider
{
    public int Id { get; set; }

    /// <summary>The provider's type, a <c>provider_type</c> code's id, or null.</summary>
    public int? ProviderTypeId { get; set; }

    public string? FirstName { get; set; }

    public string? MiddleName { get; set; }

    /// <summary>The provider's last name, which the registry is searched by.</summary>
    public required string LastName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>Whether the provider is still offered; a retired one is kept, inactive.</summary>
    public bool Active { get; set; } = true;
}
