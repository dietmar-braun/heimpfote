using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TierschutzCollect.Domain.Entities;
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

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

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

app.MapRazorComponents<App>();

app.Run();
