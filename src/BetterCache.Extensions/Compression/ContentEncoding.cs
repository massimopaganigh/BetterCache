namespace BetterCache;

/// <summary>A content coding and the suffix of the pre-compressed sibling file that carries it.</summary>
public sealed class ContentEncoding
{
    /// <summary>Creates an empty encoding, for configuration binding.</summary>
    public ContentEncoding()
    {
    }

    /// <summary>Creates an encoding.</summary>
    public ContentEncoding(string name, string fileExtension)
    {
        Name = name;
        FileExtension = fileExtension;
    }

    /// <summary>Content-Encoding token, for example <c>br</c>, <c>gzip</c> or <c>zstd</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Suffix of the sibling file, for example <c>.br</c>.</summary>
    public string FileExtension { get; set; } = string.Empty;
}
