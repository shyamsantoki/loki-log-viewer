namespace LokiLogViewer.Shared.Search;

public sealed record LogQueryRequest(
    IReadOnlyList<LogCondition> Conditions,
    DateTimeOffset From,
    DateTimeOffset To,
    int Limit,
    // Pass back the previous response's NextCursor to fetch the next (older) page.
    long? Cursor = null);
