using System.Diagnostics;
using System.Reflection;

namespace PixelPress.Desktop.Infrastructure;

/// <summary>
/// The project's public URLs, and the one way to open them.
///
/// They live here rather than being typed into the Help menu's click handlers
/// so that the repository moving is a one-line change, and so the About dialog
/// and the menu cannot end up pointing at different places.
///
/// Failures are swallowed for the same reason <see cref="SystemFolders"/>
/// swallows its own: a machine with no registered browser should quietly do
/// nothing rather than raise an error about a menu item that was not the point
/// of the app.
/// </summary>
public static class ProjectLinks
{
    public const string Repository = "https://github.com/dwdas9/pixelpress";

    public const string UserGuide = "https://github.com/dwdas9/pixelpress#readme";

    public const string Documentation = "https://github.com/dwdas9/pixelpress/tree/main/docs";

    public const string Issues = "https://github.com/dwdas9/pixelpress/issues";

    /// <summary>The version the assembly was actually built with, rather than a
    /// string typed into the About box that drifts a release behind. Comes from
    /// &lt;Version&gt; in Directory.Build.props.</summary>
    public static string AppVersion
    {
        get
        {
            var informational = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (string.IsNullOrWhiteSpace(informational))
            {
                return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
            }

            // The SDK appends the source-revision id ("0.1.0+abc123"); the build
            // metadata is noise in a dialog aimed at users.
            var plus = informational.IndexOf('+');
            return plus < 0 ? informational : informational[..plus];
        }
    }

    public static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Swallowed deliberately; see the class remarks.
        }
    }
}
