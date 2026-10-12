using TierschutzCollect.Domain.Entities;
using TierschutzCollect.Domain.Enums;

namespace Portal.Tests.Domain;

public class EntityDefaultsTests
{
    [Fact]
    public void Organization_HasSensibleDefaults()
    {
        var org = new Organization { Name = "Tierheim", Slug = "tierheim" };

        Assert.NotEqual(Guid.Empty, org.Id);
        Assert.Equal("DE", org.Country);
        Assert.Equal(OrganizationType.Tierheim, org.Type);
        Assert.False(org.IsVerified);
        Assert.Empty(org.Animals);
        Assert.True((DateTime.UtcNow - org.CreatedAt).TotalSeconds < 5);
    }

    [Fact]
    public void Animal_HasSensibleDefaults()
    {
        var animal = new Animal { ExternalId = "1", Name = "Bello", Slug = "bello" };

        Assert.NotEqual(Guid.Empty, animal.Id);
        Assert.Equal(AnimalStatus.Vermittelbar, animal.Status);
        Assert.False(animal.IsUrgent);
        Assert.Empty(animal.Images);
    }

    [Fact]
    public void Animal_CompatibilityFlags_DefaultToUnknown()
    {
        var animal = new Animal { ExternalId = "1", Name = "Bello", Slug = "bello" };

        Assert.Equal(Compatibility.Unbekannt, animal.GoodWithChildren);
        Assert.Equal(Compatibility.Unbekannt, animal.GoodWithDogs);
        Assert.Equal(Compatibility.Unbekannt, animal.GoodWithCats);
        Assert.Equal(Compatibility.Unbekannt, animal.SuitableForApartment);
    }

    [Fact]
    public void NewEntities_GetDifferentIds()
    {
        var a = new Animal { ExternalId = "1", Name = "A", Slug = "a" };
        var b = new Animal { ExternalId = "2", Name = "B", Slug = "b" };

        Assert.NotEqual(a.Id, b.Id);
    }
}
