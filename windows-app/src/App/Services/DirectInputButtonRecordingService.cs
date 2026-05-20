using FieldForce.App.Models;
using Vortice.DirectInput;

namespace FieldForce.App.Services;

public sealed class DirectInputButtonRecordingService : IDisposable
{
    private readonly AppLogService _log;
    private readonly IFfbBackend _backend;
    private readonly IDirectInput8 _directInput;
    private readonly TimeSpan _warmupDuration;
    private readonly Dictionary<string, IDirectInputDevice8> _devices = [];
    private readonly Dictionary<string, bool[]> _previousButtons = [];
    private readonly Dictionary<string, DeviceInfo> _knownDevices = [];
    private readonly HashSet<DirectInputButtonKey> _suppressedButtons = [];
    private DateTimeOffset _armedAt = DateTimeOffset.MaxValue;
    private bool _disposed;

    public DirectInputButtonRecordingService(AppLogService log, IFfbBackend backend)
        : this(log, backend, TimeSpan.FromMilliseconds(350))
    {
    }

    public DirectInputButtonRecordingService(AppLogService log, IFfbBackend backend, TimeSpan warmupDuration)
    {
        _log = log;
        _backend = backend;
        _warmupDuration = warmupDuration;
        _directInput = DInput.DirectInput8Create();
    }

    public void Start(IEnumerable<DeviceInfo> devices)
    {
        Stop();
        _armedAt = DateTimeOffset.UtcNow + _warmupDuration;
        foreach (var deviceInfo in devices)
        {
            _knownDevices[deviceInfo.StableId] = deviceInfo;
            if (_backend.SelectedDevice?.StableId == deviceInfo.StableId)
            {
                if (_backend.TryGetSelectedDeviceButtons(out var selectedButtons))
                {
                    _previousButtons[deviceInfo.StableId] = selectedButtons;
                    SuppressPressedButtons(deviceInfo.StableId, selectedButtons);
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
                var buttons = ReadButtons(device) ?? [];
                _previousButtons[deviceInfo.StableId] = buttons;
                SuppressPressedButtons(deviceInfo.StableId, buttons);
            }
            catch (Exception ex)
            {
                _log.Warning("Could not open DirectInput device for keybind recording: {DeviceName}. {Error}", deviceInfo.DisplayName, ex.Message);
            }
        }
    }

    public DirectInputRecordingPollResult Poll()
    {
        var now = DateTimeOffset.UtcNow;
        var preparing = now < _armedAt;
        var currentByDevice = new Dictionary<string, bool[]>();

        foreach (var deviceInfo in _knownDevices.Values)
        {
            var current = ReadButtons(deviceInfo);
            if (current is null)
            {
                continue;
            }

            currentByDevice[deviceInfo.StableId] = current;
            _previousButtons.TryGetValue(deviceInfo.StableId, out var previous);
            for (var index = 0; index < current.Length; index++)
            {
                var key = new DirectInputButtonKey(deviceInfo.StableId, index);
                if (preparing && current[index])
                {
                    _suppressedButtons.Add(key);
                }

                if (_suppressedButtons.Contains(key) && !current[index])
                {
                    _suppressedButtons.Remove(key);
                }
            }
        }

        if (preparing)
        {
            foreach (var (stableId, current) in currentByDevice)
            {
                _previousButtons[stableId] = current;
            }

            return DirectInputRecordingPollResult.Preparing;
        }

        var anyHeldSuppressed = currentByDevice.Any(pair =>
            pair.Value.Select((pressed, index) => pressed && _suppressedButtons.Contains(new DirectInputButtonKey(pair.Key, index))).Any(held => held));
        if (anyHeldSuppressed)
        {
            foreach (var (stableId, current) in currentByDevice)
            {
                _previousButtons[stableId] = current;
            }

            return DirectInputRecordingPollResult.ReleaseHeldControls;
        }

        foreach (var deviceInfo in _knownDevices.Values)
        {
            if (!currentByDevice.TryGetValue(deviceInfo.StableId, out var current))
            {
                continue;
            }

            _previousButtons.TryGetValue(deviceInfo.StableId, out var previous);
            _previousButtons[deviceInfo.StableId] = current;
            for (var index = 0; index < current.Length; index++)
            {
                var key = new DirectInputButtonKey(deviceInfo.StableId, index);
                if (_suppressedButtons.Contains(key))
                {
                    continue;
                }

                var wasPressed = previous is not null && index < previous.Length && previous[index];
                if (current[index] && !wasPressed)
                {
                    return DirectInputRecordingPollResult.Pressed(new DirectInputButtonPress(deviceInfo, index));
                }
            }
        }

        return DirectInputRecordingPollResult.WaitingForButton;
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
        _suppressedButtons.Clear();
        _armedAt = DateTimeOffset.MaxValue;
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

    private void SuppressPressedButtons(string stableId, bool[] buttons)
    {
        for (var index = 0; index < buttons.Length; index++)
        {
            if (buttons[index])
            {
                _suppressedButtons.Add(new DirectInputButtonKey(stableId, index));
            }
        }
    }
}

public sealed record DirectInputButtonPress(DeviceInfo Device, int ButtonIndex);
public sealed record DirectInputButtonKey(string DeviceStableId, int ButtonIndex);

public enum DirectInputRecordingState
{
    Preparing,
    ReleaseHeldControls,
    WaitingForButton,
    Pressed
}

public sealed record DirectInputRecordingPollResult(DirectInputRecordingState State, DirectInputButtonPress? Press)
{
    public static DirectInputRecordingPollResult Preparing { get; } = new(DirectInputRecordingState.Preparing, null);
    public static DirectInputRecordingPollResult ReleaseHeldControls { get; } = new(DirectInputRecordingState.ReleaseHeldControls, null);
    public static DirectInputRecordingPollResult WaitingForButton { get; } = new(DirectInputRecordingState.WaitingForButton, null);
    public static DirectInputRecordingPollResult Pressed(DirectInputButtonPress press) => new(DirectInputRecordingState.Pressed, press);
}
