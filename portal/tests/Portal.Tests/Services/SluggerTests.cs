using TierschutzCollect.Infrastructure.Services;

namespace Portal.Tests.Services;

public class SluggerTests
{
    [Theory]
    [InlineData("Bello", "bello")]
    [InlineData("Rex 2", "rex-2")]
    [InlineData("Müller's Bär", "mueller-s-baer")]
    [InlineData("Straße", "strasse")]
    [InlineData("Café Crème", "cafe-creme")]
    [InlineData("  Hallo   Welt  ", "hallo-welt")]
    [InlineData("---a---", "a")]
    [InlineData("A/B\\C", "a-b-c")]
    public void Slugify_ProducesCleanUrlSegments(string input, string expected)
    {
        Assert.Equal(expected, Slugger.Slugify(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("日本")]
    public void Slugify_ReturnsEmptyStringWhenNothingUsable(string input)
    {
        Assert.Equal(string.Empty, Slugger.Slugify(input));
    }

    [Fact]
    public void Slugify_OnlyContainsLowercaseAsciiLettersDigitsAndDashes()
    {
        var slug = Slugger.Slugify("Ünïcödé Nàme #42 – Test!");

        Assert.Matches("^[a-z0-9-]+$", slug);
        Assert.DoesNotContain("--", slug);
    }
}
