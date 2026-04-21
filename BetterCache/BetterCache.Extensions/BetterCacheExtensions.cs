namespace BetterCache
{
    /// <summary>
    /// Registration helpers for BetterCache.
    /// </summary>
    public static class BetterCacheExtensions
    {
        /// <summary>Registers BetterCache services. Call before <see cref="UseBetterCache"/>.</summary>
        public static IServiceCollection AddBetterCache(this IServiceCollection services, Action<BetterCacheOptions>? configure = null)
        {
            services.AddOptions<BetterCacheOptions>();

            if (configure is not null)
                services.Configure(configure);

            return services;
        }

        /// <summary>
        /// Adds cache headers + optional pre-compressed asset serving. Call early in the pipeline,
        /// before <c>MapStaticAssets</c>.
        /// </summary>
        public static IApplicationBuilder UseBetterCache(this IApplicationBuilder app)
        {
            app.UseMiddleware<BetterCacheMiddleware>();
            app.UseMiddleware<PrecompressedStaticFileMiddleware>();

            return app;
        }
    }
}
