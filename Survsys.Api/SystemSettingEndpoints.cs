using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/system-settings</c>: the system configuration settings, each a
/// value kept under a name, listed, saved by name and deleted.
/// </summary>
/// <remarks>
/// Administration, so <see cref="PrivilegeRules"/> holds every route to
/// <see cref="Privileges.ManageUsers"/> without a word here. A save is by the
/// name in the address, adding the setting where there is none, so the screen
/// needs no separate add.
/// </remarks>
public static class SystemSettingEndpoints
{
    private const int NameLength = 100;

    private const int DescriptionLength = 500;

    /// <summary>
    /// Why this is not a setting this tier will save under
    /// <paramref name="name"/>, or null when it is one.
    /// </summary>
    public static string? WhyNotASystemSetting(string? name, SaveSystemSettingRequest? request)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "name is required: a setting is chosen by its name.";
        }

        if (name.Trim().Length > NameLength)
        {
            return $"name is longer than {NameLength} characters.";
        }

        if (request?.Value is null)
        {
            return "value is required: empty sets the setting to nothing.";
        }

        if (request.Description?.Trim().Length > DescriptionLength)
        {
            return $"description is longer than {DescriptionLength} characters.";
        }

        return null;
    }

    /// <summary>
    /// Registers what the system-setting endpoints need of the container.
    /// </summary>
    public static IServiceCollection AddSystemSettings(this IServiceCollection services) =>
        services.AddScoped<SystemSettingQueries>();

    public static IEndpointRouteBuilder MapSystemSettings(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/system-settings", GetSystemSettings)
            .WithName("GetSystemSettings")
            .WithSummary("Every system setting, by name.");

        routes.MapPut("/api/system-settings/{name}", SaveSystemSetting)
            .WithName("SaveSystemSetting")
            .WithSummary("Saves a system setting under its name, adding it where there is none.");

        routes.MapDelete("/api/system-settings/{name}", DeleteSystemSetting)
            .WithName("DeleteSystemSetting")
            .WithSummary("Deletes a system setting.");

        return routes;
    }

    private static async Task<Ok<IReadOnlyList<SystemSettingResponse>>> GetSystemSettings(
        SystemSettingQueries settings,
        CancellationToken cancellationToken = default) =>
        TypedResults.Ok(await settings.All(cancellationToken));

    private static async Task<Results<Ok<SystemSettingResponse>, ProblemHttpResult>> SaveSystemSetting(
        SystemSettingQueries settings,
        string name,
        SaveSystemSettingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotASystemSetting(name, request) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The setting asked for is not one this endpoint can save.");
        }

        return TypedResults.Ok(await settings.Save(name.Trim(), request, cancellationToken));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteSystemSetting(
        SystemSettingQueries settings,
        string name,
        CancellationToken cancellationToken = default) =>
        await settings.Delete(name.Trim(), cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.Problem(
                detail: $"No system setting is named '{name}'.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such system setting.");
}

/// <summary>
/// The system-setting reads and writes, and the only thing in this tier that
/// holds the <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>ExportLogQueries</c> is: it exists only to serve
/// <see cref="SystemSettingEndpoints"/>. A save and a delete go through the
/// change tracker, so each passes through the audit interceptor.
/// </remarks>
internal sealed class SystemSettingQueries(SurvsysDbContext database)
{
    /// <summary>Every setting, by name.</summary>
    public async Task<IReadOnlyList<SystemSettingResponse>> All(CancellationToken cancellationToken) =>
        await database.SystemSettings
            .OrderBy(setting => setting.Name)
            .Select(setting => new SystemSettingResponse(setting.Name, setting.Value, setting.Description))
            .ToListAsync(cancellationToken);

    /// <summary>Saves the setting under its name, adding it where there is none, and answers it as saved.</summary>
    public async Task<SystemSettingResponse> Save(
        string name, SaveSystemSettingRequest request, CancellationToken cancellationToken)
    {
        var setting = await database.SystemSettings.SingleOrDefaultAsync(item => item.Name == name, cancellationToken);

        if (setting is null)
        {
            setting = new SystemSetting { Name = name };
            database.SystemSettings.Add(setting);
        }

        setting.Value = request.Value;
        setting.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        await database.SaveChangesAsync(cancellationToken);

        return new SystemSettingResponse(setting.Name, setting.Value, setting.Description);
    }

    /// <summary>Deletes the setting, answering whether there was one by that name.</summary>
    public async Task<bool> Delete(string name, CancellationToken cancellationToken)
    {
        if (await database.SystemSettings.SingleOrDefaultAsync(item => item.Name == name, cancellationToken) is not { } setting)
        {
            return false;
        }

        database.SystemSettings.Remove(setting);
        await database.SaveChangesAsync(cancellationToken);

        return true;
    }
}
