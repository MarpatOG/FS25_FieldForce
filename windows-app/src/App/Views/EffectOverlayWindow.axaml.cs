using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using FieldForce.App.ViewModels;

namespace FieldForce.App.Views;

public partial class EffectOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;

    private bool _positionLoaded;

    public EffectOverlayWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        PositionChanged += (_, _) => SavePosition();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        LoadPosition();
        ApplyWindowStyles();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.EffectOverlayClickThrough))
        {
            ApplyWindowStyles();
        }
    }

    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { EffectOverlayClickThrough: true })
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
            SavePosition();
        }
    }

    private void LoadPosition()
    {
        if (_positionLoaded || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        Position = new PixelPoint(viewModel.EffectOverlayX, viewModel.EffectOverlayY);
        _positionLoaded = true;
    }

    private void SavePosition()
    {
        if (!_positionLoaded || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.SaveEffectOverlayPosition(Position.X, Position.Y);
    }

    private void ApplyWindowStyles()
    {
        if (OperatingSystem.IsWindows())
        {
            var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var clickThrough = DataContext is MainWindowViewModel { EffectOverlayClickThrough: true };
            var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
            style |= WsExLayered | WsExToolWindow;

            if (clickThrough)
            {
                style |= WsExTransparent;
            }
            else
            {
                style &= ~WsExTransparent;
            }

            SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style));
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        base.OnClosed(e);
    }

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
