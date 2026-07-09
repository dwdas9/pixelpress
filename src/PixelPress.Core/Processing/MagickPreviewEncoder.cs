using ImageMagick;
using PixelPress.Core.Formats;
using PixelPress.Core.Jobs;

namespace PixelPress.Core.Processing;

/// <summary>
/// Magick.NET-backed <see cref="IPreviewEncoder"/>. Encodes one still
/// image (the first frame of an animated source is enough for a preview)
/// to an in-memory buffer, applying the same metadata / resize / quality
/// steps the real codec would, so the numbers it reports match what a run
/// would actually write. Nothing here touches disk.
/// </summary>
internal sealed class MagickPreviewEncoder : IPreviewEncoder
{
    public PreviewResult? Encode(PreviewRequest request)
    {
        try
        {
            var sourceSize = new FileInfo(request.SourcePath).Length;

            using var image = new MagickImage(request.SourcePath);
            var sourceWidth = (int)image.Width;
            var sourceHeight = (int)image.Height;

            if (request.MetadataPolicy == MetadataPolicy.Strip)
            {
                image.Strip();
            }

            if (request.ResizeEnabled && request.ResizeMaxDimensionPixels > 0)
            {
                image.Resize(new MagickGeometry(
                    (uint)request.ResizeMaxDimensionPixels,
                    (uint)request.ResizeMaxDimensionPixels)
                { Greater = true });
            }

            if (FormatRegistry.Get(request.OutputFormat).HasQualityDial)
            {
                image.Quality = (uint)request.Quality;
            }

            image.Format = MagickFormatMap.ToMagickFormat(request.OutputFormat);
            var bytes = image.ToByteArray();

            return new PreviewResult
            {
                OutputImage = bytes,
                OutputSizeBytes = bytes.LongLength,
                SourceSizeBytes = sourceSize,
                SourceWidth = sourceWidth,
                SourceHeight = sourceHeight,
                OutputWidth = (int)image.Width,
                OutputHeight = (int)image.Height,
            };
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException)
        {
            // A preview failure is never fatal — the UI falls back to the
            // heuristic estimate for this image.
            return null;
        }
    }
}
