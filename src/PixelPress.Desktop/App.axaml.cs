using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
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

            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainWindowViewModel>(),
            };

            desktop.ShutdownRequested += (_, _) => _services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
