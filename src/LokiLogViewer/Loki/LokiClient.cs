using System.Net.Http.Json;
using System.Text.Json;
using LokiLogViewer.Shared.Search;
using Microsoft.AspNetCore.WebUtilities;

namespace LokiLogViewer.Loki;

internal sealed class LokiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<string>> GetLabelsAsync(
        string? selector, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var url = BuildUrl("/loki/api/v1/labels", selector, from, to);
        var result = await httpClient.GetFromJsonAsync<LokiLabelsResponse>(url, JsonOptions, ct);
        return result?.Data ?? [];
    }

    public async Task<IReadOnlyList<string>> GetLabelValuesAsync(
        string labelName, string? selector, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var url = BuildUrl($"/loki/api/v1/label/{Uri.EscapeDataString(labelName)}/values", selector, from, to);
        var result = await httpClient.GetFromJsonAsync<LokiLabelsResponse>(url, JsonOptions, ct);
        return result?.Data ?? [];
    }

    // start/end are raw unix-nanosecond timestamps (not DateTimeOffset) so that a pagination
    // cursor taken from a previous response's log line timestamp keeps its full precision -
    // round-tripping through DateTimeOffset (millisecond resolution) could skip or repeat lines
    // that share a millisecond at the page boundary.
    public async Task<IReadOnlyList<LokiStream>> QueryRangeAsync(
        string selector, long startUnixNano, long endUnixNano, int limit, string direction, CancellationToken ct)
    {
        var query = new Dictionary<string, string?>
        {
            ["query"] = selector,
            ["start"] = startUnixNano.ToString(),
            ["end"] = endUnixNano.ToString(),
            ["limit"] = limit.ToString(),
            ["direction"] = direction
        };

        var url = QueryHelpers.AddQueryString("/loki/api/v1/query_range", query);
        var result = await httpClient.GetFromJsonAsync<LokiQueryRangeResponse>(url, JsonOptions, ct);
        return result?.Data?.Result ?? [];
    }

    private static string BuildUrl(string path, string? selector, DateTimeOffset from, DateTimeOffset to)
    {
        var query = new Dictionary<string, string?>
        {
            ["start"] = UnixTime.ToUnixNanoseconds(from).ToString(),
            ["end"] = UnixTime.ToUnixNanoseconds(to).ToString()
        };
        if (!string.IsNullOrEmpty(selector))
        {
            query["query"] = selector;
        }

        return QueryHelpers.AddQueryString(path, query);
    }
}
