using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FS25FfbBridge.App.Services;

public sealed class PanicHotkeyService : NativeWindow, IDisposable
{
    private const int HotkeyId = 0x2525;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint VkPause = 0x13;

    private readonly AppLogService _log;
    private bool _registered;

    public PanicHotkeyService(AppLogService log)
    {
        _log = log;
        CreateHandle(new CreateParams());
    }

    public event Action? Pressed;

    public void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = RegisterHotKey(Handle, HotkeyId, ModControl | ModAlt, VkPause);
        if (_registered)
        {
            _log.Information("Panic hotkey registered: Ctrl+Alt+Pause");
        }
        else
        {
            _log.Warning("Panic hotkey registration failed. Window-focused fallback remains active.");
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke();
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(Handle, HotkeyId);
            _registered = false;
        }

        DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
