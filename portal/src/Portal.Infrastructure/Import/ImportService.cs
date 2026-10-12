using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TierschutzCollect.Domain.Entities;
using TierschutzCollect.Domain.Enums;
using TierschutzCollect.Infrastructure.Services;

namespace TierschutzCollect.Infrastructure.Import;

public class ImportService(AppDbContext db)
{
    public async Task<ImportResult> ImportAsync(Organization org, ImportRequest req, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var items = req.Animals.DistinctBy(a => a.ExternalId).ToList();
        var externalIds = items.Select(a => a.ExternalId).ToList();

        var existing = await db.Animals
            .Include(a => a.Images)
            .Where(a => a.OrganizationId == org.Id && externalIds.Contains(a.ExternalId))
            .ToDictionaryAsync(a => a.ExternalId, ct);

        var breedCache = new Dictionary<(Species, string), Breed>();
        int created = 0, updated = 0, unchanged = 0;

        foreach (var dto in items)
        {
            var hash = ComputeHash(dto);

            if (existing.TryGetValue(dto.ExternalId, out var animal))
            {
                animal.LastSeenAt = now;

                // Unchanged: only refresh LastSeenAt. Archived animals that reappear are applied again.
                if (animal.ContentHash == hash && animal.Status != AnimalStatus.Archiviert)
                {
                    unchanged++;
                    continue;
                }

                await ApplyAsync(animal, dto, req.Source, hash, now, breedCache, ct);
                updated++;
            }
            else
            {
                var id = Guid.NewGuid();
                var baseSlug = Slugger.Slugify(dto.Name);
                if (baseSlug.Length == 0) baseSlug = "tier";

                animal = new Animal
                {
                    Id = id,
                    OrganizationId = org.Id,
                    ExternalId = dto.ExternalId,
                    Name = dto.Name.Trim(),
                    Slug = $"{baseSlug}-{id.ToString("N")[..8]}",
                    FirstSeenAt = now
                };
                await ApplyAsync(animal, dto, req.Source, hash, now, breedCache, ct);
                db.Animals.Add(animal);
                created++;
            }
        }

        await db.SaveChangesAsync(ct);

        var archived = 0;
        if (req.IsFullSnapshot)
        {
            // Archive everything from this source that was not seen in this run (LastSeenAt < now).
            archived = await db.Animals
                .Where(a => a.OrganizationId == org.Id
                            && a.Source == req.Source
                            && a.LastSeenAt < now
                            && a.Status != AnimalStatus.Archiviert)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.Status, AnimalStatus.Archiviert)
                    .SetProperty(a => a.UpdatedAt, now), ct);
        }

        return new ImportResult(created, updated, unchanged, archived);
    }

    private async Task ApplyAsync(
        Animal a, AnimalImportDto d, DataSource source, string hash, DateTime now,
        Dictionary<(Species, string), Breed> breedCache, CancellationToken ct)
    {
        a.Name = d.Name.Trim();
        a.Species = d.Species;
        a.IsMixedBreed = d.IsMixedBreed;
        a.Sex = d.Sex;
        a.BirthDate = d.BirthDate;
        a.ApproxAgeMonths = d.ApproxAgeMonths;
        a.Size = d.Size;
        a.WeightKg = d.WeightKg;
        a.Color = d.Color?.Trim();
        a.IsNeutered = d.IsNeutered;
        a.Description = d.Description;
        a.AdoptionFee = d.AdoptionFee;
        a.Status = d.Status;
        a.IsUrgent = d.IsUrgent;
        a.NeedsExperiencedOwner = d.NeedsExperiencedOwner;
        a.HasSpecialNeeds = d.HasSpecialNeeds;
        a.GoodWithChildren = d.GoodWithChildren;
        a.GoodWithDogs = d.GoodWithDogs;
        a.GoodWithCats = d.GoodWithCats;
        a.SuitableForApartment = d.SuitableForApartment;
        a.SourceUrl = d.SourceUrl;
        a.Source = source;
        a.ContentHash = hash;
        a.LastSeenAt = now;
        a.UpdatedAt = now;

        var breed = string.IsNullOrWhiteSpace(d.BreedName)
            ? null
            : await GetOrCreateBreedAsync(d.Species, d.BreedName.Trim(), breedCache, ct);
        a.Breed = breed;
        if (breed is null) a.BreedId = null;

        a.Images.Clear();
        var order = 0;
        foreach (var url in d.ImageUrls ?? [])
        {
            a.Images.Add(new AnimalImage { Url = url, SortOrder = order, IsPrimary = order == 0 });
            order++;
        }
    }

    private async Task<Breed> GetOrCreateBreedAsync(
        Species species, string name, Dictionary<(Species, string), Breed> cache, CancellationToken ct)
    {
        var key = (species, name.ToLowerInvariant());
        if (cache.TryGetValue(key, out var cached)) return cached;

        var lower = name.ToLower();
        var breed = await db.Breeds.FirstOrDefaultAsync(b => b.Species == species && b.Name.ToLower() == lower, ct);
        if (breed is null)
        {
            breed = new Breed { Species = species, Name = name };
            db.Breeds.Add(breed);
        }
        cache[key] = breed;
        return breed;
    }

    private static string ComputeHash(AnimalImportDto dto) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto)))).ToLowerInvariant();
}
