using ImageMagick;
using PixelPress.Core.Formats;
using PixelPress.Core.Jobs;

namespace PixelPress.Core.Processing;

/// <summary>One format's verification outcome.</summary>
public sealed record CodecVerificationResult(string FormatName, bool Success, string? ErrorMessage);

/// <summary>Full verification report across every encodable format.</summary>
public sealed record CodecVerificationReport(IReadOnlyList<CodecVerificationResult> Results)
{
    public bool AllPassed => Results.All(r => r.Success);
}

/// <summary>
/// Round-trips a synthetic test image through every encodable format.
/// Exists because codec behaviour is the one part of this engine that
/// cannot be verified in CI or on a developer's machine other than the
/// two we actually ship for — this gives a one-command way to confirm
/// the format matrix on real Windows and real macOS. No sample images
/// required; a small solid-colour image is generated in memory.
/// </summary>
public static class CodecVerifier
{
    public static CodecVerificationReport Run()
    {
        var codec = new MagickImageCodec();
        var results = new List<CodecVerificationResult>();

        var tempDirectory = Path.Combine(
            Path.GetTempPath(), $"pixelpress-codec-check-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var sourcePath = CreateSampleImage(tempDirectory);

            foreach (var format in FormatRegistry.EncodableFormats)
            {
                var destinationPath = Path.Combine(
                    tempDirectory, $"sample.{format.CanonicalExtension}");

                var request = new CodecRequest
                {
                    SourcePath = sourcePath,
                    DestinationPath = destinationPath,
                    OutputFormat = format.Id,
                    Preset = Presets.Presets.Balanced,
                    MetadataPolicy = MetadataPolicy.Preserve,
                };

                var result = codec.Transcode(request);
                results.Add(new CodecVerificationResult(
                    format.DisplayName, result.Success, result.ErrorMessage));
            }
        }
        finally
        {
            try
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup of a scratch temp folder; not fatal.
            }
        }

        return new CodecVerificationReport(results);
    }

    private static string CreateSampleImage(string directory)
    {
        var path = Path.Combine(directory, "sample.png");
        using var image = new MagickImage(MagickColors.CornflowerBlue, 64, 64);
        image.Write(path);
        return path;
    }
}
