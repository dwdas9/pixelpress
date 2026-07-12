using PixelPress.Core.Formats;
using PixelPress.Core.Jobs;
using PixelPress.Core.Processing;
using Xunit;

namespace PixelPress.Core.Tests.Processing;

/// <summary>
/// The rule that keeps an optimizer from inflating files. The bug this
/// exists to prevent shipped once: a 615 MB batch came out at 886 MB
/// because a near-original re-encode grew every JPEG and nothing checked.
/// </summary>
public sealed class InflationGuardTests
{
    private static bool Guard(
        ImageFormatId source = ImageFormatId.Jpeg,
        ImageFormatId output = ImageFormatId.Jpeg,
        bool wasResized = false,
        MetadataPolicy metadata = MetadataPolicy.Preserve,
        long sourceBytes = 1_000,
        long outputBytes = 1_500) =>
        InflationGuard.ShouldKeepOriginal(
            source, output, wasResized, metadata, sourceBytes, outputBytes);

    [Fact]
    public void Keeps_the_original_when_a_same_format_reencode_grows_the_file() =>
        Assert.True(Guard(sourceBytes: 1_000, outputBytes: 1_500));

    [Fact]
    public void Keeps_the_original_when_the_reencode_is_exactly_the_same_size() =>
        Assert.True(Guard(sourceBytes: 1_000, outputBytes: 1_000));

    [Fact]
    public void Uses_the_reencode_when_it_actually_saves_bytes() =>
        Assert.False(Guard(sourceBytes: 1_000, outputBytes: 400));

    [Fact]
    public void Honours_a_format_conversion_even_when_it_grows_the_file()
    {
        // The user asked for WebP. Handing back the PNG because the WebP came
        // out larger would silently ignore the request.
        Assert.False(Guard(
            source: ImageFormatId.Png,
            output: ImageFormatId.WebP,
            sourceBytes: 1_000,
            outputBytes: 1_500));
    }

    [Fact]
    public void Honours_a_resize_even_when_it_grows_the_file()
    {
        // Resize was requested for the dimensions (an upload cap), not the
        // bytes. Returning the original would return the wrong size image.
        Assert.False(Guard(wasResized: true, sourceBytes: 1_000, outputBytes: 1_500));
    }

    [Fact]
    public void Honours_metadata_stripping_even_when_it_grows_the_file()
    {
        // Stripping is a privacy request — EXIF, GPS. Keeping the original to
        // save bytes would silently retain the location data the user asked to
        // remove. Never trade that away.
        Assert.False(Guard(
            metadata: MetadataPolicy.Strip,
            sourceBytes: 1_000,
            outputBytes: 1_500));
    }

    [Fact]
    public void A_resize_that_did_not_actually_shrink_anything_is_still_a_pure_reencode()
    {
        // Resize enabled, but the source was already under the cap, so no
        // dimensions changed. That is still nothing but a re-compression.
        Assert.True(Guard(wasResized: false, sourceBytes: 1_000, outputBytes: 1_200));
    }
}
