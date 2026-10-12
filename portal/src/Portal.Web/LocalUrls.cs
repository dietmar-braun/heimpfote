namespace TierschutzCollect.Web;

public static class LocalUrls
{
    /// <summary>
    /// Returns the path only if it is a local address of this site (protects against open redirects).
    /// Allowed: "/admin", "/tiere?art=Hund". Rejected: "//evil.com", "/\evil.com", "http://…", "javascript:…".
    /// </summary>
    public static string? SafePath(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (url[0] != '/') return null;
        if (url.Length > 1 && (url[1] == '/' || url[1] == '\\')) return null;
        if (url.Contains('\r') || url.Contains('\n')) return null;
        return url;
    }
}
