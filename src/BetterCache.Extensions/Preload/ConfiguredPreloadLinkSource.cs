namespace BetterCache;

/// <summary>Contributes <see cref="PreloadOptions.Links"/>.</summary>
internal sealed class ConfiguredPreloadLinkSource(IOptions<BetterCacheOptions> options) : IPreloadLinkSource
{
    public IEnumerable<PreloadLink> GetLinks(HttpContext context) => options.Value.Preload.Links;
}
