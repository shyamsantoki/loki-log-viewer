using System.ComponentModel.DataAnnotations;

namespace LokiLogViewer.Loki;

public sealed class LokiOptions
{
    public const string SectionName = "Loki";

    [Required]
    public required string BaseUrl { get; set; }

    /// <summary>X-Scope-OrgID header, only needed against a multi-tenant Loki.</summary>
    public string? OrgId { get; set; }

    /// <summary>Sent as "Authorization: Bearer {BearerToken}" when set.</summary>
    public string? BearerToken { get; set; }

    /// <summary>Caps how many LogQL selectors one query tree may expand into.</summary>
    public int MaxBranches { get; set; } = 25;

    public int DefaultLimit { get; set; } = 200;
}
