using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FS25FfbBridge.App.Models;
using FS25FfbBridge.App.Services;

namespace FS25FfbBridge.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ConfigStore _configStore;
    private readonly IFfbBackend _backend;
    private readonly TelemetryReceiverService _telemetryReceiver;
    private readonly GameplayFfbController _gameplayFfb;
    private readonly SafetyManager _safety;
    private readonly AppLogService _log;
    private readonly PanicHotkeyService _panicHotkey;
    private AppConfig _config;
    private IntPtr _windowHandle;
    private bool _disposed;

    [ObservableProperty]
    private DeviceInfo? _selectedDevice;

    [ObservableProperty]
    private string _selectedDeviceStatus = "No FFB device selected";

    [ObservableProperty]
    private string _deviceCapabilityStatus = "Scan devices to inspect FFB support";

    [ObservableProperty]
    private int _globalForceLimitPercent;

    [ObservableProperty]
    private int _deviceForceLimitPercent;

    [ObservableProperty]
    private string _configPath;

    [ObservableProperty]
    private string _logPath;

    [ObservableProperty]
    private string _backendStatus = "DirectInput backend idle";

    [ObservableProperty]
    private string _telemetryStatus = "Waiting";

    [ObservableProperty]
    private string _telemetryEndpoint = "127.0.0.1:34325";

    [ObservableProperty]
    private string _packetRate = "0 pkt/s";

    [ObservableProperty]
    private string _udpStatus = "Not started";

    [ObservableProperty]
    private string _fileStatus = "Not started";

    [ObservableProperty]
    private string _lastPacketSource = "none";

    [ObservableProperty]
    private string _lastTransportError = "none";

    [ObservableProperty]
    private string _lastPacketAge = "none";

    [ObservableProperty]
    private string _currentVehicle = "No vehicle";

    [ObservableProperty]
    private string _vehicleType = "Unknown";

    [ObservableProperty]
    private string _speedKmh = "-";

    [ObservableProperty]
    private string _steeringAngle = "-";

    [ObservableProperty]
    private string _rpm = "-";

    [ObservableProperty]
    private string _engineStatus = "-";

    [ObservableProperty]
    private string _mass = "-";

    [ObservableProperty]
    private string _totalMass = "-";

    [ObservableProperty]
    private string _massAndTotal = "- / -";

    [ObservableProperty]
    private string _isOnField = "-";

    [ObservableProperty]
    private string _gameState = "-";

    [ObservableProperty]
    private string _rawTelemetryPreview = "";

    [ObservableProperty]
    private string _telemetryParseStatus = "No packets yet";

    public MainWindowViewModel()
        : this(new ConfigStore(), null, null, null)
    {
    }

    public MainWindowViewModel(ConfigStore configStore, IFfbBackend? backend, TelemetryReceiverService? telemetryReceiver, AppLogService? log)
    {
        _configStore = configStore;
        _log = log ?? new AppLogService();
        _backend = backend ?? new DirectInputFfbBackend(_log);
        _telemetryReceiver = telemetryReceiver ?? new TelemetryReceiverService(_log);
        _safety = new SafetyManager(_backend, _log);
        _panicHotkey = new PanicHotkeyService(_log);
        _panicHotkey.Pressed += HandlePanicHotkey;
        _panicHotkey.Register();
        _config = _configStore.Load();
        _globalForceLimitPercent = _config.GlobalForceLimitPercent;
        _deviceForceLimitPercent = _config.DeviceForceLimitPercent;
        _configPath = _configStore.ConfigPath;
        _logPath = _log.LogPath;
        Logs = _log.Entries;

        _backend.UpdateForceLimits(GlobalForceLimitPercent, DeviceForceLimitPercent);
        _telemetryReceiver.StateChanged += OnTelemetryStateChanged;
        _telemetryReceiver.Start(_config.TelemetryHost, _config.TelemetryPort, _config.TelemetryLostTimeoutMs, _config.TelemetryFilePath);
        _gameplayFfb = new GameplayFfbController(_telemetryReceiver, _backend, _log);
        _log.Information("Application initialized. Config={ConfigPath}", ConfigPath);
    }

    public ObservableCollection<DeviceInfo> Devices { get; } = [];
    public ObservableCollection<string> Logs { get; }
    public string PanicHotkey => _config.PanicHotkey;
    public bool CanRunEffects => SelectedDevice?.IsForceFeedbackCapable == true && _backend.HasSelectedFfbDevice;
    public string DeviceCountText => $"{Devices.Count} device(s)";
    public string FfbReadyText => CanRunEffects ? "FFB ready" : "FFB inactive";
    public string TelemetryReadyText => TelemetryStatus;

    public void InitializeWindowHandle(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        ScanDevices();
    }

    public void HandlePanicHotkey()
    {
        _safety.OnPanicHotkey();
        BackendStatus = "Emergency stop active";
        StopAllEffectsCommand.NotifyCanExecuteChanged();
    }

    public void HandleClosing()
    {
        _safety.OnAppClosing();
    }

    partial void OnSelectedDeviceChanged(DeviceInfo? value)
    {
        SelectDevice(value);
    }

    partial void OnGlobalForceLimitPercentChanged(int value)
    {
        GlobalForceLimitPercent = Math.Clamp(value, 0, 100);
        SaveForceLimits();
    }

    partial void OnDeviceForceLimitPercentChanged(int value)
    {
        DeviceForceLimitPercent = Math.Clamp(value, 0, 100);
        SaveForceLimits();
    }

    [RelayCommand]
    private void ScanDevices()
    {
        var previousStableId = SelectedDevice?.StableId ?? _config.SelectedDeviceStableId;
        var scanned = _backend.ScanDevices();

        Devices.Clear();
        foreach (var device in scanned)
        {
            Devices.Add(device);
        }

        OnPropertyChanged(nameof(DeviceCountText));

        var persisted = Devices.FirstOrDefault(device => device.StableId == previousStableId);
        if (persisted is not null)
        {
            SelectedDevice = persisted;
        }
        else
        {
            if (_backend.HasSelectedFfbDevice)
            {
                _safety.OnDeviceDisconnect();
            }

            SelectedDevice = null;
            SelectedDeviceStatus = string.IsNullOrWhiteSpace(previousStableId)
                ? "No FFB device selected"
                : "Saved device is not connected";
            DeviceCapabilityStatus = Devices.Count == 0
                ? "No DirectInput game-control devices found"
                : "Select a force feedback device";
            BackendStatus = "Waiting for device";
        }

        RefreshCommandStates();
    }

    [RelayCommand(CanExecute = nameof(CanRunEffects))]
    private void SpringTest() => StartEffect(FfbEffectKind.Spring);

    [RelayCommand(CanExecute = nameof(CanRunEffects))]
    private void DamperTest() => StartEffect(FfbEffectKind.Damper);

    [RelayCommand(CanExecute = nameof(CanRunEffects))]
    private void ConstantLeft() => StartEffect(FfbEffectKind.ConstantLeft);

    [RelayCommand(CanExecute = nameof(CanRunEffects))]
    private void ConstantRight() => StartEffect(FfbEffectKind.ConstantRight);

    [RelayCommand(CanExecute = nameof(CanRunEffects))]
    private void LowVibration() => StartEffect(FfbEffectKind.LowVibration);

    [RelayCommand]
    private void StopAllEffects()
    {
        _safety.StopAll("user stop");
        BackendStatus = "All effects stopped";
    }

    private void StartEffect(FfbEffectKind kind)
    {
        _safety.StartTestEffect(kind);
        BackendStatus = $"{kind} test running";
    }

    private void SelectDevice(DeviceInfo? device)
    {
        if (device is null)
        {
            SelectedDeviceStatus = "No FFB device selected";
            DeviceCapabilityStatus = "No selected device";
            RefreshCommandStates();
            return;
        }

        DeviceCapabilityStatus = device.FfbStatus;
        if (!device.IsForceFeedbackCapable)
        {
            SelectedDeviceStatus = $"{device.DisplayName} selected for inspection only";
            BackendStatus = "Selected device has no FFB capability";
            RefreshCommandStates();
            return;
        }

        if (_windowHandle == IntPtr.Zero)
        {
            SelectedDeviceStatus = "Window handle unavailable; cannot acquire DirectInput device";
            RefreshCommandStates();
            return;
        }

        var selected = _backend.SelectDevice(device, _windowHandle, GlobalForceLimitPercent, DeviceForceLimitPercent);
        if (selected)
        {
            SelectedDeviceStatus = $"{device.DisplayName} acquired";
            BackendStatus = "DirectInput FFB ready";
            _config.SelectedDeviceStableId = device.StableId;
            _configStore.Save(_config);
        }
        else
        {
            SelectedDeviceStatus = $"{device.DisplayName} could not be acquired";
            BackendStatus = "DirectInput acquisition failed";
        }

        RefreshCommandStates();
    }

    private void SaveForceLimits()
    {
        _config.GlobalForceLimitPercent = GlobalForceLimitPercent;
        _config.DeviceForceLimitPercent = DeviceForceLimitPercent;
        _backend.UpdateForceLimits(GlobalForceLimitPercent, DeviceForceLimitPercent);
        _configStore.Save(_config);
        _log.Information("Force limits updated: global={GlobalLimit}%, device={DeviceLimit}%", GlobalForceLimitPercent, DeviceForceLimitPercent);
    }

    private void RefreshCommandStates()
    {
        OnPropertyChanged(nameof(CanRunEffects));
        OnPropertyChanged(nameof(FfbReadyText));
        SpringTestCommand.NotifyCanExecuteChanged();
        DamperTestCommand.NotifyCanExecuteChanged();
        ConstantLeftCommand.NotifyCanExecuteChanged();
        ConstantRightCommand.NotifyCanExecuteChanged();
        LowVibrationCommand.NotifyCanExecuteChanged();
    }

    private void OnTelemetryStateChanged(TelemetryReceiverState state)
    {
        Dispatcher.UIThread.Post(() => ApplyTelemetryState(state));
    }

    private void ApplyTelemetryState(TelemetryReceiverState state)
    {
        TelemetryStatus = state.Status.ToString();
        TelemetryEndpoint = state.Endpoint;
        PacketRate = $"{state.PacketRate:0} pkt/s";
        UdpStatus = state.UdpStatus;
        FileStatus = state.FileStatus;
        LastPacketSource = state.LastPacketSource;
        LastTransportError = string.IsNullOrWhiteSpace(state.LastTransportError) ? "none" : state.LastTransportError;
        LastPacketAge = state.LastPacketAge is null ? "none" : $"{state.LastPacketAge.Value.TotalMilliseconds:0} ms";
        RawTelemetryPreview = state.LastRawPacket;
        TelemetryParseStatus = string.IsNullOrWhiteSpace(state.LastParseError)
            ? "Last packet parsed"
            : $"Parse error: {state.LastParseError}";

        var packet = state.LastPacket;
        if (packet is null)
        {
            CurrentVehicle = "No vehicle";
            VehicleType = "Unknown";
            SpeedKmh = "-";
            SteeringAngle = "-";
            Rpm = "-";
            EngineStatus = "-";
            Mass = "-";
            TotalMass = "-";
            MassAndTotal = "- / -";
            IsOnField = "-";
            GameState = "-";
        }
        else
        {
            CurrentVehicle = string.IsNullOrWhiteSpace(packet.VehicleName) ? "No vehicle" : packet.VehicleName;
            VehicleType = string.IsNullOrWhiteSpace(packet.VehicleType) ? "Unknown" : packet.VehicleType;
            SpeedKmh = FormatNumber(packet.SpeedKmh, "0.0 km/h");
            SteeringAngle = FormatNumber(packet.SteeringAngle, "0.000");
            Rpm = FormatNumber(packet.Rpm, "0 rpm");
            EngineStatus = packet.EngineStarted is null ? "-" : packet.EngineStarted.Value ? "Started" : "Stopped";
            Mass = FormatNumber(packet.Mass, "0 kg");
            TotalMass = FormatNumber(packet.TotalMass, "0 kg");
            MassAndTotal = $"{Mass} / {TotalMass}";
            IsOnField = packet.IsOnField is null ? "-" : packet.IsOnField.Value ? "Yes" : "No";
            GameState = string.IsNullOrWhiteSpace(packet.GameState) ? "-" : packet.GameState;
        }

        OnPropertyChanged(nameof(TelemetryReadyText));
    }

    private static string FormatNumber(double? value, string format)
    {
        return value is null ? "-" : value.Value.ToString(format);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _safety.StopAll("view model disposed");
        _gameplayFfb.Dispose();
        _telemetryReceiver.StateChanged -= OnTelemetryStateChanged;
        _telemetryReceiver.Dispose();
        _panicHotkey.Dispose();
        _backend.Dispose();
        _log.Dispose();
    }
}
