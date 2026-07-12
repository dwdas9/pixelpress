using PixelPress.Core.Formats;
using PixelPress.Core.Jobs;

namespace PixelPress.Core.Processing;

/// <summary>
/// The rule that stops an *optimizer* from making files bigger.
///
/// Re-encoding an already-compressed image at a high quality setting
/// legitimately produces a larger file — the encoder spends bits
/// preserving the compression artifacts of the previous encode. At
/// "Near-original" quality on a JPEG corpus this is the normal case, not
/// an edge case, and without this rule a batch can grow by 40% while the
/// UI cheerfully reports "optimized".
///
/// So: when the encode was a pure re-compression and it came out no
/// smaller than the source, the original wins and the encode is thrown
/// away.
///
/// "Pure re-compression" is doing real work in that sentence. The rule
/// only applies when the user asked for nothing except smaller bytes:
///
/// - **Format conversion** (PNG → WebP) is a transformation the user
///   explicitly requested. Handing back the PNG because the WebP came out
///   larger would silently ignore the request.
/// - **A resize that actually shrank the image** was requested for its
///   dimensions (an upload limit, say), not its bytes.
/// - **Metadata stripping** is usually a *privacy* request — EXIF, GPS.
///   Keeping the original would silently retain the location data the
///   user asked to remove. Never trade that for bytes.
///
/// In all three cases the user gets what they asked for, even if it is
/// bigger, and the UI reports the real number.
///
/// Shared by <see cref="JobExecutor"/> and the preview encoder on purpose:
/// the preview must promise exactly what the run will do.
/// </summary>
public static class InflationGuard
{
    public static bool ShouldKeepOriginal(
        ImageFormatId sourceFormat,
        ImageFormatId outputFormat,
        bool wasResized,
        MetadataPolicy metadataPolicy,
        long sourceBytes,
        long outputBytes)
    {
        if (sourceFormat != outputFormat)
        {
            return false;
        }

        if (wasResized)
        {
            return false;
        }

        if (metadataPolicy == MetadataPolicy.Strip)
        {
            return false;
        }

        return outputBytes >= sourceBytes;
    }
}
