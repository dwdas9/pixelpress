namespace PixelPress.Core.Formats;

/// <summary>
/// Stable identifiers for every image format PixelPress knows about.
/// These are PixelPress's own identities, independent of the processing
/// library, so the engine implementation can change without breaking
/// the public contract.
/// </summary>
public enum ImageFormatId
{
    Jpeg,
    Png,
    WebP,
    Avif,
    Heic,
    Tiff,
    Bmp,
    Gif,
    JpegXl,
    Ico,
    CameraRaw,
}

/// <summary>
/// Everything the application needs to know about one image format:
/// how to recognise it on disk and what the engine can do with it.
/// The decode/encode split is deliberate and load-bearing: HEIC and
/// camera RAW are input-only (patent/delegate constraints on encoding),
/// and the UI must never offer an output format with CanEncode == false.
/// </summary>
public sealed record ImageFormat
{
    /// <summary>Stable identity used across the engine, settings and UI.</summary>
    public required ImageFormatId Id { get; init; }

    /// <summary>Human-readable name shown in the UI (e.g. "JPEG").</summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// File extensions (lower-case, without dot) that map to this format.
    /// The first entry is the canonical extension used when writing.
    /// </summary>
    public required IReadOnlyList<string> Extensions { get; init; }

    /// <summary>The engine can read files of this format.</summary>
    public required bool CanDecode { get; init; }

    /// <summary>The engine can write files of this format.</summary>
    public required bool CanEncode { get; init; }

    /// <summary>Format supports multi-frame animation (GIF, WebP).</summary>
    public bool SupportsAnimation { get; init; }

    /// <summary>Format supports an alpha channel.</summary>
    public bool SupportsTransparency { get; init; }

    /// <summary>
    /// Compression is lossless by nature (PNG, BMP). Used by the planner
    /// to estimate savings and by the UI to phrase quality descriptions.
    /// </summary>
    public bool IsLossless { get; init; }

    /// <summary>Canonical extension used for output files, without dot.</summary>
    public string CanonicalExtension => Extensions[0];
}
