using TierschutzCollect.Web;

namespace Portal.Tests.Web;

public class LocalUrlsTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/admin")]
    [InlineData("/admin/tiere?status=Archiviert&seite=2")]
    [InlineData("/tiere/bello-1234abcd")]
    public void LocalPaths_AreAccepted(string url)
    {
        Assert.Equal(url, LocalUrls.SafePath(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("https://evil.example")]
    [InlineData("http://evil.example/admin")]
    [InlineData("javascript:alert(1)")]
    [InlineData("admin")]
    [InlineData("/admin\r\nSet-Cookie: x=1")]
    public void ExternalOrMalformedUrls_AreRejected(string? url)
    {
        Assert.Null(LocalUrls.SafePath(url));
    }
}
