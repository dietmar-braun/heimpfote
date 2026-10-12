using Microsoft.EntityFrameworkCore;
using TierschutzCollect.Infrastructure;
using TierschutzCollect.Infrastructure.Services;

namespace TierschutzCollect.Web;

/// <summary>Checks the X-Api-Key header and stores the matching organization in HttpContext.Items.</summary>
public class ApiKeyFilter(AppDbContext db) : IEndpointFilter
{
    public const string OrgKey = "organization";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var http = ctx.HttpContext;

        if (!http.Request.Headers.TryGetValue("X-Api-Key", out var key) || string.IsNullOrWhiteSpace(key))
            return Results.Unauthorized();

        var hash = ApiKeys.Hash(key.ToString());
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.ApiKeyHash == hash, http.RequestAborted);
        if (org is null) return Results.Unauthorized();

        http.Items[OrgKey] = org;
        return await next(ctx);
    }
}
