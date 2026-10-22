using LokiLogViewer.Loki;
using LokiLogViewer.Shared.Querying;
using LokiLogViewer.Shared.Search;

namespace LokiLogViewer.Search;

internal sealed class LogSearchService(LokiClient lokiClient)
{
    public async Task<LogQueryResponse> QueryAsync(LogQueryRequest request, CancellationToken ct)
    {
        var selector = LogQlBuilder.BuildSelector(request.Conditions);

        var startNano = UnixTime.ToUnixNanoseconds(request.From);
        var endNano = request.Cursor ?? UnixTime.ToUnixNanoseconds(request.To);

        var streams = await lokiClient.QueryRangeAsync(selector, startNano, endNano, request.Limit, "backward", ct);

        // One selector can still match several streams (distinct label combinations), so their
        // values need merging and re-sorting into one timeline before paging.
        var merged = new List<LogEntry>();
        foreach (var stream in streams)
        {
            foreach (var entry in stream.Values)
            {
                if (entry.Length < 2 || !long.TryParse(entry[0], out var timestampNano))
                {
                    continue;
                }

                merged.Add(new LogEntry(timestampNano, entry[1], stream.Labels));
            }
        }

        merged.Sort((a, b) => b.TimestampUnixNano.CompareTo(a.TimestampUnixNano));

        var page = merged.Take(request.Limit).ToList();
        // A full page suggests there may be more; the cursor for the next page is the oldest
        // timestamp in this one. Multiple lines sharing that exact nanosecond at the boundary
        // is a known, rare edge case that can skip or repeat a line - acceptable for v1.
        long? nextCursor = page.Count == request.Limit ? page[^1].TimestampUnixNano : null;

        return new LogQueryResponse(page, nextCursor);
    }

    public Task<IReadOnlyList<string>> GetLabelsAsync(LabelsRequest request, CancellationToken ct) =>
        lokiClient.GetLabelsAsync(BuildContextSelector(request.Context), request.From, request.To, ct);

    public Task<IReadOnlyList<string>> GetLabelValuesAsync(string labelName, LabelsRequest request, CancellationToken ct) =>
        lokiClient.GetLabelValuesAsync(labelName, BuildContextSelector(request.Context), request.From, request.To, ct);

    private static string? BuildContextSelector(IReadOnlyList<LogCondition> context) =>
        context.Count == 0 ? null : LogQlBuilder.BuildSelector(context);
}
