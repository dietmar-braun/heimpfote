using Microsoft.EntityFrameworkCore;
using Portal.Tests.Helpers;
using TierschutzCollect.Domain.Entities;
using TierschutzCollect.Domain.Enums;

namespace Portal.Tests.Data;

public class DbModelTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private static Animal NewAnimal(Guid orgId, string externalId, string slug,
        Species species = Species.Hund, List<AnimalImage>? images = null) => new()
    {
        OrganizationId = orgId,
        ExternalId = externalId,
        Name = "Test",
        Slug = slug,
        Species = species,
        Images = images ?? new()
    };

    [Fact]
    public async Task ExternalId_MustBeUniquePerOrganization()
    {
        var org = await _db.AddOrganizationAsync();
        await using var ctx = _db.NewContext();
        ctx.Animals.Add(NewAnimal(org.Id, "x", "a-1"));
        ctx.Animals.Add(NewAnimal(org.Id, "x", "a-2"));

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task ExternalId_MayRepeatAcrossOrganizations()
    {
        var org1 = await _db.AddOrganizationAsync("A");
        var org2 = await _db.AddOrganizationAsync("B");
        await using var ctx = _db.NewContext();
        ctx.Animals.Add(NewAnimal(org1.Id, "x", "a-1"));
        ctx.Animals.Add(NewAnimal(org2.Id, "x", "a-2"));

        await ctx.SaveChangesAsync();

        Assert.Equal(2, await ctx.Animals.CountAsync());
    }

    [Fact]
    public async Task AnimalSlug_MustBeUnique()
    {
        var org = await _db.AddOrganizationAsync();
        await using var ctx = _db.NewContext();
        ctx.Animals.Add(NewAnimal(org.Id, "1", "gleich"));
        ctx.Animals.Add(NewAnimal(org.Id, "2", "gleich"));

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task OrganizationSlug_MustBeUnique()
    {
        await using var ctx = _db.NewContext();
        ctx.Organizations.Add(new Organization { Name = "A", Slug = "gleich" });
        ctx.Organizations.Add(new Organization { Name = "B", Slug = "gleich" });

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task ApiKeyHash_MustBeUnique()
    {
        await using var ctx = _db.NewContext();
        ctx.Organizations.Add(new Organization { Name = "A", Slug = "a", ApiKeyHash = "hash" });
        ctx.Organizations.Add(new Organization { Name = "B", Slug = "b", ApiKeyHash = "hash" });

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Breed_MustBeUniquePerSpecies()
    {
        await using var ctx = _db.NewContext();
        ctx.Breeds.Add(new Breed { Species = Species.Hund, Name = "Mix" });
        ctx.Breeds.Add(new Breed { Species = Species.Hund, Name = "Mix" });

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Enums_AreStoredAsReadableStrings()
    {
        var org = await _db.AddOrganizationAsync();
        await using (var ctx = _db.NewContext())
        {
            ctx.Animals.Add(NewAnimal(org.Id, "1", "luna-1", Species.Katze));
            await ctx.SaveChangesAsync();
        }

        await using var read = _db.NewContext();
        var values = await read.Database
            .SqlQueryRaw<string>("SELECT \"Species\" AS \"Value\" FROM \"Animals\"")
            .ToListAsync();

        Assert.Equal("Katze", Assert.Single(values));
    }

    [Fact]
    public async Task DeletingOrganization_DeletesItsAnimalsAndImages()
    {
        var org = await _db.AddOrganizationAsync();
        await using (var ctx = _db.NewContext())
        {
            ctx.Animals.Add(NewAnimal(org.Id, "1", "a-1", images:
                [new AnimalImage { Url = "https://x.de/1.jpg" }]));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _db.NewContext())
        {
            ctx.Organizations.Remove(await ctx.Organizations.SingleAsync());
            await ctx.SaveChangesAsync();
        }

        await using var read = _db.NewContext();
        Assert.Equal(0, await read.Animals.CountAsync());
        Assert.Equal(0, await read.AnimalImages.CountAsync());
    }

    [Fact]
    public async Task DeletingBreed_KeepsAnimals_AndClearsReference()
    {
        var org = await _db.AddOrganizationAsync();
        await using (var ctx = _db.NewContext())
        {
            var breed = new Breed { Species = Species.Hund, Name = "Labrador" };
            var animal = NewAnimal(org.Id, "1", "a-1");
            animal.Breed = breed;
            ctx.Animals.Add(animal);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _db.NewContext())
        {
            ctx.Breeds.Remove(await ctx.Breeds.SingleAsync());
            await ctx.SaveChangesAsync();
        }

        await using var read = _db.NewContext();
        var stored = await read.Animals.SingleAsync();
        Assert.Null(stored.BreedId);
    }

    [Fact]
    public async Task DecimalAndDateOnlyValues_RoundTrip()
    {
        var org = await _db.AddOrganizationAsync();
        await using (var ctx = _db.NewContext())
        {
            var a = NewAnimal(org.Id, "1", "a-1");
            a.WeightKg = 12.5m;
            a.AdoptionFee = 250m;
            a.BirthDate = new DateOnly(2022, 3, 15);
            ctx.Animals.Add(a);
            await ctx.SaveChangesAsync();
        }

        await using var read = _db.NewContext();
        var stored = await read.Animals.SingleAsync();
        Assert.Equal(12.5m, stored.WeightKg);
        Assert.Equal(250m, stored.AdoptionFee);
        Assert.Equal(new DateOnly(2022, 3, 15), stored.BirthDate);
    }
}
