namespace BetterCache;

/// <summary>Options for the Cache-Control middleware.</summary>
public sealed class CacheControlOptions
{
    /// <summary>Immutable caching for content-hashed URLs: one year, never revalidated.</summary>
    public const string Immutable = "public, max-age=31536000, immutable";

    /// <summary>
    /// Rules applied when <see cref="UseDefaultRules"/> is true, after <see cref="Rules"/> and any
    /// <see cref="ICacheControlRule"/> registered in DI: fingerprinted file names are immutable, every other
    /// <c>_framework</c> file (boot manifest, <c>dotnet.js</c>, <c>blazor.web.js</c>) must be revalidated.
    /// </summary>
    public static IReadOnlyList<ICacheControlRule> DefaultRules { get; } =
    [
        new FingerprintedFileRule(Immutable),
        new CacheControlRule("/_framework/", "no-cache"),
    ];

    /// <summary>Adds the middleware in <c>UseBetterCache</c>. Default <c>true</c>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Replace a Cache-Control header already set by the endpoint (for example by <c>MapStaticAssets</c>).
    /// Default <c>false</c>: rules only fill the header in when it is missing.
    /// </summary>
    public bool OverrideExisting { get; set; }

    /// <summary>Evaluated first; the first rule that returns a value wins.</summary>
    public IList<CacheControlRule> Rules { get; set; } = [];

    /// <summary>Append <see cref="DefaultRules"/> after the custom rules. Default <c>true</c>.</summary>
    public bool UseDefaultRules { get; set; } = true;
}
