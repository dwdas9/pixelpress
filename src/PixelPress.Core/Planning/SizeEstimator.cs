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
        // Targets with no lossy quality dial cannot trade fidelity for bytes, so
        // their size is dictated by the *source's* compression, not by any
        // setting. They are estimated on their own terms — see NoQualityDialRatio,
        // and see the bug it exists to fix.
        if (!output.HasQualityDial)
        {
            return NoQualityDialRatio(source, output);
        }

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
    /// How a target with no lossy quality dial (GIF, PNG, BMP, TIFF) sizes up
    /// against its source.
    ///
    /// This is the case the estimator used to get catastrophically wrong. GIF is
    /// neither <c>IsLossless</c> nor <c>HasQualityDial</c>, so a JPEG → GIF batch
    /// fell through every branch above and landed on the generic "lossy → lossy
    /// re-encode: solid gains" ratio of 0.65. A 2.1 GB folder of wedding photos
    /// was therefore advertised as "~35% smaller → 1.4 GB" while the real encode
    /// of the very first image came out 38% *larger*. The batch would have grown
    /// to roughly 2.9 GB. Nothing downstream was wrong: the estimate was.
    ///
    /// The governing fact is that these formats store every pixel they are given.
    /// A lossy source has already thrown most of its bytes away, so coding it back
    /// into a palette (GIF) or a lossless container (PNG, BMP, TIFF) must *put
    /// those bytes back* — and photographic noise is the worst case for all four,
    /// because there is no flat colour for them to exploit. Growth is the normal
    /// outcome here, not the edge case.
    ///
    /// Ratios are output bytes per source byte, and coarse by design. They lean
    /// slightly pessimistic, in keeping with the rest of the file: on a growth the
    /// pessimistic direction is to over-predict it, so the real encode comes in
    /// under the warning rather than over it.
    /// </summary>
    private static double NoQualityDialRatio(ImageFormat source, ImageFormat output)
    {
        var sourceClass = Classify(source);

        return output.Id switch
        {
            // 256 colours + LZW. Measured against real 4032px iPhone photos at
            // 1.39× and 1.68×, and identical at quality 1, 50 and 95 — the dial
            // is not connected to anything. 1.75 keeps the warning ahead of the
            // worst case seen rather than behind it.
            ImageFormatId.Gif => sourceClass switch
            {
                SourceClass.Lossy => 1.75,
                SourceClass.Palette => 1.00,
                SourceClass.Lossless => 0.50,
                SourceClass.Uncompressed => 0.12,
                _ => 0.10,
            },

            // Lossless coding of photographic noise: routinely 3–5× a JPEG.
            ImageFormatId.Png => sourceClass switch
            {
                SourceClass.Lossy => 4.00,
                SourceClass.Palette => 2.00,
                SourceClass.Lossless => 0.85,
                SourceClass.Uncompressed => 0.35,
                _ => 0.60,
            },

            ImageFormatId.Tiff => sourceClass switch
            {
                SourceClass.Lossy => 5.00,
                SourceClass.Palette => 2.50,
                SourceClass.Lossless => 1.20,
                SourceClass.Uncompressed => 0.90,
                _ => 0.80,
            },

            // Uncompressed. The growth is bounded only by how hard the source was
            // compressed in the first place, which is to say: not bounded.
            ImageFormatId.Bmp => sourceClass switch
            {
                SourceClass.Lossy => 10.00,
                SourceClass.Palette => 5.00,
                SourceClass.Lossless => 2.50,
                SourceClass.Uncompressed => 1.00,
                _ => 1.50,
            },

            _ => 1.00,
        };
    }

    /// <summary>What the source has already done to its bytes — which is the only
    /// thing that predicts what a lossless target will have to spend putting them
    /// back.</summary>
    private enum SourceClass
    {
        Lossy,
        Palette,
        Lossless,
        Uncompressed,
        Raw,
    }

    private static SourceClass Classify(ImageFormat source) => source.Id switch
    {
        ImageFormatId.Jpeg or ImageFormatId.Avif or ImageFormatId.Heic => SourceClass.Lossy,

        // WebP and JPEG XL have lossless modes, and nothing in a file extension
        // says which one was used. Assume the common case (lossy) and accept that
        // a lossless WebP source will be over-estimated.
        ImageFormatId.WebP or ImageFormatId.JpegXl => SourceClass.Lossy,

        ImageFormatId.Gif or ImageFormatId.Ico => SourceClass.Palette,
        ImageFormatId.Png => SourceClass.Lossless,
        ImageFormatId.Bmp or ImageFormatId.Tiff => SourceClass.Uncompressed,
        ImageFormatId.CameraRaw => SourceClass.Raw,
        _ => SourceClass.Lossy,
    };

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
