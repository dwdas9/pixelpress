using ImageMagick;
using PixelPress.Core.Formats;

namespace PixelPress.Core.Processing;

/// <summary>Maps PixelPress format identities to ImageMagick's own enum.
/// The single place this translation lives, shared by the codec and the
/// preview encoder so the two can never drift.</summary>
internal static class MagickFormatMap
{
    public static MagickFormat ToMagickFormat(ImageFormatId id) => id switch
    {
        ImageFormatId.Jpeg => MagickFormat.Jpeg,
        ImageFormatId.Png => MagickFormat.Png,
        ImageFormatId.WebP => MagickFormat.WebP,
        ImageFormatId.Avif => MagickFormat.Avif,
        ImageFormatId.Tiff => MagickFormat.Tiff,
        ImageFormatId.Bmp => MagickFormat.Bmp,
        ImageFormatId.Gif => MagickFormat.Gif,
        ImageFormatId.JpegXl => MagickFormat.Jxl,
        _ => throw new NotSupportedException($"{id} is not an encodable format."),
    };
}
