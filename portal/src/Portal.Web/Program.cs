using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TierschutzCollect.Domain.Entities;
using TierschutzCollect.Domain.Enums;
using TierschutzCollect.Infrastructure;
using TierschutzCollect.Infrastructure.Import;
using TierschutzCollect.Infrastructure.Services;
using TierschutzCollect.Web;
using TierschutzCollect.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        npgsql => npgsql.MigrationsAssembly("Portal.Infrastructure")));

builder.Services.AddScoped<ImportService>();
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

// ---- Login (ASP.NET Core Identity with cookie) ----
builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.AddIdentityCore<ApplicationUser>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 10;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/login";
    o.AccessDeniedPath = "/zugriff-verweigert";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireRole(IdentitySeeder.AdminRole));

// Blazor: static server-side rendering (ready-made HTML, good for SEO)
builder.Services.AddRazorComponents();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // Demo organization incl. API key, which is only logged once
    if (!await db.Organizations.AnyAsync())
    {
        var key = ApiKeys.Generate();
        db.Organizations.Add(new Organization
        {
            Name = "Demo Tierheim",
            Slug = "demo-tierheim",
            City = "Berlin",
            ApiKeyHash = ApiKeys.Hash(key)
        });
        await db.SaveChangesAsync();
        app.Logger.LogWarning("Demo-API-Key (nur jetzt sichtbar): {Key}", key);
    }

    // Demo animals so the site is not empty
    if (!await db.Animals.AnyAsync())
    {
        var org = await db.Organizations.FirstAsync();
        var import = scope.ServiceProvider.GetRequiredService<ImportService>();
        await DevSeed.SeedAnimalsAsync(import, org);
    }
}

// Create the "Admin" role and the first admin user (only if Admin:Email/Admin:Password are configured)
using (var identityScope = app.Services.CreateScope())
{
    await IdentitySeeder.SeedAsync(identityScope.ServiceProvider, app.Configuration, app.Logger);
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// ---- Import API (authenticated via API key, not via cookie) ----
var api = app.MapGroup("/api").AddEndpointFilter<ApiKeyFilter>();

api.MapPost("/import/animals", async (
    ImportRequest req, HttpContext http, ImportService import, CancellationToken ct) =>
{
    var errors = ImportValidator.Validate(req);
    if (errors.Count > 0) return Results.ValidationProblem(errors);

    var org = (Organization)http.Items[ApiKeyFilter.OrgKey]!;
    var result = await import.ImportAsync(org, req, ct);
    return Results.Ok(result);
});

// ---- Sign out (POST + antiforgery token; [FromForm] enables the token check) ----
app.MapPost("/abmelden", async ([FromForm] string? returnUrl, SignInManager<ApplicationUser> signIn) =>
{
    await signIn.SignOutAsync();
    return Results.LocalRedirect("/");
});

// ---- Admin: change the status of an animal (form POST from /admin/tiere) ----
app.MapPost("/admin/tiere/{id:guid}/status", async (
    Guid id, [FromForm] string status, [FromForm] string? returnUrl, AppDbContext db) =>
{
    if (!Enum.TryParse<AnimalStatus>(status, true, out var newStatus) || !Enum.IsDefined(newStatus))
        return Results.BadRequest("Ungültiger Status.");

    await db.Animals.Where(a => a.Id == id).ExecuteUpdateAsync(s => s
        .SetProperty(a => a.Status, newStatus)
        .SetProperty(a => a.UpdatedAt, DateTime.UtcNow));

    return Results.LocalRedirect(LocalUrls.SafePath(returnUrl) ?? "/admin/tiere");
}).RequireAuthorization("Admin");

app.MapRazorComponents<App>();

app.Run();
