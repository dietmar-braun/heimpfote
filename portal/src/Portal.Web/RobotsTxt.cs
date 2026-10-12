namespace TierschutzCollect.Web;

public static class RobotsTxt
{
    /// <summary>Public pages are crawlable; admin, login and API paths are not. Points crawlers to the sitemap.</summary>
    public static string Build(string baseUrl) => string.Join('\n',
        "User-agent: *",
        "Allow: /",
        "Disallow: /admin",
        "Disallow: /login",
        "Disallow: /zugriff-verweigert",
        "Disallow: /api",
        $"Sitemap: {SiteUrls.Combine(baseUrl, "sitemap.xml")}") + "\n";
}
