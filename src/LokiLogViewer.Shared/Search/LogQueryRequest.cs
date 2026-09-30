using LokiLogViewer.Shared.Querying;

namespace LokiLogViewer.Shared.Search;

public sealed record LogQueryRequest(
    QueryNode Filter,
    DateTimeOffset From,
    DateTimeOffset To,
    int Limit,
    // Pass back the previous response's NextCursor to fetch the next (older) page.
    long? Cursor = null);
