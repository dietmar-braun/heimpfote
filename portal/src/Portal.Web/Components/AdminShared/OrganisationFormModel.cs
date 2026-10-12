using System.ComponentModel.DataAnnotations;
using TierschutzCollect.Domain.Entities;
using TierschutzCollect.Domain.Enums;

namespace TierschutzCollect.Web.Components.AdminShared;

/// <summary>Form model for organizations (create and edit).</summary>
public class OrganisationFormModel : IValidatableObject
{
    [Required(ErrorMessage = "Name ist erforderlich."), StringLength(200)]
    public string Name { get; set; } = "";

    public OrganizationType Type { get; set; } = OrganizationType.Tierheim;

    [StringLength(500)]
    public string? WebsiteUrl { get; set; }

    [EmailAddress(ErrorMessage = "Ungültige E-Mail-Adresse."), StringLength(200)]
    public string? Email { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(200)]
    public string? Street { get; set; }

    [StringLength(10)]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(WebsiteUrl) && AnimalDisplay.SafeHttpUrl(WebsiteUrl.Trim()) is null)
            yield return new ValidationResult("Bitte eine gültige http(s)-Adresse angeben.", [nameof(WebsiteUrl)]);
    }

    public static OrganisationFormModel From(Organization o) => new()
    {
        Name = o.Name,
        Type = o.Type,
        WebsiteUrl = o.WebsiteUrl,
        Email = o.Email,
        Phone = o.Phone,
        Street = o.Street,
        PostalCode = o.PostalCode,
        City = o.City
    };

    public void ApplyTo(Organization o)
    {
        o.Name = Name.Trim();
        o.Type = Type;
        o.WebsiteUrl = AnimalDisplay.SafeHttpUrl(WebsiteUrl?.Trim());
        o.Email = Clean(Email);
        o.Phone = Clean(Phone);
        o.Street = Clean(Street);
        o.PostalCode = Clean(PostalCode);
        o.City = Clean(City);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
