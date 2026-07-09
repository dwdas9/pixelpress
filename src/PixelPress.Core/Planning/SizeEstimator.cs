using PixelPress.Core.Formats;

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
        int quality)
    {
        var ratio = BaseRatio(source, output) * QualityFactor(quality, output);
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

    /// <summary>
    /// Maps the 1–100 quality dial to a multiplier on the base ratio, for
    /// formats that actually have a quality knob. A deliberately simple,
    /// monotonic linear curve calibrated so quality 80 (the default)
    /// leaves the base ratio unchanged, higher quality inflates the
    /// estimate, and lower quality shrinks it. Coarse by design — the
    /// exact number comes from the live preview encode, not here.
    /// </summary>
    private static double QualityFactor(int quality, ImageFormat output)
    {
        // Lossless/palette outputs have no quality dial; quality is a no-op.
        if (!output.HasQualityDial)
        {
            return 1.0;
        }

        // 0.20 + 0.010·q → 1.00 at q=80, ~1.20 at q=100, ~0.21 at q=1.
        return 0.20 + 0.010 * Math.Clamp(quality, 1, 100);
    }
}
