using Portal.Tests.Helpers;
using TierschutzCollect.Domain.Entities;
using TierschutzCollect.Domain.Enums;
using TierschutzCollect.Infrastructure.Search;

namespace Portal.Tests.Search;

public class AnimalSearchServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private Organization _org = null!;

    public void Dispose() => _db.Dispose();

    private async Task<Organization> OrgAsync() => _org ??= await _db.AddOrganizationAsync("Tierheim Test");

    private async Task AddAsync(string name, Action<Animal>? configure = null)
    {
        var org = await OrgAsync();
        var animal = new Animal
        {
            OrganizationId = org.Id,
            ExternalId = Guid.NewGuid().ToString("N"),
            Name = name,
            Slug = $"{name.ToLowerInvariant()}-{Guid.NewGuid().ToString("N")[..8]}"
        };
        configure?.Invoke(animal);

        await using var ctx = _db.NewContext();
        ctx.Animals.Add(animal);
        await ctx.SaveChangesAsync();
    }

    private async Task<AnimalSearchResult> SearchAsync(AnimalSearchCriteria criteria, int page = 1, int pageSize = 12)
    {
        await using var ctx = _db.NewContext();
        return await new AnimalSearchService(ctx).SearchAsync(criteria, page, pageSize);
    }

    private static string[] Names(AnimalSearchResult result) =>
        result.Items.Select(i => i.Name).OrderBy(n => n).ToArray();

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    // ---------- Basics ----------

    [Fact]
    public async Task EmptyCriteria_ReturnsAllAdoptableAnimals()
    {
        await AddAsync("A");
        await AddAsync("B");

        var result = await SearchAsync(new AnimalSearchCriteria());

        Assert.Equal(2, result.Total);
    }

    [Theory]
    [InlineData(AnimalStatus.Reserviert)]
    [InlineData(AnimalStatus.Vermittelt)]
    [InlineData(AnimalStatus.Archiviert)]
    public async Task OnlyAdoptableAnimalsAreReturned(AnimalStatus status)
    {
        await AddAsync("Sichtbar");
        await AddAsync("Unsichtbar", a => a.Status = status);

        var result = await SearchAsync(new AnimalSearchCriteria());

        Assert.Equal(["Sichtbar"], Names(result));
    }

    // ---------- Simple filters ----------

    [Fact]
    public async Task SpeciesFilter_OnlyReturnsThatSpecies()
    {
        await AddAsync("Bello", a => a.Species = Species.Hund);
        await AddAsync("Luna", a => a.Species = Species.Katze);

        var result = await SearchAsync(new AnimalSearchCriteria { Species = Species.Katze });

        Assert.Equal(["Luna"], Names(result));
    }

    [Fact]
    public async Task SexFilter_OnlyReturnsThatSex()
    {
        await AddAsync("Er", a => a.Sex = Sex.Maennlich);
        await AddAsync("Sie", a => a.Sex = Sex.Weiblich);
        await AddAsync("Unbekannt");

        var result = await SearchAsync(new AnimalSearchCriteria { Sex = Sex.Weiblich });

        Assert.Equal(["Sie"], Names(result));
    }

    [Fact]
    public async Task SizeFilter_OnlyReturnsThatSize()
    {
        await AddAsync("Klein", a => a.Size = AnimalSize.Klein);
        await AddAsync("Gross", a => a.Size = AnimalSize.Gross);

        var result = await SearchAsync(new AnimalSearchCriteria { Size = AnimalSize.Gross });

        Assert.Equal(["Gross"], Names(result));
    }

    [Fact]
    public async Task NeuteredOnly_ExcludesNotNeuteredAndUnknown()
    {
        await AddAsync("Ja", a => a.IsNeutered = true);
        await AddAsync("Nein", a => a.IsNeutered = false);
        await AddAsync("Unbekannt");

        var result = await SearchAsync(new AnimalSearchCriteria { NeuteredOnly = true });

        Assert.Equal(["Ja"], Names(result));
    }

    [Fact]
    public async Task UrgentOnly_OnlyReturnsUrgentAnimals()
    {
        await AddAsync("Dringend", a => a.IsUrgent = true);
        await AddAsync("Normal");

        var result = await SearchAsync(new AnimalSearchCriteria { UrgentOnly = true });

        Assert.Equal(["Dringend"], Names(result));
    }

    // ---------- Compatibility ----------

    [Fact]
    public async Task CompatibilityFilter_OnlyMatchesExplicitYes()
    {
        await AddAsync("Ja", a => a.GoodWithChildren = Compatibility.Ja);
        await AddAsync("Nein", a => a.GoodWithChildren = Compatibility.Nein);
        await AddAsync("Unbekannt", a => a.GoodWithChildren = Compatibility.Unbekannt);

        var result = await SearchAsync(new AnimalSearchCriteria { GoodWithChildren = true });

        Assert.Equal(["Ja"], Names(result));
    }

    [Fact]
    public async Task EachCompatibilityFilterUsesItsOwnField()
    {
        await AddAsync("Hundefreund", a => a.GoodWithDogs = Compatibility.Ja);
        await AddAsync("Katzenfreund", a => a.GoodWithCats = Compatibility.Ja);
        await AddAsync("Wohnungstier", a => a.SuitableForApartment = Compatibility.Ja);

        Assert.Equal(["Hundefreund"], Names(await SearchAsync(new AnimalSearchCriteria { GoodWithDogs = true })));
        Assert.Equal(["Katzenfreund"], Names(await SearchAsync(new AnimalSearchCriteria { GoodWithCats = true })));
        Assert.Equal(["Wohnungstier"], Names(await SearchAsync(new AnimalSearchCriteria { SuitableForApartment = true })));
    }

    // ---------- Age groups ----------

    private async Task SeedAgesAsync()
    {
        await AddAsync("JungGeburt", a => a.BirthDate = Today.AddMonths(-6));
        await AddAsync("JungSchaetzung", a => a.ApproxAgeMonths = 6);
        await AddAsync("ErwachsenGeburt", a => a.BirthDate = Today.AddYears(-3));
        await AddAsync("ErwachsenSchaetzung", a => a.ApproxAgeMonths = 36);
        await AddAsync("SeniorGeburt", a => a.BirthDate = Today.AddYears(-10));
        await AddAsync("SeniorSchaetzung", a => a.ApproxAgeMonths = 120);
        await AddAsync("OhneAlter");
    }

    [Theory]
    [InlineData(AgeGroup.Young, new[] { "JungGeburt", "JungSchaetzung" })]
    [InlineData(AgeGroup.Adult, new[] { "ErwachsenGeburt", "ErwachsenSchaetzung" })]
    [InlineData(AgeGroup.Senior, new[] { "SeniorGeburt", "SeniorSchaetzung" })]
    public async Task AgeGroupFilter_UsesBirthDateOrEstimatedAge(AgeGroup group, string[] expected)
    {
        await SeedAgesAsync();

        var result = await SearchAsync(new AnimalSearchCriteria { AgeGroup = group });

        Assert.Equal(expected.OrderBy(n => n).ToArray(), Names(result));
    }

    [Fact]
    public async Task AgeGroupFilter_ExcludesAnimalsWithoutAgeInformation()
    {
        await SeedAgesAsync();

        foreach (var group in Enum.GetValues<AgeGroup>())
        {
            var result = await SearchAsync(new AnimalSearchCriteria { AgeGroup = group });
            Assert.DoesNotContain("OhneAlter", Names(result));
        }
    }

    [Fact]
    public async Task BirthDate_TakesPrecedenceOverEstimatedAge()
    {
        // Born three years ago, but the (stale) estimate says 3 months
        await AddAsync("Widerspruch", a => { a.BirthDate = Today.AddYears(-3); a.ApproxAgeMonths = 3; });

        Assert.Empty((await SearchAsync(new AnimalSearchCriteria { AgeGroup = AgeGroup.Young })).Items);
        Assert.Single((await SearchAsync(new AnimalSearchCriteria { AgeGroup = AgeGroup.Adult })).Items);
    }

    // ---------- Text search (portable fallback) ----------

    [Fact]
    public async Task TextSearch_MatchesNameCaseInsensitive()
    {
        await AddAsync("Bello");
        await AddAsync("Luna");

        var result = await SearchAsync(new AnimalSearchCriteria { Text = "bEL" });

        Assert.Equal(["Bello"], Names(result));
    }

    [Fact]
    public async Task TextSearch_MatchesDescription()
    {
        await AddAsync("A", a => a.Description = "Sehr verschmust und ruhig");
        await AddAsync("B", a => a.Description = "Aktiv und verspielt");

        var result = await SearchAsync(new AnimalSearchCriteria { Text = "verschmust" });

        Assert.Equal(["A"], Names(result));
    }

    [Fact]
    public async Task TextSearch_MatchesBreedName()
    {
        await AddAsync("A", a => a.Breed = new Breed { Species = Species.Hund, Name = "Labrador" });
        await AddAsync("B");

        var result = await SearchAsync(new AnimalSearchCriteria { Text = "labra" });

        Assert.Equal(["A"], Names(result));
    }

    [Fact]
    public async Task TextSearch_WithoutMatch_ReturnsNothing()
    {
        await AddAsync("Bello");

        var result = await SearchAsync(new AnimalSearchCriteria { Text = "gibtesnicht" });

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task BlankText_IsIgnored()
    {
        await AddAsync("Bello");

        var result = await SearchAsync(new AnimalSearchCriteria { Text = "   " });

        Assert.Equal(1, result.Total);
    }

    // ---------- Combination ----------

    [Fact]
    public async Task Filters_AreCombinedWithAnd()
    {
        await AddAsync("Treffer", a => { a.Species = Species.Hund; a.Size = AnimalSize.Gross; a.GoodWithChildren = Compatibility.Ja; });
        await AddAsync("FalscheArt", a => { a.Species = Species.Katze; a.Size = AnimalSize.Gross; a.GoodWithChildren = Compatibility.Ja; });
        await AddAsync("FalscheGroesse", a => { a.Species = Species.Hund; a.Size = AnimalSize.Klein; a.GoodWithChildren = Compatibility.Ja; });
        await AddAsync("KeineKinder", a => { a.Species = Species.Hund; a.Size = AnimalSize.Gross; a.GoodWithChildren = Compatibility.Nein; });

        var result = await SearchAsync(new AnimalSearchCriteria
        {
            Species = Species.Hund,
            Size = AnimalSize.Gross,
            GoodWithChildren = true
        });

        Assert.Equal(["Treffer"], Names(result));
    }

    // ---------- Sorting ----------

    private async Task SeedSortAsync()
    {
        var now = DateTime.UtcNow;
        await AddAsync("A", a => { a.IsUrgent = true; a.FirstSeenAt = now.AddDays(-3); });
        await AddAsync("B", a => { a.FirstSeenAt = now; });
        await AddAsync("C", a => { a.IsUrgent = true; a.FirstSeenAt = now.AddDays(-1); });
    }

    [Fact]
    public async Task Sort_Recommended_PutsUrgentFirstThenNewest()
    {
        await SeedSortAsync();

        var result = await SearchAsync(new AnimalSearchCriteria { Sort = AnimalSort.Recommended });

        Assert.Equal(["C", "A", "B"], result.Items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public async Task Sort_Newest_OrdersByFirstSeenDescending()
    {
        await SeedSortAsync();

        var result = await SearchAsync(new AnimalSearchCriteria { Sort = AnimalSort.Newest });

        Assert.Equal(["B", "C", "A"], result.Items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public async Task Sort_NameAsc_OrdersAlphabetically()
    {
        await AddAsync("Charlie");
        await AddAsync("Anton");
        await AddAsync("Berta");

        var result = await SearchAsync(new AnimalSearchCriteria { Sort = AnimalSort.NameAsc });

        Assert.Equal(["Anton", "Berta", "Charlie"], result.Items.Select(i => i.Name).ToArray());
    }

    // ---------- Paging ----------

    private async Task SeedFiveAsync()
    {
        for (var i = 1; i <= 5; i++) await AddAsync($"Tier{i}");
    }

    [Fact]
    public async Task Paging_SplitsResultsIntoPages()
    {
        await SeedFiveAsync();
        var criteria = new AnimalSearchCriteria { Sort = AnimalSort.NameAsc };

        var first = await SearchAsync(criteria, page: 1, pageSize: 2);
        var last = await SearchAsync(criteria, page: 3, pageSize: 2);

        Assert.Equal(5, first.Total);
        Assert.Equal(3, first.PageCount);
        Assert.Equal(["Tier1", "Tier2"], first.Items.Select(i => i.Name).ToArray());
        Assert.Equal(["Tier5"], last.Items.Select(i => i.Name).ToArray());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(99, 3)]
    public async Task Paging_ClampsOutOfRangePages(int requested, int expected)
    {
        await SeedFiveAsync();

        var result = await SearchAsync(new AnimalSearchCriteria(), requested, pageSize: 2);

        Assert.Equal(expected, result.Page);
    }

    [Fact]
    public async Task NoResults_StillHasOnePage()
    {
        var result = await SearchAsync(new AnimalSearchCriteria());

        Assert.Equal(0, result.Total);
        Assert.Equal(1, result.PageCount);
        Assert.Equal(1, result.Page);
    }

    // ---------- Projection ----------

    [Fact]
    public async Task Projection_ContainsOrganizationBreedAndFirstImage()
    {
        await AddAsync("Bello", a =>
        {
            a.Species = Species.Hund;
            a.Breed = new Breed { Species = Species.Hund, Name = "Labrador" };
            a.Images.Add(new AnimalImage { Url = "https://example.org/zweites.jpg", SortOrder = 1 });
            a.Images.Add(new AnimalImage { Url = "https://example.org/erstes.jpg", SortOrder = 0 });
        });

        var item = Assert.Single((await SearchAsync(new AnimalSearchCriteria())).Items);

        Assert.Equal("Tierheim Test", item.OrganizationName);
        Assert.Equal("Labrador", item.Breed);
        Assert.Equal("https://example.org/erstes.jpg", item.ImageUrl);
        Assert.StartsWith("bello-", item.Slug);
    }
}
