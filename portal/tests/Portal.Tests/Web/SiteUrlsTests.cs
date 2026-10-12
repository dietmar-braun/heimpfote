using TierschutzCollect.Web;

namespace Portal.Tests.Web;

public class SiteUrlsTests
{
    [Theory]
    [InlineData("https://tierschutz.example", "http://localhost:5080/", "https://tierschutz.example")]
    [InlineData("https://tierschutz.example/", "http://localhost:5080/", "https://tierschutz.example")]
    [InlineData("", "http://localhost:5080/", "http://localhost:5080")]
    [InlineData(null, "http://localhost:5080/", "http://localhost:5080")]
    [InlineData("   ", "http://localhost:5080/", "http://localhost:5080")]
    public void Base_PrefersConfiguredUrl_AndTrimsTrailingSlash(string? configured, string requestBase, string expected)
    {
        Assert.Equal(expected, SiteUrls.Base(configured, requestBase));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.org")]
    [InlineData("tierschutz.example")]
    public void Base_IgnoresConfiguredValuesThatAreNotHttpUrls(string configured)
    {
        Assert.Equal("http://localhost:5080", SiteUrls.Base(configured, "http://localhost:5080/"));
    }

    [Theory]
    [InlineData("https://x.de", "tiere", "https://x.de/tiere")]
    [InlineData("https://x.de/", "/tiere", "https://x.de/tiere")]
    [InlineData("https://x.de", "tiere/bello-1234", "https://x.de/tiere/bello-1234")]
    [InlineData("https://x.de", "", "https://x.de/")]
    [InlineData("https://x.de/", "/", "https://x.de/")]
    public void Combine_UsesExactlyOneSlash(string baseUrl, string path, string expected)
    {
        Assert.Equal(expected, SiteUrls.Combine(baseUrl, path));
    }
}
