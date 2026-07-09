using PixelPress.Core.Formats;
using PixelPress.Core.Jobs;

namespace PixelPress.Core.Processing;

/// <summary>
/// Encodes a single image at chosen settings to an in-memory buffer, for
/// the live studio preview (ADR-0006 §4). This is a read-only third
/// concern — neither planning (which touches no pixels) nor execution
/// (which writes files). It exists so the UI can show the exact output
/// size, exact output dimensions, and a side-by-side preview for the one
/// image the user is inspecting, without writing anything to disk.
///
/// Public (unlike <see cref="IImageCodec"/>) because the desktop layer
/// consumes it directly; its contract exposes only PixelPress and
/// framework types, never an ImageMagick type. Construct the default
/// implementation via <see cref="PreviewEncoding.CreateDefault"/>.
/// </summary>
public interface IPreviewEncoder
{
    /// <summary>Encodes the request in memory. Returns null for an
    /// ordinary failure (corrupt/unreadable source, unsupported content)
    /// so the caller can fall back to the heuristic estimate instead of
    /// crashing the preview.</summary>
    PreviewResult? Encode(PreviewRequest request);
}

/// <summary>What to encode for one preview, mirroring the lossy knobs of
/// a <see cref="JobRequest"/> for a single file.</summary>
public sealed record PreviewRequest
{
    public required string SourcePath { get; init; }

    public required ImageFormatId OutputFormat { get; init; }

    /// <summary>Lossy quality 1–100; applied only to formats whose
    /// <see cref="ImageFormat.HasQualityDial"/> is true.</summary>
    public required int Quality { get; init; }

    public MetadataPolicy MetadataPolicy { get; init; } = MetadataPolicy.Preserve;

    public bool ResizeEnabled { get; init; }

    public int ResizeMaxDimensionPixels { get; init; } = 2048;
}

/// <summary>The exact result of a preview encode: the encoded bytes plus
/// the numbers the studio's statistics strip reports.</summary>
public sealed record PreviewResult
{
    /// <summary>The encoded output image, ready to hand to a bitmap
    /// decoder for the "after" pane.</summary>
    public required byte[] OutputImage { get; init; }

    public required long OutputSizeBytes { get; init; }

    public required long SourceSizeBytes { get; init; }

    public required int SourceWidth { get; init; }

    public required int SourceHeight { get; init; }

    public required int OutputWidth { get; init; }

    public required int OutputHeight { get; init; }

    /// <summary>True when resize actually shrank the image (output
    /// dimensions differ from source).</summary>
    public bool WasResized => OutputWidth != SourceWidth || OutputHeight != SourceHeight;
}

/// <summary>Factory for the default preview encoder, keeping the
/// ImageMagick-backed implementation internal — same pattern as
/// <see cref="JobExecutor"/> owning its codec privately.</summary>
public static class PreviewEncoding
{
    public static IPreviewEncoder CreateDefault() => new MagickPreviewEncoder();
}
