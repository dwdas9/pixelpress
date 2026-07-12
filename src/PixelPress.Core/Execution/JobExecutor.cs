using System.Collections.Concurrent;
using System.Diagnostics;
using PixelPress.Core.Jobs;
using PixelPress.Core.Planning;
using PixelPress.Core.Processing;
using PixelPress.Core.Services;

namespace PixelPress.Core.Execution;

/// <summary>
/// Phase two of the two-phase pipeline: walks a <see cref="JobPlan"/>
/// and actually processes each file. Runs on a bounded worker pool sized
/// to the machine's core count, writes atomically (temp file, then
/// rename), and treats a single file's failure as data — the batch
/// always continues, results are collected, and the caller decides what
/// to do with the summary.
///
/// The public constructor takes only <see cref="IFileSystem"/>; the
/// Magick.NET codec is constructed internally so it never appears in
/// this class's public surface. The internal constructor exists purely
/// for tests, which substitute a fake codec to verify this orchestration
/// logic without depending on a working Magick.NET reference.
/// </summary>
public sealed class JobExecutor
{
    private readonly IFileSystem _fileSystem;
    private readonly IImageCodec _codec;

    public JobExecutor(IFileSystem fileSystem) : this(fileSystem, new MagickImageCodec())
    {
    }

    internal JobExecutor(IFileSystem fileSystem, IImageCodec codec)
    {
        _fileSystem = fileSystem;
        _codec = codec;
    }

    public Task<ExecutionSummary> ExecuteAsync(
        JobPlan plan,
        JobRequest request,
        IProgress<ExecutionProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        // Deliberately NOT passing cancellationToken to Task.Run itself:
        // if the token were already cancelled, Task.Run would cancel this
        // task before the delegate ever ran, skipping the try/catch inside
        // Execute() and throwing instead of returning a graceful
        // ExecutionSummary with WasCancelled set. The token is honored
        // exactly once, inside Execute's Parallel.ForEach.
        Task.Run(() => Execute(plan, request, progress, cancellationToken));

    private ExecutionSummary Execute(
        JobPlan plan,
        JobRequest request,
        IProgress<ExecutionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var results = new ConcurrentBag<ItemResult>();
        var completed = 0;
        var completedBytes = 0L;
        var total = plan.Items.Count;
        var wasCancelled = false;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
                CancellationToken = cancellationToken,
            };

            Parallel.ForEach(plan.Items, options, item =>
            {
                var result = ProcessItem(
                    item, request.MetadataPolicy, request.Quality,
                    request.ResizeEnabled, request.ResizeMaxDimensionPixels);
                results.Add(result);

                // Both counters are bumped from several worker threads at once,
                // so both go through Interlocked. They can still be read here
                // slightly out of step with each other; that only ever perturbs
                // a throughput figure by one file, which nobody can perceive.
                var done = Interlocked.Increment(ref completed);
                var doneBytes = Interlocked.Add(ref completedBytes, item.SourceBytes);

                progress?.Report(new ExecutionProgress
                {
                    Completed = done,
                    Total = total,
                    CurrentFileName = Path.GetFileName(item.SourcePath),
                    LastResult = result,
                    CompletedSourceBytes = doneBytes,
                    Elapsed = stopwatch.Elapsed,
                });
            });
        }
        catch (OperationCanceledException)
        {
            // Cancellation is honored between files, not mid-file: work
            // already in flight on other threads finishes and is recorded
            // above; Parallel.ForEach simply stops starting new items.
            wasCancelled = true;
        }

        return new ExecutionSummary
        {
            Results = results.OrderBy(r => r.SourcePath, StringComparer.Ordinal).ToList(),
            WasCancelled = wasCancelled,
        };
    }

    private ItemResult ProcessItem(
        PlannedItem item,
        MetadataPolicy metadataPolicy,
        int quality,
        bool resizeEnabled,
        int resizeMaxDimensionPixels)
    {
        var tempPath = _fileSystem.GetTempFilePath(item.OutputPath);

        try
        {
            var outputDirectory = Path.GetDirectoryName(item.OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                _fileSystem.CreateDirectory(outputDirectory);
            }

            var codecResult = _codec.Transcode(new CodecRequest
            {
                SourcePath = item.SourcePath,
                DestinationPath = tempPath,
                OutputFormat = item.OutputFormat,
                Quality = quality,
                MetadataPolicy = metadataPolicy,
                ResizeEnabled = resizeEnabled,
                ResizeMaxDimensionPixels = resizeMaxDimensionPixels,
            });

            if (!codecResult.Success)
            {
                _fileSystem.DeleteFile(tempPath);
                return Failed(item, codecResult.ErrorMessage ?? "This file could not be processed.");
            }

            // An optimizer must never hand back a bigger file. Re-encoding an
            // already-compressed image at high quality routinely inflates it,
            // so when the encode wins nothing, throw it away and keep the
            // original.
            if (InflationGuard.ShouldKeepOriginal(
                    item.SourceFormat,
                    item.OutputFormat,
                    codecResult.WasResized,
                    metadataPolicy,
                    item.SourceBytes,
                    codecResult.OutputBytes ?? long.MaxValue))
            {
                _fileSystem.DeleteFile(tempPath);

                // In overwrite mode the original is already at the output path;
                // there is nothing to do. Writing to a separate folder, it still
                // has to get there, or the batch silently drops a file.
                if (!PathsMatch(item.SourcePath, item.OutputPath))
                {
                    _fileSystem.CopyFile(item.SourcePath, item.OutputPath, overwrite: true);
                }

                return new ItemResult
                {
                    SourcePath = item.SourcePath,
                    OutputPath = item.OutputPath,
                    Outcome = ItemOutcome.KeptOriginal,
                    SourceBytes = item.SourceBytes,
                    OutputBytes = item.SourceBytes,
                };
            }

            _fileSystem.MoveFileAtomic(tempPath, item.OutputPath, overwrite: true);

            return new ItemResult
            {
                SourcePath = item.SourcePath,
                OutputPath = item.OutputPath,
                Outcome = ItemOutcome.Success,
                SourceBytes = item.SourceBytes,
                OutputBytes = codecResult.OutputBytes,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _fileSystem.DeleteFile(tempPath);
            return Failed(item, $"Could not write the output file: {ex.Message}");
        }
    }

    private static bool PathsMatch(string a, string b) =>
        string.Equals(
            Path.GetFullPath(a),
            Path.GetFullPath(b),
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    private static ItemResult Failed(PlannedItem item, string message) => new()
    {
        SourcePath = item.SourcePath,
        OutputPath = item.OutputPath,
        Outcome = ItemOutcome.Failed,
        SourceBytes = item.SourceBytes,
        ErrorMessage = message,
    };
}
