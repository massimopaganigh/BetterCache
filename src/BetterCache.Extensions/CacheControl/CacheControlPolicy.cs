namespace BetterCache;

/// <summary>First match wins over configured rules, then DI-registered rules, then the defaults.</summary>
internal sealed class CacheControlPolicy(IOptions<BetterCacheOptions> options, IEnumerable<ICacheControlRule> registeredRules) : ICacheControlPolicy
{
    private readonly ICacheControlRule[] _rules =
    [
        .. options.Value.CacheControl.Rules,
        .. registeredRules,
        .. options.Value.CacheControl.UseDefaultRules ? CacheControlOptions.DefaultRules : [],
    ];

    public string? GetCacheControl(HttpContext context, PathString path)
    {
        foreach (var rule in _rules)
        {
            if (rule.Evaluate(context, path) is { } value)
                return value;
        }

        return null;
    }
}
