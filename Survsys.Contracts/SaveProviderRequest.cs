namespace Survsys.Contracts;

/// <summary>
/// A provider to create or to save over an existing one: what
/// <c>POST /api/providers</c> and <c>PUT /api/providers/{id}</c> take.
/// </summary>
/// <remarks>
/// A blank optional value is stored as null. The provider type is a code on
/// the <c>provider_type</c> list.
/// </remarks>
/// <param name="LastName">The last name. Required.</param>
/// <param name="FirstName">The first name, or null.</param>
/// <param name="MiddleName">The middle name, or null.</param>
/// <param name="ProviderTypeId">The provider's type, a <c>provider_type</c> code's id, or null.</param>
/// <param name="Phone">The telephone number, or null.</param>
/// <param name="Email">The email address, or null.</param>
/// <param name="Active">Whether the provider is still offered; true unless said otherwise.</param>
public record SaveProviderRequest(
    string LastName,
    string? FirstName = null,
    string? MiddleName = null,
    int? ProviderTypeId = null,
    string? Phone = null,
    string? Email = null,
    bool Active = true);
