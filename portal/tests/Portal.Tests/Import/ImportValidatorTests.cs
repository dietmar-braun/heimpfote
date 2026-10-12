using TierschutzCollect.Domain.Enums;
using TierschutzCollect.Infrastructure.Import;

namespace Portal.Tests.Import;

public class ImportValidatorTests
{
    private static ImportRequest Req(params AnimalImportDto[] animals) =>
        new(DataSource.Crawler, true, animals.ToList());

    private static AnimalImportDto Valid(string id = "1", string name = "Bello") =>
        new(id, name, Species.Hund);

    [Fact]
    public void NullRequest_IsInvalid()
    {
        var errors = ImportValidator.Validate(null);

        Assert.True(errors.ContainsKey("animals"));
    }

    [Fact]
    public void EmptyAnimalList_IsInvalid()
    {
        var errors = ImportValidator.Validate(Req());

        Assert.True(errors.ContainsKey("animals"));
    }

    [Fact]
    public void ExactlyMaxBatchSize_IsValid()
    {
        var animals = Enumerable.Range(0, ImportValidator.MaxBatchSize)
            .Select(i => Valid($"id{i}")).ToArray();

        Assert.Empty(ImportValidator.Validate(Req(animals)));
    }

    [Fact]
    public void MoreThanMaxBatchSize_IsInvalid()
    {
        var animals = Enumerable.Range(0, ImportValidator.MaxBatchSize + 1)
            .Select(i => Valid($"id{i}")).ToArray();

        Assert.True(ImportValidator.Validate(Req(animals)).ContainsKey("animals"));
    }

    [Fact]
    public void ValidAnimal_HasNoErrors()
    {
        Assert.Empty(ImportValidator.Validate(Req(Valid())));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingExternalId_IsInvalid(string externalId)
    {
        var errors = ImportValidator.Validate(Req(Valid(externalId)));

        Assert.True(errors.ContainsKey("animals[0].externalId"));
    }

    [Fact]
    public void TooLongExternalId_IsInvalid()
    {
        var errors = ImportValidator.Validate(Req(Valid(new string('x', 201))));

        Assert.True(errors.ContainsKey("animals[0].externalId"));
    }

    [Fact]
    public void ExternalIdWith200Chars_IsValid()
    {
        Assert.Empty(ImportValidator.Validate(Req(Valid(new string('x', 200)))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void MissingName_IsInvalid(string name)
    {
        var errors = ImportValidator.Validate(Req(Valid("1", name)));

        Assert.True(errors.ContainsKey("animals[0].name"));
    }

    [Fact]
    public void TooLongName_IsInvalid()
    {
        var errors = ImportValidator.Validate(Req(Valid("1", new string('n', 201))));

        Assert.True(errors.ContainsKey("animals[0].name"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(601)]
    public void ImplausibleAge_IsInvalid(int months)
    {
        var dto = Valid() with { ApproxAgeMonths = months };

        Assert.True(ImportValidator.Validate(Req(dto)).ContainsKey("animals[0].approxAgeMonths"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(600)]
    public void BoundaryAges_AreValid(int months)
    {
        var dto = Valid() with { ApproxAgeMonths = months };

        Assert.Empty(ImportValidator.Validate(Req(dto)));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.org/bild.jpg")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("/relativ/bild.jpg")]
    [InlineData("kein url")]
    public void NonHttpImageUrls_AreRejected(string url)
    {
        var dto = Valid() with { ImageUrls = [url] };

        Assert.True(ImportValidator.Validate(Req(dto)).ContainsKey("animals[0].imageUrls"));
    }

    [Theory]
    [InlineData("https://example.org/bild.jpg")]
    [InlineData("http://example.org/bild.jpg")]
    public void HttpAndHttpsImageUrls_AreAccepted(string url)
    {
        var dto = Valid() with { ImageUrls = [url] };

        Assert.Empty(ImportValidator.Validate(Req(dto)));
    }

    [Fact]
    public void TooLongImageUrl_IsRejected()
    {
        var dto = Valid() with { ImageUrls = ["https://example.org/" + new string('a', 2000)] };

        Assert.True(ImportValidator.Validate(Req(dto)).ContainsKey("animals[0].imageUrls"));
    }

    [Fact]
    public void InvalidSourceUrl_IsRejected()
    {
        var dto = Valid() with { SourceUrl = "javascript:void(0)" };

        Assert.True(ImportValidator.Validate(Req(dto)).ContainsKey("animals[0].sourceUrl"));
    }

    [Fact]
    public void ErrorKeysContainTheIndexOfTheOffendingAnimal()
    {
        var errors = ImportValidator.Validate(Req(Valid("1"), Valid("2", ""), Valid("3")));

        Assert.Single(errors);
        Assert.True(errors.ContainsKey("animals[1].name"));
    }

    [Fact]
    public void MultipleProblemsOnOneAnimal_AreAllReported()
    {
        var dto = Valid("", "") with { ApproxAgeMonths = -5 };

        var errors = ImportValidator.Validate(Req(dto));

        Assert.True(errors.ContainsKey("animals[0].externalId"));
        Assert.True(errors.ContainsKey("animals[0].name"));
        Assert.True(errors.ContainsKey("animals[0].approxAgeMonths"));
    }
}
