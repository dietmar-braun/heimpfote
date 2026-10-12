using TierschutzCollect.Domain.Enums;
using TierschutzCollect.Infrastructure.Search;
using TierschutzCollect.Web;

namespace Portal.Tests.Web;

public class SearchQueryTests
{
    private static AnimalSearchCriteria Parse(
        string? q = null, string? art = null, string? geschlecht = null, string? groesse = null, string? alter = null,
        string? kastriert = null, string? kinder = null, string? hunde = null, string? katzen = null,
        string? wohnung = null, string? dringend = null, string? sort = null) =>
        SearchQuery.Parse(q, art, geschlecht, groesse, alter, kastriert, kinder, hunde, katzen, wohnung, dringend, sort);

    [Fact]
    public void NoParameters_GiveEmptyCriteria()
    {
        Assert.Equal(new AnimalSearchCriteria(), Parse());
    }

    [Fact]
    public void ValidValues_AreParsed()
    {
        var c = Parse(q: "Bello", art: "Hund", geschlecht: "maennlich", groesse: "gross", alter: "senior",
            kastriert: "1", kinder: "1", hunde: "on", katzen: "true", wohnung: "1", dringend: "1", sort: "name");

        Assert.Equal("Bello", c.Text);
        Assert.Equal(Species.Hund, c.Species);
        Assert.Equal(Sex.Maennlich, c.Sex);
        Assert.Equal(AnimalSize.Gross, c.Size);
        Assert.Equal(AgeGroup.Senior, c.AgeGroup);
        Assert.True(c.NeuteredOnly);
        Assert.True(c.GoodWithChildren);
        Assert.True(c.GoodWithDogs);
        Assert.True(c.GoodWithCats);
        Assert.True(c.SuitableForApartment);
        Assert.True(c.UrgentOnly);
        Assert.Equal(AnimalSort.NameAsc, c.Sort);
    }

    [Fact]
    public void Tokens_AreCaseInsensitive()
    {
        var c = Parse(art: "hUnD", geschlecht: "WEIBLICH", groesse: "Klein", alter: "JUNG", sort: "NEUESTE");

        Assert.Equal(Species.Hund, c.Species);
        Assert.Equal(Sex.Weiblich, c.Sex);
        Assert.Equal(AnimalSize.Klein, c.Size);
        Assert.Equal(AgeGroup.Young, c.AgeGroup);
        Assert.Equal(AnimalSort.Newest, c.Sort);
    }

    [Theory]
    [InlineData("Drache")]
    [InlineData("1")]
    [InlineData("99")]
    [InlineData("")]
    [InlineData("   ")]
    public void InvalidSpecies_IsIgnored(string art)
    {
        Assert.Null(Parse(art: art).Species);
    }

    [Fact]
    public void InvalidTokens_AreIgnored()
    {
        var c = Parse(geschlecht: "divers", groesse: "riesig", alter: "uralt", sort: "zufall");

        Assert.Null(c.Sex);
        Assert.Null(c.Size);
        Assert.Null(c.AgeGroup);
        Assert.Equal(AnimalSort.Recommended, c.Sort);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("no")]
    [InlineData("yes")]
    [InlineData("")]
    [InlineData(null)]
    public void OnlyKnownCheckboxValuesCountAsChecked(string? value)
    {
        Assert.False(Parse(kastriert: value).NeuteredOnly);
    }

    [Fact]
    public void Text_IsTrimmedAndLimited()
    {
        Assert.Equal("Bello", Parse(q: "  Bello  ").Text);
        Assert.Null(Parse(q: "   ").Text);
        Assert.Equal(SearchQuery.MaxTextLength, Parse(q: new string('a', 500)).Text!.Length);
    }

    [Fact]
    public void ToQueryString_IsEmptyForEmptyCriteria()
    {
        Assert.Equal("", SearchQuery.ToQueryString(new AnimalSearchCriteria()));
    }

    [Fact]
    public void ToQueryString_OnlyContainsPageFromSecondPage()
    {
        Assert.Equal("", SearchQuery.ToQueryString(new AnimalSearchCriteria(), 1));
        Assert.Equal("?seite=2", SearchQuery.ToQueryString(new AnimalSearchCriteria(), 2));
    }

    [Fact]
    public void ToQueryString_EscapesText()
    {
        var c = new AnimalSearchCriteria { Text = "Bello & Co" };

        Assert.Equal("?q=Bello%20%26%20Co", SearchQuery.ToQueryString(c));
    }

    [Fact]
    public void ToQueryString_DoesNotContainDefaultSort()
    {
        var c = new AnimalSearchCriteria { Species = Species.Katze };

        Assert.Equal("?art=Katze", SearchQuery.ToQueryString(c));
    }

    [Fact]
    public void ParseAndToQueryString_RoundTrip()
    {
        var original = new AnimalSearchCriteria
        {
            Text = "ruhig & lieb",
            Species = Species.Hund,
            Sex = Sex.Weiblich,
            Size = AnimalSize.Mittel,
            AgeGroup = AgeGroup.Adult,
            NeuteredOnly = true,
            GoodWithChildren = true,
            GoodWithDogs = true,
            GoodWithCats = true,
            SuitableForApartment = true,
            UrgentOnly = true,
            Sort = AnimalSort.Newest
        };

        var query = SearchQuery.ToQueryString(original, 3);
        var values = System.Web.HttpUtility.ParseQueryString(query.TrimStart('?'));
        var parsed = Parse(values["q"], values["art"], values["geschlecht"], values["groesse"], values["alter"],
            values["kastriert"], values["kinder"], values["hunde"], values["katzen"], values["wohnung"],
            values["dringend"], values["sort"]);

        Assert.Equal(original, parsed);
        Assert.Equal("3", values["seite"]);
    }

    [Fact]
    public void HasFiltersBeyondSpecies_IgnoresSpecies()
    {
        Assert.False(SearchQuery.HasFiltersBeyondSpecies(new AnimalSearchCriteria()));
        Assert.False(SearchQuery.HasFiltersBeyondSpecies(new AnimalSearchCriteria { Species = Species.Hund }));
    }

    [Theory]
    [InlineData("text")]
    [InlineData("sex")]
    [InlineData("size")]
    [InlineData("age")]
    [InlineData("neutered")]
    [InlineData("urgent")]
    [InlineData("sort")]
    public void HasFiltersBeyondSpecies_DetectsOtherFilters(string which)
    {
        var c = which switch
        {
            "text" => new AnimalSearchCriteria { Text = "x" },
            "sex" => new AnimalSearchCriteria { Sex = Sex.Maennlich },
            "size" => new AnimalSearchCriteria { Size = AnimalSize.Klein },
            "age" => new AnimalSearchCriteria { AgeGroup = AgeGroup.Young },
            "neutered" => new AnimalSearchCriteria { NeuteredOnly = true },
            "urgent" => new AnimalSearchCriteria { UrgentOnly = true },
            _ => new AnimalSearchCriteria { Sort = AnimalSort.NameAsc }
        };

        Assert.True(SearchQuery.HasFiltersBeyondSpecies(c));
    }

    [Fact]
    public void OptionTokens_AllParseBackToTheirValue()
    {
        foreach (var (token, _) in SearchQuery.SexOptions) Assert.NotNull(Parse(geschlecht: token).Sex);
        foreach (var (token, _) in SearchQuery.SizeOptions) Assert.NotNull(Parse(groesse: token).Size);
        foreach (var (token, _) in SearchQuery.AgeOptions) Assert.NotNull(Parse(alter: token).AgeGroup);
        foreach (var (token, _) in SearchQuery.SortOptions.Where(o => o.Token != ""))
            Assert.NotEqual(AnimalSort.Recommended, Parse(sort: token).Sort);
    }
}
