namespace BetterCache;

/// <summary>Adds the BetterCache middleware.</summary>
public static class BetterCacheApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the enabled middleware: Cache-Control rules, compressed <c>blazor.web.js</c> and pre-compressed files.
    /// Call before <c>UseStaticFiles</c> / <c>MapStaticAssets</c>.
    /// </summary>
    public static IApplicationBuilder UseBetterCache(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<BetterCacheOptions>>().Value;

        if (options.CacheControl.Enabled)
            app.UseMiddleware<CacheControlMiddleware>();

        if (options.BlazorScript.Enabled)
            app.UseMiddleware<BlazorWebScriptMiddleware>();

        if (options.PrecompressedFiles.Enabled)
            app.UseMiddleware<PrecompressedFilesMiddleware>();

        return app;
    }
}
