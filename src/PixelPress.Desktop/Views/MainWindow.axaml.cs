using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.VisualTree;
using PixelPress.Core.Formats;
using PixelPress.Desktop.Infrastructure;
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

        // A right-click must act on the row under the cursor, not on whatever
        // was left-clicked last. ListBox does not select on right-press, so the
        // selection is moved here — tunnelling, to land before the ContextMenu
        // opens and reads the selection.
        QueueList.AddHandler(PointerPressedEvent, OnQueuePointerPressed, RoutingStrategies.Tunnel);
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    // --- Viewport zoom -----------------------------------------------------

    /// <summary>Wheel over the preview zooms, as every image viewer does.
    /// Marked handled so the enclosing ScrollViewer does not also scroll —
    /// the pane would lurch sideways under the cursor otherwise.</summary>
    private void OnViewportWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!ViewModel.HasPreview)
        {
            return;
        }

        ViewModel.NudgeZoom(e.Delta.Y);
        e.Handled = true;
    }

    private void OnQueuePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(QueueList);
        if (!point.Properties.IsRightButtonPressed)
        {
            return;
        }

        if (e.Source is Control source &&
            source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { DataContext: PlanItemRow row })
        {
            ViewModel.SelectedPlanItem = row;
        }
    }

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

    private void OnOpenOutputFolderClick(object? sender, RoutedEventArgs e) =>
        SystemFolders.OpenFolder(ViewModel.OutputDirectory);

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    /// <summary>Help → Supported Formats. Answers the one question the format
    /// registry can answer authoritatively, from the registry itself rather
    /// than a hand-maintained list that would drift out of date.</summary>
    private async void OnSupportedFormatsClick(object? sender, RoutedEventArgs e)
    {
        var decodable = FormatRegistry.All.Where(f => f.CanDecode).Select(f => f.DisplayName);
        var encodable = FormatRegistry.EncodableFormats.Select(f => f.DisplayName);

        var dialog = new Window
        {
            Title = "Supported formats",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Can open:",
                        FontWeight = FontWeight.SemiBold,
                    },
                    new TextBlock
                    {
                        Text = string.Join(", ", decodable),
                        TextWrapping = TextWrapping.Wrap,
                    },
                    new TextBlock
                    {
                        Text = "Can save as:",
                        FontWeight = FontWeight.SemiBold,
                    },
                    new TextBlock
                    {
                        Text = string.Join(", ", encodable),
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        };

        await dialog.ShowDialog(this);
    }

    private static List<string> ToLocalPaths(IEnumerable<IStorageItem> items) =>
        items
            .Select(item => item.TryGetLocalPath())
            .Where(path => path is not null)
            .Select(path => path!)
            .ToList();
}
