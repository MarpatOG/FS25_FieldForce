using System.Runtime.InteropServices;
using System.Windows.Forms;
using FieldForce.App.Models;

namespace FieldForce.App.Services;

public sealed class KeybindDispatcherService : NativeWindow, IDisposable
{
    private const int FirstHotkeyId = 0x2600;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private readonly AppLogService _log;
    private readonly IJoystickButtonPoller _joystickButtonPoller;
    private readonly Dictionary<int, KeybindAction> _registeredHotkeys = [];
    private bool _disposed;

    public KeybindDispatcherService(AppLogService log, IFfbBackend backend)
        : this(log, new DirectInputJoystickButtonPoller(log, backend))
    {
    }

    public KeybindDispatcherService(AppLogService log, IJoystickButtonPoller joystickButtonPoller)
    {
        _log = log;
        _joystickButtonPoller = joystickButtonPoller;
        _joystickButtonPoller.ButtonPressed += OnJoystickButtonPressed;
        CreateHandle(new CreateParams());
    }

    public event Action<KeybindAction>? Pressed;
    public event Action<KeybindAction, string>? StatusChanged;

    public void Apply(KeybindsConfig keybinds)
    {
        UnregisterKeyboardHotkeys();
        var directInputBindings = new Dictionary<KeybindAction, InputBinding>();

        foreach (var action in KeybindsConfig.Actions)
        {
            var binding = keybinds.Get(action);
            if (binding.Kind == InputBindingKind.Keyboard)
            {
                RegisterKeyboardHotkey(action, binding);
            }
            else if (binding.Kind == InputBindingKind.DirectInputButton)
            {
                directInputBindings[action] = binding;
                StatusChanged?.Invoke(action, "Listening");
            }
            else
            {
                StatusChanged?.Invoke(action, "Unassigned");
            }
        }

        _joystickButtonPoller.Apply(directInputBindings);
    }

    internal void DispatchPressedBinding(InputBinding pressedBinding)
    {
        foreach (var (action, binding) in _joystickButtonPoller.Bindings)
        {
            if (binding.Equals(pressedBinding))
            {
                Pressed?.Invoke(action);
                return;
            }
        }
    }

    private void RegisterKeyboardHotkey(KeybindAction action, InputBinding binding)
    {
        var hotkeyId = FirstHotkeyId + (int)action;
        var modifiers = ToNativeModifiers(binding.Modifiers) | ModNoRepeat;
        var virtualKey = (uint)binding.VirtualKey.GetValueOrDefault();
        if (virtualKey == 0)
        {
            StatusChanged?.Invoke(action, "Invalid keyboard bind");
            return;
        }

        if (RegisterHotKey(Handle, hotkeyId, modifiers, virtualKey))
        {
            _registeredHotkeys[hotkeyId] = action;
            StatusChanged?.Invoke(action, "Registered");
            _log.Information("Keybind registered: {Action} = {Binding}", action, binding.DisplayText);
            return;
        }

        var error = Marshal.GetLastWin32Error();
        StatusChanged?.Invoke(action, $"Registration failed ({error})");
        _log.Warning("Keybind registration failed: {Action} = {Binding}, Win32Error={Error}", action, binding.DisplayText, error);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && _registeredHotkeys.TryGetValue(m.WParam.ToInt32(), out var action))
        {
            Pressed?.Invoke(action);
        }

        base.WndProc(ref m);
    }

    private void OnJoystickButtonPressed(KeybindAction action)
    {
        Pressed?.Invoke(action);
    }

    private void UnregisterKeyboardHotkeys()
    {
        foreach (var hotkeyId in _registeredHotkeys.Keys.ToArray())
        {
            UnregisterHotKey(Handle, hotkeyId);
        }

        _registeredHotkeys.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnregisterKeyboardHotkeys();
        _joystickButtonPoller.ButtonPressed -= OnJoystickButtonPressed;
        _joystickButtonPoller.Dispose();
        DestroyHandle();
    }

    private static uint ToNativeModifiers(KeyboardModifiers modifiers)
    {
        uint native = 0;
        if (modifiers.HasFlag(KeyboardModifiers.Alt))
        {
            native |= ModAlt;
        }

        if (modifiers.HasFlag(KeyboardModifiers.Control))
        {
            native |= ModControl;
        }

        if (modifiers.HasFlag(KeyboardModifiers.Shift))
        {
            native |= ModShift;
        }

        if (modifiers.HasFlag(KeyboardModifiers.Windows))
        {
            native |= ModWin;
        }

        return native;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

public interface IJoystickButtonPoller : IDisposable
{
    IReadOnlyDictionary<KeybindAction, InputBinding> Bindings { get; }
    event Action<KeybindAction>? ButtonPressed;
    void Apply(IReadOnlyDictionary<KeybindAction, InputBinding> bindings);
}
