using System.ComponentModel.DataAnnotations;
using TierschutzCollect.Domain.Entities;
using TierschutzCollect.Domain.Enums;
using TierschutzCollect.Web.Components.AdminShared;

namespace Portal.Tests.Web;

public class OrganisationFormModelTests
{
    private static List<ValidationResult> Validate(OrganisationFormModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void ValidModel_HasNoErrors()
    {
        var model = new OrganisationFormModel { Name = "Tierheim Mitte", Email = "info@example.org", WebsiteUrl = "https://example.org" };

        Assert.Empty(Validate(model));
    }

    [Fact]
    public void MissingName_IsInvalid()
    {
        var results = Validate(new OrganisationFormModel { Name = "" });

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(OrganisationFormModel.Name)));
    }

    [Fact]
    public void InvalidEmail_IsInvalid()
    {
        var results = Validate(new OrganisationFormModel { Name = "A", Email = "keine-mail" });

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(OrganisationFormModel.Email)));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.org")]
    [InlineData("kein link")]
    public void NonHttpWebsite_IsInvalid(string url)
    {
        var results = Validate(new OrganisationFormModel { Name = "A", WebsiteUrl = url });

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(OrganisationFormModel.WebsiteUrl)));
    }

    [Fact]
    public void EmptyWebsite_IsAllowed()
    {
        Assert.Empty(Validate(new OrganisationFormModel { Name = "A", WebsiteUrl = "  " }));
    }

    [Fact]
    public void ApplyTo_TrimsValues_AndTurnsBlankIntoNull()
    {
        var org = new Organization { Name = "alt", Slug = "alt" };
        var model = new OrganisationFormModel
        {
            Name = "  Tierheim Nord  ",
            Type = OrganizationType.Verein,
            Phone = "   ",
            City = " Hamburg ",
            WebsiteUrl = "https://example.org"
        };

        model.ApplyTo(org);

        Assert.Equal("Tierheim Nord", org.Name);
        Assert.Equal(OrganizationType.Verein, org.Type);
        Assert.Null(org.Phone);
        Assert.Equal("Hamburg", org.City);
        Assert.Equal("https://example.org/", org.WebsiteUrl);
    }

    [Fact]
    public void ApplyTo_DoesNotChangeTheSlug()
    {
        var org = new Organization { Name = "alt", Slug = "feste-adresse" };

        new OrganisationFormModel { Name = "Ganz neuer Name" }.ApplyTo(org);

        Assert.Equal("feste-adresse", org.Slug);
    }

    [Fact]
    public void ApplyTo_DropsUnsafeWebsite()
    {
        var org = new Organization { Name = "A", Slug = "a", WebsiteUrl = "https://alt.example/" };

        new OrganisationFormModel { Name = "A", WebsiteUrl = "javascript:alert(1)" }.ApplyTo(org);

        Assert.Null(org.WebsiteUrl);
    }

    [Fact]
    public void From_ThenApplyTo_RoundTrips()
    {
        var org = new Organization
        {
            Name = "Tierheim Süd", Slug = "sued", Type = OrganizationType.Pflegestelle,
            Email = "a@b.de", Phone = "123", Street = "Weg 1", PostalCode = "80331", City = "München",
            WebsiteUrl = "https://example.org/"
        };

        var copy = new Organization { Name = "x", Slug = "y" };
        OrganisationFormModel.From(org).ApplyTo(copy);

        Assert.Equal(org.Name, copy.Name);
        Assert.Equal(org.Type, copy.Type);
        Assert.Equal(org.Email, copy.Email);
        Assert.Equal(org.Phone, copy.Phone);
        Assert.Equal(org.Street, copy.Street);
        Assert.Equal(org.PostalCode, copy.PostalCode);
        Assert.Equal(org.City, copy.City);
        Assert.Equal(org.WebsiteUrl, copy.WebsiteUrl);
    }
}
