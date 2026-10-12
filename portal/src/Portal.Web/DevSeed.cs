using TierschutzCollect.Domain.Entities;
using TierschutzCollect.Domain.Enums;
using TierschutzCollect.Infrastructure.Import;

namespace TierschutzCollect.Web;

/// <summary>Sample data for local development (only created in Development mode and only if the animals table is empty).</summary>
public static class DevSeed
{
    public static Task<ImportResult> SeedAnimalsAsync(ImportService import, Organization org) =>
        import.ImportAsync(org, new ImportRequest(DataSource.Manuell, false,
        [
            new("demo-1", "Bello", Species.Hund, BreedName: "Labrador", Sex: Sex.Maennlich, ApproxAgeMonths: 36,
                Size: AnimalSize.Gross, WeightKg: 28, IsNeutered: true, AdoptionFee: 250,
                GoodWithChildren: Compatibility.Ja, GoodWithDogs: Compatibility.Ja, GoodWithCats: Compatibility.Unbekannt,
                Description: "Bello ist ein freundlicher, verspielter Rüde.\nEr sucht eine aktive Familie mit Garten."),
            new("demo-2", "Luna", Species.Katze, Sex: Sex.Weiblich, ApproxAgeMonths: 12, Size: AnimalSize.Klein,
                IsNeutered: true, IsUrgent: true, GoodWithChildren: Compatibility.Ja, GoodWithCats: Compatibility.Ja,
                Description: "Luna ist neugierig und verschmust und wäre gern Freigängerin."),
            new("demo-3", "Max", Species.Hund, BreedName: "Mischling", IsMixedBreed: true, Sex: Sex.Maennlich, ApproxAgeMonths: 84,
                Size: AnimalSize.Mittel, GoodWithChildren: Compatibility.Nein, GoodWithCats: Compatibility.Nein,
                Description: "Max ist ein ruhiger Senior und liebt lange, gemütliche Spaziergänge."),
            new("demo-4", "Mia", Species.Katze, BreedName: "Europäisch Kurzhaar", Sex: Sex.Weiblich, ApproxAgeMonths: 6,
                Size: AnimalSize.Klein, SuitableForApartment: Compatibility.Ja,
                Description: "Mia ist ein junges Kätzchen und sucht ein Zuhause, auch als Wohnungskatze."),
            new("demo-5", "Coco", Species.Kleintier, BreedName: "Kaninchen", Sex: Sex.Weiblich, ApproxAgeMonths: 24,
                Size: AnimalSize.Klein, Description: "Coco braucht einen Partner und viel Platz zum Hoppeln."),
            new("demo-6", "Rocky", Species.Hund, BreedName: "Deutscher Schäferhund", Sex: Sex.Maennlich, ApproxAgeMonths: 60,
                Size: AnimalSize.Gross, NeedsExperiencedOwner: true, IsUrgent: true,
                Description: "Rocky ist klug und braucht eine erfahrene, konsequente Hand."),
        ]));
}
