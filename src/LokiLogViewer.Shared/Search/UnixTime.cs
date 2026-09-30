namespace LokiLogViewer.Shared.Search;

public static class UnixTime
{
    public static long ToUnixNanoseconds(DateTimeOffset dt) => dt.ToUnixTimeMilliseconds() * 1_000_000L;
}
