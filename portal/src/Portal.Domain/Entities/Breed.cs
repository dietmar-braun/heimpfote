using TierschutzCollect.Domain.Enums;

namespace TierschutzCollect.Domain.Entities;

public class Breed
{
    public int Id { get; set; }
    public Species Species { get; set; }
    public required string Name { get; set; }
}
