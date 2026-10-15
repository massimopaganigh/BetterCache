namespace BetterCache;

/// <summary>
/// Preloads the WebAssembly runtime with the exact fingerprinted URLs from the boot manifest, so the
/// downloads start while the page head is parsed instead of after
/// <c>blazor.web.js</c> → <c>dotnet.js</c> → <c>blazor.boot.json</c> have run in sequence.
/// </summary>
internal sealed class WebAssemblyPreloadLinkSource(BootManifestProvider bootManifest, IOptions<BetterCacheOptions> options) : IPreloadLinkSource
{
    private readonly BetterCacheOptions _options = options.Value;
    private PreloadLink[]? _manifestLinks;

    public IEnumerable<PreloadLink> GetLinks(HttpContext context)
    {
        // With Cache Storage on, warm loads read that copy and every preload would be wasted.
        if (!_options.Preload.PreloadWebAssembly || bootManifest.Manifest is not { CacheBootResources: false } manifest)
            return [];

        _manifestLinks ??= CreateLinks(manifest, _options.WebAssembly.FrameworkDirectory, _options.Preload.EffectiveWebAssemblySections);

        // blazor.web.js imports dotnet.js through the import map, which points at its fingerprinted URL.
        var dotnetJs = _options.WebAssembly.FrameworkDirectory + "dotnet.js";
        var assets = context.GetEndpoint()?.Metadata.GetMetadata<ResourceAssetCollection>();

        return [new PreloadLink { Rel = "modulepreload", Href = assets?[dotnetJs] ?? dotnetJs }, .. _manifestLinks];
    }

    internal static PreloadLink[] CreateLinks(BootManifest manifest, string frameworkDirectory, IReadOnlyList<string> sections)
    {
        var links = new List<PreloadLink>();

        foreach (var section in sections)
        {
            if (!manifest.Resources.TryGetValue(section, out var files))
                continue;

            // jsModule* sections are ES modules the runtime import()s; the rest are fetch()ed. Chrome ignores
            // integrity on as=fetch preloads (crbug.com/981419) and still reuses them, so none is emitted there.
            var isModule = section.StartsWith("jsModule", StringComparison.Ordinal);

            foreach (var (name, integrity) in files)
            {
                links.Add(isModule
                    ? new PreloadLink { Rel = "modulepreload", Href = frameworkDirectory + name, Integrity = integrity }
                    : new PreloadLink { As = "fetch", CrossOrigin = "anonymous", Href = frameworkDirectory + name });
            }
        }

        return [.. links];
    }
}
