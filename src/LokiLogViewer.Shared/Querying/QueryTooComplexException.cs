namespace LokiLogViewer.Shared.Querying;

public sealed class QueryTooComplexException(int maxBranches)
    : Exception($"Query expands to more than {maxBranches} OR-branches; simplify the filter.")
{
    public int MaxBranches { get; } = maxBranches;
}
