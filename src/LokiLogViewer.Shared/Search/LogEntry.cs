namespace LokiLogViewer.Shared.Search;

public sealed record LogEntry(long TimestampUnixNano, string Line, IReadOnlyDictionary<string, string> Labels)
{
    public DateTimeOffset Timestamp => DateTimeOffset.FromUnixTimeMilliseconds(TimestampUnixNano / 1_000_000);
}
