using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TierschutzCollect.Domain.Entities;

namespace TierschutzCollect.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Animal> Animals => Set<Animal>();
    public DbSet<Breed> Breeds => Set<Breed>();
    public DbSet<AnimalImage> AnimalImages => Set<AnimalImage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Store all enums as readable strings in the DB (robust against reordering enum values)
        builder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(30);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b); // Identity tables

        b.Entity<Organization>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Slug).HasMaxLength(200);
            e.Property(x => x.Country).HasMaxLength(2);
            e.Property(x => x.ApiKeyHash).HasMaxLength(64);
            e.Property(x => x.DetectedCms).HasMaxLength(50);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.ApiKeyHash).IsUnique();
        });

        b.Entity<Breed>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => new { x.Species, x.Name }).IsUnique();
        });

        b.Entity<Animal>(e =>
        {
            e.Property(x => x.ExternalId).HasMaxLength(200);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Slug).HasMaxLength(250);
            e.Property(x => x.Color).HasMaxLength(100);
            e.Property(x => x.SourceUrl).HasMaxLength(2000);
            e.Property(x => x.ContentHash).HasMaxLength(64);
            e.Property(x => x.WeightKg).HasPrecision(6, 2);
            e.Property(x => x.AdoptionFee).HasPrecision(8, 2);

            e.HasIndex(x => new { x.OrganizationId, x.ExternalId }).IsUnique();
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.Status, x.Species });

            e.HasOne(x => x.Organization).WithMany(o => o.Animals)
                .HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Breed).WithMany()
                .HasForeignKey(x => x.BreedId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Images).WithOne(i => i.Animal)
                .HasForeignKey(i => i.AnimalId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AnimalImage>(e =>
        {
            e.Property(x => x.Url).HasMaxLength(2000);
        });
    }
}
