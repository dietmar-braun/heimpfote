using System.Text.RegularExpressions;
using TierschutzCollect.Infrastructure.Services;

namespace Portal.Tests.Services;

public class ApiKeysTests
{
    [Fact]
    public void Generate_HasPrefixAndExpectedFormat()
    {
        var key = ApiKeys.Generate();

        Assert.StartsWith("tsc_", key);
        Assert.Equal(4 + 48, key.Length);
        Assert.Matches("^tsc_[0-9a-f]{48}$", key);
    }

    [Fact]
    public void Generate_ReturnsUniqueKeys()
    {
        var keys = Enumerable.Range(0, 100).Select(_ => ApiKeys.Generate()).ToHashSet();

        Assert.Equal(100, keys.Count);
    }

    [Fact]
    public void Hash_IsDeterministic()
    {
        Assert.Equal(ApiKeys.Hash("tsc_abc"), ApiKeys.Hash("tsc_abc"));
    }

    [Fact]
    public void Hash_DiffersForDifferentKeys()
    {
        Assert.NotEqual(ApiKeys.Hash("tsc_a"), ApiKeys.Hash("tsc_b"));
    }

    [Fact]
    public void Hash_IsLowercaseSha256Hex()
    {
        var hash = ApiKeys.Hash("abc");

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
        // Known SHA-256 test vector
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    }

    [Fact]
    public void Hash_DoesNotContainPlainKey()
    {
        var key = ApiKeys.Generate();

        Assert.DoesNotContain(key, ApiKeys.Hash(key));
    }
}
