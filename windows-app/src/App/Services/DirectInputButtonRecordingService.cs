using FieldForce.App.Models;
using Vortice.DirectInput;

namespace FieldForce.App.Services;

public sealed class DirectInputButtonRecordingService : IDisposable
{
    private readonly AppLogService _log;
    private readonly IFfbBackend _backend;
    private readonly IDirectInput8 _directInput;
    private readonly Dictionary<string, IDirectInputDevice8> _devices = [];
    private readonly Dictionary<string, bool[]> _previousButtons = [];
    private readonly Dictionary<string, DeviceInfo> _knownDevices = [];
    private bool _disposed;

    public DirectInputButtonRecordingService(AppLogService log, IFfbBackend backend)
    {
        _log = log;
        _backend = backend;
        _directInput = DInput.DirectInput8Create();
    }

    public void Start(IEnumerable<DeviceInfo> devices)
    {
        Stop();
        foreach (var deviceInfo in devices)
        {
            _knownDevices[deviceInfo.StableId] = deviceInfo;
            if (_backend.SelectedDevice?.StableId == deviceInfo.StableId)
            {
                if (_backend.TryGetSelectedDeviceButtons(out var selectedButtons))
                {
                    _previousButtons[deviceInfo.StableId] = selectedButtons;
                }

                continue;
            }

            try
            {
                var device = _directInput.CreateDevice(deviceInfo.InstanceGuid);
                device.SetDataFormat<RawJoystickState>();
                device.SetCooperativeLevel(IntPtr.Zero, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                device.Acquire();
                _devices[deviceInfo.StableId] = device;
                _previousButtons[deviceInfo.StableId] = ReadButtons(device) ?? [];
            }
            catch (Exception ex)
            {
                _log.Warning("Could not open DirectInput device for keybind recording: {DeviceName}. {Error}", deviceInfo.DisplayName, ex.Message);
            }
        }
    }

    public DirectInputButtonPress? Poll()
    {
        foreach (var deviceInfo in _knownDevices.Values)
        {
            var current = ReadButtons(deviceInfo);
            if (current is null)
            {
                continue;
            }

            _previousButtons.TryGetValue(deviceInfo.StableId, out var previous);
            _previousButtons[deviceInfo.StableId] = current;
            for (var index = 0; index < current.Length; index++)
            {
                var wasPressed = previous is not null && index < previous.Length && previous[index];
                if (current[index] && !wasPressed)
                {
                    return new DirectInputButtonPress(deviceInfo, index);
                }
            }
        }

        return null;
    }

    public void Stop()
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
                // Best effort during recording cleanup.
            }
        }

        _devices.Clear();
        _previousButtons.Clear();
        _knownDevices.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _directInput.Dispose();
    }

    private bool[]? ReadButtons(DeviceInfo deviceInfo)
    {
        try
        {
            if (_backend.SelectedDevice?.StableId == deviceInfo.StableId)
            {
                return _backend.TryGetSelectedDeviceButtons(out var selectedButtons) ? selectedButtons : null;
            }

            return _devices.TryGetValue(deviceInfo.StableId, out var device) ? ReadButtons(device) : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool[]? ReadButtons(IDirectInputDevice8 device)
    {
        device.Poll();
        return device.GetCurrentJoystickState().Buttons.ToArray();
    }
}

public sealed record DirectInputButtonPress(DeviceInfo Device, int ButtonIndex);
