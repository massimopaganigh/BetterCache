using System.Buffers;

namespace BetterCache;

/// <summary>
/// Matches file names carrying a static web assets fingerprint, a 10-character lowercase base-36 segment:
/// <c>dotnet.native.pkfvibo6od.wasm</c>, <c>app.jago5f97na.css</c>, <c>Sample.5jbna5rlia.styles.css</c>.
/// </summary>
/// <remarks>
/// The segment must contain a digit, so ordinary 10-letter words such as <c>jquery.validation.js</c> are
/// never cached as immutable. A fingerprint made only of letters is missed and simply revalidates.
/// </remarks>
public sealed class FingerprintedFileRule(string value) : ICacheControlRule
{
    private const int FingerprintLength = 10;
    private static readonly SearchValues<char> Base36 = SearchValues.Create("abcdefghijklmnopqrstuvwxyz0123456789");

    /// <summary>The Cache-Control value applied to fingerprinted files.</summary>
    public string Value { get; } = value;

    /// <inheritdoc />
    public string? Evaluate(HttpContext context, PathString path) => IsFingerprinted(path.Value) ? Value : null;

    internal static bool IsFingerprinted(ReadOnlySpan<char> path)
    {
        var fileName = path[(path.LastIndexOf('/') + 1)..];
        var first = fileName.IndexOf('.');
        var last = fileName.LastIndexOf('.');

        if (first < 0 || first == last)
            return false;

        // Fingerprints sit strictly between the base name and the extension.
        var middle = fileName[(first + 1)..last];

        foreach (var segment in middle.Split('.'))
        {
            if (IsFingerprint(middle[segment]))
                return true;
        }

        return false;
    }

    private static bool IsFingerprint(ReadOnlySpan<char> segment) =>
        segment.Length == FingerprintLength
        && !segment.ContainsAnyExcept(Base36)
        && segment.ContainsAnyInRange('0', '9');
}
