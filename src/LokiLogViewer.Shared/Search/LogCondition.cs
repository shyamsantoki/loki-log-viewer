namespace LokiLogViewer.Shared.Search;

// A single "label equals value" filter. There is deliberately no operator, negation, or
// nesting - search is a flat list of these, always ANDed together.
public sealed record LogCondition(string Label, string Value);
