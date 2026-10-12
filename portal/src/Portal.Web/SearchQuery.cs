using TierschutzCollect.Domain.Enums;
using TierschutzCollect.Infrastructure.Search;

namespace TierschutzCollect.Web;

/// <summary>
/// Translates between URL query parameters and <see cref="AnimalSearchCriteria"/>.
/// Parameter names and tokens are German on purpose (they are part of the public URLs).
/// Invalid values are ignored instead of causing errors.
/// </summary>
public static class SearchQuery
{
    public const int MaxTextLength = 100;

    private static readonly Dictionary<string, Sex> Sexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["maennlich"] = Sex.Maennlich,
        ["weiblich"] = Sex.Weiblich
    };

    private static readonly Dictionary<string, AnimalSize> Sizes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["klein"] = AnimalSize.Klein,
        ["mittel"] = AnimalSize.Mittel,
        ["gross"] = AnimalSize.Gross
    };

    private static readonly Dictionary<string, AgeGroup> Ages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jung"] = AgeGroup.Young,
        ["erwachsen"] = AgeGroup.Adult,
        ["senior"] = AgeGroup.Senior
    };

    private static readonly Dictionary<string, AnimalSort> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["neueste"] = AnimalSort.Newest,
        ["name"] = AnimalSort.NameAsc
    };

    // Options for the filter form: (URL token, label shown to the user)
    public static readonly IReadOnlyList<(string Token, string Label)> SexOptions =
        [("maennlich", "Männlich"), ("weiblich", "Weiblich")];

    public static readonly IReadOnlyList<(string Token, string Label)> SizeOptions =
        [("klein", "Klein"), ("mittel", "Mittel"), ("gross", "Groß")];

    public static readonly IReadOnlyList<(string Token, string Label)> AgeOptions =
        [("jung", "Jung (bis 1 Jahr)"), ("erwachsen", "Erwachsen (1–7 Jahre)"), ("senior", "Senior (ab 8 Jahren)")];

    public static readonly IReadOnlyList<(string Token, string Label)> SortOptions =
        [("", "Empfohlen"), ("neueste", "Neueste zuerst"), ("name", "Name A–Z")];

    public static AnimalSearchCriteria Parse(
        string? q, string? art, string? geschlecht, string? groesse, string? alter,
        string? kastriert, string? kinder, string? hunde, string? katzen, string? wohnung,
        string? dringend, string? sort)
    {
        var text = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        if (text is { Length: > MaxTextLength }) text = text[..MaxTextLength];

        return new AnimalSearchCriteria
        {
            Text = text,
            Species = ParseSpecies(art),
            Sex = Lookup(Sexes, geschlecht),
            Size = Lookup(Sizes, groesse),
            AgeGroup = Lookup(Ages, alter),
            NeuteredOnly = IsChecked(kastriert),
            GoodWithChildren = IsChecked(kinder),
            GoodWithDogs = IsChecked(hunde),
            GoodWithCats = IsChecked(katzen),
            SuitableForApartment = IsChecked(wohnung),
            UrgentOnly = IsChecked(dringend),
            Sort = Lookup(Sorts, sort) ?? AnimalSort.Recommended
        };
    }

    /// <summary>Builds "?a=b&amp;c=d" (or an empty string) for the given criteria and page.</summary>
    public static string ToQueryString(AnimalSearchCriteria c, int page = 1)
    {
        var parts = new List<string>();

        void Add(string name, string? value)
        {
            if (!string.IsNullOrEmpty(value)) parts.Add($"{name}={Uri.EscapeDataString(value)}");
        }

        Add("q", c.Text);
        Add("art", c.Species?.ToString());
        Add("geschlecht", SexToken(c.Sex));
        Add("groesse", SizeToken(c.Size));
        Add("alter", AgeToken(c.AgeGroup));
        if (c.NeuteredOnly) parts.Add("kastriert=1");
        if (c.GoodWithChildren) parts.Add("kinder=1");
        if (c.GoodWithDogs) parts.Add("hunde=1");
        if (c.GoodWithCats) parts.Add("katzen=1");
        if (c.SuitableForApartment) parts.Add("wohnung=1");
        if (c.UrgentOnly) parts.Add("dringend=1");
        Add("sort", SortToken(c.Sort));
        if (page > 1) parts.Add($"seite={page}");

        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }

    /// <summary>
    /// True if anything other than the species is set. Such result pages are not meant to be indexed
    /// by search engines (too many near-duplicate combinations).
    /// </summary>
    public static bool HasFiltersBeyondSpecies(AnimalSearchCriteria c) =>
        c.Text is not null || c.Sex is not null || c.Size is not null || c.AgeGroup is not null ||
        c.NeuteredOnly || c.GoodWithChildren || c.GoodWithDogs || c.GoodWithCats ||
        c.SuitableForApartment || c.UrgentOnly || c.Sort != AnimalSort.Recommended;

    public static string? SexToken(Sex? value) => Token(Sexes, value);
    public static string? SizeToken(AnimalSize? value) => Token(Sizes, value);
    public static string? AgeToken(AgeGroup? value) => Token(Ages, value);
    public static string? SortToken(AnimalSort value) => value == AnimalSort.Recommended ? null : Token(Sorts, value);

    private static Species? ParseSpecies(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();

        // Reject numeric input: Enum.TryParse would happily turn "1" into a species
        if (int.TryParse(trimmed, out _)) return null;

        return Enum.TryParse<Species>(trimmed, true, out var species) && Enum.IsDefined(species) ? species : null;
    }

    private static T? Lookup<T>(Dictionary<string, T> map, string? value) where T : struct =>
        value is not null && map.TryGetValue(value.Trim(), out var result) ? result : null;

    private static string? Token<T>(Dictionary<string, T> map, T? value) where T : struct =>
        value is null ? null : map.FirstOrDefault(kv => EqualityComparer<T>.Default.Equals(kv.Value, value.Value)).Key;

    private static bool IsChecked(string? value) =>
        value is not null &&
        (value.Equals("1", StringComparison.Ordinal) ||
         value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
         value.Equals("true", StringComparison.OrdinalIgnoreCase));
}
