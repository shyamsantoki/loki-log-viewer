using System.Text.Json.Serialization;

namespace LokiLogViewer.Loki;

// Raw shapes returned by Loki's /loki/api/v1/* endpoints - see
// https://grafana.com/docs/loki/latest/reference/loki-http-api/

internal sealed record LokiLabelsResponse(string Status, IReadOnlyList<string>? Data);

internal sealed record LokiQueryRangeResponse(string Status, LokiQueryRangeData? Data);

internal sealed record LokiQueryRangeData(
    [property: JsonPropertyName("resultType")] string ResultType,
    [property: JsonPropertyName("result")] IReadOnlyList<LokiStream>? Result);

internal sealed record LokiStream(
    [property: JsonPropertyName("stream")] IReadOnlyDictionary<string, string> Labels,
    // Each entry is ["<unix-nanosecond timestamp as string>", "<log line>"].
    [property: JsonPropertyName("values")] IReadOnlyList<string[]> Values);
