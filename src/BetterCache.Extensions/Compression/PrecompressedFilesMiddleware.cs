using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace BetterCache;

/// <summary>
/// Serves <c>app.js.br</c> / <c>app.js.gz</c> in place of <c>app.js</c> when the client accepts the encoding.
/// The file itself is sent by an inner <see cref="StaticFileMiddleware"/>, so ETag, Last-Modified,
/// conditional requests, HEAD and Range behave exactly as for any static file.
/// </summary>
internal sealed class PrecompressedFilesMiddleware
{
    private static readonly object OriginalPathKey = new();

    private readonly IReadOnlyList<ContentEncoding> _encodings;
    private readonly IFileProvider _files;
    private readonly IContentEncodingNegotiator _negotiator;
    private readonly RequestDelegate _next;
    private readonly RequestDelegate _sendFile;

    public PrecompressedFilesMiddleware(RequestDelegate next, IWebHostEnvironment environment, ILoggerFactory loggerFactory, IOptions<BetterCacheOptions> options, IContentEncodingNegotiator negotiator)
    {
        var settings = options.Value.PrecompressedFiles;

        _next = next;
        _negotiator = negotiator;
        _encodings = settings.EffectiveEncodings;
        _files = settings.FileProvider ?? environment.WebRootFileProvider;

        var staticFileOptions = new StaticFileOptions
        {
            FileProvider = _files,
            ContentTypeProvider = new PrecompressedContentTypeProvider(_encodings),
            OnPrepareResponse = AddEncodingHeaders,
        };

        _sendFile = new StaticFileMiddleware(RestorePathAndContinue, environment, Options.Create(staticFileOptions), loggerFactory).Invoke;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;

        // A matched endpoint (MapStaticAssets, a page, an API) owns the response.
        if (context.GetEndpoint() is not null
            || !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            || !Path.HasExtension(request.Path.Value))
            return _next(context);

        // shortcut: an uncompressed response gets no "Vary: Accept-Encoding" even when siblings exist;
        // add it here if a shared cache serves clients that differ in compression support.
        foreach (var encoding in _negotiator.Negotiate(request, _encodings))
        {
            var sibling = request.Path.Value + encoding.FileExtension;

            if (!_files.GetFileInfo(sibling).Exists)
                continue;

            context.Items[OriginalPathKey] = request.Path;
            request.Path = new PathString(sibling);

            return _sendFile(context);
        }

        return _next(context);
    }

    private void AddEncodingHeaders(StaticFileResponseContext context)
    {
        if (FindEncoding(_encodings, context.File.Name) is not { } encoding)
            return;

        var headers = context.Context.Response.Headers;

        headers.ContentEncoding = encoding.Name;
        headers.Append(HeaderNames.Vary, HeaderNames.AcceptEncoding);
    }

    private Task RestorePathAndContinue(HttpContext context)
    {
        if (context.Items.Remove(OriginalPathKey, out var path))
            context.Request.Path = (PathString)path!;

        return _next(context);
    }

    internal static ContentEncoding? FindEncoding(IReadOnlyList<ContentEncoding> encodings, string fileName)
    {
        foreach (var encoding in encodings)
        {
            if (fileName.EndsWith(encoding.FileExtension, StringComparison.OrdinalIgnoreCase))
                return encoding;
        }

        return null;
    }

    /// <summary>Reports the Content-Type of the original file for its compressed sibling.</summary>
    private sealed class PrecompressedContentTypeProvider(IReadOnlyList<ContentEncoding> encodings) : IContentTypeProvider
    {
        private readonly FileExtensionContentTypeProvider _inner = new();

        public bool TryGetContentType(string subpath, out string contentType)
        {
            var encoding = FindEncoding(encodings, subpath);
            var original = encoding is null ? subpath : subpath[..^encoding.FileExtension.Length];

            return _inner.TryGetContentType(original, out contentType!);
        }
    }
}
