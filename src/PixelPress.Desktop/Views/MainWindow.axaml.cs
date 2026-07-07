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
/// Code-behind handles the parts of input acquisition that are
/// inherently platform/UI concepts — drag-drop, native file and folder
/// pickers — then hands plain local paths to the view model. Keeping
/// this here (rather than behind an injected service) avoids an
/// abstraction with only one implementation and one caller.
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
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    // Drag-over visual feedback: the dashed outline and inner surface
    // pick up the accent while files hover, and revert on leave/drop.
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
        // correct in both light and dark mode; hardcoded colors here
        // would paint light-theme values onto a dark window.
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
        // Buttons inside the drop zone handle their own clicks; only open
        // the picker when the press landed on the zone itself, otherwise
        // a button click would open two dialogs.
        if (e.Source is Control source &&
            source.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        await OpenFilePickerAsync();
    }

    private async void OnDropZonePressedButton(object? sender, RoutedEventArgs e) =>
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

    private void OnToggleThemeClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        var goingDark = app.ActualThemeVariant != ThemeVariant.Dark;
        app.RequestedThemeVariant = goingDark ? ThemeVariant.Dark : ThemeVariant.Light;

        if (this.FindControl<Button>("ThemeToggle") is { } toggle)
        {
            toggle.Content = goingDark ? "☀️  Light" : "🌙  Dark";
        }
    }

    private async void OnBrowseFolderClick(object? sender, RoutedEventArgs e)
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

    private void OnOpenOutputFolderClick(object? sender, RoutedEventArgs e)
    {
        var path = ViewModel.OutputDirectory;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        // No cross-platform "open in file manager" API exists in .NET;
        // each OS has its own launcher for this. Errors here (e.g. no
        // file manager available in a stripped-down environment) are
        // deliberately swallowed — failing to open a convenience shortcut
        // should never surface as an error to the user.
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
