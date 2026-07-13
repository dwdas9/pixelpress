using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PixelPress.Desktop.Infrastructure;

namespace PixelPress.Desktop.Views;

/// <summary>
/// The About dialog. Reads its facts from the assembly rather than repeating
/// them: the version comes from &lt;Version&gt; and the author from
/// &lt;Authors&gt; in Directory.Build.props, so bumping a release cannot leave
/// this box telling the user something false.
/// </summary>
public sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        // InitializeComponent, not AvaloniaXamlLoader.Load: only the generated
        // method wires up the x:Name fields, and without it VersionText is null.
        InitializeComponent();

        VersionText.Text = $"Version {ProjectLinks.AppVersion}";
        AuthorText.Text = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "D Das";
    }

    private void OnRepositoryClick(object? sender, RoutedEventArgs e) =>
        ProjectLinks.Open(ProjectLinks.Repository);

    private void OnDocumentationClick(object? sender, RoutedEventArgs e) =>
        ProjectLinks.Open(ProjectLinks.Documentation);

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
