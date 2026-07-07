using PixelPress.Core.Formats;
using PixelPress.Core.Presets;

namespace PixelPress.Core.Jobs;

/// <summary>Where processed files are written.</summary>
public enum OutputPolicy
{
    /// <summary>Write to a separate output directory (the safe default).</summary>
    SeparateFolder,

    /// <summary>Replace originals in place. Opt-in only.</summary>
    OverwriteOriginals,
}

/// <summary>What happens to embedded metadata (EXIF, ICC, XMP).</summary>
public enum MetadataPolicy
{
    /// <summary>Keep metadata in the output (the default).</summary>
    Preserve,

    /// <summary>Remove metadata from the output.</summary>
    Strip,
}

/// <summary>
/// A complete description of one batch job: what to process and every
/// policy needed to process it without asking further questions.
/// Immutable; produced by the UI, consumed by the planner.
/// </summary>
public sealed record JobRequest
{
    /// <summary>Files and/or directories the user provided. Directories
    /// are scanned recursively.</summary>
    public required IReadOnlyList<string> InputPaths { get; init; }

    /// <summary>Target directory for outputs. Required when
    /// <see cref="OutputPolicy.SeparateFolder"/>; ignored when
    /// overwriting originals.</summary>
    public string? OutputDirectory { get; init; }

    public OutputPolicy OutputPolicy { get; init; } = OutputPolicy.SeparateFolder;

    /// <summary>Format to convert to, or null to keep each file's own
    /// format (pure optimization). Must be an encodable format.</summary>
    public ImageFormatId? TargetFormat { get; init; }

    public PresetId Preset { get; init; } = PresetId.Balanced;

    public MetadataPolicy MetadataPolicy { get; init; } = MetadataPolicy.Preserve;

    /// <summary>Mirror source folder structure inside the output
    /// directory (default) instead of flattening.</summary>
    public bool PreserveFolderStructure { get; init; } = true;
}
