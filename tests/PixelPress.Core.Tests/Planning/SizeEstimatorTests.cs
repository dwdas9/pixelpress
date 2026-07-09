using PixelPress.Core.Formats;
using PixelPress.Core.Planning;
using Xunit;

namespace PixelPress.Core.Tests.Planning;

public sealed class SizeEstimatorTests
{
    private static readonly ImageFormat Jpeg = FormatRegistry.Get(ImageFormatId.Jpeg);
    private static readonly ImageFormat Png = FormatRegistry.Get(ImageFormatId.Png);

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
}
