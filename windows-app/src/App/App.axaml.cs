using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using FS25FfbBridge.App.Services;
using FS25FfbBridge.App.ViewModels;
using FS25FfbBridge.App.Views;

namespace FS25FfbBridge.App;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            var log = new AppLogService();
            var configStore = new ConfigStore();
            var backend = new DirectInputFfbBackend(log);
            var telemetryReceiver = new TelemetryReceiverService(log);
            var viewModel = new MainWindowViewModel(configStore, backend, telemetryReceiver, log);

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            desktop.ShutdownRequested += (_, _) => viewModel.HandleClosing();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
