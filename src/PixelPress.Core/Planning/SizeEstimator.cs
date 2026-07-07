using PixelPress.Core.Formats;
using PixelPress.Core.Presets;

namespace PixelPress.Core.Planning;

/// <summary>
/// Coarse output-size estimation for the plan preview. These are honest
/// heuristics, not promises — the UI presents them as "estimated" and the
/// completion summary reports the real numbers. Ratios are deliberately
/// conservative (slightly pessimistic) so real results tend to beat the
/// estimate rather than disappoint.
/// </summary>
internal static class SizeEstimator
{
    public static long Estimate(
        long sourceBytes,
        ImageFormat source,
        ImageFormat output,
        PresetId preset)
    {
        var ratio = BaseRatio(source, output) * PresetFactor(preset, output);
        return Math.Max(1, (long)(sourceBytes * ratio));
    }

    private static double BaseRatio(ImageFormat source, ImageFormat output)
    {
        // Uncompressed/lightly-compressed sources shrink dramatically
        // into any lossy target.
        if (source.Id is ImageFormatId.Bmp or ImageFormatId.Tiff && !output.IsLossless)
        {
            return 0.10;
        }

        // HEIC/RAW → JPEG typically *grows* HEIC (HEVC compresses harder
        // than JPEG) and shrinks RAW enormously. Split the cases honestly.
        if (source.Id == ImageFormatId.Heic)
        {
            return output.Id is ImageFormatId.Avif or ImageFormatId.WebP ? 0.95 : 1.40;
        }

        if (source.Id == ImageFormatId.CameraRaw)
        {
            return 0.15;
        }

        // Lossless → lossless (PNG re-compression): modest gains.
        if (source.IsLossless && output.IsLossless)
        {
            return 0.85;
        }

        // Lossless → lossy: big gains.
        if (source.IsLossless && !output.IsLossless)
        {
            return 0.30;
        }

        // Lossy → lossy re-encode at preset quality: solid gains.
        return 0.65;
    }

    private static double PresetFactor(PresetId preset, ImageFormat output)
    {
        // Lossless outputs have no quality dial; preset barely matters.
        if (output.IsLossless)
        {
            return 1.0;
        }

        return preset switch
        {
            PresetId.HighQuality => 1.25,
            PresetId.Balanced => 1.0,
            PresetId.SmallestSize => 0.70,
            _ => 1.0,
        };
    }
}
