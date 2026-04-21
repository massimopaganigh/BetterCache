namespace BetterCache
{
    /// <summary>
    /// Applies aggressive HTTP cache headers for framework assets and revalidation for the boot manifest.
    /// Mirrors the default .NET 10 Blazor caching behavior.
    /// </summary>
    public sealed class BetterCacheMiddleware
    {
        private readonly string _immutableHeader;
        private readonly RequestDelegate _next;
        private readonly BetterCacheOptions _options;

        public BetterCacheMiddleware(RequestDelegate next, IOptions<BetterCacheOptions> options)
        {
            _next = next;
            _options = options.Value;
            _immutableHeader = $"public, max-age={_options.ImmutableMaxAgeSeconds}, immutable";
        }

        #region PRIVATE METHODS
        private static void ApplyHeaders(HttpContext ctx, BetterCacheOptions opts, string immutable)
        {
            var path = ctx.Request.Path.Value;

            if (string.IsNullOrEmpty(path))
                return;

            if (path.EndsWith(opts.BootManifestFileName, StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.Headers.CacheControl = "no-cache, must-revalidate";

                return;
            }

            if (path.Contains(opts.FrameworkPathSegment, StringComparison.OrdinalIgnoreCase))
                ctx.Response.Headers.CacheControl = immutable;
        }
        #endregion

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value;

            if (!string.IsNullOrEmpty(path))
                context.Response.OnStarting(static state =>
                {
                    var (ctx, opts, immutable) = ((HttpContext, BetterCacheOptions, string))state;

                    ApplyHeaders(ctx, opts, immutable);

                    return Task.CompletedTask;
                }, (context, _options, _immutableHeader));

            await _next(context);
        }
    }
}
