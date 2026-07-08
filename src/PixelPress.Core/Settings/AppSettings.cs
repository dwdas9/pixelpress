using PixelPress.Core.Formats;
using PixelPress.Core.Jobs;
using PixelPress.Core.Presets;

namespace PixelPress.Core.Settings;

/// <summary>
/// The user's sticky preferences for the advanced panel, persisted across
/// launches. See <see cref="ISettingsStore"/> for how this is loaded and
/// saved, and ADR-0005 for why these are the fields that persist.
/// </summary>
public sealed record AppSettings
{
    public PresetId Preset { get; init; } = PresetId.Balanced;

    /// <summary>Format to convert to, or null to keep each file's own
    /// format. Mirrors <see cref="JobRequest.TargetFormat"/>.</summary>
    public ImageFormatId? TargetFormat { get; init; }

    public MetadataPolicy MetadataPolicy { get; init; } = MetadataPolicy.Preserve;

    public OutputPolicy OutputPolicy { get; init; } = OutputPolicy.SeparateFolder;

    public bool ResizeEnabled { get; init; }

    public int ResizeMaxDimensionPixels { get; init; } = 2048;

    /// <summary>The settings a first-ever launch starts from.</summary>
    public static AppSettings Default { get; } = new();
}
