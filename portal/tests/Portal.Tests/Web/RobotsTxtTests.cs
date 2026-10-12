using TierschutzCollect.Web;

namespace Portal.Tests.Web;

public class RobotsTxtTests
{
    [Fact]
    public void AllowsPublicPages_AndBlocksAdminLoginAndApi()
    {
        var lines = RobotsTxt.Build("https://x.de").Split('\n');

        Assert.Contains("User-agent: *", lines);
        Assert.Contains("Allow: /", lines);
        Assert.Contains("Disallow: /admin", lines);
        Assert.Contains("Disallow: /login", lines);
        Assert.Contains("Disallow: /api", lines);
    }

    [Fact]
    public void PointsToTheSitemap()
    {
        Assert.Contains("Sitemap: https://x.de/sitemap.xml", RobotsTxt.Build("https://x.de/").Split('\n'));
    }

    [Fact]
    public void EndsWithNewline()
    {
        Assert.EndsWith("\n", RobotsTxt.Build("https://x.de"));
    }
}
