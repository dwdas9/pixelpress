using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelPress.Core.Execution;
using PixelPress.Core.Formats;
using PixelPress.Core.Jobs;
using PixelPress.Core.Planning;
using PixelPress.Core.Processing;
using PixelPress.Core.Settings;

namespace PixelPress.Desktop.ViewModels;

/// <summary>The five states the main window cycles through, exactly one
/// visible at a time.</summary>
public enum WorkflowStage
{
    AwaitingInput,
    Planning,
    PlanReady,
    Running,
    Summary,
}

/// <summary>Which rows the queue shows. Earns its keep after a run with
/// failures, when the three bad files are buried among ninety-seven good
/// ones.</summary>
public enum QueueFilter
{
    All,
    Optimized,
    Failed,
}

/// <summary>
/// View model for the main window. Owns the full workflow: input,
/// planning, plan preview, execution, and the completion summary.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly JobPlanner _planner;
    private readonly JobExecutor _executor;
    private readonly IPreviewEncoder _previewEncoder;
    private IReadOnlyList<string> _inputPaths = [];
    private JobRequest? _currentRequest;
    private CancellationTokenSource? _runCts;
    private CancellationTokenSource? _replanDebounceCts;
    private CancellationTokenSource? _previewCts;

    public MainWindowViewModel(
        JobPlanner planner,
        JobExecutor executor,
        IPreviewEncoder previewEncoder,
        ISettingsStore settingsStore)
    {
        _planner = planner;
        _executor = executor;
        _previewEncoder = previewEncoder;
        AvailableTargetFormats = BuildAvailableTargetFormats();
        SupportedInputSummary = BuildSupportedInputSummary();

        var settings = settingsStore.Load();
        _quality = Math.Clamp(settings.Quality, 1, 100);
        _selectedTargetFormatOption = AvailableTargetFormats.FirstOrDefault(
            f => f.Id == settings.TargetFormat) ?? AvailableTargetFormats[0];
        _stripMetadata = settings.MetadataPolicy == MetadataPolicy.Strip;
        _overwriteOriginals = settings.OutputPolicy == OutputPolicy.OverwriteOriginals;
        _resizeEnabled = settings.ResizeEnabled;
        _resizeMaxDimensionPixels = settings.ResizeMaxDimensionPixels;
    }

    // --- Quality ---------------------------------------------------------

    /// <summary>The single lossy quality dial, 1–100 (see ADR-0006). The
    /// slider binds here; moving it re-estimates the batch totals live.</summary>
    [ObservableProperty]
    private int _quality;

    partial void OnQualityChanged(int value)
    {
        OnPropertyChanged(nameof(QualityLabel));
        // The slider fires this rapidly while dragging; debounce so we
        // re-plan once the user settles rather than on every tick.
        if (Stage == WorkflowStage.PlanReady)
        {
            _ = DebouncedReplanAsync();
        }
    }

    /// <summary>Plain-language name for the current quality band, shown
    /// next to the slider. Pure UI — carries no engine state.</summary>
    public string QualityLabel => Quality switch
    {
        <= 45 => "Smaller file",
        <= 70 => "Balanced",
        <= 90 => "High quality",
        _ => "Near-original",
    };

    // --- Controls rail ----------------------------------------------------

    /// <summary>One selectable entry in the format-override list; <see
    /// cref="Id"/> is null for "keep original format."</summary>
    public sealed record FormatOption(string DisplayName, ImageFormatId? Id);

    public IReadOnlyList<FormatOption> AvailableTargetFormats { get; }

    [ObservableProperty]
    private FormatOption _selectedTargetFormatOption;

    partial void OnSelectedTargetFormatOptionChanged(FormatOption value)
    {
        if (Stage == WorkflowStage.PlanReady)
        {
            _ = ReplanAsync();
        }
    }

    [ObservableProperty]
    private bool _stripMetadata;

    partial void OnStripMetadataChanged(bool value)
    {
        if (Stage == WorkflowStage.PlanReady)
        {
            _ = ReplanAsync();
        }
    }

    [ObservableProperty]
    private bool _overwriteOriginals;

    partial void OnOverwriteOriginalsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowOutputFolderControl));
        if (Stage == WorkflowStage.PlanReady)
        {
            _ = ReplanAsync();
        }
    }

    /// <summary>Hides the "Change output folder…" control once the user
    /// chooses to overwrite originals in place — there is no output
    /// folder to change.</summary>
    public bool ShowOutputFolderControl => !OverwriteOriginals;

    [ObservableProperty]
    private bool _resizeEnabled;

    partial void OnResizeEnabledChanged(bool value)
    {
        if (Stage == WorkflowStage.PlanReady)
        {
            _ = ReplanAsync();
        }
    }

    [ObservableProperty]
    private int _resizeMaxDimensionPixels = 2048;

    partial void OnResizeMaxDimensionPixelsChanged(int value)
    {
        if (Stage == WorkflowStage.PlanReady)
        {
            _ = ReplanAsync();
        }
    }

    /// <summary>Snapshot of the controls rail's current values, saved on
    /// exit via <see cref="ISettingsStore"/>.</summary>
    public AppSettings ExportSettings() => new()
    {
        Quality = Quality,
        TargetFormat = SelectedTargetFormatOption.Id,
        MetadataPolicy = StripMetadata ? MetadataPolicy.Strip : MetadataPolicy.Preserve,
        OutputPolicy = OverwriteOriginals ? OutputPolicy.OverwriteOriginals : OutputPolicy.SeparateFolder,
        ResizeEnabled = ResizeEnabled,
        ResizeMaxDimensionPixels = ResizeMaxDimensionPixels,
    };

    private static List<FormatOption> BuildAvailableTargetFormats() =>
        new[] { new FormatOption("Keep original format", null) }
            .Concat(FormatRegistry.EncodableFormats.Select(f => new FormatOption(f.DisplayName, f.Id)))
            .ToList();

    public string SupportedInputSummary { get; }

    [ObservableProperty]
    private string? _statusMessage;

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatusMessage));

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private JobPlan? _currentPlan;

    partial void OnCurrentPlanChanged(JobPlan? value)
    {
        var previouslySelectedPath = SelectedPlanItem?.SourcePath;
        _planItemRows = BuildPlanItemRows(value);

        OnPropertyChanged(nameof(ImageCount));
        OnPropertyChanged(nameof(CanOptimize));
        OnPropertyChanged(nameof(OptimizeButtonLabel));
        OnPropertyChanged(nameof(OriginalSizeText));
        OnPropertyChanged(nameof(EstimatedSizeText));
        OnPropertyChanged(nameof(EstimatedSavingsSummary));
        OnPropertyChanged(nameof(FormatFallbackCount));
        OnPropertyChanged(nameof(HasFormatFallbacks));
        OnPropertyChanged(nameof(FallbackSummary));
        OnPropertyChanged(nameof(RenamedCount));
        OnPropertyChanged(nameof(HasRenames));
        OnPropertyChanged(nameof(RenamedSummary));
        OnPropertyChanged(nameof(SkippedGroups));
        OnPropertyChanged(nameof(HasSkippedFiles));
        OnPropertyChanged(nameof(PlanItemRows));
        OnPropertyChanged(nameof(VisibleQueueRows));
        OnPropertyChanged(nameof(HasMoreItems));
        OnPropertyChanged(nameof(MoreItemsNote));
        OnPropertyChanged(nameof(StatusBarText));
        RaiseQueueCountsChanged();

        // Not awaited: the queue is already on screen with its badges, and
        // the pictures fill in behind it.
        _ = LoadThumbnailsAsync();

        // Keep inspecting the same image across a re-plan; fall back to the
        // first one when it left the batch (or this is a fresh drop).
        _isApplyingPlan = true;
        try
        {
            SelectedPlanItem = _planItemRows.FirstOrDefault(r => r.SourcePath == previouslySelectedPath)
                ?? _planItemRows.FirstOrDefault();
        }
        finally
        {
            _isApplyingPlan = false;
        }

        // An emptied plan (Start Over, or a failed re-plan) leaves nothing to
        // preview. The guard above suppressed the selection's own refresh, so
        // tear the panes down here.
        if (SelectedPlanItem is null)
        {
            _previewCts?.Cancel();
            ClearPreview();
        }
    }

    // --- Stage ------------------------------------------------------------

    [ObservableProperty]
    private WorkflowStage _stage = WorkflowStage.AwaitingInput;

    partial void OnStageChanged(WorkflowStage value)
    {
        OnPropertyChanged(nameof(ShowDropZone));
        OnPropertyChanged(nameof(IsPlanning));
        OnPropertyChanged(nameof(HasPlan));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(HasSummary));
        OnPropertyChanged(nameof(StatusBarText));
    }

    public bool ShowDropZone => Stage == WorkflowStage.AwaitingInput;

    public bool IsPlanning => Stage == WorkflowStage.Planning;

    public bool HasPlan => Stage == WorkflowStage.PlanReady;

    public bool IsRunning => Stage == WorkflowStage.Running;

    public bool HasSummary => Stage == WorkflowStage.Summary;

    // --- Plan preview display properties -----------------------------

    public int ImageCount => CurrentPlan?.Items.Count ?? 0;

    public bool CanOptimize => ImageCount > 0;

    public string OptimizeButtonLabel =>
        $"Optimize {ImageCount} {(ImageCount == 1 ? "image" : "images")}";

    public string OriginalSizeText =>
        CurrentPlan is null ? string.Empty : FormatBytes(CurrentPlan.TotalSourceBytes);

    public string EstimatedSizeText =>
        CurrentPlan is null ? string.Empty : FormatBytes(CurrentPlan.TotalEstimatedOutputBytes);

    public string EstimatedSavingsSummary => CurrentPlan is null
        ? string.Empty
        : FormatSavings(CurrentPlan.TotalSourceBytes, CurrentPlan.TotalEstimatedOutputBytes);

    public int FormatFallbackCount => CurrentPlan?.FormatFallbackCount ?? 0;

    public bool HasFormatFallbacks => FormatFallbackCount > 0;

    public string FallbackSummary => FormatFallbackCount switch
    {
        0 => string.Empty,
        1 => "1 file will convert to JPEG because its original format cannot be saved.",
        _ => $"{FormatFallbackCount} files will convert to JPEG because their original format cannot be saved.",
    };

    public int RenamedCount => CurrentPlan?.RenamedCount ?? 0;

    public bool HasRenames => RenamedCount > 0;

    public string RenamedSummary => RenamedCount switch
    {
        0 => string.Empty,
        1 => "1 file was renamed to avoid overwriting another file.",
        _ => $"{RenamedCount} files were renamed to avoid overwriting another file.",
    };

    public bool HasSkippedFiles => CurrentPlan is { Skipped.Count: > 0 };

    private const int MaxFileRowsShown = 100;

    /// <summary>Live rows, keyed by source path. Survives a re-plan so a
    /// settled slider tick does not re-decode 100 thumbnails or wipe the
    /// statuses of a finished run. Pruned when a file leaves the batch.</summary>
    private readonly Dictionary<string, PlanItemRow> _rowCache = [];

    private IReadOnlyList<PlanItemRow> _planItemRows = [];

    /// <summary>Every row of the current plan, capped at
    /// <see cref="MaxFileRowsShown"/> — the cap bounds the number of
    /// thumbnails decoded, not the batch, which runs in full.</summary>
    public IReadOnlyList<PlanItemRow> PlanItemRows => _planItemRows;

    /// <summary>The rows the queue actually shows, after
    /// <see cref="SelectedQueueFilter"/>. Bound by the view.</summary>
    public IReadOnlyList<PlanItemRow> VisibleQueueRows => SelectedQueueFilter switch
    {
        QueueFilter.Optimized => _planItemRows.Where(r => r.IsOptimized).ToList(),
        QueueFilter.Failed => _planItemRows.Where(r => r.IsFailed).ToList(),
        _ => _planItemRows,
    };

    [ObservableProperty]
    private QueueFilter _selectedQueueFilter = QueueFilter.All;

    partial void OnSelectedQueueFilterChanged(QueueFilter value) =>
        OnPropertyChanged(nameof(VisibleQueueRows));

    public int OptimizedCount => _planItemRows.Count(r => r.IsOptimized);

    public int FailedItemCount => _planItemRows.Count(r => r.IsFailed);

    public int SkippedCount => CurrentPlan?.Skipped.Count ?? 0;

    private void RaiseQueueCountsChanged()
    {
        OnPropertyChanged(nameof(OptimizedCount));
        OnPropertyChanged(nameof(FailedItemCount));
        OnPropertyChanged(nameof(SkippedCount));
    }

    /// <summary>Rebuilds the row list for a new plan, reusing the existing row
    /// object for any file that is still in the batch so its thumbnail and
    /// status carry over. Only the planner-derived fields are rewritten.</summary>
    private List<PlanItemRow> BuildPlanItemRows(JobPlan? plan)
    {
        if (plan is null)
        {
            DisposeRowCache();
            return [];
        }

        var rows = new List<PlanItemRow>(Math.Min(plan.Items.Count, MaxFileRowsShown));

        foreach (var item in plan.Items.Take(MaxFileRowsShown))
        {
            if (!_rowCache.TryGetValue(item.SourcePath, out var row))
            {
                row = new PlanItemRow(item.SourcePath);
                _rowCache[item.SourcePath] = row;
            }

            row.FormatSummary = item.SourceFormat == item.OutputFormat
                ? FormatRegistry.Get(item.SourceFormat).DisplayName
                : $"{FormatRegistry.Get(item.SourceFormat).DisplayName} → {FormatRegistry.Get(item.OutputFormat).DisplayName}";
            row.OutputFormatName = FormatRegistry.Get(item.OutputFormat).DisplayName;
            row.SizeText = FormatBytes(item.SourceBytes);
            row.OutputFormat = item.OutputFormat;
            row.SourceBytes = item.SourceBytes;
            row.EstimatedOutputBytes = item.EstimatedOutputBytes;

            rows.Add(row);
        }

        // Files that left the batch take their thumbnails with them.
        var live = rows.Select(r => r.SourcePath).ToHashSet(StringComparer.Ordinal);
        foreach (var stale in _rowCache.Keys.Where(k => !live.Contains(k)).ToList())
        {
            _rowCache[stale].Thumbnail?.Dispose();
            _rowCache.Remove(stale);
        }

        return rows;
    }

    // --- Thumbnails -------------------------------------------------------

    private const int ThumbnailWidthPixels = 64;

    private CancellationTokenSource? _thumbnailCts;

    /// <summary>Fills in thumbnails one at a time on a background thread,
    /// publishing each as it lands so the queue populates progressively
    /// rather than stalling on the whole batch. Rows that already have one
    /// (carried over from the previous plan) are skipped, so this is a no-op
    /// on a re-plan that changed nothing but the quality.</summary>
    private async Task LoadThumbnailsAsync()
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        var cts = _thumbnailCts = new CancellationTokenSource();
        var token = cts.Token;

        foreach (var row in _planItemRows)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (row.Thumbnail is not null)
            {
                continue;
            }

            var path = row.SourcePath;
            var bitmap = await Task.Run(() => DecodeThumbnail(path), token);

            if (token.IsCancellationRequested)
            {
                bitmap?.Dispose();
                return;
            }

            row.Thumbnail = bitmap;
        }
    }

    /// <summary>Decodes at thumbnail resolution rather than decoding full-size
    /// and scaling down — a 6000×4000 source becomes 64px wide without ever
    /// materialising 24M pixels. Returns null for formats no platform decoder
    /// can read; the row shows a format badge instead.</summary>
    private static Bitmap? DecodeThumbnail(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, ThumbnailWidthPixels);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void DisposeRowCache()
    {
        foreach (var row in _rowCache.Values)
        {
            row.Thumbnail?.Dispose();
        }

        _rowCache.Clear();
    }

    public bool HasMoreItems => ImageCount > MaxFileRowsShown;

    public string MoreItemsNote => HasMoreItems
        ? $"+ {ImageCount - MaxFileRowsShown} more"
        : string.Empty;

    /// <summary>Persistent status bar text, derived from the stage.</summary>
    public string StatusBarText => Stage switch
    {
        WorkflowStage.AwaitingInput => "Ready",
        WorkflowStage.Planning => "Reading files…",
        WorkflowStage.PlanReady => $"{ImageCount} {(ImageCount == 1 ? "image" : "images")} queued",
        WorkflowStage.Running => RunProgress is { } p ? $"Optimizing {p.Completed} of {p.Total}…" : "Optimizing…",
        WorkflowStage.Summary => RunSummary is { } s
            ? (s.WasCancelled ? "Stopped" : $"Done — {s.SucceededCount} optimized")
            : "Done",
        _ => string.Empty,
    };

    public IReadOnlyList<string> SkippedGroups => CurrentPlan is null
        ? []
        : CurrentPlan.Skipped
            .GroupBy(s => s.Reason)
            .Select(g => $"{g.Count()} file{(g.Count() == 1 ? "" : "s")} skipped — {Describe(g.Key)}")
            .ToList();

    private static string Describe(SkipReason reason) => reason switch
    {
        SkipReason.NotAnImage => "not a supported image type",
        SkipReason.Missing => "no longer found where it was dropped from",
        SkipReason.Duplicate => "duplicate of another file already included",
        _ => "skipped",
    };

    // --- Live studio preview (ADR-0006 §4) --------------------------------

    /// <summary>True while <see cref="OnCurrentPlanChanged"/> is swapping in
    /// a new row list. The selection assignment it makes must not kick off
    /// its own preview — <see cref="ReplanAsync"/> already refreshes the
    /// preview once the plan lands, and two encodes of the same image is
    /// pure waste.</summary>
    private bool _isApplyingPlan;

    /// <summary>The one image the studio panes and statistics strip
    /// describe. Everything else on screen is whole-batch.</summary>
    [ObservableProperty]
    private PlanItemRow? _selectedPlanItem;

    partial void OnSelectedPlanItemChanged(PlanItemRow? value)
    {
        if (_isApplyingPlan)
        {
            return;
        }

        _ = RefreshPreviewAsync();
    }

    [ObservableProperty]
    private Bitmap? _originalPreviewImage;

    [ObservableProperty]
    private Bitmap? _outputPreviewImage;

    /// <summary>Source path the "before" pane currently shows, so a quality
    /// change re-encodes the output without re-decoding the original.</summary>
    private string? _originalPreviewPath;

    [ObservableProperty]
    private bool _isPreviewBusy;

    [ObservableProperty]
    private bool _hasPreview;

    [ObservableProperty]
    private string _previewFileName = string.Empty;

    [ObservableProperty]
    private string _previewOriginalSizeText = string.Empty;

    [ObservableProperty]
    private string _previewOutputSizeText = string.Empty;

    [ObservableProperty]
    private string _previewSavingsText = string.Empty;

    [ObservableProperty]
    private string _previewDimensionsText = string.Empty;

    /// <summary>Tells the user whether the numbers beside it are the real
    /// encode or the heuristic. ADR-0006 forbids conflating the two.</summary>
    [ObservableProperty]
    private string _previewAccuracyNote = string.Empty;

    /// <summary>Set when the encode succeeded but no bitmap decoder on this
    /// platform can display the result (AVIF, JPEG XL). The statistics are
    /// still exact — only the picture is missing.</summary>
    [ObservableProperty]
    private string? _outputPreviewUnavailableNote;

    public bool HasOutputPreviewUnavailableNote => !string.IsNullOrEmpty(OutputPreviewUnavailableNote);

    partial void OnOutputPreviewUnavailableNoteChanged(string? value) =>
        OnPropertyChanged(nameof(HasOutputPreviewUnavailableNote));

    /// <summary>Zoom applied to both panes at once. 1.0 fits the image to
    /// the pane; above that the panes scroll.</summary>
    [ObservableProperty]
    private double _zoomFactor = 1.0;

    partial void OnZoomFactorChanged(double value) => OnPropertyChanged(nameof(ZoomLabel));

    public string ZoomLabel => $"{ZoomFactor * 100:0}%";

    [RelayCommand]
    private void ResetZoom() => ZoomFactor = 1.0;

    /// <summary>Encodes the selected image at the current settings and
    /// republishes the panes and statistics. Cancels any encode still in
    /// flight, so dragging the quality slider leaves one winner rather than
    /// letting a stale result overwrite a fresh one.
    ///
    /// Every caller launches this without awaiting, so nothing observes what
    /// it throws. A superseded request is the normal case — <c>Task.Run</c>
    /// faults with <see cref="OperationCanceledException"/> when the newer
    /// request cancels the token before the pool picks the work up — and it
    /// must not reach the UI thread as an unhandled exception.</summary>
    private async Task RefreshPreviewAsync()
    {
        try
        {
            await RefreshPreviewCoreAsync();
        }
        catch (OperationCanceledException)
        {
            // A newer settings change is already encoding. Nothing to do.
        }
    }

    private async Task RefreshPreviewCoreAsync()
    {
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        var cts = _previewCts = new CancellationTokenSource();
        var token = cts.Token;

        if (SelectedPlanItem is not { } row)
        {
            ClearPreview();
            return;
        }

        HasPreview = true;
        PreviewFileName = row.FileName;
        PreviewOriginalSizeText = FormatBytes(row.SourceBytes);

        // The "before" pane depends only on the source file, so it is
        // decoded once per selection rather than once per slider tick.
        if (_originalPreviewPath != row.SourcePath)
        {
            var sourcePath = row.SourcePath;
            var decoded = await Task.Run(() => TryDecodeBitmap(() => new Bitmap(sourcePath)), token);

            if (token.IsCancellationRequested)
            {
                decoded?.Dispose();
                return;
            }

            // Publish before disposing: the old bitmap may still be sitting in
            // the renderer's current batch, and disposing one it is about to
            // draw is a crash, not a leak.
            var previous = OriginalPreviewImage;
            OriginalPreviewImage = decoded;
            previous?.Dispose();
            _originalPreviewPath = row.SourcePath;
        }

        IsPreviewBusy = true;

        var request = new PreviewRequest
        {
            SourcePath = row.SourcePath,
            OutputFormat = row.OutputFormat,
            Quality = Quality,
            MetadataPolicy = StripMetadata ? MetadataPolicy.Strip : MetadataPolicy.Preserve,
            ResizeEnabled = ResizeEnabled,
            ResizeMaxDimensionPixels = ResizeMaxDimensionPixels,
        };

        // Encoding a full-size image blocks for tens of milliseconds or
        // more; awaiting Task.Run keeps the slider smooth and lands the
        // property sets back on the UI thread.
        var result = await Task.Run(() => _previewEncoder.Encode(request), token);

        if (token.IsCancellationRequested)
        {
            return;
        }

        IsPreviewBusy = false;
        ApplyPreviewResult(result, row);
    }

    private void ApplyPreviewResult(PreviewResult? result, PlanItemRow row)
    {
        if (result is null)
        {
            SetOutputPreviewImage(null);

            // Encode failed for this one image. Show the planner's heuristic
            // rather than nothing, and say so.
            OutputPreviewUnavailableNote = "This image couldn't be previewed.";
            PreviewOutputSizeText = FormatBytes(row.EstimatedOutputBytes);
            PreviewSavingsText = FormatSavings(row.SourceBytes, row.EstimatedOutputBytes);
            PreviewDimensionsText = string.Empty;
            PreviewAccuracyNote = "Estimated";
            return;
        }

        var bytes = result.OutputImage;
        SetOutputPreviewImage(TryDecodeBitmap(() => new Bitmap(new MemoryStream(bytes))));
        OutputPreviewUnavailableNote = OutputPreviewImage is null
            ? $"{FormatRegistry.Get(row.OutputFormat).DisplayName} previews aren't supported on this system — the numbers beside it are still exact."
            : null;

        PreviewOutputSizeText = FormatBytes(result.OutputSizeBytes);
        PreviewSavingsText = FormatSavings(result.SourceSizeBytes, result.OutputSizeBytes, approximate: false);
        PreviewDimensionsText = result.WasResized
            ? $"{result.SourceWidth} × {result.SourceHeight}  →  {result.OutputWidth} × {result.OutputHeight}"
            : $"{result.OutputWidth} × {result.OutputHeight}";
        PreviewAccuracyNote = "Exact — this image, encoded";
    }

    /// <summary>Swaps the "after" pane's bitmap, publishing the new one
    /// before releasing the old — see the note in <see cref="RefreshPreviewAsync"/>.</summary>
    private void SetOutputPreviewImage(Bitmap? next)
    {
        var previous = OutputPreviewImage;
        OutputPreviewImage = next;
        previous?.Dispose();
    }

    private void ClearPreview()
    {
        var previousOriginal = OriginalPreviewImage;
        OriginalPreviewImage = null;
        previousOriginal?.Dispose();

        SetOutputPreviewImage(null);
        _originalPreviewPath = null;

        HasPreview = false;
        IsPreviewBusy = false;
        PreviewFileName = string.Empty;
        PreviewOriginalSizeText = string.Empty;
        PreviewOutputSizeText = string.Empty;
        PreviewSavingsText = string.Empty;
        PreviewDimensionsText = string.Empty;
        PreviewAccuracyNote = string.Empty;
        OutputPreviewUnavailableNote = null;
    }

    /// <summary>Bitmap construction throws for formats the platform decoder
    /// cannot read (AVIF, JPEG XL, HEIC, camera RAW) and for unreadable
    /// files. The exception type is the decoder's business, not ours, and
    /// this runs inside a fire-and-forget task where anything that escapes
    /// takes the process down — so every failure degrades to "no picture."
    /// The statistics come from the encoder, not from here, and stay exact
    /// either way.</summary>
    private static Bitmap? TryDecodeBitmap(Func<Bitmap> create)
    {
        try
        {
            return create();
        }
        catch (Exception)
        {
            return null;
        }
    }

    // --- Running display properties ---------------------------------

    [ObservableProperty]
    private ExecutionProgress? _runProgress;

    partial void OnRunProgressChanged(ExecutionProgress? value)
    {
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(StatusBarText));
        OnPropertyChanged(nameof(ThroughputText));
        OnPropertyChanged(nameof(EtaText));
        OnPropertyChanged(nameof(HasRunMetrics));

        // Mark the file that just finished. Rows past MaxFileRowsShown are not
        // cached and simply miss — the batch still runs them, the queue just
        // doesn't show them.
        if (value?.LastResult is not { } result ||
            !_rowCache.TryGetValue(result.SourcePath, out var row))
        {
            return;
        }

        row.Status = result.Outcome == ItemOutcome.Success
            ? QueueItemStatus.Optimized
            : QueueItemStatus.Failed;
        row.ErrorMessage = result.ErrorMessage;

        RaiseQueueCountsChanged();

        if (SelectedQueueFilter != QueueFilter.All)
        {
            OnPropertyChanged(nameof(VisibleQueueRows));
        }
    }

    public int ProgressPercent => RunProgress is null || RunProgress.Total == 0
        ? 0
        : (int)(100.0 * RunProgress.Completed / RunProgress.Total);

    public string ProgressLabel => RunProgress is null
        ? string.Empty
        : $"{RunProgress.Completed} of {RunProgress.Total} — {RunProgress.CurrentFileName}";

    /// <summary>Bytes per second, measured over the whole run so far. Suppressed
    /// for the first half-second: dividing by a near-zero elapsed time produces
    /// a number in the gigabytes that is true, useless, and alarming.</summary>
    private double? BytesPerSecond
    {
        get
        {
            if (RunProgress is not { CompletedSourceBytes: > 0 } p ||
                p.Elapsed.TotalSeconds < 0.5)
            {
                return null;
            }

            return p.CompletedSourceBytes / p.Elapsed.TotalSeconds;
        }
    }

    public bool HasRunMetrics => BytesPerSecond is not null;

    public string ThroughputText =>
        BytesPerSecond is { } rate ? $"{FormatBytes((long)rate)}/s" : string.Empty;

    /// <summary>Estimated time remaining, extrapolated from bytes rather than
    /// file count — a batch of one 40 MB raw and ninety-nine thumbnails is not
    /// 1% done when the first file lands, and users watch the clock.</summary>
    public string EtaText
    {
        get
        {
            if (BytesPerSecond is not { } rate ||
                CurrentPlan is not { } plan ||
                RunProgress is not { } p)
            {
                return string.Empty;
            }

            var remaining = plan.TotalSourceBytes - p.CompletedSourceBytes;
            if (remaining <= 0)
            {
                return string.Empty;
            }

            return FormatDuration(TimeSpan.FromSeconds(remaining / rate));
        }
    }

    private static string FormatDuration(TimeSpan span) => span switch
    {
        { TotalHours: >= 1 } => $"{(int)span.TotalHours}h {span.Minutes}m",
        { TotalMinutes: >= 1 } => $"{span.Minutes}m {span.Seconds}s",
        _ => $"{Math.Max(1, span.Seconds)}s",
    };

    [ObservableProperty]
    private bool _isCancelling;

    partial void OnIsCancellingChanged(bool value)
    {
        OnPropertyChanged(nameof(CancelButtonLabel));
        OnPropertyChanged(nameof(CanCancel));
    }

    public string CancelButtonLabel => IsCancelling ? "Cancelling…" : "Cancel";

    public bool CanCancel => !IsCancelling;

    // --- Summary display properties -----------------------------------

    [ObservableProperty]
    private ExecutionSummary? _runSummary;

    partial void OnRunSummaryChanged(ExecutionSummary? value)
    {
        OnPropertyChanged(nameof(SummaryHeadline));
        OnPropertyChanged(nameof(SummaryOriginalSizeText));
        OnPropertyChanged(nameof(SummaryFinalSizeText));
        OnPropertyChanged(nameof(SummarySavings));
        OnPropertyChanged(nameof(HasFailures));
        OnPropertyChanged(nameof(FailureMessages));
    }

    public string SummaryHeadline
    {
        get
        {
            if (RunSummary is not { } summary)
            {
                return string.Empty;
            }

            if (summary.WasCancelled)
            {
                return $"Stopped — {summary.SucceededCount} of {summary.TotalCount} optimized";
            }

            return summary.FailedCount == 0
                ? $"{summary.SucceededCount} {(summary.SucceededCount == 1 ? "image" : "images")} optimized"
                : $"{summary.SucceededCount} of {summary.TotalCount} images optimized";
        }
    }

    public string SummaryOriginalSizeText =>
        RunSummary is null ? string.Empty : FormatBytes(RunSummary.TotalSourceBytes);

    public string SummaryFinalSizeText =>
        RunSummary is null ? string.Empty : FormatBytes(RunSummary.TotalOutputBytes);

    public string SummarySavings => RunSummary is null
        ? string.Empty
        : FormatSavings(RunSummary.TotalSourceBytes, RunSummary.TotalOutputBytes);

    public bool HasFailures => RunSummary is { FailedCount: > 0 };

    public IReadOnlyList<string> FailureMessages => RunSummary is null
        ? []
        : RunSummary.Failures
            .Select(f => $"{Path.GetFileName(f.SourcePath)} — {f.ErrorMessage}")
            .ToList();

    // --- Input handling -------------------------------------------------

    /// <summary>Called by the view after a drop, file pick, or folder pick.
    /// Replaces any previous batch — starting a new drop while a plan is
    /// showing requires Start Over first, keeping the flow unambiguous.</summary>
    public async Task AddPathsAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        _inputPaths = paths;
        OutputDirectory = ComputeDefaultOutputDirectory(paths);
        await ReplanAsync();
    }

    /// <summary>Called by the view after the user picks a different output
    /// folder from the plan preview screen.</summary>
    public async Task ChangeOutputDirectoryAsync(string newDirectory)
    {
        OutputDirectory = newDirectory;
        if (_inputPaths.Count > 0)
        {
            await ReplanAsync();
        }
    }

    [RelayCommand]
    private void StartOver()
    {
        _thumbnailCts?.Cancel();
        _inputPaths = [];
        _currentRequest = null;
        SelectedQueueFilter = QueueFilter.All;

        // Setting this to null runs BuildPlanItemRows(null), which disposes
        // every cached thumbnail.
        CurrentPlan = null;

        RunProgress = null;
        RunSummary = null;
        IsCancelling = false;
        StatusMessage = null;
        Stage = WorkflowStage.AwaitingInput;
    }

    /// <summary>Re-plans after a short quiet period, cancelling any
    /// re-plan still pending from an earlier slider tick. Keeps the live
    /// estimate responsive without re-scanning on every pixel of drag.
    /// Unlike <see cref="ReplanAsync"/> it stays on the plan-ready screen
    /// (no flash to the "reading files…" state) while settling.</summary>
    private async Task DebouncedReplanAsync()
    {
        _replanDebounceCts?.Cancel();
        _replanDebounceCts?.Dispose();
        var cts = _replanDebounceCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(180, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_inputPaths.Count > 0)
        {
            await ReplanAsync(showPlanningState: false);
        }
    }

    private async Task ReplanAsync(bool showPlanningState = true)
    {
        if (showPlanningState)
        {
            Stage = WorkflowStage.Planning;
        }

        StatusMessage = null;

        try
        {
            var request = new JobRequest
            {
                InputPaths = _inputPaths,
                OutputDirectory = OutputDirectory,
                Quality = Quality,
                TargetFormat = SelectedTargetFormatOption.Id,
                MetadataPolicy = StripMetadata ? MetadataPolicy.Strip : MetadataPolicy.Preserve,
                OutputPolicy = OverwriteOriginals ? OutputPolicy.OverwriteOriginals : OutputPolicy.SeparateFolder,
                ResizeEnabled = ResizeEnabled,
                ResizeMaxDimensionPixels = ResizeMaxDimensionPixels,
            };

            CurrentPlan = await Task.Run(() => _planner.CreatePlan(request));
            _currentRequest = request;
            Stage = WorkflowStage.PlanReady;

            // The plan's inputs are exactly the preview's inputs, so this is
            // the single place the preview is refreshed for a settings change.
            // Not awaited: the batch estimate is already on screen and must
            // not wait behind a full-size encode.
            _ = RefreshPreviewAsync();
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            CurrentPlan = null;
            StatusMessage = "Something went wrong reading those files. Please try again.";
            Stage = WorkflowStage.AwaitingInput;
        }
    }

    // --- Execution --------------------------------------------------------

    [RelayCommand]
    private async Task OptimizeAsync()
    {
        if (CurrentPlan is null || _currentRequest is null)
        {
            return;
        }

        // A re-run reuses the rows, so last run's ticks and crosses have to go
        // before this run starts writing its own.
        foreach (var row in _planItemRows)
        {
            row.Status = QueueItemStatus.Pending;
            row.ErrorMessage = null;
        }

        SelectedQueueFilter = QueueFilter.All;
        RaiseQueueCountsChanged();

        Stage = WorkflowStage.Running;
        IsCancelling = false;
        RunProgress = new ExecutionProgress { Completed = 0, Total = CurrentPlan.Items.Count, CurrentFileName = string.Empty };

        _runCts = new CancellationTokenSource();

        // System.Progress<T> captures the current SynchronizationContext at
        // construction time — since we're still on the UI thread here (the
        // first await hasn't happened yet), reports marshal back to the UI
        // thread automatically, so RunProgress can be set directly below.
        var progress = new Progress<ExecutionProgress>(p => RunProgress = p);

        RunSummary = await _executor.ExecuteAsync(CurrentPlan, _currentRequest, progress, _runCts.Token);

        _runCts.Dispose();
        _runCts = null;
        Stage = WorkflowStage.Summary;
    }

    [RelayCommand]
    private void CancelRun()
    {
        IsCancelling = true;
        _runCts?.Cancel();
    }

    private static string ComputeDefaultOutputDirectory(IReadOnlyList<string> inputs)
    {
        var first = inputs[0];
        var baseDirectory = Directory.Exists(first) ? first : Path.GetDirectoryName(first) ?? first;
        return Path.Combine(baseDirectory, "PixelPress Optimized");
    }

    private static string BuildSupportedInputSummary()
    {
        var names = FormatRegistry.All.Where(f => f.CanDecode).Select(f => f.DisplayName);
        return $"Supports {string.Join(", ", names)}";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{size:0} {units[unitIndex]}" : $"{size:0.#} {units[unitIndex]}";
    }

    /// <param name="approximate">False only for the live preview encode,
    /// whose bytes are measured rather than guessed. ADR-0006 requires the
    /// UI to keep the two apart, and the tilde is how it says so.</param>
    private static string FormatSavings(long sourceBytes, long outputBytes, bool approximate = true)
    {
        if (sourceBytes == 0)
        {
            return string.Empty;
        }

        var percent = 100.0 * (1.0 - (double)outputBytes / sourceBytes);
        var prefix = approximate ? "~" : string.Empty;

        return percent switch
        {
            >= 1 => $"{prefix}{percent:0}% smaller",
            <= -1 => "roughly the same size (some formats need re-encoding)",
            _ => "roughly the same size",
        };
    }

    /// <summary>Disposes the in-flight cancellation token source, if any -
    /// covers the case where the app closes while a run is still active.
    /// Wired to the DI container's own disposal in App.axaml.cs.</summary>
    public void Dispose()
    {
        _runCts?.Dispose();
        _replanDebounceCts?.Dispose();
        _previewCts?.Dispose();
        _thumbnailCts?.Dispose();
        OriginalPreviewImage?.Dispose();
        OutputPreviewImage?.Dispose();
        DisposeRowCache();
    }
}
