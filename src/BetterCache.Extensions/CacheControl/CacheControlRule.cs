using System.Text.RegularExpressions;

namespace BetterCache;

/// <summary>
/// Applies <see cref="Value"/> when the request path matches <see cref="PathPattern"/>.
/// Bindable from configuration: <c>{ "PathPattern": "^/images/", "Value": "public, max-age=3600" }</c>.
/// </summary>
public sealed class CacheControlRule : ICacheControlRule
{
    private Regex? _regex;

    /// <summary>Creates an empty rule, for configuration binding.</summary>
    public CacheControlRule()
    {
    }

    /// <summary>Creates a rule.</summary>
    public CacheControlRule(string pathPattern, string value)
    {
        PathPattern = pathPattern;
        Value = value;
    }

    /// <summary>
    /// Case-insensitive regular expression matched against the request path. It runs with
    /// <see cref="RegexOptions.NonBacktracking"/> (linear time on untrusted paths), so lookarounds and
    /// backreferences are not supported.
    /// </summary>
    public string PathPattern { get; set; } = string.Empty;

    /// <summary>The Cache-Control header value.</summary>
    public string Value { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? Evaluate(HttpContext context, PathString path) =>
        (_regex ??= CreateRegex(PathPattern)).IsMatch(path.Value ?? string.Empty) ? Value : null;

    internal static Regex CreateRegex(string pattern) =>
        new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
}
