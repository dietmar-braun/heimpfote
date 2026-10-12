using Microsoft.EntityFrameworkCore;
using Portal.Tests.Helpers;
using TierschutzCollect.Domain.Entities;
using TierschutzCollect.Domain.Enums;
using TierschutzCollect.Infrastructure.Import;

namespace Portal.Tests.Import;

public class ImportServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    // ---------- Helpers ----------

    private static AnimalImportDto Dto(
        string id = "1",
        string name = "Bello",
        Species species = Species.Hund,
        string? description = null,
        string? breed = null,
        List<string>? images = null,
        AnimalStatus status = AnimalStatus.Vermittelbar) =>
        new(id, name, species, BreedName: breed, Description: description, ImageUrls: images, Status: status);

    private static ImportRequest Req(DataSource source, bool snapshot, params AnimalImportDto[] animals) =>
        new(source, snapshot, animals.ToList());

    private async Task<ImportResult> RunAsync(Organization org, ImportRequest req)
    {
        await using var ctx = _db.NewContext();
        return await new ImportService(ctx).ImportAsync(org, req);
    }

    /// <summary>Short pause so that LastSeenAt reliably differs between two imports.</summary>
    private static Task Pause() => Task.Delay(30);

    // ---------- Creating ----------

    [Fact]
    public async Task NewAnimals_AreCreated()
    {
        var org = await _db.AddOrganizationAsync();

        var result = await RunAsync(org, Req(DataSource.Crawler, false, Dto("1"), Dto("2", "Luna", Species.Katze)));

        Assert.Equal(new ImportResult(2, 0, 0, 0), result);
        await using var ctx = _db.NewContext();
        Assert.Equal(2, await ctx.Animals.CountAsync());
    }

    [Fact]
    public async Task NewAnimal_GetsSlugSourceAndOrganization()
    {
        var org = await _db.AddOrganizationAsync();

        await RunAsync(org, Req(DataSource.Plugin, false, Dto("1", "Bello")));

        await using var ctx = _db.NewContext();
        var animal = await ctx.Animals.SingleAsync();
        Assert.Matches("^bello-[0-9a-f]{8}$", animal.Slug);
        Assert.Equal(DataSource.Plugin, animal.Source);
        Assert.Equal(org.Id, animal.OrganizationId);
        Assert.False(string.IsNullOrEmpty(animal.ContentHash));
    }

    [Fact]
    public async Task NameWithoutUsableCharacters_FallsBackToTierSlug()
    {
        var org = await _db.AddOrganizationAsync();

        await RunAsync(org, Req(DataSource.Crawler, false, Dto("1", "!!!")));

        await using var ctx = _db.NewContext();
        Assert.Matches("^tier-[0-9a-f]{8}$", (await ctx.Animals.SingleAsync()).Slug);
    }

    [Fact]
    public async Task DuplicateExternalIdsInOneBatch_AreImportedOnce()
    {
        var org = await _db.AddOrganizationAsync();

        var result = await RunAsync(org, Req(DataSource.Crawler, false, Dto("1"), Dto("1", "Anderer Name")));

        Assert.Equal(1, result.Created);
        await using var ctx = _db.NewContext();
        Assert.Equal(1, await ctx.Animals.CountAsync());
    }

    [Fact]
    public async Task SameExternalIdInTwoOrganizations_AreSeparateAnimals()
    {
        var org1 = await _db.AddOrganizationAsync("Tierheim A");
        var org2 = await _db.AddOrganizationAsync("Tierheim B");

        await RunAsync(org1, Req(DataSource.Crawler, false, Dto("1")));
        var result = await RunAsync(org2, Req(DataSource.Crawler, false, Dto("1")));

        Assert.Equal(1, result.Created);
        await using var ctx = _db.NewContext();
        Assert.Equal(2, await ctx.Animals.CountAsync());
    }

    // ---------- Updating / unchanged ----------

    [Fact]
    public async Task SameDataTwice_IsReportedAsUnchanged_AndOnlyRefreshesLastSeen()
    {
        var org = await _db.AddOrganizationAsync();
        await RunAsync(org, Req(DataSource.Crawler, false, Dto("1")));
        DateTime firstSeen;
        await using (var ctx = _db.NewContext())
            firstSeen = (await ctx.Animals.SingleAsync()).LastSeenAt;
        await Pause();

        var result = await RunAsync(org, Req(DataSource.Crawler, false, Dto("1")));

        Assert.Equal(new ImportResult(0, 0, 1, 0), result);
        await using var ctx2 = _db.NewContext();
        var animal = await ctx2.Animals.SingleAsync();
        Assert.True(animal.LastSeenAt > firstSeen);
    }

    [Fact]
    public async Task ChangedData_UpdatesExistingAnimal_AndKeepsSlug()
    {
        var org = await _db.AddOrganizationAsync();
        await RunAsync(org, Req(DataSource.Crawler, false, Dto("1", description: "alt")));
        string slugBefore;
        await using (var ctx = _db.NewContext())
            slugBefore = (await ctx.Animals.SingleAsync()).Slug;

        var result = await RunAsync(org, Req(DataSource.Crawler, false, Dto("1", description: "neu")));

        Assert.Equal(new ImportResult(0, 1, 0, 0), result);
        await using var ctx2 = _db.NewContext();
        var animal = await ctx2.Animals.SingleAsync();
        Assert.Equal("neu", animal.Description);
        Assert.Equal(slugBefore, animal.Slug);
    }

    // ---------- Images ----------

    [Fact]
    public async Task Images_KeepOrder_AndFirstIsPrimary()
    {
        var org = await _db.AddOrganizationAsync();
        var images = new List<string> { "https://x.de/1.jpg", "https://x.de/2.jpg", "https://x.de/3.jpg" };

        await RunAsync(org, Req(DataSource.Crawler, false, Dto("1", images: images)));

        await using var ctx = _db.NewContext();
        var stored = await ctx.AnimalImages.OrderBy(i => i.SortOrder).ToListAsync();
        Assert.Equal(images, stored.Select(i => i.Url).ToList());
        Assert.True(stored[0].IsPrimary);
        Assert.All(stored.Skip(1), i => Assert.False(i.IsPrimary));
    }

    [Fact]
    public async Task ChangedImages_ReplaceTheOldOnes()
    {
        var org = await _db.AddOrganizationAsync();
        await RunAsync(org, Req(DataSource.Crawler, false,
            Dto("1", images: ["https://x.de/1.jpg", "https://x.de/2.jpg"])));

        await RunAsync(org, Req(DataSource.Crawler, false,
            Dto("1", images: ["https://x.de/neu.jpg"])));

        await using var ctx = _db.NewContext();
        var image = await ctx.AnimalImages.SingleAsync();
        Assert.Equal("https://x.de/neu.jpg", image.Url);
    }

    // ---------- Breeds ----------

    [Fact]
    public async Task Breeds_AreCreatedOnce_CaseInsensitive()
    {
        var org = await _db.AddOrganizationAsync();

        await RunAsync(org, Req(DataSource.Crawler, false,
            Dto("1", breed: "Labrador"),
            Dto("2", "Rex", breed: "labrador")));

        await using var ctx = _db.NewContext();
        Assert.Equal(1, await ctx.Breeds.CountAsync());
        Assert.Equal(2, await ctx.Animals.CountAsync(a => a.BreedId != null));
    }

    [Fact]
    public async Task SameBreedNameForDifferentSpecies_CreatesSeparateBreeds()
    {
        var org = await _db.AddOrganizationAsync();

        await RunAsync(org, Req(DataSource.Crawler, false,
            Dto("1", species: Species.Hund, breed: "Mix"),
            Dto("2", "Mia", Species.Katze, breed: "Mix")));

        await using var ctx = _db.NewContext();
        Assert.Equal(2, await ctx.Breeds.CountAsync());
    }

    [Fact]
    public async Task RemovedBreed_ClearsTheBreedReference()
    {
        var org = await _db.AddOrganizationAsync();
        await RunAsync(org, Req(DataSource.Crawler, false, Dto("1", breed: "Labrador")));

        await RunAsync(org, Req(DataSource.Crawler, false, Dto("1", breed: null)));

        await using var ctx = _db.NewContext();
        Assert.Null((await ctx.Animals.SingleAsync()).BreedId);
    }

    // ---------- Snapshot / archiving ----------

    [Fact]
    public async Task FullSnapshot_ArchivesAnimalsThatAreMissing()
    {
        var org = await _db.AddOrganizationAsync();
        await RunAsync(org, Req(DataSource.Crawler, true, Dto("a"), Dto("b", "Luna")));
        await Pause();

        var result = await RunAsync(org, Req(DataSource.Crawler, true, Dto("a")));

        Assert.Equal(1, result.Archived);
        await using var ctx = _db.NewContext();
        Assert.Equal(AnimalStatus.Vermittelbar, (await ctx.Animals.SingleAsync(x => x.ExternalId == "a")).Status);
        Assert.Equal(AnimalStatus.Archiviert, (await ctx.Animals.SingleAsync(x => x.ExternalId == "b")).Status);
    }

    [Fact]
    public async Task PartialImport_DoesNotArchiveAnything()
    {
        var org = await _db.AddOrganizationAsync();
        await RunAsync(org, Req(DataSource.Crawler, false, Dto("a"), Dto("b", "Luna")));
        await Pause();

        var result = await RunAsync(org, Req(DataSource.Crawler, false, Dto("a")));

        Assert.Equal(0, result.Archived);
        await using var ctx = _db.NewContext();
        Assert.Equal(2, await ctx.Animals.CountAsync(a => a.Status == AnimalStatus.Vermittelbar));
    }

    [Fact]
    public async Task FullSnapshot_DoesNotTouchAnimalsFromOtherSources()
    {
        var org = await _db.AddOrganizationAsync();
        await RunAsync(org, Req(DataSource.Plugin, true, Dto("p1"), Dto("p2", "Luna")));
        await Pause();

        var result = await RunAsync(org, Req(DataSource.Crawler, true, Dto("c1", "Max")));

        Assert.Equal(0, result.Archived);
        await using var ctx = _db.NewContext();
        Assert.Equal(2, await ctx.Animals.CountAsync(a => a.Source == DataSource.Plugin && a.Status == AnimalStatus.Vermittelbar));
    }

    [Fact]
    public async Task FullSnapshot_DoesNotTouchOtherOrganizations()
    {
        var org1 = await _db.AddOrganizationAsync("Tierheim A");
        var org2 = await _db.AddOrganizationAsync("Tierheim B");
        await RunAsync(org1, Req(DataSource.Crawler, true, Dto("1"), Dto("2", "Luna")));
        await Pause();

        await RunAsync(org2, Req(DataSource.Crawler, true, Dto("9", "Max")));

        await using var ctx = _db.NewContext();
        Assert.Equal(2, await ctx.Animals.CountAsync(a => a.OrganizationId == org1.Id && a.Status == AnimalStatus.Vermittelbar));
    }

    [Fact]
    public async Task ArchivedAnimalThatReappears_IsReactivated()
    {
        var org = await _db.AddOrganizationAsync();
        await RunAsync(org, Req(DataSource.Crawler, true, Dto("a"), Dto("b", "Luna")));
        await Pause();
        await RunAsync(org, Req(DataSource.Crawler, true, Dto("a")));
        await Pause();

        // "b" comes back unchanged
        var result = await RunAsync(org, Req(DataSource.Crawler, false, Dto("b", "Luna")));

        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Unchanged);
        await using var ctx = _db.NewContext();
        Assert.Equal(AnimalStatus.Vermittelbar, (await ctx.Animals.SingleAsync(x => x.ExternalId == "b")).Status);
    }

    [Fact]
    public async Task StatusFromSource_IsTakenOver()
    {
        var org = await _db.AddOrganizationAsync();
        await RunAsync(org, Req(DataSource.Crawler, false, Dto("1")));

        await RunAsync(org, Req(DataSource.Crawler, false, Dto("1", status: AnimalStatus.Reserviert)));

        await using var ctx = _db.NewContext();
        Assert.Equal(AnimalStatus.Reserviert, (await ctx.Animals.SingleAsync()).Status);
    }
}
