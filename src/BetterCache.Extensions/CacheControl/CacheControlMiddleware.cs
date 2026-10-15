namespace BetterCache;

/// <summary>Sets Cache-Control from <see cref="ICacheControlPolicy"/> when the response starts.</summary>
internal sealed class CacheControlMiddleware(RequestDelegate next, ICacheControlPolicy policy, IOptions<BetterCacheOptions> options)
{
    private readonly bool _overrideExisting = options.Value.CacheControl.OverrideExisting;

    public Task InvokeAsync(HttpContext context)
    {
        // Captured now: later middleware may rewrite the path (for example to a .br sibling).
        var path = context.Request.Path;

        context.Response.OnStarting(() =>
        {
            Apply(context, path);

            return Task.CompletedTask;
        });

        return next(context);
    }

    private void Apply(HttpContext context, PathString path)
    {
        var response = context.Response;

        // Never let an error page inherit a long-lived policy.
        if (response.StatusCode is not (>= 200 and < 300 or StatusCodes.Status304NotModified))
            return;

        if (!_overrideExisting && response.Headers.CacheControl.Count > 0)
            return;

        if (policy.GetCacheControl(context, path) is { } value)
            response.Headers.CacheControl = value;
    }
}
