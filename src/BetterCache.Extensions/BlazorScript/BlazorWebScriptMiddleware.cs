using Microsoft.Net.Http.Headers;

namespace BetterCache;

/// <summary>
/// Serves <c>blazor.web.js</c> pre-compressed in place of the framework endpoint, which sends it uncompressed.
/// Its plain URL is revalidated (<c>no-cache</c>); the fingerprinted URL rendered by <see cref="BlazorWebScript"/>
/// is immutable, so repeat visits skip the request entirely.
/// </summary>
internal sealed class BlazorWebScriptMiddleware(RequestDelegate next, BlazorWebScriptAsset script, IContentEncodingNegotiator negotiator)
{
    private static readonly PathString PlainPath = new(BlazorWebScriptAsset.Path);

    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;

        if (!(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)))
            return next(context);

        var fingerprinted = request.Path.Equals(new PathString(script.FingerprintedPath), StringComparison.OrdinalIgnoreCase);

        if (!fingerprinted && !request.Path.Equals(PlainPath, StringComparison.OrdinalIgnoreCase))
            return next(context);

        var encoding = negotiator.Negotiate(request, BlazorWebScriptAsset.Encodings).FirstOrDefault();
        var representation = script.Get(encoding?.Name) ?? script.Get(null)!;
        var response = context.Response;
        var headers = response.Headers;

        headers.ContentType = "text/javascript";
        headers.CacheControl = fingerprinted ? CacheControlOptions.Immutable : "no-cache";
        headers.Vary = HeaderNames.AcceptEncoding;
        headers.ETag = representation.ETag;

        if (IsNotModified(request, representation.ETag))
        {
            response.StatusCode = StatusCodes.Status304NotModified;

            return Task.CompletedTask;
        }

        if (representation.Encoding is { } contentEncoding)
            headers.ContentEncoding = contentEncoding;

        response.ContentLength = representation.Bytes.Length;

        return HttpMethods.IsHead(request.Method) ? Task.CompletedTask : response.Body.WriteAsync(representation.Bytes, context.RequestAborted).AsTask();
    }

    // If-None-Match uses the weak comparison (RFC 9110 §13.1.2): W/ prefixes are ignored.
    private static bool IsNotModified(HttpRequest request, string etag) =>
        request.GetTypedHeaders().IfNoneMatch.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Tag.Equals(etag, StringComparison.Ordinal));
}
