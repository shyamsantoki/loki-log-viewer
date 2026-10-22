namespace LokiLogViewer.Shared.Search;

// Used to scope the /labels/{name}/values picker to whatever other fixed fields already have a
// value, so e.g. picking a namespace narrows down the containers offered for the next field.
public sealed record LabelsRequest(
    IReadOnlyList<LogCondition> Context,
    DateTimeOffset From,
    DateTimeOffset To);
