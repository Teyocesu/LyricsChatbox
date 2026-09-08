using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace LyricsChatbox;

public sealed partial class LyricsResolver
{
    public async Task<IReadOnlyList<ManualCandidate>> SearchManualAsync(TrackIdentity track, string query, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 512 || !double.IsFinite(track.Duration)) return [];
        var choices = new List<ManualCandidate>();
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            deadline.CancelAfter(PrimaryDeadline);
            try
            {
                var records = await GetAsync<LyricsRecord[]>("search?q=" + Uri.EscapeDataString(query), deadline.Token) ?? [];
                choices.AddRange(ManualMatching.Suggestions(track, records.Where(r => r is not null && LrcParser.Parse(r.SyncedLyrics).Lines.Count > 0))
                    .DistinctBy(r => r.Id).Take(5).Select(r => new ManualCandidate("LRCLIB", r with { SyncedLyrics = null })));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is IOException or HttpRequestException or JsonException or ArgumentException or OperationCanceledException or ProviderCooldown) { }
        }
        if (choices.Count < 5 && secondary is not null)
        {
            var records = await secondary.SearchManualAsync(query, token);
            choices.AddRange(ManualMatching.Suggestions(track, records).DistinctBy(r => r.Id).Take(5 - choices.Count).Select(r => new ManualCandidate(secondary.Name, r with { SyncedLyrics = null })));
        }
        token.ThrowIfCancellationRequested();
        return choices.DistinctBy(c => (c.Provider, c.Metadata.Id)).Take(5).ToArray();
    }
    public async Task<ProviderResult> FetchManualAsync(ManualCandidate choice, CancellationToken token)
    {
        if (!ManualMatching.Valid(choice.Metadata)) return new(LyricsOutcome.Rejected);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(6));
        try
        {
            LyricsRecord? record;
            if (choice.Provider == "LRCLIB") record = await GetAsync<LyricsRecord>("get/" + choice.Metadata.Id, deadline.Token);
            else if (secondary?.Name == choice.Provider) record = (await secondary.FetchManualAsync(choice.Metadata, deadline.Token)).Record;
            else return new(LyricsOutcome.Rejected);
            token.ThrowIfCancellationRequested();
            return record is not null && ManualMatching.Same(choice.Metadata, record) && LrcParser.Parse(record.SyncedLyrics).Lines.Count > 0
                ? new(LyricsOutcome.Found, record) : new(LyricsOutcome.NotFound);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is IOException or HttpRequestException or JsonException or ArgumentException or OperationCanceledException or ProviderCooldown)
        { return new(LyricsOutcome.Unavailable); }
    }
}
