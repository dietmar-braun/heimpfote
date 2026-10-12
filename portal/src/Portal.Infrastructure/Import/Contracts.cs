using TierschutzCollect.Domain.Enums;

namespace TierschutzCollect.Infrastructure.Import;

/// <param name="Source">Who delivers the data? Crawler, plugin or feed.</param>
/// <param name="IsFullSnapshot">
/// true = the list contains ALL currently adoptable animals of the organization.
/// Animals from this source that are missing are archived.
/// </param>
public record ImportRequest(DataSource Source, bool IsFullSnapshot, List<AnimalImportDto> Animals);

public record AnimalImportDto(
    string ExternalId,
    string Name,
    Species Species,
    string? BreedName = null,
    bool IsMixedBreed = false,
    Sex Sex = Sex.Unbekannt,
    DateOnly? BirthDate = null,
    int? ApproxAgeMonths = null,
    AnimalSize Size = AnimalSize.Unbekannt,
    decimal? WeightKg = null,
    string? Color = null,
    bool? IsNeutered = null,
    string? Description = null,
    decimal? AdoptionFee = null,
    AnimalStatus Status = AnimalStatus.Vermittelbar,
    bool IsUrgent = false,
    bool NeedsExperiencedOwner = false,
    bool HasSpecialNeeds = false,
    Compatibility GoodWithChildren = Compatibility.Unbekannt,
    Compatibility GoodWithDogs = Compatibility.Unbekannt,
    Compatibility GoodWithCats = Compatibility.Unbekannt,
    Compatibility SuitableForApartment = Compatibility.Unbekannt,
    string? SourceUrl = null,
    List<string>? ImageUrls = null);

public record ImportResult(int Created, int Updated, int Unchanged, int Archived);
