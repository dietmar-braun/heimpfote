using TierschutzCollect.Domain.Enums;

namespace TierschutzCollect.Infrastructure.Search;

/// <summary>Slim projection of an animal for result lists (no tracking, no navigation graph).</summary>
public record AnimalListItem(
    string Slug,
    string Name,
    Species Species,
    string? Breed,
    int? ApproxAgeMonths,
    DateOnly? BirthDate,
    bool IsUrgent,
    string OrganizationName,
    string? City,
    string? ImageUrl);

public record AnimalSearchResult(IReadOnlyList<AnimalListItem> Items, int Total, int Page, int PageCount)
{
    public static AnimalSearchResult Empty { get; } = new([], 0, 1, 1);
}
