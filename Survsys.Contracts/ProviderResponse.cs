namespace Survsys.Contracts;

/// <summary>
/// A provider on the registry — a clinician a record can name — with their
/// type and how to reach them.
/// </summary>
/// <param name="Id">The provider's identifier.</param>
/// <param name="ProviderTypeId">The provider's type, a <c>provider_type</c> code's id as <c>GET /api/codes/provider_type</c> lists it, or null.</param>
/// <param name="FirstName">The first name, or null.</param>
/// <param name="MiddleName">The middle name, or null.</param>
/// <param name="LastName">The last name.</param>
/// <param name="Phone">The telephone number, or null.</param>
/// <param name="Email">The email address, or null.</param>
/// <param name="Active">Whether the provider is still offered.</param>
public record ProviderResponse(
    int Id,
    int? ProviderTypeId,
    string? FirstName,
    string? MiddleName,
    string LastName,
    string? Phone,
    string? Email,
    bool Active);
