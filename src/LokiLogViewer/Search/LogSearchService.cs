using LokiLogViewer.Loki;
using LokiLogViewer.Shared.Search;
using LokiLogViewer.Shared.Querying;
using Microsoft.Extensions.Options;

namespace LokiLogViewer.Search;

internal sealed class LogSearchService(LokiClient lokiClient, IOptions<LokiOptions> options)
{
    public async Task<LogQueryResponse> QueryAsync(LogQueryRequest request, CancellationToken ct)
    {
        var selectors = LogQlBuilder.BuildSelectors(request.Filter, options.Value.MaxBranches);
        if (selectors.Count == 0)
        {
            return new LogQueryResponse([], null);
        }

        var startNano = UnixTime.ToUnixNanoseconds(request.From);
        var endNano = request.Cursor ?? UnixTime.ToUnixNanoseconds(request.To);

        var branchResults = await Task.WhenAll(selectors.Select(selector =>
            lokiClient.QueryRangeAsync(selector, startNano, endNano, request.Limit, "backward", ct)));

        // The same log line can be returned by more than one OR-branch when their selectors
        // overlap, so de-duplicate on (timestamp, labels, line) before merging.
        var seen = new HashSet<(long Ts, string LabelsKey, string Line)>();
        var merged = new List<LogEntry>();

        foreach (var stream in branchResults.SelectMany(streams => streams))
        {
            var labelsKey = string.Join(",", stream.Labels.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value}"));

            foreach (var entry in stream.Values)
            {
                if (entry.Length < 2 || !long.TryParse(entry[0], out var timestampNano))
                {
                    continue;
                }

                var line = entry[1];
                if (seen.Add((timestampNano, labelsKey, line)))
                {
                    merged.Add(new LogEntry(timestampNano, line, stream.Labels));
                }
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

    private static string? BuildContextSelector(IReadOnlyList<ConditionNode> context)
    {
        if (context.Count == 0)
        {
            return null;
        }

        return LogQlBuilder.BuildSelectors(new GroupNode(GroupOperator.And, context)).Single();
    }
}
