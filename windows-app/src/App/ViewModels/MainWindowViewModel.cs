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
    private bool _loadingConfig;
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

    [ObservableProperty]
    private bool _gameplayFfbEnabled;

    [ObservableProperty]
    private bool _speedSpringEnabled;

    [ObservableProperty]
    private int _speedSpringStrengthPercent;

    [ObservableProperty]
    private int _speedSpringMaxPercent;

    [ObservableProperty]
    private FfbCurveKind _speedSpringCurve;

    [ObservableProperty]
    private bool _speedDamperEnabled;

    [ObservableProperty]
    private int _speedDamperStrengthPercent;

    [ObservableProperty]
    private int _speedDamperMaxPercent;

    [ObservableProperty]
    private FfbCurveKind _speedDamperCurve;

    [ObservableProperty]
    private bool _loadResistanceEnabled;

    [ObservableProperty]
    private int _loadResistanceStrengthPercent;

    [ObservableProperty]
    private int _loadResistanceMaxPercent;

    [ObservableProperty]
    private FfbCurveKind _loadResistanceCurve;

    [ObservableProperty]
    private bool _engineVibrationEnabled;

    [ObservableProperty]
    private int _engineVibrationStrengthPercent;

    [ObservableProperty]
    private int _engineVibrationMaxPercent;

    [ObservableProperty]
    private FfbCurveKind _engineVibrationCurve;

    [ObservableProperty]
    private bool _surfaceFeedbackEnabled;

    [ObservableProperty]
    private int _surfaceFeedbackStrengthPercent;

    [ObservableProperty]
    private int _surfaceFeedbackMaxPercent;

    [ObservableProperty]
    private FfbCurveKind _surfaceFeedbackCurve;

    [ObservableProperty]
    private string _activeGameplayEffects = "None";

    [ObservableProperty]
    private string _springOutput = "0%";

    [ObservableProperty]
    private string _damperOutput = "0%";

    [ObservableProperty]
    private string _engineVibrationOutput = "0%";

    [ObservableProperty]
    private string _surfaceFeedbackOutput = "0%";

    [ObservableProperty]
    private string _loadFactor = "1.00";

    [ObservableProperty]
    private string _telemetryFade = "0%";

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
        _loadingConfig = true;
        _globalForceLimitPercent = _config.GlobalForceLimitPercent;
        _deviceForceLimitPercent = _config.DeviceForceLimitPercent;
        LoadGameplaySettingsIntoUi(_config.GameplayFfb);
        _loadingConfig = false;
        _configPath = _configStore.ConfigPath;
        _logPath = _log.LogPath;
        Logs = _log.Entries;

        _backend.UpdateForceLimits(GlobalForceLimitPercent, DeviceForceLimitPercent);
        _telemetryReceiver.StateChanged += OnTelemetryStateChanged;
        _telemetryReceiver.Start(_config.TelemetryHost, _config.TelemetryPort, _config.TelemetryLostTimeoutMs, _config.TelemetryFilePath);
        _gameplayFfb = new GameplayFfbController(_telemetryReceiver, _backend, _log, () => _config.GameplayFfb, OnGameplayOutputChanged);
        _log.Information("Application initialized. Config={ConfigPath}", ConfigPath);
    }

    public ObservableCollection<DeviceInfo> Devices { get; } = [];
    public ObservableCollection<string> Logs { get; }
    public IReadOnlyList<FfbCurveKind> CurveKinds { get; } =
    [
        FfbCurveKind.Smooth,
        FfbCurveKind.Linear,
        FfbCurveKind.Aggressive
    ];
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
        GameplayFfbEnabled = false;
        _safety.OnPanicHotkey();
        OnGameplayOutputChanged(GameplayFfbOutput.Zero);
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
        GameplayFfbEnabled = false;
        _safety.StopAll("user stop");
        OnGameplayOutputChanged(GameplayFfbOutput.Zero);
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

    partial void OnGameplayFfbEnabledChanged(bool value)
    {
        SaveGameplaySettingsFromUi();
        if (!value && !_loadingConfig)
        {
            _backend.StopGameplayEffects("gameplay ffb disabled");
            OnGameplayOutputChanged(GameplayFfbOutput.Zero);
        }
    }
    partial void OnSpeedSpringEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedSpringStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedSpringMaxPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedSpringCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedDamperEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedDamperStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedDamperMaxPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedDamperCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnLoadResistanceEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnLoadResistanceStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnLoadResistanceMaxPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnLoadResistanceCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnEngineVibrationEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnEngineVibrationStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnEngineVibrationMaxPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnEngineVibrationCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnSurfaceFeedbackEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnSurfaceFeedbackStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSurfaceFeedbackMaxPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSurfaceFeedbackCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();

    private void LoadGameplaySettingsIntoUi(GameplayFfbSettings settings)
    {
        GameplayFfbEnabled = settings.Enabled;
        SpeedSpringEnabled = settings.SpeedSpring.Enabled;
        SpeedSpringStrengthPercent = settings.SpeedSpring.StrengthPercent;
        SpeedSpringMaxPercent = settings.SpeedSpring.MaxOutputPercent;
        SpeedSpringCurve = settings.SpeedSpring.Curve;
        SpeedDamperEnabled = settings.SpeedDamper.Enabled;
        SpeedDamperStrengthPercent = settings.SpeedDamper.StrengthPercent;
        SpeedDamperMaxPercent = settings.SpeedDamper.MaxOutputPercent;
        SpeedDamperCurve = settings.SpeedDamper.Curve;
        LoadResistanceEnabled = settings.LoadResistance.Enabled;
        LoadResistanceStrengthPercent = settings.LoadResistance.StrengthPercent;
        LoadResistanceMaxPercent = settings.LoadResistance.MaxOutputPercent;
        LoadResistanceCurve = settings.LoadResistance.Curve;
        EngineVibrationEnabled = settings.EngineVibration.Enabled;
        EngineVibrationStrengthPercent = settings.EngineVibration.StrengthPercent;
        EngineVibrationMaxPercent = settings.EngineVibration.MaxOutputPercent;
        EngineVibrationCurve = settings.EngineVibration.Curve;
        SurfaceFeedbackEnabled = settings.SurfaceFeedback.Enabled;
        SurfaceFeedbackStrengthPercent = settings.SurfaceFeedback.StrengthPercent;
        SurfaceFeedbackMaxPercent = settings.SurfaceFeedback.MaxOutputPercent;
        SurfaceFeedbackCurve = settings.SurfaceFeedback.Curve;
    }

    private void SaveGameplaySettingsFromUi()
    {
        if (_loadingConfig)
        {
            return;
        }

        _config.GameplayFfb.Enabled = GameplayFfbEnabled;
        _config.GameplayFfb.SpeedSpring.Enabled = SpeedSpringEnabled;
        _config.GameplayFfb.SpeedSpring.StrengthPercent = Math.Clamp(SpeedSpringStrengthPercent, 0, 100);
        _config.GameplayFfb.SpeedSpring.MaxOutputPercent = Math.Clamp(SpeedSpringMaxPercent, 0, 100);
        _config.GameplayFfb.SpeedSpring.Curve = SpeedSpringCurve;
        _config.GameplayFfb.SpeedDamper.Enabled = SpeedDamperEnabled;
        _config.GameplayFfb.SpeedDamper.StrengthPercent = Math.Clamp(SpeedDamperStrengthPercent, 0, 100);
        _config.GameplayFfb.SpeedDamper.MaxOutputPercent = Math.Clamp(SpeedDamperMaxPercent, 0, 100);
        _config.GameplayFfb.SpeedDamper.Curve = SpeedDamperCurve;
        _config.GameplayFfb.LoadResistance.Enabled = LoadResistanceEnabled;
        _config.GameplayFfb.LoadResistance.StrengthPercent = Math.Clamp(LoadResistanceStrengthPercent, 0, 100);
        _config.GameplayFfb.LoadResistance.MaxOutputPercent = Math.Clamp(LoadResistanceMaxPercent, 0, 100);
        _config.GameplayFfb.LoadResistance.Curve = LoadResistanceCurve;
        _config.GameplayFfb.EngineVibration.Enabled = EngineVibrationEnabled;
        _config.GameplayFfb.EngineVibration.StrengthPercent = Math.Clamp(EngineVibrationStrengthPercent, 0, 100);
        _config.GameplayFfb.EngineVibration.MaxOutputPercent = Math.Clamp(EngineVibrationMaxPercent, 0, 100);
        _config.GameplayFfb.EngineVibration.Curve = EngineVibrationCurve;
        _config.GameplayFfb.SurfaceFeedback.Enabled = SurfaceFeedbackEnabled;
        _config.GameplayFfb.SurfaceFeedback.StrengthPercent = Math.Clamp(SurfaceFeedbackStrengthPercent, 0, 100);
        _config.GameplayFfb.SurfaceFeedback.MaxOutputPercent = Math.Clamp(SurfaceFeedbackMaxPercent, 0, 100);
        _config.GameplayFfb.SurfaceFeedback.Curve = SurfaceFeedbackCurve;
        _configStore.Save(_config);
        _log.Information("Gameplay FFB settings updated");
    }

    private void OnGameplayOutputChanged(GameplayFfbOutput output)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ActiveGameplayEffects = output.ActiveEffectsText;
            SpringOutput = $"{output.SpringPercent}%";
            DamperOutput = $"{output.DamperPercent}%";
            EngineVibrationOutput = output.EngineVibrationPercent > 0
                ? $"{output.EngineVibrationPercent}% @ {output.EngineVibrationHz} Hz"
                : "0%";
            SurfaceFeedbackOutput = output.SurfaceVibrationPercent > 0
                ? $"{output.SurfaceVibrationPercent}% @ {output.SurfaceVibrationHz} Hz"
                : "0%";
            LoadFactor = output.LoadFactor.ToString("0.00");
            TelemetryFade = $"{output.TelemetryFade * 100:0}%";
        });
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
