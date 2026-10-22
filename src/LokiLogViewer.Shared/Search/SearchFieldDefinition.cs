namespace LokiLogViewer.Shared.Search;

// One of the server-configured quick-filter fields (see Loki:SearchFields in appsettings.json).
public sealed record SearchFieldDefinition(string Label, string DisplayName);
