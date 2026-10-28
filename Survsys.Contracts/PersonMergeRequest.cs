namespace Survsys.Contracts;

/// <summary>
/// Two people found to be one: what <c>POST /api/person-merges</c> merges.
/// </summary>
/// <param name="KeepPersonId">The person kept, who receives everything.</param>
/// <param name="MergePersonId">
/// The person merged away, left on file as a tombstone pointing at the kept one.
/// </param>
public record PersonMergeRequest(int KeepPersonId, int MergePersonId);
