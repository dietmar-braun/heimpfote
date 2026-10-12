namespace TierschutzCollect.Infrastructure.Import;

public static class ImportValidator
{
    public const int MaxBatchSize = 500;

    /// <summary>Returns errors in the format expected by ValidationProblem (empty = valid).</summary>
    public static Dictionary<string, string[]> Validate(ImportRequest? req)
    {
        var errors = new Dictionary<string, string[]>();

        if (req?.Animals is null || req.Animals.Count == 0)
        {
            errors["animals"] = ["Mindestens ein Tier erforderlich."];
            return errors;
        }
        if (req.Animals.Count > MaxBatchSize)
        {
            errors["animals"] = [$"Maximal {MaxBatchSize} Tiere pro Anfrage."];
            return errors;
        }

        for (var i = 0; i < req.Animals.Count; i++)
        {
            var a = req.Animals[i];
            if (string.IsNullOrWhiteSpace(a.ExternalId) || a.ExternalId.Length > 200)
                errors[$"animals[{i}].externalId"] = ["Pflichtfeld, max. 200 Zeichen."];
            if (string.IsNullOrWhiteSpace(a.Name) || a.Name.Length > 200)
                errors[$"animals[{i}].name"] = ["Pflichtfeld, max. 200 Zeichen."];
            if (a.ApproxAgeMonths is < 0 or > 600)
                errors[$"animals[{i}].approxAgeMonths"] = ["Unplausibler Wert."];

            foreach (var url in a.ImageUrls ?? [])
            {
                if (!IsHttpUrl(url))
                {
                    errors[$"animals[{i}].imageUrls"] = ["Nur http(s)-URLs erlaubt."];
                    break;
                }
            }
            if (a.SourceUrl is not null && !IsHttpUrl(a.SourceUrl))
                errors[$"animals[{i}].sourceUrl"] = ["Nur http(s)-URLs erlaubt."];
        }
        return errors;
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps)
        && value.Length <= 2000;
}
