using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Portal.Tests.Helpers;
using TierschutzCollect.Domain.Entities;
using TierschutzCollect.Web;

namespace Portal.Tests.Web;

public class ApiKeyFilterTests : IDisposable
{
    private const string ValidKey = "tsc_testkey";
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private static DefaultHttpContext HttpWithKey(string? key)
    {
        var http = new DefaultHttpContext();
        if (key is not null) http.Request.Headers["X-Api-Key"] = key;
        return http;
    }

    private async Task<(object? Result, bool NextCalled, HttpContext Http)> RunAsync(string? key)
    {
        await using var ctx = _db.NewContext();
        var filter = new ApiKeyFilter(ctx);
        var http = HttpWithKey(key);
        var nextCalled = false;

        var result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(http),
            _ =>
            {
                nextCalled = true;
                return ValueTask.FromResult<object?>("weiter");
            });

        return (result, nextCalled, http);
    }

    [Fact]
    public async Task MissingHeader_ReturnsUnauthorized()
    {
        await _db.AddOrganizationAsync(apiKey: ValidKey);

        var (result, nextCalled, _) = await RunAsync(null);

        Assert.IsType<UnauthorizedHttpResult>(result);
        Assert.False(nextCalled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankHeader_ReturnsUnauthorized(string key)
    {
        await _db.AddOrganizationAsync(apiKey: ValidKey);

        var (result, nextCalled, _) = await RunAsync(key);

        Assert.IsType<UnauthorizedHttpResult>(result);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task UnknownKey_ReturnsUnauthorized()
    {
        await _db.AddOrganizationAsync(apiKey: ValidKey);

        var (result, nextCalled, _) = await RunAsync("tsc_falsch");

        Assert.IsType<UnauthorizedHttpResult>(result);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task KeyIsNotAcceptedInItsHashedForm()
    {
        await _db.AddOrganizationAsync(apiKey: ValidKey);
        string storedHash;
        await using (var ctx = _db.NewContext())
            storedHash = ctx.Organizations.Single().ApiKeyHash!;

        var (result, nextCalled, _) = await RunAsync(storedHash);

        Assert.IsType<UnauthorizedHttpResult>(result);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task ValidKey_CallsNext_AndStoresOrganization()
    {
        var org = await _db.AddOrganizationAsync("Mein Tierheim", ValidKey);

        var (result, nextCalled, http) = await RunAsync(ValidKey);

        Assert.True(nextCalled);
        Assert.Equal("weiter", result);
        var stored = Assert.IsType<Organization>(http.Items[ApiKeyFilter.OrgKey]);
        Assert.Equal(org.Id, stored.Id);
    }

    [Fact]
    public async Task EachOrganizationOnlyGetsItsOwnIdentity()
    {
        await _db.AddOrganizationAsync("A", "tsc_a");
        var orgB = await _db.AddOrganizationAsync("B", "tsc_b");

        var (_, _, http) = await RunAsync("tsc_b");

        Assert.Equal(orgB.Id, ((Organization)http.Items[ApiKeyFilter.OrgKey]!).Id);
    }
}
