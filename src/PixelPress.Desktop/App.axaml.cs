using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PixelPress.Core.Settings;
using PixelPress.Desktop.Infrastructure;
using PixelPress.Desktop.ViewModels;
using PixelPress.Desktop.Views;

namespace PixelPress.Desktop;

public sealed class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _services = ServiceCollectionExtensions.BuildServices();
            var mainWindowViewModel = _services.GetRequiredService<MainWindowViewModel>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel,
            };

            desktop.ShutdownRequested += (_, _) =>
            {
                _services.GetRequiredService<ISettingsStore>().Save(mainWindowViewModel.ExportSettings());
                _services.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
