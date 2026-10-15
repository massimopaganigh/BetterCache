namespace BetterCache;

/// <summary>
/// Decides the Cache-Control value of a response. Register implementations in DI to extend the
/// default <see cref="ICacheControlPolicy"/> without replacing it.
/// </summary>
public interface ICacheControlRule
{
    /// <summary>
    /// Returns the Cache-Control value, or <c>null</c> when the rule does not apply.
    /// Called when the response starts, so response headers (for example Content-Type) are available.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <param name="path">The request path as it arrived, before any middleware rewrote it.</param>
    string? Evaluate(HttpContext context, PathString path);
}
