using PixelPress.Core.Formats;

namespace PixelPress.Core.Planning;

/// <summary>Why a file was excluded from the plan.</summary>
public enum SkipReason
{
    /// <summary>Extension is not a known image format.</summary>
    NotAnImage,

    /// <summary>Path was provided but no longer exists.</summary>
    Missing,

    /// <summary>Same source file was provided more than once.</summary>
    Duplicate,
}

/// <summary>A file the planner excluded, with the reason — shown calmly
/// in the plan preview, never as an interruption.</summary>
public sealed record SkippedFile(string SourcePath, SkipReason Reason);

/// <summary>One file that will be processed: where it comes from, where
/// it goes, and everything the UI needs to explain the plan honestly.</summary>
public sealed record PlannedItem
{
    public required string SourcePath { get; init; }

    /// <summary>Fully resolved output path, conflicts already resolved.</summary>
    public required string OutputPath { get; init; }

    public required ImageFormatId SourceFormat { get; init; }

    public required ImageFormatId OutputFormat { get; init; }

    public required long SourceBytes { get; init; }

    /// <summary>Rough expected output size; presented as an estimate.</summary>
    public required long EstimatedOutputBytes { get; init; }

    /// <summary>True when the output format differs from the source
    /// because the source is input-only (e.g. HEIC → JPEG), as opposed
    /// to the user explicitly requesting a conversion.</summary>
    public bool FormatFallbackApplied { get; init; }

    /// <summary>True when the output name was auto-suffixed to avoid a
    /// collision with another output or an existing file.</summary>
    public bool RenamedToAvoidConflict { get; init; }

    /// <summary>The plan expects this file to come out *bigger* than it went in.
    ///
    /// Only ever true for a conversion the user explicitly asked for: a pure
    /// re-compression that grows is thrown away by
    /// <see cref="Processing.InflationGuard"/> and the original kept, so it can
    /// never reach the disk. This flag is what lets the UI say so before the run
    /// rather than after it — an "optimizer" that grows a batch by 800 MB must
    /// ask first, and the only place it can ask is here, while the plan is still
    /// just a plan.</summary>
    public bool ExpectedToInflate => EstimatedOutputBytes > SourceBytes;
}

/// <summary>
/// The complete pre-flight plan: everything the run will do, decided
/// before any pixel is touched. The plan preview screen renders this;
/// the executor walks it.
/// </summary>
public sealed record JobPlan
{
    public required IReadOnlyList<PlannedItem> Items { get; init; }

    public required IReadOnlyList<SkippedFile> Skipped { get; init; }

    public long TotalSourceBytes => Items.Sum(i => i.SourceBytes);

    public long TotalEstimatedOutputBytes => Items.Sum(i => i.EstimatedOutputBytes);

    public int FormatFallbackCount => Items.Count(i => i.FormatFallbackApplied);

    public int RenamedCount => Items.Count(i => i.RenamedToAvoidConflict);

    /// <summary>How many files this plan expects to grow.</summary>
    public int InflatingCount => Items.Count(i => i.ExpectedToInflate);

    /// <summary>True when the batch as a whole is expected to end up larger than
    /// it started. The count above can be non-zero on a batch that still nets a
    /// saving (a handful of small PNGs among a thousand JPEGs); this is the figure
    /// that says the run defeats its own purpose.</summary>
    public bool ExpectedToInflate => TotalEstimatedOutputBytes > TotalSourceBytes;

    public bool IsEmpty => Items.Count == 0;
}
