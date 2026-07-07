using PixelPress.Core.Formats;

namespace PixelPress.Core.Presets;

/// <summary>Stable identifiers for the three quality presets.</summary>
public enum PresetId
{
    HighQuality,
    Balanced,
    SmallestSize,
}

/// <summary>
/// A quality preset: the user-facing promise ("Balanced") plus the
/// per-format encoder parameters that fulfil it. Users never see the
/// numbers; they see the display name and description. The numeric
/// values are the single place to tune output quality per format.
/// </summary>
public sealed record OptimizationPreset
{
    public required PresetId Id { get; init; }

    /// <summary>Short name shown on the preset selector.</summary>
    public required string DisplayName { get; init; }

    /// <summary>One-line plain-language description of the trade-off.</summary>
    public required string Description { get; init; }

    /// <summary>JPEG quality, 1–100.</summary>
    public required int JpegQuality { get; init; }

    /// <summary>WebP quality, 1–100 (lossy).</summary>
    public required int WebPQuality { get; init; }

    /// <summary>AVIF quality, 1–100 (lossy).</summary>
    public required int AvifQuality { get; init; }

    /// <summary>JPEG XL quality, 1–100 (lossy).</summary>
    public required int JpegXlQuality { get; init; }

    /// <summary>
    /// PNG effort, 1–9. PNG is lossless, so this trades encode time for
    /// file size instead of trading visual quality.
    /// </summary>
    public required int PngCompressionLevel { get; init; }

    /// <summary>
    /// Returns the quality value for a lossy target format, or null when
    /// the format has no quality dial (lossless formats, GIF's palette).
    /// </summary>
    public int? QualityFor(ImageFormatId format) => format switch
    {
        ImageFormatId.Jpeg => JpegQuality,
        ImageFormatId.WebP => WebPQuality,
        ImageFormatId.Avif => AvifQuality,
        ImageFormatId.JpegXl => JpegXlQuality,
        _ => null,
    };
}

/// <summary>The built-in presets, in UI display order.</summary>
public static class Presets
{
    public static OptimizationPreset HighQuality { get; } = new()
    {
        Id = PresetId.HighQuality,
        DisplayName = "High Quality",
        Description = "Virtually indistinguishable from the original. Modest size savings.",
        JpegQuality = 90,
        WebPQuality = 88,
        AvifQuality = 75,
        JpegXlQuality = 90,
        PngCompressionLevel = 9,
    };

    public static OptimizationPreset Balanced { get; } = new()
    {
        Id = PresetId.Balanced,
        DisplayName = "Balanced",
        Description = "Great for sharing and everyday use. Strong size savings.",
        JpegQuality = 80,
        WebPQuality = 75,
        AvifQuality = 60,
        JpegXlQuality = 78,
        PngCompressionLevel = 9,
    };

    public static OptimizationPreset SmallestSize { get; } = new()
    {
        Id = PresetId.SmallestSize,
        DisplayName = "Smallest Size",
        Description = "Maximum compression. Fine for previews, email and quick sharing.",
        JpegQuality = 65,
        WebPQuality = 60,
        AvifQuality = 45,
        JpegXlQuality = 62,
        PngCompressionLevel = 9,
    };

    /// <summary>All presets in the order they appear in the UI.</summary>
    public static IReadOnlyList<OptimizationPreset> All { get; } =
        [HighQuality, Balanced, SmallestSize];

    /// <summary>Resolves a preset id to its definition.</summary>
    public static OptimizationPreset Get(PresetId id) => id switch
    {
        PresetId.HighQuality => HighQuality,
        PresetId.Balanced => Balanced,
        PresetId.SmallestSize => SmallestSize,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown preset."),
    };
}
