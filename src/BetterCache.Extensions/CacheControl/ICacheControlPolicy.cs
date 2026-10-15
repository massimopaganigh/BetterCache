namespace BetterCache;

/// <summary>Chooses the Cache-Control value of a response. Replace it in DI to take full control.</summary>
public interface ICacheControlPolicy
{
    /// <summary>Returns the Cache-Control value, or <c>null</c> to leave the response untouched.</summary>
    /// <param name="context">The current request.</param>
    /// <param name="path">The request path as it arrived, before any middleware rewrote it.</param>
    string? GetCacheControl(HttpContext context, PathString path);
}
