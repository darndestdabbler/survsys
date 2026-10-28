using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/ecr-senders</c>: the organisations whose eICRs reach this
/// jurisdiction, each with its onboarding stage and settings.
/// </summary>
/// <remarks>
/// Kept by an administrator: listed, added to, saved over and deleted, keyed
/// by the custodian id the sender's eICRs name. The stage records onboarding
/// and gates nothing; the lab setting is read when an eICR is written to a
/// case. As in <see cref="EcrConditionMappingEndpoints"/>, the handlers are
/// handed <see cref="EcrSenderQueries"/> and not the <c>DbContext</c>.
/// </remarks>
public static class EcrSenderEndpoints
{
    private const int SenderIdLength = 300;

    private const int NameLength = 200;

    /// <summary>
    /// Why this is not a sender this tier will save, as far as can be said
    /// without the database, or null when it is one.
    /// </summary>
    public static string? WhyNotASender(SaveEcrSenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SenderId))
        {
            return "senderId is required: a sender is known by the custodian id its eICRs name.";
        }

        if (request.SenderId.Trim().Length > SenderIdLength)
        {
            return $"senderId is longer than {SenderIdLength} characters.";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "name is required.";
        }

        if (request.Name.Trim().Length > NameLength)
        {
            return $"name is longer than {NameLength} characters.";
        }

        return EcrSender.Stages.Contains(request.Stage)
            ? null
            : $"stage must be one of {string.Join(", ", EcrSender.Stages)}.";
    }

    /// <summary>Registers what the sender endpoints need of the container.</summary>
    public static IServiceCollection AddEcrSenders(this IServiceCollection services) =>
        services.AddScoped<EcrSenderQueries>();

    public static IEndpointRouteBuilder MapEcrSenders(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/ecr-senders", GetSenders)
            .WithName("GetEcrSenders")
            .WithSummary("The eCR senders, each with its onboarding stage and settings.");

        routes.MapPost("/api/ecr-senders", CreateSender)
            .WithName("CreateEcrSender")
            .WithSummary("Adds an eCR sender.");

        routes.MapPut("/api/ecr-senders/{id:int}", UpdateSender)
            .WithName("UpdateEcrSender")
            .WithSummary("Saves over an eCR sender.");

        routes.MapDelete("/api/ecr-senders/{id:int}", DeleteSender)
            .WithName("DeleteEcrSender")
            .WithSummary("Deletes an eCR sender.");

        return routes;
    }

    private static async Task<Ok<IReadOnlyList<EcrSenderResponse>>> GetSenders(
        EcrSenderQueries senders,
        CancellationToken cancellationToken = default) =>
        TypedResults.Ok(await senders.List(cancellationToken));

    private static async Task<Results<Created<EcrSenderResponse>, ProblemHttpResult>> CreateSender(
        EcrSenderQueries senders,
        SaveEcrSenderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await WhyNot(senders, null, request, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var created = await senders.Save(null, request, cancellationToken);
        return TypedResults.Created($"/api/ecr-senders/{created!.Id}", created);
    }

    private static async Task<Results<Ok<EcrSenderResponse>, ProblemHttpResult>> UpdateSender(
        EcrSenderQueries senders,
        int id,
        SaveEcrSenderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await WhyNot(senders, id, request, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        return await senders.Save(id, request, cancellationToken) is { } saved
            ? TypedResults.Ok(saved)
            : NoSuchSender(id);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteSender(
        EcrSenderQueries senders,
        int id,
        CancellationToken cancellationToken = default) =>
        await senders.Delete(id, cancellationToken)
            ? TypedResults.NoContent()
            : NoSuchSender(id);

    /// <summary>The refusal of a sender to save over <paramref name="id"/>, or to add where it is null; null to save it.</summary>
    private static async Task<ProblemHttpResult?> WhyNot(
        EcrSenderQueries senders,
        int? id,
        SaveEcrSenderRequest request,
        CancellationToken cancellationToken)
    {
        if (WhyNotASender(request) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The sender asked for is not one this endpoint can save.");
        }

        if (await senders.SenderIdHolder(request.SenderId, cancellationToken) is { } holder && holder != id)
        {
            return TypedResults.Problem(
                detail: $"Sender {request.SenderId.Trim()} is already on file; save over that one instead.",
                statusCode: StatusCodes.Status409Conflict,
                title: "The sender is already on file.");
        }

        return null;
    }

    private static ProblemHttpResult NoSuchSender(int id) =>
        TypedResults.Problem(
            detail: $"No eCR sender has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such sender.");
}

/// <summary>The sender queries, and the only thing in this tier that holds the <c>DbContext</c> for them.</summary>
internal sealed class EcrSenderQueries(SurvsysDbContext database)
{
    /// <summary>Every sender, by name.</summary>
    public async Task<IReadOnlyList<EcrSenderResponse>> List(CancellationToken cancellationToken) =>
        await database.EcrSenders
            .OrderBy(sender => sender.Name)
            .ThenBy(sender => sender.SenderId)
            .Select(sender => new EcrSenderResponse(
                sender.Id, sender.SenderId, sender.Name, sender.Stage, sender.TakesLabResults))
            .ToListAsync(cancellationToken);

    /// <summary>The id of the sender with that sender id, or null where none has it.</summary>
    public async Task<int?> SenderIdHolder(string senderId, CancellationToken cancellationToken) =>
        await database.EcrSenders
            .Where(sender => sender.SenderId == senderId.Trim())
            .Select(sender => (int?)sender.Id)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Saves the sender over the one with the id, or as a new one where the id
    /// is null, and answers it; null where no sender has the id. The caller
    /// has proven the request one to save.
    /// </summary>
    public async Task<EcrSenderResponse?> Save(
        int? id,
        SaveEcrSenderRequest request,
        CancellationToken cancellationToken)
    {
        var sender = id is null
            ? new EcrSender()
            : await database.EcrSenders.SingleOrDefaultAsync(sender => sender.Id == id, cancellationToken);

        if (sender is null)
        {
            return null;
        }

        sender.SenderId = request.SenderId.Trim();
        sender.Name = request.Name.Trim();
        sender.Stage = request.Stage;
        sender.TakesLabResults = request.TakesLabResults;

        if (id is null)
        {
            database.EcrSenders.Add(sender);
        }

        await database.SaveChangesAsync(cancellationToken);
        return new EcrSenderResponse(sender.Id, sender.SenderId, sender.Name, sender.Stage, sender.TakesLabResults);
    }

    /// <summary>Deletes the sender with the id, answering whether there was one.</summary>
    public async Task<bool> Delete(int id, CancellationToken cancellationToken) =>
        await database.EcrSenders
            .Where(sender => sender.Id == id)
            .ExecuteDeleteAsync(cancellationToken) > 0;
}
