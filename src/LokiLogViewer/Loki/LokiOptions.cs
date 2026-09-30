using System.ComponentModel.DataAnnotations;

namespace LokiLogViewer.Loki;

public sealed class LokiOptions
{
    public const string SectionName = "Loki";

    [Required]
    public required string BaseUrl { get; set; }

    /// <summary>X-Scope-OrgID header, only needed against a self-hosted multi-tenant Loki.</summary>
    public string? OrgId { get; set; }

    /// <summary>Sent as "Authorization: Bearer {BearerToken}" when set - for a reverse proxy/gateway in front of Loki that expects a bearer token.</summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// Sent as HTTP Basic auth when both are set. Grafana Cloud Loki uses this: username is the
    /// stack's Loki instance ID, password is an access policy token. Ignored if BearerToken is set.
    /// </summary>
    public string? BasicAuthUsername { get; set; }
    public string? BasicAuthPassword { get; set; }

    /// <summary>Caps how many LogQL selectors one query tree may expand into.</summary>
    public int MaxBranches { get; set; } = 25;

    public int DefaultLimit { get; set; } = 200;
}
