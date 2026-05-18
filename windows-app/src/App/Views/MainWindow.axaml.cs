using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.ComponentModel;
using FieldForce.App.ViewModels;

namespace FieldForce.App.Views;

public partial class MainWindow : Window
{
    private EffectOverlayWindow? _effectOverlayWindow;

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
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            SyncEffectOverlayWindow(viewModel);
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            CloseEffectOverlayWindow();
            viewModel.HandleClosing();
            viewModel.Dispose();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.EffectOverlayEnabled) &&
            sender is MainWindowViewModel viewModel)
        {
            SyncEffectOverlayWindow(viewModel);
        }
    }

    private void SyncEffectOverlayWindow(MainWindowViewModel viewModel)
    {
        if (!viewModel.EffectOverlayEnabled)
        {
            _effectOverlayWindow?.Hide();
            return;
        }

        if (_effectOverlayWindow is null)
        {
            _effectOverlayWindow = new EffectOverlayWindow
            {
                DataContext = viewModel
            };
            _effectOverlayWindow.Closed += (_, _) => _effectOverlayWindow = null;
        }

        if (!_effectOverlayWindow.IsVisible)
        {
            _effectOverlayWindow.Show(this);
        }
    }

    private void CloseEffectOverlayWindow()
    {
        if (_effectOverlayWindow is null)
        {
            return;
        }

        var overlay = _effectOverlayWindow;
        _effectOverlayWindow = null;
        overlay.Close();
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

    private async void OnChooseTelemetryFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose FS25 telemetry folder",
            AllowMultiple = false
        });

        var folderPath = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(folderPath) &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetTelemetryFolder(folderPath);
        }
    }

    private void OnEffectCategorySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        CenterSelectedEffectCategory();
    }

    private void OnTireSurfaceScaleKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (sender is not Avalonia.Controls.TextBox textBox)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitTireSurfaceScaleText(textBox);
            Focus();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Back or Key.Delete or Key.Left or Key.Right or Key.Tab or Key.Home or Key.End ||
            e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        if (!IsDigitKey(e.Key))
        {
            e.Handled = true;
        }
    }

    private void OnTireSurfaceScaleTextInput(object? sender, TextInputEventArgs e)
    {
        if (sender is not Avalonia.Controls.TextBox textBox || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        var selectedTextLength = textBox.SelectionEnd - textBox.SelectionStart;
        var proposedLength = (textBox.Text?.Length ?? 0) - selectedTextLength + e.Text.Length;
        if (!e.Text.All(char.IsDigit) || proposedLength > 2)
        {
            e.Handled = true;
        }
    }

    private void OnTireSurfaceScaleLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Avalonia.Controls.TextBox textBox)
        {
            CommitTireSurfaceScaleText(textBox);
        }
    }

    private static void CommitTireSurfaceScaleText(Avalonia.Controls.TextBox textBox)
    {
        var value = int.TryParse(textBox.Text, out var parsed)
            ? Math.Clamp(parsed, 1, 10)
            : 1;
        if (textBox.DataContext is TireSurfaceMatrixRow row &&
            textBox.Tag is string profile)
        {
            row.SetScale(profile, value, notify: true);
        }

        textBox.Text = value.ToString();
    }

    private static bool IsDigitKey(Key key)
    {
        return key is >= Key.D0 and <= Key.D9 or >= Key.NumPad0 and <= Key.NumPad9;
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
