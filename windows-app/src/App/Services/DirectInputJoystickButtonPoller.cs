using FieldForce.App.Models;
using Vortice.DirectInput;

namespace FieldForce.App.Services;

public sealed class DirectInputJoystickButtonPoller : IJoystickButtonPoller
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(25);
    private readonly AppLogService _log;
    private readonly IFfbBackend _backend;
    private readonly IDirectInput8 _directInput;
    private readonly object _lock = new();
    private readonly Dictionary<KeybindAction, InputBinding> _bindings = [];
    private readonly Dictionary<string, IDirectInputDevice8> _devices = [];
    private readonly Dictionary<string, bool[]> _previousButtons = [];
    private System.Threading.Timer? _timer;
    private bool _disposed;

    public DirectInputJoystickButtonPoller(AppLogService log, IFfbBackend backend)
    {
        _log = log;
        _backend = backend;
        _directInput = DInput.DirectInput8Create();
    }

    public IReadOnlyDictionary<KeybindAction, InputBinding> Bindings => _bindings;
    public event Action<KeybindAction>? ButtonPressed;

    public void Apply(IReadOnlyDictionary<KeybindAction, InputBinding> bindings)
    {
        lock (_lock)
        {
            _bindings.Clear();
            foreach (var (action, binding) in bindings)
            {
                _bindings[action] = binding;
            }

            ResetDevices();
            if (_bindings.Count == 0)
            {
                _timer?.Dispose();
                _timer = null;
                return;
            }

            OpenUnselectedDevices();
            _timer ??= new System.Threading.Timer(_ => Poll(), null, PollInterval, PollInterval);
        }
    }

    private void OpenUnselectedDevices()
    {
        foreach (var binding in _bindings.Values)
        {
            if (string.IsNullOrWhiteSpace(binding.DeviceStableId) ||
                _backend.SelectedDevice?.StableId == binding.DeviceStableId ||
                _devices.ContainsKey(binding.DeviceStableId))
            {
                continue;
            }

            var instance = _directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly)
                .FirstOrDefault(device => BuildStableId(device) == binding.DeviceStableId);
            if (instance is null || instance.InstanceGuid == Guid.Empty)
            {
                _log.Warning("Keybind DirectInput device not found: {DeviceName} ({StableId})", binding.DeviceName, binding.DeviceStableId);
                continue;
            }

            try
            {
                var device = _directInput.CreateDevice(instance.InstanceGuid);
                device.SetDataFormat<RawJoystickState>();
                device.SetCooperativeLevel(IntPtr.Zero, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                device.Acquire();
                _devices[binding.DeviceStableId] = device;
            }
            catch (Exception ex)
            {
                _log.Warning("Could not open DirectInput keybind device {DeviceName}: {Error}", binding.DeviceName, ex.Message);
            }
        }
    }

    private void Poll()
    {
        List<KeybindAction> fired = [];
        lock (_lock)
        {
            foreach (var group in _bindings.GroupBy(pair => pair.Value.DeviceStableId ?? ""))
            {
                if (string.IsNullOrWhiteSpace(group.Key))
                {
                    continue;
                }

                var current = ReadButtons(group.Key);
                if (current is null)
                {
                    continue;
                }

                _previousButtons.TryGetValue(group.Key, out var previous);
                foreach (var (action, binding) in group)
                {
                    var index = binding.ButtonIndex.GetValueOrDefault(-1);
                    if (index < 0 || index >= current.Length)
                    {
                        continue;
                    }

                    var wasPressed = previous is not null && index < previous.Length && previous[index];
                    if (current[index] && !wasPressed)
                    {
                        fired.Add(action);
                    }
                }

                _previousButtons[group.Key] = current;
            }
        }

        foreach (var action in fired)
        {
            ButtonPressed?.Invoke(action);
        }
    }

    private bool[]? ReadButtons(string stableId)
    {
        try
        {
            if (_backend.SelectedDevice?.StableId == stableId &&
                _backend.TryGetSelectedDeviceButtons(out var selectedButtons))
            {
                return selectedButtons;
            }

            if (!_devices.TryGetValue(stableId, out var device))
            {
                return null;
            }

            device.Poll();
            var state = device.GetCurrentJoystickState();
            return state.Buttons.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private void ResetDevices()
    {
        foreach (var device in _devices.Values)
        {
            try
            {
                device.Unacquire();
                device.Dispose();
            }
            catch
            {
                // Best effort during keybind reconfiguration.
            }
        }

        _devices.Clear();
        _previousButtons.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer?.Dispose();
        ResetDevices();
        _directInput.Dispose();
    }

    private static string BuildStableId(DeviceInstance instance)
    {
        var product = string.IsNullOrWhiteSpace(instance.ProductName) ? "unknown" : instance.ProductName.Trim();
        return $"{instance.ProductGuid:N}:{instance.InstanceGuid:N}:{product}";
    }
}
