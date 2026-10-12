namespace TierschutzCollect.Web;

public static class SiteUrls
{
    /// <summary>
    /// The public base URL without trailing slash. A valid, configured http(s) URL (setting "Site:BaseUrl")
    /// wins; otherwise the base URI of the current request is used (fine locally, but wrong behind some proxies).
    /// </summary>
    public static string Base(string? configured, string requestBase)
    {
        var value = IsHttpUrl(configured) ? configured! : requestBase;
        return value.Trim().TrimEnd('/');
    }

    /// <summary>Joins base URL and path with exactly one slash. An empty path yields the home page URL.</summary>
    public static string Combine(string baseUrl, string path)
    {
        var trimmedBase = baseUrl.TrimEnd('/');
        return path.Trim('/').Length == 0 ? trimmedBase + "/" : $"{trimmedBase}/{path.TrimStart('/')}";
    }

    private static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
