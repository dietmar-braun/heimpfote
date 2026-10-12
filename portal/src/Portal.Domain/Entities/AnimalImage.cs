namespace TierschutzCollect.Domain.Entities;

public class AnimalImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnimalId { get; set; }
    public Animal Animal { get; set; } = null!;
    public required string Url { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
}
