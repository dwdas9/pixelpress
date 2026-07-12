using PixelPress.Core.Formats;
using PixelPress.Core.Jobs;

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

    /// <summary>Lossy quality, 1–100 (see ADR-0006). Applied only to
    /// formats whose <see cref="ImageFormat.HasQualityDial"/> is true.</summary>
    public required int Quality { get; init; }

    public required MetadataPolicy MetadataPolicy { get; init; }

    public bool ResizeEnabled { get; init; }

    public int ResizeMaxDimensionPixels { get; init; } = 2048;
}

/// <summary>The outcome of one transcode attempt.</summary>
internal sealed record CodecResult
{
    public required bool Success { get; init; }

    /// <summary>Size of the file written to DestinationPath, when successful.</summary>
    public long? OutputBytes { get; init; }

    /// <summary>True when the resize step actually changed the image's
    /// dimensions. Distinct from "resize was enabled": a source already
    /// smaller than the cap is never upscaled, so the request is a no-op and
    /// the encode is still a pure re-compression. <see cref="InflationGuard"/>
    /// needs to tell those apart.</summary>
    public bool WasResized { get; init; }

    /// <summary>Plain-language reason for failure, safe to show a user.</summary>
    public string? ErrorMessage { get; init; }
}
