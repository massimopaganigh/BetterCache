namespace BetterCache;

/// <summary>
/// Supplies preload links for a page. Every registered source contributes; links are de-duplicated by
/// <see cref="PreloadLink.Href"/>. Register more with <c>services.AddSingleton&lt;IPreloadLinkSource, MySource&gt;()</c>.
/// </summary>
public interface IPreloadLinkSource
{
    /// <summary>Returns the links to preload for the page being rendered.</summary>
    IEnumerable<PreloadLink> GetLinks(HttpContext context);
}
