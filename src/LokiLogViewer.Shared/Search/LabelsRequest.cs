using LokiLogViewer.Shared.Querying;

namespace LokiLogViewer.Shared.Search;

// Used to scope the /labels and /labels/{name}/values pickers to whatever conditions are already
// set in the AND-group the user is currently editing. Deliberately flat (no nested OR) - it's a
// single selector, not a full filter tree.
public sealed record LabelsRequest(
    IReadOnlyList<ConditionNode> Context,
    DateTimeOffset From,
    DateTimeOffset To);
