namespace PixelPress.Core.Execution;

/// <summary>How one file's processing attempt ended.</summary>
public enum ItemOutcome
{
    Success,
    Failed,
}

/// <summary>The outcome of processing one planned item. Always produced —
/// a failure never throws out of the executor, it becomes one of these
/// instead, so the batch continues past a single bad file.</summary>
public sealed record ItemResult
{
    public required string SourcePath { get; init; }

    public required string OutputPath { get; init; }

    public required ItemOutcome Outcome { get; init; }

    public required long SourceBytes { get; init; }

    /// <summary>Actual output size, present only when Outcome is Success.</summary>
    public long? OutputBytes { get; init; }

    /// <summary>Plain-language failure reason, present only when Outcome is Failed.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>Reported after each file finishes, for a progress bar.</summary>
public sealed record ExecutionProgress
{
    public required int Completed { get; init; }

    public required int Total { get; init; }

    public required string CurrentFileName { get; init; }

    /// <summary>The item that just finished, so the caller can mark it in a
    /// queue without waiting for the whole-run <see cref="ExecutionSummary"/>.
    /// Null only for a synthetic "nothing has happened yet" report.</summary>
    public ItemResult? LastResult { get; init; }

    /// <summary>Source bytes belonging to the files finished so far, and the
    /// wall-clock time they took. Together these give a throughput, and the
    /// plan's remaining bytes give an ETA.
    ///
    /// Bytes rather than file count on purpose: a batch of one 40 MB raw and
    /// ninety-nine 200 KB thumbnails is 1% complete by bytes and 1% complete
    /// by count only by coincidence, and users watch the clock, not the
    /// file index.</summary>
    public long CompletedSourceBytes { get; init; }

    public TimeSpan Elapsed { get; init; }
}

/// <summary>The complete outcome of a run: every item's result plus the
/// aggregate numbers the completion summary screen needs.</summary>
public sealed record ExecutionSummary
{
    public required IReadOnlyList<ItemResult> Results { get; init; }

    /// <summary>True when the run was cancelled before every item finished.</summary>
    public bool WasCancelled { get; init; }

    public int TotalCount => Results.Count;

    public int SucceededCount => Results.Count(r => r.Outcome == ItemOutcome.Success);

    public int FailedCount => Results.Count(r => r.Outcome == ItemOutcome.Failed);

    public long TotalSourceBytes => Results.Sum(r => r.SourceBytes);

    public long TotalOutputBytes => Results
        .Where(r => r.Outcome == ItemOutcome.Success)
        .Sum(r => r.OutputBytes ?? 0);

    public IReadOnlyList<ItemResult> Failures => Results
        .Where(r => r.Outcome == ItemOutcome.Failed)
        .ToList();
}
