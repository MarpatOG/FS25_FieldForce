using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

    private void OnEffectCategorySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        CenterSelectedEffectCategory();
    }

    private void CenterSelectedEffectCategory()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var selectedTab = EffectCategoryTabs
                .GetVisualDescendants()
                .OfType<TabItem>()
                .FirstOrDefault(tab => tab.IsSelected);

            if (selectedTab is null ||
                EffectCategoryScroller.Viewport.Width <= 0 ||
                EffectCategoryScroller.Extent.Width <= EffectCategoryScroller.Viewport.Width)
            {
                return;
            }

            var point = selectedTab.TranslatePoint(new Avalonia.Point(0, 0), EffectCategoryScroller);
            if (point is null)
            {
                return;
            }

            var targetOffset = EffectCategoryScroller.Offset.X +
                               point.Value.X +
                               (selectedTab.Bounds.Width / 2) -
                               (EffectCategoryScroller.Viewport.Width / 2);
            var maxOffset = Math.Max(0, EffectCategoryScroller.Extent.Width - EffectCategoryScroller.Viewport.Width);
            EffectCategoryScroller.Offset = new Vector(Math.Clamp(targetOffset, 0, maxOffset), EffectCategoryScroller.Offset.Y);
        }, DispatcherPriority.Loaded);
    }
}
