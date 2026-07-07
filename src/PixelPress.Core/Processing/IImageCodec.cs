using PixelPress.Core.Formats;
using PixelPress.Core.Jobs;
using PixelPress.Core.Presets;

namespace PixelPress.Core.Processing;

/// <summary>
/// The seam between the executor and whatever library actually decodes
/// and encodes pixels. Internal so no ImageMagick type ever appears in
/// Core's public API — swapping the processing library later (see
/// ARCHITECTURE.md) means adding a new implementation of this interface,
/// nothing else. Also what makes JobExecutor testable without a working
/// Magick.NET reference: tests substitute a fake implementation.
/// </summary>
internal interface IImageCodec
{
    /// <summary>Decodes the source file and writes the result to
    /// <see cref="CodecRequest.DestinationPath"/>. Never throws for
    /// ordinary failures (corrupt input, unsupported content) — those
    /// are reported via <see cref="CodecResult.Success"/> so the batch
    /// can continue past one bad file.</summary>
    CodecResult Transcode(CodecRequest request);
}

/// <summary>Everything the codec needs to process one file.</summary>
internal sealed record CodecRequest
{
    public required string SourcePath { get; init; }

    /// <summary>Where to write the result. The executor always passes a
    /// temp path here, never the final destination — atomicity is the
    /// executor's responsibility, not the codec's.</summary>
    public required string DestinationPath { get; init; }

    public required ImageFormatId OutputFormat { get; init; }

    public required OptimizationPreset Preset { get; init; }

    public required MetadataPolicy MetadataPolicy { get; init; }
}

/// <summary>The outcome of one transcode attempt.</summary>
internal sealed record CodecResult
{
    public required bool Success { get; init; }

    /// <summary>Size of the file written to DestinationPath, when successful.</summary>
    public long? OutputBytes { get; init; }

    /// <summary>Plain-language reason for failure, safe to show a user.</summary>
    public string? ErrorMessage { get; init; }
}
