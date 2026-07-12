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

        // A same-format re-encode can never end up bigger than the source: if
        // it does, InflationGuard keeps the original. Predicting growth here
        // would promise something the executor will not do.
        if (source.Id == output.Id)
        {
            ratio = Math.Min(ratio, 1.0);
        }

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
    /// formats that actually have a quality knob. Monotonic, coarse by
    /// design — the exact number comes from the live preview encode.
    ///
    /// The curve is steeper above 80 than below it, and that is not a fudge
    /// factor. Output size does not rise linearly with the quality setting:
    /// the top of the range is where an encoder starts spending
    /// disproportionate bits preserving detail (including the compression
    /// artifacts of a previous encode), which is exactly why a "near
    /// original" re-encode can come out *larger* than the file it started
    /// from. A linear curve predicted a comfortable saving at quality 100
    /// while the real encode grew the file — the estimate has to bend where
    /// reality bends.
    /// </summary>
    private static double QualityFactor(int quality, ImageFormat output)
    {
        // Lossless/palette outputs have no quality dial; quality is a no-op.
        if (!output.HasQualityDial)
        {
            return 1.0;
        }

        var q = Math.Clamp(quality, 1, 100);

        // Below the default: 0.20 + 0.010·q → ~0.21 at q=1, 1.00 at q=80.
        if (q <= 80)
        {
            return 0.20 + (0.010 * q);
        }

        // Above it, three times as steep → 1.60 at q=100. Against the 0.65
        // lossy→lossy base that lands just over 1.0, which the same-format
        // clamp in Estimate turns into "expect roughly no saving" — which is
        // the honest prediction for a near-original re-encode.
        return 1.0 + (0.030 * (q - 80));
    }
}
