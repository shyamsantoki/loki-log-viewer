namespace LokiLogViewer.Shared.Search;

public sealed record LogQueryResponse(IReadOnlyList<LogEntry> Entries, long? NextCursor);
