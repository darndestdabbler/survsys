using System.Text.Json;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// Answers a request that classified a case with what the classification
/// decided, in a <see cref="Name"/> header holding a
/// <see cref="CaseClassificationResponse"/> as JSON.
/// </summary>
/// <remarks>
/// A header rather than a member of each route's body, so every route that
/// classifies answers the same way — a 204 delete included — and no response
/// shape changes. The JSON escapes every character outside ASCII, which keeps
/// the findings a legal header value.
/// </remarks>
public static class CaseClassificationHeader
{
    /// <summary>The header's name.</summary>
    public const string Name = "Case-Classification";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The header's value for that case's outcome.</summary>
    public static string Value(int reportId, ClassificationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return JsonSerializer.Serialize(
            new CaseClassificationResponse(reportId, outcome.CaseStatus, outcome.Findings), Json);
    }

    /// <summary>
    /// Adds the header to every response whose request classified a case, as
    /// <see cref="SurvsysDbContext.Classified"/> holds it when the response starts.
    /// </summary>
    public static IApplicationBuilder UseCaseClassificationHeader(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                if (context.RequestServices.GetService<SurvsysDbContext>()?.Classified is { } classified)
                {
                    context.Response.Headers[Name] = Value(classified.ReportId, classified.Outcome);
                }

                return Task.CompletedTask;
            });

            return next(context);
        });
}
