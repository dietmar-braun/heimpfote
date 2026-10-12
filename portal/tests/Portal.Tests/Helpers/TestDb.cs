using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TierschutzCollect.Domain.Entities;
using TierschutzCollect.Infrastructure;
using TierschutzCollect.Infrastructure.Services;

namespace Portal.Tests.Helpers;

/// <summary>
/// Fresh SQLite in-memory database per test. The connection stays open as long as the object lives.
/// NewContext() returns a new DbContext on the same database – so arrange/act/assert
/// work with separate change trackers, as in the real application.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var ctx = NewContext();
        ctx.Database.EnsureCreated();
    }

    /// <summary>The open SQLite connection (for tests that build their own service provider).</summary>
    public SqliteConnection Connection => _connection;

    public AppDbContext NewContext() => new(_options);

    public async Task<Organization> AddOrganizationAsync(string name = "Test Tierheim", string? apiKey = null)
    {
        var org = new Organization
        {
            Name = name,
            Slug = $"{Slugger.Slugify(name)}-{Guid.NewGuid().ToString("N")[..6]}",
            ApiKeyHash = ApiKeys.Hash(apiKey ?? Guid.NewGuid().ToString())
        };
        await using var ctx = NewContext();
        ctx.Organizations.Add(org);
        await ctx.SaveChangesAsync();
        return org;
    }

    public void Dispose() => _connection.Dispose();
}
