using PixelPress.Core.Formats;
using PixelPress.Core.Presets;
using Xunit;

namespace PixelPress.Core.Tests.Formats;

public sealed class PresetTests
{
    [Fact]
    public void There_are_exactly_three_presets_in_display_order()
    {
        Assert.Equal(
            new[] { PresetId.HighQuality, PresetId.Balanced, PresetId.SmallestSize },
            Presets.Presets.All.Select(p => p.Id));
    }

    [Fact]
    public void Quality_strictly_decreases_from_high_to_smallest_for_every_lossy_format()
    {
        ImageFormatId[] lossyFormats =
            [ImageFormatId.Jpeg, ImageFormatId.WebP, ImageFormatId.Avif, ImageFormatId.JpegXl];

        foreach (var format in lossyFormats)
        {
            var high = Presets.Presets.HighQuality.QualityFor(format)!.Value;
            var balanced = Presets.Presets.Balanced.QualityFor(format)!.Value;
            var smallest = Presets.Presets.SmallestSize.QualityFor(format)!.Value;

            Assert.True(high > balanced, $"{format}: HighQuality must exceed Balanced.");
            Assert.True(balanced > smallest, $"{format}: Balanced must exceed SmallestSize.");
        }
    }

    [Fact]
    public void All_quality_values_are_within_encoder_range()
    {
        foreach (var preset in Presets.Presets.All)
        {
            Assert.InRange(preset.JpegQuality, 1, 100);
            Assert.InRange(preset.WebPQuality, 1, 100);
            Assert.InRange(preset.AvifQuality, 1, 100);
            Assert.InRange(preset.JpegXlQuality, 1, 100);
            Assert.InRange(preset.PngCompressionLevel, 1, 9);
        }
    }

    [Fact]
    public void Lossless_formats_have_no_quality_dial()
    {
        Assert.Null(Presets.Presets.Balanced.QualityFor(ImageFormatId.Png));
        Assert.Null(Presets.Presets.Balanced.QualityFor(ImageFormatId.Bmp));
        Assert.Null(Presets.Presets.Balanced.QualityFor(ImageFormatId.Gif));
    }

    [Fact]
    public void Every_preset_has_a_name_and_a_plain_language_description()
    {
        Assert.All(Presets.Presets.All, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(p.Description));
        });
    }
}
