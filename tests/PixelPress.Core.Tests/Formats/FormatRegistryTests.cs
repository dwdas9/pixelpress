using PixelPress.Core.Formats;
using Xunit;

namespace PixelPress.Core.Tests.Formats;

public sealed class FormatRegistryTests
{
    [Theory]
    [InlineData("jpg", ImageFormatId.Jpeg)]
    [InlineData("JPEG", ImageFormatId.Jpeg)]
    [InlineData(".png", ImageFormatId.Png)]
    [InlineData(".HEIC", ImageFormatId.Heic)]
    [InlineData("webp", ImageFormatId.WebP)]
    [InlineData("avif", ImageFormatId.Avif)]
    [InlineData("tiff", ImageFormatId.Tiff)]
    [InlineData("CR2", ImageFormatId.CameraRaw)]
    public void FromExtension_recognises_known_extensions_case_insensitively(
        string extension, ImageFormatId expected)
    {
        var format = FormatRegistry.FromExtension(extension);

        Assert.NotNull(format);
        Assert.Equal(expected, format.Id);
    }

    [Theory]
    [InlineData("txt")]
    [InlineData(".docx")]
    [InlineData("")]
    [InlineData("   ")]
    public void FromExtension_returns_null_for_unknown_or_empty(string extension)
    {
        Assert.Null(FormatRegistry.FromExtension(extension));
    }

    [Theory]
    [InlineData(@"C:\photos\IMG_0001.HEIC", ImageFormatId.Heic)]
    [InlineData("/Users/d/pictures/cat.jpeg", ImageFormatId.Jpeg)]
    public void FromPath_uses_the_file_extension(string path, ImageFormatId expected)
    {
        var format = FormatRegistry.FromPath(path);

        Assert.NotNull(format);
        Assert.Equal(expected, format.Id);
    }

    [Fact]
    public void FromPath_returns_null_when_there_is_no_extension()
    {
        Assert.Null(FormatRegistry.FromPath("/tmp/README"));
    }

    [Fact]
    public void Heic_camera_raw_and_ico_are_input_only()
    {
        Assert.False(FormatRegistry.Get(ImageFormatId.Heic).CanEncode);
        Assert.False(FormatRegistry.Get(ImageFormatId.CameraRaw).CanEncode);
        Assert.False(FormatRegistry.Get(ImageFormatId.Ico).CanEncode);
        Assert.True(FormatRegistry.Get(ImageFormatId.Heic).CanDecode);
        Assert.True(FormatRegistry.Get(ImageFormatId.CameraRaw).CanDecode);
    }

    [Fact]
    public void Encodable_formats_never_include_input_only_formats()
    {
        Assert.All(FormatRegistry.EncodableFormats, f => Assert.True(f.CanEncode));
        Assert.DoesNotContain(FormatRegistry.EncodableFormats,
            f => f.Id is ImageFormatId.Heic or ImageFormatId.CameraRaw or ImageFormatId.Ico);
    }

    [Fact]
    public void Every_extension_maps_to_exactly_one_format()
    {
        var duplicates = FormatRegistry.All
            .SelectMany(f => f.Extensions)
            .GroupBy(e => e)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Every_format_declares_at_least_one_extension_and_can_decode()
    {
        Assert.All(FormatRegistry.All, f =>
        {
            Assert.NotEmpty(f.Extensions);
            // A format we can neither read nor write has no reason to exist.
            Assert.True(f.CanDecode || f.CanEncode);
        });
    }
}
