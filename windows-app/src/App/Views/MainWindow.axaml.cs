using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FS25FfbBridge.App.ViewModels;

namespace FS25FfbBridge.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosing;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            viewModel.InitializeWindowHandle(handle);
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.HandleClosing();
            viewModel.Dispose();
        }
    }

    private void OnKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Pause &&
            e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
            e.KeyModifiers.HasFlag(KeyModifiers.Alt) &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.HandlePanicHotkey();
            e.Handled = true;
        }
    }
}
