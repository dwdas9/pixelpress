using System.Diagnostics;

namespace PixelPress.Desktop.Infrastructure;

/// <summary>
/// Opening a folder in the desktop's file manager. There is no
/// cross-platform .NET API for this — each OS has its own launcher — so it
/// is isolated here rather than smeared across the view and the view model.
///
/// Every failure is swallowed on purpose. This is a convenience shortcut;
/// a machine with no file manager (a stripped-down Linux install, a locked
/// down kiosk) should quietly do nothing, never raise an error about a
/// button that was not the point of the app.
/// </summary>
public static class SystemFolders
{
    public static void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        Launch(
            windows: ("explorer.exe", $"\"{path}\""),
            macos: ("open", path),
            linux: ("xdg-open", path));
    }

    /// <summary>Opens the containing folder with the file itself selected,
    /// where the platform can do that. Linux's xdg-open has no notion of
    /// selecting a file, so it falls back to opening the folder.</summary>
    public static void RevealFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var folder = Path.GetDirectoryName(path);

        Launch(
            windows: ("explorer.exe", $"/select,\"{path}\""),
            macos: ("open", $"-R \"{path}\""),
            linux: ("xdg-open", folder ?? path));
    }

    private static void Launch(
        (string File, string Args) windows,
        (string File, string Args) macos,
        (string File, string Args) linux)
    {
        var command =
            OperatingSystem.IsWindows() ? windows :
            OperatingSystem.IsMacOS() ? macos :
            OperatingSystem.IsLinux() ? linux :
            default;

        if (command.File is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(command.File, command.Args) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Swallowed deliberately; see the class remarks.
        }
    }
}
