namespace LokiLogViewer.Shared.Search;

// One of the server-configured quick-filter fields (see Loki:SearchFields in appsettings.json).
// When Values is set, the field is restricted to that fixed list (rendered as a dropdown)
// instead of free text with live autocomplete fetched from Loki.
public sealed record SearchFieldDefinition(string Label, string DisplayName, IReadOnlyList<string>? Values = null);
