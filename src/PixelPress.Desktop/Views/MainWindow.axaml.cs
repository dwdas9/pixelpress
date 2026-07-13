using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PixelPress.Core.Formats;
using PixelPress.Desktop.Infrastructure;
using PixelPress.Desktop.ViewModels;

namespace PixelPress.Desktop.Views;

/// <summary>
/// Code-behind handles the parts of the workspace that are inherently
/// view concepts and carry no engine state: drag-drop and the native
/// file/folder pickers, the theme toggle, and the viewer controls.
///
/// The comparison surface's own geometry — the wipe position, the zoom, the
/// pan — lives inside <see cref="CompareView"/> rather than here or in the
/// view model. It is measured in device pixels against that control's bounds,
/// means nothing to the planner or the encoder, and must not survive a
/// re-plan; a view-model property would make it look like part of the job.
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

    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);

        // A right-click must act on the row under the cursor, not on whatever
        // was left-clicked last. ListBox does not select on right-press, so the
        // selection is moved here — tunnelling, to land before the ContextMenu
        // opens and reads the selection.
        QueueList.AddHandler(PointerPressedEvent, OnQueuePointerPressed, RoutingStrategies.Tunnel);

        SyncThemeToggle();

        // The view model decides *whether* to ask; the window is the only thing
        // that can actually ask. DataContext is set by the DI container after
        // construction, so the hook is attached when it arrives.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.ConfirmInflatingRunAsync = ConfirmInflatingRunAsync;
            }
        };
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    /// <summary>The last thing standing between "Optimize 354 images" and 354
    /// files written larger than they started.
    ///
    /// Deliberately not a toast, not a banner, and not defaulted to Continue: the
    /// button that opens this says "Optimize", the run does the opposite, and the
    /// user has to actively agree to that before a single byte is written.</summary>
    private async Task<bool> ConfirmInflatingRunAsync(string explanation)
    {
        var confirmed = false;

        var proceed = new Button { Content = "Convert anyway" };
        proceed.Classes.Add("subtle");

        var cancel = new Button { Content = "Cancel" };
        cancel.Classes.Add("primary");

        var dialog = new Window
        {
            Title = "This will not save space",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = FindThemeBrush("CanvasBrush"),
            Content = new StackPanel
            {
                Margin = new Thickness(22),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = explanation,
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { proceed, cancel },
                    },
                },
            },
        };

        proceed.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Close();
        };

        cancel.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);

        return confirmed;
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

    // --- Viewer controls ---------------------------------------------------

    // The toolbar and the View menu are two doors into the same four operations,
    // so they share handlers rather than each owning a copy of the arithmetic —
    // which lives in CompareView, where the bounds it depends on actually are.

    private void OnZoomInClick(object? sender, RoutedEventArgs e) => Compare.ZoomIn();

    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => Compare.ZoomOut();

    private void OnFitToWindowClick(object? sender, RoutedEventArgs e) => Compare.FitToWindow();

    private void OnActualSizeClick(object? sender, RoutedEventArgs e) => Compare.ActualSize();

    private void OnResetViewClick(object? sender, RoutedEventArgs e) => Compare.ResetView();

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

        SyncThemeToggle();
    }

    /// <summary>Points the toggle at the theme it will switch *to*, which is the
    /// only reading of a one-button theme switch that people agree on: a sun
    /// while dark, a moon while light.
    ///
    /// Icon rather than the old "🌙  Dark" label — an emoji is drawn by the
    /// platform's colour font, ignores Foreground, and renders differently on
    /// Windows and macOS, so it could not be made to match the rest of the
    /// chrome.</summary>
    private void SyncThemeToggle()
    {
        var isDark = (Application.Current?.ActualThemeVariant ?? ActualThemeVariant) == ThemeVariant.Dark;

        ThemeToggleIcon.Data = FindGeometry(isDark ? "IconSun" : "IconMoon");
        ToolTip.SetTip(ThemeToggle, isDark ? "Switch to light theme" : "Switch to dark theme");
    }

    private Geometry? FindGeometry(string key) =>
        this.TryFindResource(key, out var value) ? value as Geometry : null;

    private void OnOpenOutputFolderClick(object? sender, RoutedEventArgs e) =>
        SystemFolders.OpenFolder(ViewModel.OutputDirectory);

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    // --- Help ---------------------------------------------------------------

    private void OnUserGuideClick(object? sender, RoutedEventArgs e) =>
        ProjectLinks.Open(ProjectLinks.UserGuide);

    private void OnProjectWebsiteClick(object? sender, RoutedEventArgs e) =>
        ProjectLinks.Open(ProjectLinks.Repository);

    private void OnReportIssueClick(object? sender, RoutedEventArgs e) =>
        ProjectLinks.Open(ProjectLinks.Issues);

    private async void OnAboutClick(object? sender, RoutedEventArgs e) =>
        await new AboutWindow().ShowDialog(this);

    /// <summary>Help → Keyboard Shortcuts. The list is short on purpose: it holds
    /// every gesture the app actually binds, and nothing it doesn't. The mouse
    /// gestures on the comparison surface are listed alongside them — they are
    /// unlabelled on screen, so this dialog is the only place they are written
    /// down.</summary>
    private async void OnKeyboardShortcutsClick(object? sender, RoutedEventArgs e)
    {
        (string Keys, string Action)[] shortcuts =
        [
            ("Ctrl+O", "Add files to the queue"),
            ("Ctrl+Shift+O", "Add a folder to the queue"),
            ("Ctrl+Enter", "Optimize the batch"),
            ("Delete", "Remove the selected image from the queue"),
            ("Ctrl+plus / Ctrl+minus", "Zoom the preview in and out"),
            ("Ctrl+0", "Fit the image to the window"),
            ("Ctrl+1", "Actual size (100%)"),
            ("Scroll wheel", "Zoom about the pointer"),
            ("Drag", "Pan a zoomed image"),
            ("Drag the divider", "Wipe between original and optimized"),
            ("Double-click", "Toggle between fit and 100%"),
        ];

        var rows = new StackPanel { Spacing = 8 };

        foreach (var (keys, action) in shortcuts)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("140,*"),
            };

            var keyText = new TextBlock { Text = keys, FontWeight = FontWeight.SemiBold, FontSize = 12 };
            var actionText = new TextBlock { Text = action, FontSize = 12, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(actionText, 1);

            row.Children.Add(keyText);
            row.Children.Add(actionText);
            rows.Children.Add(row);
        }

        await ShowInfoDialogAsync("Keyboard shortcuts", rows);
    }

    /// <summary>Help → Supported Formats. Answers the one question the format
    /// registry can answer authoritatively, from the registry itself rather
    /// than a hand-maintained list that would drift out of date.</summary>
    private async void OnSupportedFormatsClick(object? sender, RoutedEventArgs e)
    {
        var decodable = FormatRegistry.All.Where(f => f.CanDecode).Select(f => f.DisplayName);
        var encodable = FormatRegistry.EncodableFormats.Select(f => f.DisplayName);

        var body = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = "Can open:", FontWeight = FontWeight.SemiBold, FontSize = 12 },
                new TextBlock { Text = string.Join(", ", decodable), TextWrapping = TextWrapping.Wrap, FontSize = 12 },
                new TextBlock { Text = "Can save as:", FontWeight = FontWeight.SemiBold, FontSize = 12 },
                new TextBlock { Text = string.Join(", ", encodable), TextWrapping = TextWrapping.Wrap, FontSize = 12 },
            },
        };

        await ShowInfoDialogAsync("Supported formats", body);
    }

    /// <summary>The shared shell for the small Help dialogs, so they cannot
    /// drift apart in size, padding or theming. Anything with buttons and its
    /// own state earns a real window instead — see <see cref="AboutWindow"/>.</summary>
    private async Task ShowInfoDialogAsync(string title, Control body)
    {
        var close = new Button
        {
            Content = "Close",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        close.Classes.Add("subtle");

        var dialog = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = FindThemeBrush("CanvasBrush"),
            Content = new StackPanel
            {
                Margin = new Thickness(22),
                Spacing = 16,
                Children = { body, close },
            },
        };

        close.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
    }

    private static List<string> ToLocalPaths(IEnumerable<IStorageItem> items) =>
        items
            .Select(item => item.TryGetLocalPath())
            .Where(path => path is not null)
            .Select(path => path!)
            .ToList();
}
