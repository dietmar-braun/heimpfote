using Microsoft.EntityFrameworkCore;
using TierschutzCollect.Domain.Entities;
using TierschutzCollect.Domain.Enums;

namespace TierschutzCollect.Infrastructure.Search;

public class AnimalSearchService(AppDbContext db)
{
    public async Task<AnimalSearchResult> SearchAsync(
        AnimalSearchCriteria criteria, int page, int pageSize, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var filtered = ApplyFilters(db.Animals.AsNoTracking(), criteria, today, db.Database.IsNpgsql());

        var total = await filtered.CountAsync(ct);
        var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, pageCount);

        var items = await ApplySort(filtered, criteria.Sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AnimalListItem(
                a.Slug,
                a.Name,
                a.Species,
                a.Breed != null ? a.Breed.Name : null,
                a.ApproxAgeMonths,
                a.BirthDate,
                a.IsUrgent,
                a.Organization.Name,
                a.Organization.City,
                a.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()))
            .ToListAsync(ct);

        return new AnimalSearchResult(items, total, page, pageCount);
    }

    /// <summary>
    /// Applies all filters. Static and free of side effects so it can be tested against any provider.
    /// <paramref name="fullText"/> enables PostgreSQL full-text search (not available on SQLite).
    /// </summary>
    public static IQueryable<Animal> ApplyFilters(
        IQueryable<Animal> source, AnimalSearchCriteria c, DateOnly today, bool fullText)
    {
        // Only adoptable animals are ever shown in the public search
        var q = source.Where(a => a.Status == AnimalStatus.Vermittelbar);

        if (c.Species is { } species) q = q.Where(a => a.Species == species);
        if (c.Sex is { } sex) q = q.Where(a => a.Sex == sex);
        if (c.Size is { } size) q = q.Where(a => a.Size == size);
        if (c.NeuteredOnly) q = q.Where(a => a.IsNeutered == true);
        if (c.UrgentOnly) q = q.Where(a => a.IsUrgent);

        // "Unknown" never counts as a match for a compatibility filter
        if (c.GoodWithChildren) q = q.Where(a => a.GoodWithChildren == Compatibility.Ja);
        if (c.GoodWithDogs) q = q.Where(a => a.GoodWithDogs == Compatibility.Ja);
        if (c.GoodWithCats) q = q.Where(a => a.GoodWithCats == Compatibility.Ja);
        if (c.SuitableForApartment) q = q.Where(a => a.SuitableForApartment == Compatibility.Ja);

        if (c.AgeGroup is { } group) q = ApplyAgeGroup(q, group, today);
        if (!string.IsNullOrWhiteSpace(c.Text)) q = ApplyText(q, c.Text.Trim(), fullText);

        return q;
    }

    /// <summary>
    /// A birth date takes precedence over the estimated age (same rule as the display).
    /// Animals without any age information never match an age filter.
    /// </summary>
    private static IQueryable<Animal> ApplyAgeGroup(IQueryable<Animal> q, AgeGroup group, DateOnly today)
    {
        var youngCutoff = today.AddMonths(-12);
        var seniorCutoff = today.AddMonths(-96);

        return group switch
        {
            AgeGroup.Young => q.Where(a =>
                (a.BirthDate != null && a.BirthDate > youngCutoff) ||
                (a.BirthDate == null && a.ApproxAgeMonths != null && a.ApproxAgeMonths < 12)),

            AgeGroup.Adult => q.Where(a =>
                (a.BirthDate != null && a.BirthDate <= youngCutoff && a.BirthDate > seniorCutoff) ||
                (a.BirthDate == null && a.ApproxAgeMonths >= 12 && a.ApproxAgeMonths < 96)),

            AgeGroup.Senior => q.Where(a =>
                (a.BirthDate != null && a.BirthDate <= seniorCutoff) ||
                (a.BirthDate == null && a.ApproxAgeMonths >= 96)),

            _ => q
        };
    }

    private static IQueryable<Animal> ApplyText(IQueryable<Animal> q, string text, bool fullText)
    {
        var term = text.ToLower();

        if (fullText)
        {
            // Stemmed German full-text match on name and description (uses the GIN index),
            // plus substring matches so that partial names ("Bel" -> "Bello") and breeds still work.
            return q.Where(a =>
                EF.Functions.ToTsVector("german", a.Name + " " + a.Description)
                    .Matches(EF.Functions.WebSearchToTsQuery("german", text)) ||
                a.Name.ToLower().Contains(term) ||
                (a.Breed != null && a.Breed.Name.ToLower().Contains(term)));
        }

        // Portable fallback (SQLite in tests): case-insensitive substring match
        return q.Where(a =>
            a.Name.ToLower().Contains(term) ||
            (a.Description != null && a.Description.ToLower().Contains(term)) ||
            (a.Breed != null && a.Breed.Name.ToLower().Contains(term)));
    }

    /// <summary>The Id is always the last sort key so that paging is stable.</summary>
    private static IOrderedQueryable<Animal> ApplySort(IQueryable<Animal> q, AnimalSort sort) => sort switch
    {
        AnimalSort.Newest => q.OrderByDescending(a => a.FirstSeenAt).ThenBy(a => a.Id),
        AnimalSort.NameAsc => q.OrderBy(a => a.Name).ThenBy(a => a.Id),
        _ => q.OrderByDescending(a => a.IsUrgent).ThenByDescending(a => a.FirstSeenAt).ThenBy(a => a.Id)
    };
}
