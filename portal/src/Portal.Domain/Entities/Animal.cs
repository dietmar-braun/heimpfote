using TierschutzCollect.Domain.Enums;

namespace TierschutzCollect.Domain.Entities;

public class Animal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // --- Origin / synchronization ---
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    /// <summary>ID of the animal at the source. Unique together with OrganizationId.</summary>
    public required string ExternalId { get; set; }
    public DataSource Source { get; set; }
    public string? SourceUrl { get; set; }
    /// <summary>Hash of the content fields – detects changes without comparing every field.</summary>
    public string? ContentHash { get; set; }
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    /// <summary>When the source last delivered this animal. Basis for automatic archiving.</summary>
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // --- Master data ---
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public Species Species { get; set; }
    public int? BreedId { get; set; }
    public Breed? Breed { get; set; }
    public bool IsMixedBreed { get; set; }
    public Sex Sex { get; set; }
    public DateOnly? BirthDate { get; set; }
    /// <summary>Estimated age in months, used when no birth date is known.</summary>
    public int? ApproxAgeMonths { get; set; }
    public AnimalSize Size { get; set; }
    public decimal? WeightKg { get; set; }
    public string? Color { get; set; }
    public bool? IsNeutered { get; set; }
    public string? Description { get; set; }
    public decimal? AdoptionFee { get; set; }

    // --- Adoption ---
    public AnimalStatus Status { get; set; } = AnimalStatus.Vermittelbar;
    public bool IsUrgent { get; set; }
    public bool NeedsExperiencedOwner { get; set; }
    public bool HasSpecialNeeds { get; set; }

    // --- Compatibility (used as search filters) ---
    public Compatibility GoodWithChildren { get; set; }
    public Compatibility GoodWithDogs { get; set; }
    public Compatibility GoodWithCats { get; set; }
    public Compatibility SuitableForApartment { get; set; }

    public List<AnimalImage> Images { get; set; } = new();
}
