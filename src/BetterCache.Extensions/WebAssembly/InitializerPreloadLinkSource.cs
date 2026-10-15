namespace BetterCache;

/// <summary>
/// Preloads this package's JS initializer. blazor.web.js imports every initializer before Blazor starts,
/// so without the hint that import costs one extra round trip after blazor.web.js arrives.
/// </summary>
internal sealed class InitializerPreloadLinkSource : IPreloadLinkSource
{
    private const string InitializerPath = "_content/BetterCache.Extensions/BetterCache.Extensions.lib.module.js";

    public IEnumerable<PreloadLink> GetLinks(HttpContext context)
    {
        // The import uses the fingerprinted URL MapStaticAssets publishes, when there is one.
        var assets = context.GetEndpoint()?.Metadata.GetMetadata<ResourceAssetCollection>();

        return [new PreloadLink { Rel = "modulepreload", Href = assets?[InitializerPath] ?? InitializerPath }];
    }
}
