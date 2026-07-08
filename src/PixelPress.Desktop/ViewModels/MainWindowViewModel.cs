using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelPress.Core.Execution;
using PixelPress.Core.Formats;
using PixelPress.Core.Jobs;
using PixelPress.Core.Planning;
using PixelPress.Core.Presets;
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

/// <summary>
/// View model for the main window. Owns the full workflow: input,
/// planning, plan preview, execution, and the completion summary.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly JobPlanner _planner;
    private readonly JobExecutor _executor;
    private IReadOnlyList<string> _inputPaths = [];
    private JobRequest? _currentRequest;
    private CancellationTokenSource? _runCts;

    public MainWindowViewModel(JobPlanner planner, JobExecutor executor, ISettingsStore settingsStore)
    {
        _planner = planner;
        _executor = executor;
        AvailablePresets = Presets.All;
        AvailableTargetFormats = BuildAvailableTargetFormats();
        SupportedInputSummary = BuildSupportedInputSummary();

        var settings = settingsStore.Load();
        _selectedPreset = Presets.Get(settings.Preset);
        _selectedTargetFormatOption = AvailableTargetFormats.FirstOrDefault(
            f => f.Id == settings.TargetFormat) ?? AvailableTargetFormats[0];
        _stripMetadata = settings.MetadataPolicy == MetadataPolicy.Strip;
        _overwriteOriginals = settings.OutputPolicy == OutputPolicy.OverwriteOriginals;
        _resizeEnabled = settings.ResizeEnabled;
        _resizeMaxDimensionPixels = settings.ResizeMaxDimensionPixels;
    }

    public IReadOnlyList<OptimizationPreset> AvailablePresets { get; }

    [ObservableProperty]
    private OptimizationPreset _selectedPreset;

    /// <summary>Changing the preset only affects the size estimate, but a
    /// re-plan is cheap and keeps the preview honest.</summary>
    partial void OnSelectedPresetChanged(OptimizationPreset value)
    {
        if (Stage == WorkflowStage.PlanReady)
        {
            _ = ReplanAsync();
        }
    }

    // --- Advanced panel --------------------------------------------------

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

    [ObservableProperty]
    private bool _isAdvancedPanelExpanded;

    partial void OnIsAdvancedPanelExpandedChanged(bool value) =>
        OnPropertyChanged(nameof(AdvancedPanelToggleLabel));

    public string AdvancedPanelToggleLabel => IsAdvancedPanelExpanded
        ? "Advanced options  ▾"
        : "Advanced options  ▸";

    /// <summary>Snapshot of the advanced panel's current values, saved on
    /// exit via <see cref="ISettingsStore"/>.</summary>
    public AppSettings ExportSettings() => new()
    {
        Preset = SelectedPreset.Id,
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
        OnPropertyChanged(nameof(HasMoreItems));
        OnPropertyChanged(nameof(MoreItemsNote));
        OnPropertyChanged(nameof(StatusBarText));
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

    /// <summary>One row of the plan's file table.</summary>
    public sealed record PlanItemRow(string FileName, string FormatSummary, string SizeText);

    private const int MaxFileRowsShown = 100;

    /// <summary>File table rows for the plan preview, capped at
    /// <see cref="MaxFileRowsShown"/> so a 500-item batch renders
    /// instantly (ItemsControl does not virtualize).</summary>
    public IReadOnlyList<PlanItemRow> PlanItemRows => CurrentPlan is null
        ? []
        : CurrentPlan.Items
            .Take(MaxFileRowsShown)
            .Select(i => new PlanItemRow(
                Path.GetFileName(i.SourcePath),
                i.SourceFormat == i.OutputFormat
                    ? FormatRegistry.Get(i.SourceFormat).DisplayName
                    : $"{FormatRegistry.Get(i.SourceFormat).DisplayName} → {FormatRegistry.Get(i.OutputFormat).DisplayName}",
                FormatBytes(i.SourceBytes)))
            .ToList();

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

    // --- Running display properties ---------------------------------

    [ObservableProperty]
    private ExecutionProgress? _runProgress;

    partial void OnRunProgressChanged(ExecutionProgress? value)
    {
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(StatusBarText));
    }

    public int ProgressPercent => RunProgress is null || RunProgress.Total == 0
        ? 0
        : (int)(100.0 * RunProgress.Completed / RunProgress.Total);

    public string ProgressLabel => RunProgress is null
        ? string.Empty
        : $"{RunProgress.Completed} of {RunProgress.Total} — {RunProgress.CurrentFileName}";

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
    private void ToggleAdvancedPanel() => IsAdvancedPanelExpanded = !IsAdvancedPanelExpanded;

    [RelayCommand]
    private void StartOver()
    {
        _inputPaths = [];
        _currentRequest = null;
        CurrentPlan = null;
        RunProgress = null;
        RunSummary = null;
        IsCancelling = false;
        StatusMessage = null;
        Stage = WorkflowStage.AwaitingInput;
    }

    private async Task ReplanAsync()
    {
        Stage = WorkflowStage.Planning;
        StatusMessage = null;

        try
        {
            var request = new JobRequest
            {
                InputPaths = _inputPaths,
                OutputDirectory = OutputDirectory,
                Preset = SelectedPreset.Id,
                TargetFormat = SelectedTargetFormatOption.Id,
                MetadataPolicy = StripMetadata ? MetadataPolicy.Strip : MetadataPolicy.Preserve,
                OutputPolicy = OverwriteOriginals ? OutputPolicy.OverwriteOriginals : OutputPolicy.SeparateFolder,
                ResizeEnabled = ResizeEnabled,
                ResizeMaxDimensionPixels = ResizeMaxDimensionPixels,
            };

            CurrentPlan = await Task.Run(() => _planner.CreatePlan(request));
            _currentRequest = request;
            Stage = WorkflowStage.PlanReady;
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

    private static string FormatSavings(long sourceBytes, long outputBytes)
    {
        if (sourceBytes == 0)
        {
            return string.Empty;
        }

        var percent = 100.0 * (1.0 - (double)outputBytes / sourceBytes);

        return percent switch
        {
            >= 1 => $"~{percent:0}% smaller",
            <= -1 => "roughly the same size (some formats need re-encoding)",
            _ => "roughly the same size",
        };
    }

    /// <summary>Disposes the in-flight cancellation token source, if any -
    /// covers the case where the app closes while a run is still active.
    /// Wired to the DI container's own disposal in App.axaml.cs.</summary>
    public void Dispose() => _runCts?.Dispose();
}
