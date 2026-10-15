using System.Text.Json;

namespace BetterCache;

/// <summary>The parts of the Blazor WebAssembly boot manifest (<c>blazor.boot.json</c>) BetterCache uses.</summary>
/// <param name="CacheBootResources">Whether the runtime keeps boot resources in the Cache Storage API.</param>
/// <param name="Resources">Every <c>resources</c> section made of <c>file name → integrity hash</c> pairs.</param>
internal sealed record BootManifest(bool CacheBootResources, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Resources)
{
    public static BootManifest Parse(Stream json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var sections = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);

        if (root.TryGetProperty("resources", out var resources) && resources.ValueKind == JsonValueKind.Object)
        {
            foreach (var section in resources.EnumerateObject())
            {
                if (section.Value.ValueKind != JsonValueKind.Object)
                    continue;

                sections[section.Name] = section.Value.EnumerateObject()
                    .Where(entry => entry.Value.ValueKind == JsonValueKind.String)
                    .ToDictionary(entry => entry.Name, entry => entry.Value.GetString()!, StringComparer.Ordinal);
            }
        }

        // The SDK omits the key when BlazorCacheBootResources is false, and the runtime treats missing as off.
        var cacheBootResources = root.TryGetProperty("cacheBootResources", out var cache) && cache.ValueKind == JsonValueKind.True;

        return new(cacheBootResources, sections);
    }
}
