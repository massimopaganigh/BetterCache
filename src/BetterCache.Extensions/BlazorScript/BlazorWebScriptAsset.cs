using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using Microsoft.Extensions.FileProviders;

namespace BetterCache;

/// <summary>
/// The framework's own <c>blazor.web.js</c> (embedded in Microsoft.AspNetCore.Components.Endpoints, the same
/// file <c>MapRazorComponents</c> serves), its fingerprint and its pre-compressed representations.
/// </summary>
internal sealed class BlazorWebScriptAsset
{
    public const string Path = "/_framework/blazor.web.js";

    private readonly Lazy<Content> _content = new(Load);

    public static IReadOnlyList<ContentEncoding> Encodings { get; } = [new("br", ".br"), new("gzip", ".gz")];

    /// <summary>Fingerprinted path, e.g. <c>/_framework/blazor.web.4k2x9pq7ab.js</c>.</summary>
    public string FingerprintedPath => _content.Value.FingerprintedPath;

    /// <summary>Representation for <paramref name="encoding"/> (<c>null</c> = identity), or <c>null</c> when unsupported.</summary>
    public Representation? Get(string? encoding) => _content.Value.Representations.GetValueOrDefault(encoding ?? string.Empty);

    private static Content Load()
    {
        var provider = new ManifestEmbeddedFileProvider(typeof(RazorComponentsEndpointRouteBuilderExtensions).Assembly);
        var file = provider.GetFileInfo(Path.TrimStart('/'));

        if (!file.Exists)
            throw new InvalidOperationException($"The framework no longer embeds {Path}; disable BetterCache:BlazorScript.");

        using var stream = file.CreateReadStream();
        using var buffer = new MemoryStream();

        stream.CopyTo(buffer);

        var bytes = buffer.ToArray();
        var hash = SHA256.HashData(bytes);
        var fingerprint = ToFingerprint(hash);

        return new(
            $"/_framework/blazor.web.{fingerprint}.js",
            new Dictionary<string, Representation>
            {
                [string.Empty] = new(null, bytes, $"\"{fingerprint}\""),
                ["br"] = new("br", Compress(bytes, output => new BrotliStream(output, new BrotliCompressionOptions { Quality = 11 })), $"\"{fingerprint}-br\""),
                ["gzip"] = new("gzip", Compress(bytes, output => new GZipStream(output, CompressionLevel.SmallestSize)), $"\"{fingerprint}-gzip\""),
            });
    }

    // Same shape as static web assets fingerprints: 10 base-36 characters.
    internal static string ToFingerprint(byte[] hash)
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        var value = new BigInteger(hash, isUnsigned: true);
        var chars = new char[10];

        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[(int)(value % 36)];
            value /= 36;
        }

        return new string(chars);
    }

    private static byte[] Compress(byte[] bytes, Func<Stream, Stream> createEncoder)
    {
        using var output = new MemoryStream();

        using (var encoder = createEncoder(output))
            encoder.Write(bytes);

        return output.ToArray();
    }

    internal sealed record Representation(string? Encoding, byte[] Bytes, string ETag);

    private sealed record Content(string FingerprintedPath, IReadOnlyDictionary<string, Representation> Representations);
}
