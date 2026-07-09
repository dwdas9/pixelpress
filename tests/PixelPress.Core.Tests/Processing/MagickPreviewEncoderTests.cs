using ImageMagick;
using PixelPress.Core.Formats;
using PixelPress.Core.Processing;
using Xunit;

namespace PixelPress.Core.Tests.Processing;

/// <summary>
/// Exercises the real Magick-backed preview encoder end-to-end against a
/// generated sample image. Unlike the pure-logic tests these depend on
/// the bundled Magick.NET binaries actually working in this environment
/// (as <see cref="CodecVerifier"/> does on a shipping machine); JPEG/PNG
/// are used because their delegates are always present.
/// </summary>
public sealed class MagickPreviewEncoderTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), $"pixelpress-preview-tests-{Guid.NewGuid():N}");

    private readonly IPreviewEncoder _encoder = PreviewEncoding.CreateDefault();

    private string CreateSample(int width, int height)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "sample.png");
        using var image = new MagickImage(MagickColors.CornflowerBlue, (uint)width, (uint)height);
        image.Write(path);
        return path;
    }

    [Fact]
    public void Encodes_to_a_non_empty_buffer_and_reports_source_dimensions()
    {
        var source = CreateSample(200, 120);

        var result = _encoder.Encode(new PreviewRequest
        {
            SourcePath = source,
            OutputFormat = ImageFormatId.Jpeg,
            Quality = 80,
        });

        Assert.NotNull(result);
        Assert.True(result!.OutputImage.Length > 0);
        Assert.Equal(result.OutputImage.LongLength, result.OutputSizeBytes);
        Assert.Equal(200, result.SourceWidth);
        Assert.Equal(120, result.SourceHeight);
        Assert.Equal(200, result.OutputWidth);
        Assert.Equal(120, result.OutputHeight);
        Assert.False(result.WasResized);
    }

    [Fact]
    public void Lower_quality_never_yields_more_bytes_than_higher_quality()
    {
        var source = CreateSample(400, 400);

        var low = _encoder.Encode(new PreviewRequest
        {
            SourcePath = source, OutputFormat = ImageFormatId.Jpeg, Quality = 20,
        });
        var high = _encoder.Encode(new PreviewRequest
        {
            SourcePath = source, OutputFormat = ImageFormatId.Jpeg, Quality = 95,
        });

        Assert.NotNull(low);
        Assert.NotNull(high);
        Assert.True(high!.OutputSizeBytes >= low!.OutputSizeBytes);
    }

    [Fact]
    public void Resize_shrinks_the_reported_output_dimensions_and_never_upscales()
    {
        var source = CreateSample(1000, 500);

        var shrunk = _encoder.Encode(new PreviewRequest
        {
            SourcePath = source,
            OutputFormat = ImageFormatId.Jpeg,
            Quality = 80,
            ResizeEnabled = true,
            ResizeMaxDimensionPixels = 250,
        });

        Assert.NotNull(shrunk);
        Assert.True(shrunk!.WasResized);
        Assert.Equal(250, shrunk.OutputWidth);
        Assert.Equal(125, shrunk.OutputHeight);
        Assert.Equal(1000, shrunk.SourceWidth);

        var noUpscale = _encoder.Encode(new PreviewRequest
        {
            SourcePath = source,
            OutputFormat = ImageFormatId.Jpeg,
            Quality = 80,
            ResizeEnabled = true,
            ResizeMaxDimensionPixels = 5000,
        });

        Assert.NotNull(noUpscale);
        Assert.Equal(1000, noUpscale!.OutputWidth);
        Assert.False(noUpscale.WasResized);
    }

    [Fact]
    public void A_missing_source_returns_null_rather_than_throwing()
    {
        var result = _encoder.Encode(new PreviewRequest
        {
            SourcePath = Path.Combine(_dir, "does-not-exist.png"),
            OutputFormat = ImageFormatId.Jpeg,
            Quality = 80,
        });

        Assert.Null(result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
