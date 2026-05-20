using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.ComponentModel;
using FieldForce.App.Models;
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
        if (DataContext is MainWindowViewModel viewModel &&
            TryGetVirtualKey(e.Key, out var virtualKey) &&
            viewModel.HandleKeybindRecordingKeyboard(virtualKey, ToKeybindModifiers(e.KeyModifiers)))
        {
            e.Handled = true;
        }
    }

    private static KeyboardModifiers ToKeybindModifiers(KeyModifiers modifiers)
    {
        var result = KeyboardModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            result |= KeyboardModifiers.Control;
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            result |= KeyboardModifiers.Alt;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            result |= KeyboardModifiers.Shift;
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            result |= KeyboardModifiers.Windows;
        }

        return result;
    }

    private static bool TryGetVirtualKey(Key key, out int virtualKey)
    {
        virtualKey = key switch
        {
            Key.Back => 0x08,
            Key.Tab => 0x09,
            Key.Enter => 0x0D,
            Key.Pause => 0x13,
            Key.Escape => 0x1B,
            Key.Space => 0x20,
            Key.PageUp => 0x21,
            Key.PageDown => 0x22,
            Key.End => 0x23,
            Key.Home => 0x24,
            Key.Left => 0x25,
            Key.Up => 0x26,
            Key.Right => 0x27,
            Key.Down => 0x28,
            Key.Insert => 0x2D,
            Key.Delete => 0x2E,
            >= Key.D0 and <= Key.D9 => 0x30 + (key - Key.D0),
            >= Key.A and <= Key.Z => 0x41 + (key - Key.A),
            >= Key.NumPad0 and <= Key.NumPad9 => 0x60 + (key - Key.NumPad0),
            Key.Multiply => 0x6A,
            Key.Add => 0x6B,
            Key.Subtract => 0x6D,
            Key.Decimal => 0x6E,
            Key.Divide => 0x6F,
            >= Key.F1 and <= Key.F24 => 0x70 + (key - Key.F1),
            Key.OemSemicolon => 0xBA,
            Key.OemPlus => 0xBB,
            Key.OemComma => 0xBC,
            Key.OemMinus => 0xBD,
            Key.OemPeriod => 0xBE,
            Key.OemQuestion => 0xBF,
            Key.OemTilde => 0xC0,
            Key.OemOpenBrackets => 0xDB,
            Key.OemPipe => 0xDC,
            Key.OemCloseBrackets => 0xDD,
            Key.OemQuotes => 0xDE,
            _ => 0
        };

        return virtualKey != 0;
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
