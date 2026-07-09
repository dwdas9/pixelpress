using ImageMagick;
using PixelPress.Core.Formats;
using PixelPress.Core.Jobs;

namespace PixelPress.Core.Processing;

/// <summary>
/// Magick.NET-backed codec. This is the only file in the engine that
/// references ImageMagick types — everything else talks to
/// <see cref="IImageCodec"/>.
///
/// Multi-frame formats (animated GIF/WebP) are handled deliberately:
/// <c>new MagickImage(path)</c> reads only the first frame of a
/// multi-frame file, which would silently turn an animation into a
/// still image. Animated sources go through <see cref="MagickImageCollection"/>
/// instead, so motion is preserved unless the *output* format genuinely
/// cannot hold multiple frames.
/// </summary>
internal sealed class MagickImageCodec : IImageCodec
{
    public CodecResult Transcode(CodecRequest request)
    {
        try
        {
            var sourceFormat = FormatRegistry.FromPath(request.SourcePath);
            var isAnimatedSource = sourceFormat?.SupportsAnimation == true;

            return isAnimatedSource
                ? TranscodeAnimated(request)
                : TranscodeStill(request);
        }
        catch (MagickException ex)
        {
            return Failure(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failure(ex.Message);
        }
    }

    private static CodecResult TranscodeStill(CodecRequest request)
    {
        using var image = new MagickImage(request.SourcePath);

        ApplyMetadataPolicy(image, request.MetadataPolicy);
        ApplyResize(image, request.ResizeEnabled, request.ResizeMaxDimensionPixels);
        ApplyQuality(image, request.OutputFormat, request.Quality);
        image.Format = MagickFormatMap.ToMagickFormat(request.OutputFormat);
        image.Write(request.DestinationPath);

        return Success(request.DestinationPath);
    }

    private static CodecResult TranscodeAnimated(CodecRequest request)
    {
        var outputFormat = FormatRegistry.Get(request.OutputFormat);

        using var collection = new MagickImageCollection(request.SourcePath);

        if (!outputFormat.SupportsAnimation)
        {
            // Target format cannot hold multiple frames (e.g. animated GIF
            // to JPEG). Keeping the first frame is a deliberate, visible
            // choice here — not the silent frame-1-by-accident bug this
            // method exists to avoid.
            using var firstFrame = (MagickImage)collection[0].Clone();
            ApplyMetadataPolicy(firstFrame, request.MetadataPolicy);
            ApplyResize(firstFrame, request.ResizeEnabled, request.ResizeMaxDimensionPixels);
            ApplyQuality(firstFrame, request.OutputFormat, request.Quality);
            firstFrame.Format = MagickFormatMap.ToMagickFormat(request.OutputFormat);
            firstFrame.Write(request.DestinationPath);
            return Success(request.DestinationPath);
        }

        // Normalizes per-frame disposal/offset so every frame can be
        // re-encoded independently without accumulating artifacts.
        collection.Coalesce();

        foreach (var frame in collection)
        {
            ApplyMetadataPolicy(frame, request.MetadataPolicy);
            ApplyResize(frame, request.ResizeEnabled, request.ResizeMaxDimensionPixels);
            ApplyQuality(frame, request.OutputFormat, request.Quality);
            frame.Format = MagickFormatMap.ToMagickFormat(request.OutputFormat);
        }

        collection.Write(request.DestinationPath);

        return Success(request.DestinationPath);
    }

    private static void ApplyMetadataPolicy(IMagickImage<byte> image, MetadataPolicy policy)
    {
        if (policy == MetadataPolicy.Strip)
        {
            image.Strip();
        }
        // Preserve is the library's default; nothing to do.
    }

    /// <summary>Shrinks the image so neither dimension exceeds
    /// <paramref name="maxDimension"/>, preserving aspect ratio.
    /// <c>Less = true</c> means the geometry only applies when the image
    /// is larger than it — a smaller source is never upscaled.</summary>
    private static void ApplyResize(IMagickImage<byte> image, bool enabled, int maxDimension)
    {
        if (!enabled)
        {
            return;
        }

        image.Resize(new MagickGeometry((uint)maxDimension, (uint)maxDimension) { Less = true });
    }

    private static void ApplyQuality(IMagickImage<byte> image, ImageFormatId format, int quality)
    {
        if (FormatRegistry.Get(format).HasQualityDial)
        {
            image.Quality = (uint)quality;
        }
        // Lossless formats (PNG, BMP) and GIF have no lossy quality dial;
        // PNG compression-level tuning is a deferred follow-up — see
        // ARCHITECTURE.md M4 notes. Default compression is used for now.
    }

    private static CodecResult Success(string path) =>
        new() { Success = true, OutputBytes = new FileInfo(path).Length };

    private static CodecResult Failure(string message) =>
        new() { Success = false, ErrorMessage = message };
}
