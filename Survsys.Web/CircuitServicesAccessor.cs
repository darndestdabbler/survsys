using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Survsys.Web;

/// <summary>
/// The services of the Blazor circuit whose work is running now, for code
/// that the container builds outside the circuit's scope.
/// </summary>
/// <remarks>
/// <see cref="ApiTokenHandler"/> is such code: <c>IHttpClientFactory</c>
/// builds message handlers in a scope of its own, where the circuit's
/// authentication state cannot be resolved. This is the pattern Microsoft
/// documents for it — an async-local the circuit sets around each piece of
/// inbound activity — so the handler reads the circuit's services through it.
/// </remarks>
public sealed class CircuitServicesAccessor
{
    private static readonly AsyncLocal<IServiceProvider?> Current = new();

    /// <summary>The running circuit's services, or null outside a circuit.</summary>
    public IServiceProvider? Services
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}

/// <summary>
/// Sets <see cref="CircuitServicesAccessor.Services"/> to the circuit's own
/// services for the length of each piece of inbound activity.
/// </summary>
internal sealed class CircuitServicesHandler(IServiceProvider services, CircuitServicesAccessor accessor)
    : CircuitHandler
{
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next) =>
        async context =>
        {
            accessor.Services = services;

            try
            {
                await next(context);
            }
            finally
            {
                accessor.Services = null;
            }
        };
}

/// <summary>Registers <see cref="CircuitServicesAccessor"/> and what sets it.</summary>
public static class CircuitServicesAccessorExtensions
{
    public static IServiceCollection AddCircuitServicesAccessor(this IServiceCollection services) =>
        services
            .AddSingleton<CircuitServicesAccessor>()
            .AddScoped<CircuitHandler, CircuitServicesHandler>();
}
