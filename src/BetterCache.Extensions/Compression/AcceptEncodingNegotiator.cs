using Microsoft.Net.Http.Headers;

namespace BetterCache;

/// <summary>RFC 9110 §12.5.3 negotiation: honors q-values, <c>q=0</c>, <c>*</c> and an explicit <c>identity</c> preference.</summary>
internal sealed class AcceptEncodingNegotiator : IContentEncodingNegotiator
{
    public IReadOnlyList<ContentEncoding> Negotiate(HttpRequest request, IReadOnlyList<ContentEncoding> available)
    {
        // The RFC treats a missing header as "anything goes", but clients such as plain curl cannot decode
        // Brotli, so like MapStaticAssets we only compress when the client asks for it.
        if (!StringWithQualityHeaderValue.TryParseList(request.Headers.AcceptEncoding, out var accepted) || accepted.Count == 0)
            return [];

        var wildcard = QualityOf(accepted, "*");
        var identity = QualityOf(accepted, "identity") ?? 0;

        return available
            .Select(encoding => (encoding, quality: QualityOf(accepted, encoding.Name) ?? wildcard ?? 0))
            .Where(candidate => candidate.quality > 0 && candidate.quality >= identity)
            .OrderByDescending(candidate => candidate.quality) // stable: ties keep server preference order
            .Select(candidate => candidate.encoding)
            .ToArray();
    }

    private static double? QualityOf(IList<StringWithQualityHeaderValue> accepted, string coding)
    {
        foreach (var value in accepted)
        {
            if (value.Value.Equals(coding, StringComparison.OrdinalIgnoreCase))
                return value.Quality ?? 1;
        }

        return null;
    }
}
