using Microsoft.AspNetCore.Identity;
using TierschutzCollect.Infrastructure;

namespace TierschutzCollect.Web;

/// <summary>
/// Creates the "Admin" role on startup and – if configured – the first admin user.
/// Configuration: Admin:Email and Admin:Password (Development: appsettings.Development.json,
/// production: environment variables or user secrets, never in the repository).
/// An existing user is left untouched (the password is not overwritten).
/// </summary>
public static class IdentitySeeder
{
    public const string AdminRole = "Admin";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var email = config["Admin:Email"];
        var password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roles.RoleExistsAsync(AdminRole))
            await roles.CreateAsync(new IdentityRole(AdminRole));

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                logger.LogError("Admin-Benutzer konnte nicht angelegt werden: {Errors}",
                    string.Join("; ", created.Errors.Select(e => e.Description)));
                return;
            }
            logger.LogInformation("Admin-Benutzer {Email} angelegt.", email);
        }

        if (!await users.IsInRoleAsync(user, AdminRole))
            await users.AddToRoleAsync(user, AdminRole);
    }
}
