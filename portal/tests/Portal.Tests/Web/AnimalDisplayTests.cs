using TierschutzCollect.Domain.Enums;
using TierschutzCollect.Web;

namespace Portal.Tests.Web;

public class AnimalDisplayTests
{
    [Theory]
    [InlineData(null, "Alter unbekannt")]
    [InlineData(0, "0 Monate")]
    [InlineData(1, "1 Monat")]
    [InlineData(5, "5 Monate")]
    [InlineData(11, "11 Monate")]
    [InlineData(12, "1 Jahr")]
    [InlineData(23, "1 Jahr")]
    [InlineData(30, "2 Jahre")]
    public void AgeText_FromApproxMonths(int? months, string expected)
    {
        Assert.Equal(expected, AnimalDisplay.AgeText(null, months));
    }

    [Fact]
    public void AgeText_BirthDateTakesPrecedenceOverApproxAge()
    {
        var birth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-3).AddDays(-10);

        Assert.Equal("3 Jahre", AnimalDisplay.AgeText(birth, 6));
    }

    [Fact]
    public void AgeText_FutureBirthDate_IsNotNegative()
    {
        var birth = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

        Assert.Equal("0 Monate", AnimalDisplay.AgeText(birth, null));
    }

    [Theory]
    [InlineData("https://example.org/a", "https://example.org/a")]
    [InlineData("http://example.org/", "http://example.org/")]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("ftp://example.org/x", null)]
    [InlineData("/relativ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void SafeHttpUrl_OnlyAllowsHttpAndHttps(string? input, string? expected)
    {
        Assert.Equal(expected, AnimalDisplay.SafeHttpUrl(input));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("  ", "")]
    [InlineData("kurz", "kurz")]
    public void Truncate_KeepsShortTexts(string? input, string expected)
    {
        Assert.Equal(expected, AnimalDisplay.Truncate(input, 150));
    }

    [Fact]
    public void Truncate_CutsLongTextsAndAddsEllipsis()
    {
        var result = AnimalDisplay.Truncate(new string('a', 300), 150);

        Assert.Equal(151, result.Length);
        Assert.EndsWith("…", result);
    }

    [Theory]
    [InlineData(Sex.Maennlich, "Männlich")]
    [InlineData(Sex.Weiblich, "Weiblich")]
    [InlineData(Sex.Unbekannt, "Unbekannt")]
    public void SexLabel_IsGerman(Sex sex, string expected)
    {
        Assert.Equal(expected, AnimalDisplay.SexLabel(sex));
    }

    [Theory]
    [InlineData(AnimalSize.Gross, "Groß")]
    [InlineData(AnimalSize.Unbekannt, "Unbekannt")]
    public void SizeLabel_IsGerman(AnimalSize size, string expected)
    {
        Assert.Equal(expected, AnimalDisplay.SizeLabel(size));
    }

    [Fact]
    public void SpeciesEmoji_HasFallbackForEveryValue()
    {
        foreach (var species in Enum.GetValues<Species>())
            Assert.False(string.IsNullOrEmpty(AnimalDisplay.SpeciesEmoji(species)));
    }
}
