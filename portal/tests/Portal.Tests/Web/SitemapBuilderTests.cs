using System.Xml.Linq;
using TierschutzCollect.Web;

namespace Portal.Tests.Web;

public class SitemapBuilderTests
{
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private static List<XElement> Urls(string xml) => XDocument.Parse(xml).Root!.Elements(Ns + "url").ToList();

    private static string Loc(XElement url) => url.Element(Ns + "loc")!.Value;

    [Fact]
    public void ContainsHomeAndListPage_EvenWithoutAnimals()
    {
        var xml = SitemapBuilder.Build("https://x.de", []);

        var locations = Urls(xml).Select(Loc).ToList();
        Assert.Equal(["https://x.de/", "https://x.de/tiere"], locations);
    }

    [Fact]
    public void ContainsOneUrlPerAnimal_WithLastModifiedDate()
    {
        var xml = SitemapBuilder.Build("https://x.de",
            [("bello-1234abcd", new DateTime(2026, 3, 7, 15, 30, 0, DateTimeKind.Utc))]);

        var animal = Urls(xml).Single(u => Loc(u).Contains("bello"));
        Assert.Equal("https://x.de/tiere/bello-1234abcd", Loc(animal));
        Assert.Equal("2026-03-07", animal.Element(Ns + "lastmod")!.Value);
    }

    [Fact]
    public void StaticPages_HaveNoLastModified()
    {
        var xml = SitemapBuilder.Build("https://x.de", []);

        Assert.All(Urls(xml), u => Assert.Null(u.Element(Ns + "lastmod")));
    }

    [Fact]
    public void Slugs_AreUrlEscaped()
    {
        var xml = SitemapBuilder.Build("https://x.de", [("a b&c", DateTime.UtcNow)]);

        Assert.Contains(Urls(xml), u => Loc(u) == "https://x.de/tiere/a%20b%26c");
    }

    [Fact]
    public void IsWellFormedXml_WithUtf8Declaration_AndSitemapNamespace()
    {
        var xml = SitemapBuilder.Build("https://x.de", [("a", DateTime.UtcNow)]);

        var document = XDocument.Parse(xml);
        Assert.Equal("urlset", document.Root!.Name.LocalName);
        Assert.Equal(Ns, document.Root.Name.Namespace);
        Assert.StartsWith("<?xml", xml.TrimStart('\uFEFF'));
        Assert.Contains("utf-8", xml[..60].ToLowerInvariant());
    }

    [Fact]
    public void NeverExceedsTheProtocolLimit()
    {
        var animals = Enumerable.Range(0, SitemapBuilder.MaxUrls + 10)
            .Select(i => ($"tier-{i}", DateTime.UtcNow));

        var xml = SitemapBuilder.Build("https://x.de", animals);

        Assert.Equal(SitemapBuilder.MaxUrls, Urls(xml).Count);
    }
}
