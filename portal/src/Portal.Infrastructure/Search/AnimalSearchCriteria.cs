using TierschutzCollect.Domain.Enums;

namespace TierschutzCollect.Infrastructure.Search;

public enum AgeGroup
{
    /// <summary>Younger than 12 months.</summary>
    Young,
    /// <summary>12 months up to (excluding) 8 years.</summary>
    Adult,
    /// <summary>8 years and older.</summary>
    Senior
}

public enum AnimalSort
{
    /// <summary>Urgent animals first, then newest.</summary>
    Recommended,
    Newest,
    NameAsc
}

/// <summary>All filters of the animal search. Unset values (null/false) do not restrict the result.</summary>
public record AnimalSearchCriteria
{
    public string? Text { get; init; }
    public Species? Species { get; init; }
    public Sex? Sex { get; init; }
    public AnimalSize? Size { get; init; }
    public AgeGroup? AgeGroup { get; init; }
    public bool NeuteredOnly { get; init; }
    public bool GoodWithChildren { get; init; }
    public bool GoodWithDogs { get; init; }
    public bool GoodWithCats { get; init; }
    public bool SuitableForApartment { get; init; }
    public bool UrgentOnly { get; init; }
    public AnimalSort Sort { get; init; } = AnimalSort.Recommended;
}
