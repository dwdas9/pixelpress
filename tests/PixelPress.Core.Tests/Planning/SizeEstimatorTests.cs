using PixelPress.Core.Formats;
using PixelPress.Core.Planning;
using Xunit;

namespace PixelPress.Core.Tests.Planning;

public sealed class SizeEstimatorTests
{
    private static readonly ImageFormat Jpeg = FormatRegistry.Get(ImageFormatId.Jpeg);
    private static readonly ImageFormat Png = FormatRegistry.Get(ImageFormatId.Png);
    private static readonly ImageFormat Gif = FormatRegistry.Get(ImageFormatId.Gif);
    private static readonly ImageFormat Bmp = FormatRegistry.Get(ImageFormatId.Bmp);
    private static readonly ImageFormat WebP = FormatRegistry.Get(ImageFormatId.WebP);

    [Fact]
    public void Higher_quality_never_produces_a_smaller_estimate_for_a_lossy_output()
    {
        long previous = 0;
        foreach (var quality in new[] { 1, 25, 50, 80, 100 })
        {
            var estimate = SizeEstimator.Estimate(1_000_000, Jpeg, Jpeg, quality);
            Assert.True(estimate >= previous,
                $"Estimate at quality {quality} ({estimate}) should be >= the lower-quality one ({previous}).");
            previous = estimate;
        }
    }

    [Fact]
    public void Quality_has_no_effect_on_a_lossless_output()
    {
        var low = SizeEstimator.Estimate(1_000_000, Png, Png, quality: 10);
        var high = SizeEstimator.Estimate(1_000_000, Png, Png, quality: 100);

        Assert.Equal(low, high);
    }

    [Fact]
    public void An_estimate_is_always_at_least_one_byte()
    {
        Assert.True(SizeEstimator.Estimate(1, Jpeg, Jpeg, quality: 1) >= 1);
    }

    /// <summary>The regression this whole class exists to prevent.
    ///
    /// GIF is neither IsLossless nor HasQualityDial, so JPEG → GIF used to fall
    /// through every branch and land on the generic lossy → lossy ratio of 0.65.
    /// A 2.1 GB batch of photos was advertised as "~35% smaller" and would have
    /// grown to roughly 2.9 GB. The estimator must predict growth for a target
    /// that cannot compress a photograph.</summary>
    [Theory]
    [InlineData(ImageFormatId.Gif)]
    [InlineData(ImageFormatId.Png)]
    [InlineData(ImageFormatId.Bmp)]
    [InlineData(ImageFormatId.Tiff)]
    public void A_photo_converted_to_a_format_that_cannot_compress_it_is_estimated_larger(
        ImageFormatId target)
    {
        const long sourceBytes = 4_400_000;

        var estimate = SizeEstimator.Estimate(
            sourceBytes, Jpeg, FormatRegistry.Get(target), quality: 80);

        Assert.True(estimate > sourceBytes,
            $"JPEG → {target} must be estimated larger than the source, got {estimate} for {sourceBytes}.");
    }

    /// <summary>The quality dial is the user's lever for size, and on these
    /// targets it is not connected to anything. The estimate must not pretend it
    /// is — dragging quality to 8% for a GIF batch changes nothing, and an
    /// estimate that fell as it dropped would be inventing a saving.</summary>
    [Fact]
    public void Quality_cannot_talk_a_palette_target_out_of_growing()
    {
        var lowest = SizeEstimator.Estimate(4_400_000, Jpeg, Gif, quality: 1);
        var highest = SizeEstimator.Estimate(4_400_000, Jpeg, Gif, quality: 100);

        Assert.Equal(lowest, highest);
        Assert.True(lowest > 4_400_000);
    }

    [Fact]
    public void An_uncompressed_source_still_shrinks_into_a_lossy_target()
    {
        Assert.True(SizeEstimator.Estimate(10_000_000, Bmp, WebP, quality: 80) < 10_000_000);
    }

    /// <summary>Growth is specific to re-coding *compressed* pixels losslessly.
    /// A PNG re-packed as a PNG is still a modest win, and the fix must not have
    /// turned every lossless target into a predicted disaster.</summary>
    [Fact]
    public void A_lossless_source_does_not_explode_into_a_lossless_target()
    {
        Assert.True(SizeEstimator.Estimate(1_000_000, Png, Png, quality: 80) < 1_000_000);
    }
}
