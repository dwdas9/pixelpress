using PixelPress.Core.Processing;
using PixelPress.Core.Tests.Planning;

namespace PixelPress.Core.Tests.Processing;

/// <summary>
/// Stands in for MagickImageCodec so JobExecutor's orchestration
/// (parallelism, atomic writes, error isolation, cancellation) can be
/// tested without a working Magick.NET reference. On success it writes
/// a fake file to <see cref="FakeFileSystem"/> at the requested
/// destination, mirroring what a real codec does on disk — this is what
/// makes the executor's atomic-move step exercised honestly by tests.
/// </summary>
internal sealed class FakeImageCodec(FakeFileSystem fileSystem) : IImageCodec
{
    private int _callCount;

    public int CallCount => _callCount;

    /// <summary>Every request this codec was asked to process, in call order.</summary>
    public List<CodecRequest> Requests { get; } = [];

    /// <summary>Override to control the outcome per request (e.g. fail one
    /// specific source path). Defaults to always succeeding.</summary>
    public Func<CodecRequest, CodecResult>? Behavior { get; set; }

    public CodecResult Transcode(CodecRequest request)
    {
        Interlocked.Increment(ref _callCount);

        lock (Requests)
        {
            Requests.Add(request);
        }

        var result = Behavior?.Invoke(request) ?? new CodecResult { Success = true, OutputBytes = 500 };

        if (result.Success)
        {
            fileSystem.AddFile(request.DestinationPath, result.OutputBytes ?? 500);
        }

        return result;
    }
}
