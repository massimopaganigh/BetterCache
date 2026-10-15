namespace BetterCache;

/// <summary>Chooses which pre-compressed representations a client accepts.</summary>
public interface IContentEncodingNegotiator
{
    /// <summary>
    /// Returns the encodings from <paramref name="available"/> the client accepts, most preferred first.
    /// An empty list means the uncompressed representation should be served.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <param name="available">Supported encodings in server preference order.</param>
    IReadOnlyList<ContentEncoding> Negotiate(HttpRequest request, IReadOnlyList<ContentEncoding> available);
}
