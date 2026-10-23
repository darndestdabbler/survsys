using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// One stored eCR document as the quality figures read it: no XML, only what
/// is counted and what pairs it.
/// </summary>
/// <param name="Id">The document's row id.</param>
/// <param name="Kind"><see cref="EcrDocument.EicrKind"/> or <see cref="EcrDocument.RrKind"/>.</param>
/// <param name="Feed">The feed it came on, within which it is paired.</param>
/// <param name="DocumentId">Its ClinicalDocument/id.</param>
/// <param name="Sender">Its custodian, as stored.</param>
/// <param name="ReceivedAt">When it was taken in, in UTC.</param>
/// <param name="CreatedAt">When it says it was created, in UTC, or null.</param>
/// <param name="Errors">Its validation errors, fatal or error.</param>
/// <param name="Warnings">Its validation warnings.</param>
/// <param name="EicrDocumentId">For an RR, the eICR it responds to.</param>
/// <param name="Conditions">For an RR, the names of the conditions it keeps.</param>
/// <param name="Counted">
/// Whether it was received in the window asked for; one that was not is here
/// only to pair a counted one with.
/// </param>
public sealed record EcrQualityDocument(
    int Id,
    string Kind,
    string Feed,
    string DocumentId,
    string? Sender,
    DateTime ReceivedAt,
    DateTime? CreatedAt,
    int Errors,
    int Warnings,
    string? EicrDocumentId,
    IReadOnlyList<string> Conditions,
    bool Counted);

/// <summary>
/// The eCR data quality figures: the documents received, by sender and
/// condition, with their validation findings, receipt latency and the halves
/// of a pair that arrived alone.
/// </summary>
/// <remarks>
/// An eICR's sender is its custodian, and an RR is counted under its eICR's
/// sender, or under none where its eICR is not on file. An eICR's conditions
/// are those its RRs keep, and it is counted once under each, or under none
/// where they keep none; an RR is counted under the conditions it keeps.
/// Pairing is within a feed, as <see cref="EcrIntakeService.EicrOf"/> pairs.
/// </remarks>
public static class EcrQuality
{
    /// <summary>
    /// The rows for the counted documents among <paramref name="documents"/>,
    /// by sender and then condition, those with none last. The sender names
    /// are left for the caller to fill.
    /// </summary>
    public static IReadOnlyList<EcrQualityRowResponse> Rows(IReadOnlyCollection<EcrQualityDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var eicrs = documents
            .Where(document => document.Kind == EcrDocument.EicrKind)
            .GroupBy(document => (document.Feed, document.DocumentId))
            .ToDictionary(group => group.Key, group => group.First());
        var rrsByEicr = documents
            .Where(document => document.Kind == EcrDocument.RrKind && document.EicrDocumentId is not null)
            .ToLookup(document => (document.Feed, document.EicrDocumentId!));

        var counted = documents
            .Where(document => document.Counted)
            .Select(document =>
            {
                if (document.Kind == EcrDocument.EicrKind)
                {
                    var rrs = rrsByEicr[(document.Feed, document.DocumentId)].ToList();
                    return (Document: document,
                        Sender: document.Sender,
                        Conditions: rrs.SelectMany(rr => rr.Conditions).Distinct().ToList(),
                        Paired: rrs.Count > 0);
                }

                var eicr = document.EicrDocumentId is { } eicrId
                    && eicrs.TryGetValue((document.Feed, eicrId), out var found)
                    ? found
                    : null;
                return (Document: document,
                    Sender: eicr?.Sender,
                    Conditions: document.Conditions.Distinct().ToList(),
                    Paired: eicr is not null);
            });

        return counted
            .SelectMany(each => (each.Conditions.Count == 0 ? new string?[] { null } : each.Conditions.ToArray<string?>())
                .Select(condition => (each.Sender, Condition: condition, each.Document, each.Paired)))
            .GroupBy(each => (each.Sender, each.Condition))
            .OrderBy(group => group.Key.Sender is null)
            .ThenBy(group => group.Key.Sender, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Condition is null)
            .ThenBy(group => group.Key.Condition, StringComparer.Ordinal)
            .Select(group =>
            {
                var eicrRows = group.Where(each => each.Document.Kind == EcrDocument.EicrKind).ToList();
                var rrRows = group.Where(each => each.Document.Kind == EcrDocument.RrKind).ToList();
                var latencies = eicrRows
                    .Where(each => each.Document.CreatedAt is not null)
                    .Select(each => (each.Document.ReceivedAt - each.Document.CreatedAt!.Value).TotalMinutes)
                    .ToList();

                return new EcrQualityRowResponse(
                    group.Key.Sender,
                    null,
                    group.Key.Condition,
                    eicrRows.Count,
                    rrRows.Count,
                    group.Sum(each => each.Document.Errors),
                    group.Sum(each => each.Document.Warnings),
                    latencies.Count == 0 ? null : Math.Round(latencies.Average(), 1),
                    latencies.Count == 0 ? null : Math.Round(latencies.Max(), 1),
                    eicrRows.Count(each => !each.Paired),
                    rrRows.Count(each => !each.Paired));
            })
            .ToList();
    }
}
