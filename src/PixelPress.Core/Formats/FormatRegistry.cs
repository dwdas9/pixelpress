using System.Collections.Frozen;

namespace PixelPress.Core.Formats;

/// <summary>
/// Central registry of every format PixelPress supports, with lookup by
/// file extension. This is the single source of truth consumed by the
/// planner (file classification), the executor (encoder selection) and
/// the UI (output format picker). Capabilities reflect what the bundled
/// Magick.NET Q8 binaries actually deliver on Windows and macOS:
/// HEIC is decode-only (no bundled HEVC encoder) and camera RAW is
/// decode-only by nature (libraw reads, nothing writes).
/// </summary>
public static class FormatRegistry
{
    /// <summary>All known formats, in UI display order.</summary>
    public static IReadOnlyList<ImageFormat> All { get; } =
    [
        new ImageFormat
        {
            Id = ImageFormatId.Jpeg,
            DisplayName = "JPEG",
            Extensions = ["jpg", "jpeg", "jpe", "jfif"],
            CanDecode = true,
            CanEncode = true,
            SupportsTransparency = false,
            HasQualityDial = true,
        },
        new ImageFormat
        {
            Id = ImageFormatId.Png,
            DisplayName = "PNG",
            Extensions = ["png"],
            CanDecode = true,
            CanEncode = true,
            SupportsTransparency = true,
            IsLossless = true,
        },
        new ImageFormat
        {
            Id = ImageFormatId.WebP,
            DisplayName = "WebP",
            Extensions = ["webp"],
            CanDecode = true,
            CanEncode = true,
            SupportsAnimation = true,
            SupportsTransparency = true,
            HasQualityDial = true,
        },
        new ImageFormat
        {
            Id = ImageFormatId.Avif,
            DisplayName = "AVIF",
            Extensions = ["avif"],
            CanDecode = true,
            CanEncode = true,
            SupportsTransparency = true,
            HasQualityDial = true,
        },
        new ImageFormat
        {
            Id = ImageFormatId.Heic,
            DisplayName = "HEIC",
            Extensions = ["heic", "heif"],
            CanDecode = true,
            CanEncode = false, // No bundled HEVC encoder; input-only by design.
            SupportsTransparency = true,
        },
        new ImageFormat
        {
            Id = ImageFormatId.Tiff,
            DisplayName = "TIFF",
            Extensions = ["tif", "tiff"],
            CanDecode = true,
            CanEncode = true,
            SupportsTransparency = true,
        },
        new ImageFormat
        {
            Id = ImageFormatId.Bmp,
            DisplayName = "BMP",
            Extensions = ["bmp", "dib"],
            CanDecode = true,
            CanEncode = true,
            IsLossless = true,
        },
        new ImageFormat
        {
            Id = ImageFormatId.Gif,
            DisplayName = "GIF",
            Extensions = ["gif"],
            CanDecode = true,
            CanEncode = true,
            SupportsAnimation = true,
            SupportsTransparency = true,
        },
        new ImageFormat
        {
            Id = ImageFormatId.JpegXl,
            DisplayName = "JPEG XL",
            Extensions = ["jxl"],
            CanDecode = true,
            CanEncode = true,
            SupportsTransparency = true,
            HasQualityDial = true,
        },
        new ImageFormat
        {
            Id = ImageFormatId.Ico,
            DisplayName = "ICO",
            Extensions = ["ico"],
            CanDecode = true,
            CanEncode = false, // Icon authoring is out of scope; read-only.
            SupportsTransparency = true,
        },
        new ImageFormat
        {
            Id = ImageFormatId.CameraRaw,
            DisplayName = "Camera RAW",
            Extensions = ["dng", "cr2", "cr3", "nef", "arw", "orf", "rw2", "raf"],
            CanDecode = true,
            CanEncode = false, // libraw decodes; nothing sensibly encodes RAW.
        },
    ];

    /// <summary>Formats the user may choose as a conversion target.</summary>
    public static IReadOnlyList<ImageFormat> EncodableFormats { get; } =
        All.Where(f => f.CanEncode).ToArray();

    private static readonly FrozenDictionary<string, ImageFormat> ByExtension =
        All.SelectMany(f => f.Extensions.Select(e => (Extension: e, Format: f)))
           .ToFrozenDictionary(p => p.Extension, p => p.Format);

    private static readonly FrozenDictionary<ImageFormatId, ImageFormat> ById =
        All.ToFrozenDictionary(f => f.Id);

    /// <summary>Looks up a format by file extension (with or without a leading dot,
    /// any casing). Returns null for unknown extensions — the planner treats
    /// those files as "not an image" and skips them.</summary>
    public static ImageFormat? FromExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        var normalized = extension.TrimStart('.').ToLowerInvariant();
        return ByExtension.GetValueOrDefault(normalized);
    }

    /// <summary>Looks up a format by path, using its extension.</summary>
    public static ImageFormat? FromPath(string path) =>
        FromExtension(Path.GetExtension(path));

    /// <summary>Resolves a format id to its full definition.</summary>
    public static ImageFormat Get(ImageFormatId id) => ById[id];
}
