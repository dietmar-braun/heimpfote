using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Portal.Tests.Helpers;
using TierschutzCollect.Infrastructure;
using TierschutzCollect.Web;

namespace Portal.Tests.Web;

public class IdentitySeederTests : IDisposable
{
    private const string Email = "admin@example.test";
    private const string Password = "Sehr!Sicher12345";

    private readonly TestDb _db = new();
    private readonly ServiceProvider _provider;

    public IdentitySeederTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_db.Connection));
        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 10;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();
        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
    }

    private static IConfiguration Config(string? email, string? password) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:Email"] = email, ["Admin:Password"] = password })
            .Build();

    private async Task SeedAsync(string? email, string? password)
    {
        using var scope = _provider.CreateScope();
        await IdentitySeeder.SeedAsync(scope.ServiceProvider, Config(email, password), NullLogger.Instance);
    }

    [Fact]
    public async Task WithoutConfiguration_NothingIsCreated()
    {
        await SeedAsync(null, null);

        await using var ctx = _db.NewContext();
        Assert.Equal(0, await ctx.Users.CountAsync());
        Assert.Equal(0, await ctx.Roles.CountAsync());
    }

    [Fact]
    public async Task OnlyEmailConfigured_NothingIsCreated()
    {
        await SeedAsync(Email, null);

        await using var ctx = _db.NewContext();
        Assert.Equal(0, await ctx.Users.CountAsync());
    }

    [Fact]
    public async Task Configuration_CreatesAdminUserWithAdminRole()
    {
        await SeedAsync(Email, Password);

        using var scope = _provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(Email);

        Assert.NotNull(user);
        Assert.True(user!.EmailConfirmed);
        Assert.True(await users.IsInRoleAsync(user, IdentitySeeder.AdminRole));
        Assert.True(await users.CheckPasswordAsync(user, Password));
    }

    [Fact]
    public async Task RunningTwice_DoesNotCreateDuplicates()
    {
        await SeedAsync(Email, Password);
        await SeedAsync(Email, Password);

        await using var ctx = _db.NewContext();
        Assert.Equal(1, await ctx.Users.CountAsync());
        Assert.Equal(1, await ctx.Roles.CountAsync());
        Assert.Equal(1, await ctx.UserRoles.CountAsync());
    }

    [Fact]
    public async Task ExistingUser_KeepsItsPassword()
    {
        await SeedAsync(Email, Password);
        await SeedAsync(Email, "Ganz!AndereEins999");

        using var scope = _provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(Email))!;

        Assert.True(await users.CheckPasswordAsync(user, Password));
        Assert.False(await users.CheckPasswordAsync(user, "Ganz!AndereEins999"));
    }

    [Fact]
    public async Task WeakPassword_CreatesNoUser_AndDoesNotThrow()
    {
        await SeedAsync(Email, "kurz");

        await using var ctx = _db.NewContext();
        Assert.Equal(0, await ctx.Users.CountAsync());
    }
}
