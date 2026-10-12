using System.Globalization;
using TierschutzCollect.Domain.Enums;

namespace TierschutzCollect.Web;

/// <summary>Pure presentation helpers (texts, emojis, safe links) for the Blazor pages.</summary>
public static class AnimalDisplay
{
    private static readonly CultureInfo De = new("de-DE");

    public static string AgeText(DateOnly? birthDate, int? approxAgeMonths)
    {
        int? months = approxAgeMonths;
        if (birthDate is { } b)
        {
            var t = DateOnly.FromDateTime(DateTime.UtcNow);
            months = Math.Max(0, (t.Year - b.Year) * 12 + t.Month - b.Month - (t.Day < b.Day ? 1 : 0));
        }

        if (months is null) return "Alter unbekannt";
        if (months < 12) return months == 1 ? "1 Monat" : $"{months} Monate";

        var years = months.Value / 12;
        return years == 1 ? "1 Jahr" : $"{years} Jahre";
    }

    public static string SpeciesEmoji(Species s) => s switch
    {
        Species.Hund => "🐕",
        Species.Katze => "🐈",
        Species.Kleintier => "🐇",
        Species.Vogel => "🐦",
        Species.Pferd => "🐴",
        Species.Reptil => "🦎",
        _ => "🐾"
    };

    public static string SexLabel(Sex s) => s switch
    {
        Sex.Maennlich => "Männlich",
        Sex.Weiblich => "Weiblich",
        _ => "Unbekannt"
    };

    public static string SizeLabel(AnimalSize s) => s switch
    {
        AnimalSize.Klein => "Klein",
        AnimalSize.Mittel => "Mittel",
        AnimalSize.Gross => "Groß",
        _ => "Unbekannt"
    };

    public static string YesNo(bool? value) => value switch
    {
        true => "Ja",
        false => "Nein",
        _ => "Unbekannt"
    };

    public static string Money(decimal amount) => amount.ToString("C0", De);

    public static string Truncate(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var t = text.Trim();
        return t.Length <= max ? t : t[..max].TrimEnd() + "…";
    }

    /// <summary>Returns the URL only if it is a valid http(s) link (prevents e.g. javascript: links).</summary>
    public static string? SafeHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps)
            ? u.AbsoluteUri
            : null;
}
