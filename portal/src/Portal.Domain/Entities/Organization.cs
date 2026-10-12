using TierschutzCollect.Domain.Enums;

namespace TierschutzCollect.Domain.Entities;

public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public OrganizationType Type { get; set; } = OrganizationType.Tierheim;

    public string? WebsiteUrl { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    public string? Street { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string Country { get; set; } = "DE";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Hashed API key for plugin/feed imports (never store it in plain text).</summary>
    public string? ApiKeyHash { get; set; }
    public bool IsVerified { get; set; }

    /// <summary>CMS of the website as detected by the crawler (e.g. wordpress, typo3) – used to prioritize plugins.</summary>
    public string? DetectedCms { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Animal> Animals { get; set; } = new();
}
