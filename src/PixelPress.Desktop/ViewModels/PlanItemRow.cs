using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PixelPress.Core.Formats;

namespace PixelPress.Desktop.ViewModels;

/// <summary>Where one queue row stands in the current run.</summary>
public enum QueueItemStatus
{
    Pending,
    Optimized,
    Failed,
}

/// <summary>
/// One row of the asset queue.
///
/// Mutable, and deliberately reused across re-plans rather than rebuilt.
/// A re-plan fires on every settled slider tick, and it changes the
/// planner's *numbers* for a file — never the file's identity. Rebuilding
/// the row objects each time would re-decode every thumbnail and forget
/// every status, so <see cref="MainWindowViewModel"/> keeps a cache keyed
/// by <see cref="SourcePath"/> and updates these properties in place.
/// </summary>
public sealed partial class PlanItemRow : ObservableObject
{
    public PlanItemRow(string sourcePath)
    {
        SourcePath = sourcePath;
        FileName = Path.GetFileName(sourcePath);
    }

    /// <summary>Identity. Everything else on this row is derived and may be
    /// rewritten by the next plan.</summary>
    public string SourcePath { get; }

    public string FileName { get; }

    /// <summary>e.g. "PNG → WebP", or just "PNG" when the format is kept.</summary>
    [ObservableProperty]
    private string _formatSummary = string.Empty;

    [ObservableProperty]
    private string _sizeText = string.Empty;

    /// <summary>Display name of the output format ("WebP"), for the badge.
    /// The raw <see cref="ImageFormatId"/> would render as "Webp".</summary>
    [ObservableProperty]
    private string _outputFormatName = string.Empty;

    [ObservableProperty]
    private QueueItemStatus _status;

    partial void OnStatusChanged(QueueItemStatus value)
    {
        OnPropertyChanged(nameof(IsOptimized));
        OnPropertyChanged(nameof(IsFailed));
    }

    public bool IsOptimized => Status == QueueItemStatus.Optimized;

    public bool IsFailed => Status == QueueItemStatus.Failed;

    /// <summary>Null unless <see cref="Status"/> is <see cref="QueueItemStatus.Failed"/>.
    /// Shown as the row's tooltip so a failure explains itself in place,
    /// without a trip to the summary.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Decoded lazily off the UI thread, and null for the formats no
    /// platform decoder can read (AVIF, JPEG XL, camera RAW). The row falls
    /// back to a format badge, so a missing thumbnail is a cosmetic
    /// degradation rather than an empty row.</summary>
    [ObservableProperty]
    private Bitmap? _thumbnail;

    public ImageFormatId OutputFormat { get; set; }

    public long SourceBytes { get; set; }

    public long EstimatedOutputBytes { get; set; }
}
