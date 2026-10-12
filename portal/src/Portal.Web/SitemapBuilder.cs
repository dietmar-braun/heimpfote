using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TierschutzCollect.Web;

/// <summary>Builds sitemap.xml (https://www.sitemaps.org/protocol.html).</summary>
public static class SitemapBuilder
{
    /// <summary>Protocol limit per sitemap file. Beyond this a sitemap index would be needed.</summary>
    public const int MaxUrls = 50_000;

    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    public static string Build(string baseUrl, IEnumerable<(string Slug, DateTime UpdatedAtUtc)> animals)
    {
        var urls = new List<XElement>
        {
            Url(SiteUrls.Combine(baseUrl, ""), null),
            Url(SiteUrls.Combine(baseUrl, "tiere"), null)
        };

        foreach (var (slug, updatedAtUtc) in animals.Take(MaxUrls - urls.Count))
            urls.Add(Url(SiteUrls.Combine(baseUrl, $"tiere/{Uri.EscapeDataString(slug)}"), updatedAtUtc));

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(Ns + "urlset", urls));

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
            document.Save(writer);

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static XElement Url(string location, DateTime? lastModifiedUtc)
    {
        var element = new XElement(Ns + "url", new XElement(Ns + "loc", location));
        if (lastModifiedUtc is { } date)
            element.Add(new XElement(Ns + "lastmod", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        return element;
    }
}
