using System.Security.Cryptography;
using System.Text;

namespace TierschutzCollect.Infrastructure.Services;

public static class ApiKeys
{
    /// <summary>Generates a new plain-text key. Show it only once; only the hash is stored in the database.</summary>
    public static string Generate() =>
        "tsc_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    public static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
}
