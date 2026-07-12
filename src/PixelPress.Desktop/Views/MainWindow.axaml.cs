using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.VisualTree;
using PixelPress.Core.Formats;
using PixelPress.Desktop.ViewModels;

namespace PixelPress.Desktop.Views;

/// <summary>
/// Code-behind handles the parts of the workspace that are inherently
/// view concepts and carry no engine state: drag-drop and the native
/// file/folder pickers, the theme toggle, and the comparison divider.
///
/// The divider lives here rather than in the view model on purpose. Its
/// position is measured in device pixels against a control's current
/// bounds, means nothing to the planner or the encoder, and must not
/// survive a re-plan — a view model property would make it look like
/// part of the job.
/// </summary>
public sealed partial class MainWindow : Window
{
    private static readonly FilePickerFileType ImageFileType = new("Images")
    {
        Patterns = FormatRegistry.All
            .Where(f => f.CanDecode)
            .SelectMany(f => f.Extensions)
            .Select(ext => $"*.{ext}")
            .ToList(),
    };

    /// <summary>Where the divider sits, as a fraction of the comparison
    /// surface's width. Kept as a ratio rather than a pixel offset so it
    /// holds its place when the window resizes, the zoom changes, or a
    /// differently-shaped image is selected.</summary>
    private double _dividerRatio = 0.5;

    private bool _isDraggingDivider;

    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);

        // The surface resizes whenever the window, the splitters, the zoom,
        // or the selected image changes. Each of those invalidates the
        // divider's pixel position while leaving its ratio correct, so one
        // handler covers all four.
        CompareHost.PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty)
            {
                LayoutDivider();
            }
        };

        HandleLayer.PointerPressed += OnDividerPressed;
        HandleLayer.PointerMoved += OnDividerMoved;
        HandleLayer.PointerReleased += OnDividerReleased;
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    // --- Comparison divider ----------------------------------------------

    private void OnDividerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDraggingDivider = true;
        e.Pointer.Capture(HandleLayer);

        // Pressing anywhere on the surface takes the divider to the pointer,
        // rather than requiring the 28px handle to be hit first. The handle
        // is an affordance, not a hit target.
        SetDividerFromPointer(e);
    }

    private void OnDividerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDraggingDivider)
        {
            SetDividerFromPointer(e);
        }
    }

    private void OnDividerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDraggingDivider = false;
        e.Pointer.Capture(null);
    }

    /// <summary>Converts the pointer's position into a ratio. Coordinates are
    /// taken relative to <c>CompareHost</c>, which sits inside the zoom's
    /// LayoutTransformControl — so the transform is already unwound and the
    /// divider tracks the pointer at any zoom level.</summary>
    private void SetDividerFromPointer(PointerEventArgs e)
    {
        var width = CompareHost.Bounds.Width;
        if (width <= 0)
        {
            return;
        }

        var x = e.GetPosition(CompareHost).X;
        _dividerRatio = Math.Clamp(x / width, 0, 1);
        LayoutDivider();
    }

    /// <summary>Publishes the ratio to the three controls that render it: the
    /// clip band that reveals the optimized image, the divider line, and the
    /// grab handle.</summary>
    private void LayoutDivider()
    {
        var bounds = CompareHost.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var x = bounds.Width * _dividerRatio;

        AfterClip.Width = x;

        Canvas.SetLeft(DividerLine, x - (DividerLine.Width / 2));

        Canvas.SetLeft(DividerHandle, x - (DividerHandle.Width / 2));
        Canvas.SetTop(DividerHandle, (bounds.Height - DividerHandle.Height) / 2);
    }

    // --- Drag and drop -----------------------------------------------------

    // Drag-over feedback: the dashed outline and inner surface pick up the
    // accent while files hover, and revert on leave/drop.
    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            SetDropZoneHighlight(true);
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => SetDropZoneHighlight(false);

    private void SetDropZoneHighlight(bool active)
    {
        // Resolve brushes from the active theme variant so the reset is
        // correct in both light and dark mode; hardcoded colors here would
        // paint light-theme values onto a dark window.
        var outlineBrush = FindThemeBrush(active ? "AccentBrush" : "HairlineBrush");
        var innerBrush = active ? FindThemeBrush("AccentSoftBrush") : Brushes.Transparent;

        if (this.FindControl<Avalonia.Controls.Shapes.Rectangle>("DropOutline") is { } outline)
        {
            outline.Stroke = outlineBrush;
        }

        if (this.FindControl<Border>("DropZoneInner") is { } inner)
        {
            inner.Background = innerBrush;
        }
    }

    private IBrush FindThemeBrush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : Brushes.Transparent;

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        SetDropZoneHighlight(false);

        if (!e.Data.Contains(DataFormats.Files))
        {
            return;
        }

        var items = e.Data.GetFiles();
        if (items is null)
        {
            return;
        }

        var paths = ToLocalPaths(items);
        if (paths.Count > 0)
        {
            await ViewModel.AddPathsAsync(paths);
        }
    }

    private async void OnDropZonePressed(object? sender, PointerPressedEventArgs e)
    {
        // The "Choose files…" button inside the zone handles its own click;
        // without this guard the press would bubble and open two dialogs.
        if (e.Source is Control source &&
            source.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        await OpenFilePickerAsync();
    }

    // --- Input acquisition -------------------------------------------------

    private async void OnAddFilesClick(object? sender, RoutedEventArgs e) =>
        await OpenFilePickerAsync();

    private async Task OpenFilePickerAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose images",
            AllowMultiple = true,
            FileTypeFilter = [ImageFileType],
        });

        var paths = ToLocalPaths(files);
        if (paths.Count > 0)
        {
            await ViewModel.AddPathsAsync(paths);
        }
    }

    private async void OnAddFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a folder",
            AllowMultiple = true,
        });

        var paths = ToLocalPaths(folders);
        if (paths.Count > 0)
        {
            await ViewModel.AddPathsAsync(paths);
        }
    }

    private async void OnChangeOutputFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose output folder",
            AllowMultiple = false,
        });

        var folder = folders.Select(f => f.TryGetLocalPath()).FirstOrDefault(p => p is not null);
        if (folder is not null)
        {
            await ViewModel.ChangeOutputDirectoryAsync(folder);
        }
    }

    // --- Shell -------------------------------------------------------------

    private void OnToggleThemeClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        var goingDark = app.ActualThemeVariant != ThemeVariant.Dark;
        app.RequestedThemeVariant = goingDark ? ThemeVariant.Dark : ThemeVariant.Light;

        ThemeToggle.Content = goingDark ? "☀️  Light" : "🌙  Dark";
    }

    private void OnOpenOutputFolderClick(object? sender, RoutedEventArgs e)
    {
        var path = ViewModel.OutputDirectory;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        // No cross-platform "open in file manager" API exists in .NET; each OS
        // has its own launcher. Errors here (e.g. no file manager in a
        // stripped-down environment) are deliberately swallowed — failing to
        // open a convenience shortcut should never surface as an error.
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", path);
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", path);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Swallowed deliberately; see comment above.
        }
    }

    private static List<string> ToLocalPaths(IEnumerable<IStorageItem> items) =>
        items
            .Select(item => item.TryGetLocalPath())
            .Where(path => path is not null)
            .Select(path => path!)
            .ToList();
}
