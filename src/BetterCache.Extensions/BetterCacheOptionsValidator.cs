namespace BetterCache;

/// <summary>Rejects invalid configuration at startup instead of failing on the first request.</summary>
internal sealed class BetterCacheOptionsValidator : IValidateOptions<BetterCacheOptions>
{
    public ValidateOptionsResult Validate(string? name, BetterCacheOptions options)
    {
        var failures = new List<string>();

        for (var i = 0; i < options.CacheControl.Rules.Count; i++)
        {
            var rule = options.CacheControl.Rules[i];

            if (string.IsNullOrWhiteSpace(rule.Value) || string.IsNullOrEmpty(rule.PathPattern))
                failures.Add($"CacheControl.Rules[{i}] needs a PathPattern and a Value.");

            try
            {
                CacheControlRule.CreateRegex(rule.PathPattern);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                failures.Add($"CacheControl.Rules[{i}].PathPattern '{rule.PathPattern}' is invalid: {exception.Message}");
            }
        }

        for (var i = 0; i < options.PrecompressedFiles.Encodings.Count; i++)
        {
            var encoding = options.PrecompressedFiles.Encodings[i];

            if (string.IsNullOrWhiteSpace(encoding.Name) || !encoding.FileExtension.StartsWith('.'))
                failures.Add($"PrecompressedFiles.Encodings[{i}] needs a Name and a FileExtension starting with '.'.");
        }

        for (var i = 0; i < options.Preload.Links.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(options.Preload.Links[i].Href) || string.IsNullOrWhiteSpace(options.Preload.Links[i].Rel))
                failures.Add($"Preload.Links[{i}] needs an Href and a Rel.");
        }

        if (!options.WebAssembly.BootManifestPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            failures.Add("WebAssembly.BootManifestPath must point to the boot manifest JSON file.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
