using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FS25FfbBridge.App.Models;
using FS25FfbBridge.App.Services;

namespace FS25FfbBridge.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private const int MaxRawTelemetryPreviewLength = 4000;
    private readonly ConfigStore _configStore;
    private readonly IFfbBackend _backend;
    private readonly TelemetryReceiverService _telemetryReceiver;
    private readonly GameplayFfbController _gameplayFfb;
    private readonly EffectStatusWriter _effectStatusWriter;
    private readonly SafetyManager _safety;
    private readonly AppLogService _log;
    private readonly PanicHotkeyService _panicHotkey;
    private AppConfig _config;
    private IntPtr _windowHandle;
    private bool _loadingConfig;
    private bool _gameplayFfbPausedByStopAll;
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
    private string _vehicleCategory = "Unknown";

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
    private string _surfaceType = "-";

    [ObservableProperty]
    private string _surfaceAttribute = "-";

    [ObservableProperty]
    private string _wetnessAndRain = "- / -";

    [ObservableProperty]
    private string _wheelSlip = "-";

    [ObservableProperty]
    private string _groundContactRatio = "-";

    [ObservableProperty]
    private string _wheelTireTypes = "-";

    [ObservableProperty]
    private string _wheelTireProfile = "-";

    [ObservableProperty]
    private string _attitude = "- / - / -";

    [ObservableProperty]
    private string _localAcceleration = "- / - / -";

    [ObservableProperty]
    private string _bumpImpulse = "-";

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
    private bool _mechanicalFrictionEnabled;

    [ObservableProperty]
    private int _mechanicalFrictionStrengthPercent;

    [ObservableProperty]
    private int _mechanicalFrictionMaxPercent;

    [ObservableProperty]
    private FfbCurveKind _mechanicalFrictionCurve;

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
    private bool _slipFeedbackEnabled;

    [ObservableProperty]
    private int _slipFeedbackStrengthPercent;

    [ObservableProperty]
    private int _slipFeedbackMaxPercent;

    [ObservableProperty]
    private FfbCurveKind _slipFeedbackCurve;

    [ObservableProperty]
    private bool _wetnessFeedbackEnabled;

    [ObservableProperty]
    private int _wetnessFeedbackStrengthPercent;

    [ObservableProperty]
    private int _wetnessFeedbackMaxPercent;

    [ObservableProperty]
    private FfbCurveKind _wetnessFeedbackCurve;

    [ObservableProperty]
    private bool _motionFeedbackEnabled;

    [ObservableProperty]
    private int _motionFeedbackStrengthPercent;

    [ObservableProperty]
    private int _motionFeedbackMaxPercent;

    [ObservableProperty]
    private FfbCurveKind _motionFeedbackCurve;

    [ObservableProperty]
    private bool _bumpFeedbackEnabled;

    [ObservableProperty]
    private int _bumpFeedbackStrengthPercent;

    [ObservableProperty]
    private int _bumpFeedbackMaxPercent;

    [ObservableProperty]
    private FfbCurveKind _bumpFeedbackCurve;

    [ObservableProperty]
    private string _activeGameplayEffects = "None";

    [ObservableProperty]
    private string _activeVehicleCategory = "Unknown";

    [ObservableProperty]
    private string _gameplayFfbRuntimeStatus = "FFB enabled";

    [ObservableProperty]
    private string _springOutput = "0%";

    [ObservableProperty]
    private string _damperOutput = "0%";

    [ObservableProperty]
    private string _frictionOutput = "0%";

    [ObservableProperty]
    private string _engineVibrationOutput = "0%";

    [ObservableProperty]
    private string _surfaceFeedbackOutput = "0%";

    [ObservableProperty]
    private string _wetnessFeedbackOutput = "0%";

    [ObservableProperty]
    private string _slipFeedbackOutput = "0%";

    [ObservableProperty]
    private string _bumpFeedbackOutput = "0%";

    [ObservableProperty]
    private string _loadFactor = "1.00";

    [ObservableProperty]
    private string _telemetryFade = "0%";

    [ObservableProperty]
    private string _selectedEffectCategory = VehicleCategoryFfbProfile.TractorWheeled;

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
        _effectStatusWriter = new EffectStatusWriter(_log);
        _panicHotkey = new PanicHotkeyService(_log);
        _panicHotkey.Pressed += HandlePanicHotkey;
        _panicHotkey.Register();
        _config = _configStore.Load();
        _loadingConfig = true;
        _globalForceLimitPercent = _config.GlobalForceLimitPercent;
        _deviceForceLimitPercent = _config.DeviceForceLimitPercent;
        GameplayFfbEnabled = _config.GameplayFfb.Enabled;
        LoadGameplaySettingsIntoUi(GetSelectedCategoryProfile(SelectedEffectCategory));
        _loadingConfig = false;
        _configPath = _configStore.ConfigPath;
        _logPath = _log.LogPath;
        Logs = _log.Entries;

        _backend.UpdateForceLimits(GlobalForceLimitPercent, DeviceForceLimitPercent);
        _telemetryReceiver.StateChanged += OnTelemetryStateChanged;
        _telemetryReceiver.Start(
            _config.TelemetryHost,
            _config.TelemetryPort,
            _config.TelemetryLostTimeoutMs,
            _config.TelemetryFilePath,
            ffbUpdateRateHz: _config.TelemetryFfbUpdateRateHz,
            uiRefreshMs: _config.TelemetryUiRefreshMs);
        _gameplayFfb = new GameplayFfbController(
            _telemetryReceiver,
            _backend,
            _log,
            GetRuntimeGameplaySettings,
            OnGameplayOutputChanged,
            OnGameplayApplyResultChanged,
            _effectStatusWriter,
            _config.TelemetryFfbUpdateRateHz);
        _log.Information("Application initialized. Config={ConfigPath}", ConfigPath);
    }

    public ObservableCollection<DeviceInfo> Devices { get; } = [];
    public ObservableCollection<string> Logs { get; }
    public IReadOnlyList<string> EffectCategories => VehicleCategoryFfbProfile.Categories;
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
    public bool IsTelemetryConnected => TelemetryStatus == "Connected";
    public bool IsTelemetryWaiting => TelemetryStatus == "Waiting";
    public bool IsTelemetryLost => TelemetryStatus == "Lost";
    public bool IsFfbReady => CanRunEffects;
    public bool IsFfbInactive => !CanRunEffects;

    public void InitializeWindowHandle(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        ScanDevices();
    }

    public void HandlePanicHotkey()
    {
        _gameplayFfbPausedByStopAll = true;
        _safety.OnPanicHotkey();
        OnGameplayOutputChanged(GameplayFfbOutput.Zero);
        _effectStatusWriter.WriteZero(ActiveVehicleCategory);
        GameplayFfbRuntimeStatus = "Paused by emergency stop";
        BackendStatus = "Emergency stop active";
        StopAllEffectsCommand.NotifyCanExecuteChanged();
    }

    public void HandleClosing()
    {
        _safety.OnAppClosing();
        _effectStatusWriter.WriteZero(ActiveVehicleCategory);
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
    private void PreviousEffectCategory() => MoveEffectCategory(-1);

    [RelayCommand]
    private void NextEffectCategory() => MoveEffectCategory(1);

    [RelayCommand]
    private void StopAllEffects()
    {
        _gameplayFfbPausedByStopAll = true;
        _log.Information("Stop All requested: reason={Reason}, persistentGameplayConfigChanged={PersistentConfigChanged}", "user stop", false);
        _safety.StopAll("user stop");
        OnGameplayOutputChanged(GameplayFfbOutput.Zero);
        _effectStatusWriter.WriteZero(ActiveVehicleCategory);
        GameplayFfbRuntimeStatus = "Paused by Stop All";
        BackendStatus = "All effects stopped";
    }

    private void StartEffect(FfbEffectKind kind)
    {
        _safety.StartTestEffect(kind);
        BackendStatus = $"{kind} test running";
    }

    private void MoveEffectCategory(int direction)
    {
        if (EffectCategories.Count == 0)
        {
            return;
        }

        var index = -1;
        for (var i = 0; i < EffectCategories.Count; i++)
        {
            if (EffectCategories[i] == SelectedEffectCategory)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            index = 0;
        }

        var next = (index + direction) % EffectCategories.Count;
        if (next < 0)
        {
            next += EffectCategories.Count;
        }

        SelectedEffectCategory = EffectCategories[next];
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
        if (value)
        {
            _gameplayFfbPausedByStopAll = false;
            GameplayFfbRuntimeStatus = CanRunEffects ? "FFB enabled" : "Device not acquired";
        }

        SaveGameplaySettingsFromUi();
        if (!value && !_loadingConfig)
        {
            _backend.StopGameplayEffects("gameplay ffb disabled");
            OnGameplayOutputChanged(GameplayFfbOutput.Zero);
            _effectStatusWriter.WriteZero(ActiveVehicleCategory);
            GameplayFfbRuntimeStatus = "FFB disabled";
        }
    }
    partial void OnSelectedEffectCategoryChanged(string value)
    {
        if (_loadingConfig)
        {
            return;
        }

        _loadingConfig = true;
        LoadGameplaySettingsIntoUi(GetSelectedCategoryProfile(value));
        _loadingConfig = false;
    }
    partial void OnSpeedSpringEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedSpringStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedSpringMaxPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedSpringCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedDamperEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedDamperStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedDamperMaxPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedDamperCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnMechanicalFrictionEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnMechanicalFrictionStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnMechanicalFrictionMaxPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnMechanicalFrictionCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
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
    partial void OnSlipFeedbackEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnSlipFeedbackStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSlipFeedbackMaxPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSlipFeedbackCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnWetnessFeedbackEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnWetnessFeedbackStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnWetnessFeedbackMaxPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnWetnessFeedbackCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnMotionFeedbackEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnMotionFeedbackStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnMotionFeedbackMaxPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnMotionFeedbackCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnBumpFeedbackEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnBumpFeedbackStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnBumpFeedbackMaxPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnBumpFeedbackCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();

    private void LoadGameplaySettingsIntoUi(GameplayFfbSettings settings)
    {
        GameplayFfbEnabled = settings.Enabled;
        LoadGameplaySettingsIntoUi(GetSelectedCategoryProfile(SelectedEffectCategory));
    }

    private void LoadGameplaySettingsIntoUi(GameplayFfbEffectProfile settings)
    {
        SpeedSpringEnabled = settings.SpeedSpring.Enabled;
        SpeedSpringStrengthPercent = settings.SpeedSpring.StrengthPercent;
        SpeedSpringMaxPercent = settings.SpeedSpring.MaxOutputPercent;
        SpeedSpringCurve = settings.SpeedSpring.Curve;
        SpeedDamperEnabled = settings.SpeedDamper.Enabled;
        SpeedDamperStrengthPercent = settings.SpeedDamper.StrengthPercent;
        SpeedDamperMaxPercent = settings.SpeedDamper.MaxOutputPercent;
        SpeedDamperCurve = settings.SpeedDamper.Curve;
        MechanicalFrictionEnabled = settings.MechanicalFriction.Enabled;
        MechanicalFrictionStrengthPercent = settings.MechanicalFriction.StrengthPercent;
        MechanicalFrictionMaxPercent = settings.MechanicalFriction.MaxOutputPercent;
        MechanicalFrictionCurve = settings.MechanicalFriction.Curve;
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
        SlipFeedbackEnabled = settings.SlipFeedback.Enabled;
        SlipFeedbackStrengthPercent = settings.SlipFeedback.StrengthPercent;
        SlipFeedbackMaxPercent = settings.SlipFeedback.MaxOutputPercent;
        SlipFeedbackCurve = settings.SlipFeedback.Curve;
        WetnessFeedbackEnabled = settings.WetnessFeedback.Enabled;
        WetnessFeedbackStrengthPercent = settings.WetnessFeedback.StrengthPercent;
        WetnessFeedbackMaxPercent = settings.WetnessFeedback.MaxOutputPercent;
        WetnessFeedbackCurve = settings.WetnessFeedback.Curve;
        MotionFeedbackEnabled = settings.MotionFeedback.Enabled;
        MotionFeedbackStrengthPercent = settings.MotionFeedback.StrengthPercent;
        MotionFeedbackMaxPercent = settings.MotionFeedback.MaxOutputPercent;
        MotionFeedbackCurve = settings.MotionFeedback.Curve;
        BumpFeedbackEnabled = settings.BumpFeedback.Enabled;
        BumpFeedbackStrengthPercent = settings.BumpFeedback.StrengthPercent;
        BumpFeedbackMaxPercent = settings.BumpFeedback.MaxOutputPercent;
        BumpFeedbackCurve = settings.BumpFeedback.Curve;
    }

    private void SaveGameplaySettingsFromUi()
    {
        if (_loadingConfig)
        {
            return;
        }

        _config.GameplayFfb.Enabled = GameplayFfbEnabled;
        SaveGameplaySettingsToProfile(GetSelectedCategoryProfile(SelectedEffectCategory));
        _configStore.Save(_config);
        _log.Information("Gameplay FFB settings updated");
    }

    private GameplayFfbEffectProfile GetSelectedCategoryProfile(string category)
    {
        if (string.IsNullOrWhiteSpace(category) ||
            !VehicleCategoryFfbProfile.Categories.Contains(category))
        {
            category = VehicleCategoryFfbProfile.Unknown;
        }

        if (!_config.GameplayFfb.VehicleCategoryEffectProfiles.TryGetValue(category, out var profile) ||
            profile is null)
        {
            profile = GameplayFfbEffectProfile.Clone(_config.GameplayFfb);
            _config.GameplayFfb.VehicleCategoryEffectProfiles[category] = profile;
        }

        GameplayFfbEffectProfile.NormalizeEffectSettings(profile);
        return profile;
    }

    private void SaveGameplaySettingsToProfile(GameplayFfbEffectProfile profile)
    {
        profile.SpeedSpring.Enabled = SpeedSpringEnabled;
        profile.SpeedSpring.StrengthPercent = Math.Clamp(SpeedSpringStrengthPercent, 0, 100);
        profile.SpeedSpring.MaxOutputPercent = Math.Clamp(SpeedSpringMaxPercent, 0, 100);
        profile.SpeedSpring.Curve = SpeedSpringCurve;
        profile.SpeedDamper.Enabled = SpeedDamperEnabled;
        profile.SpeedDamper.StrengthPercent = Math.Clamp(SpeedDamperStrengthPercent, 0, 100);
        profile.SpeedDamper.MaxOutputPercent = Math.Clamp(SpeedDamperMaxPercent, 0, 100);
        profile.SpeedDamper.Curve = SpeedDamperCurve;
        profile.MechanicalFriction.Enabled = MechanicalFrictionEnabled;
        profile.MechanicalFriction.StrengthPercent = Math.Clamp(MechanicalFrictionStrengthPercent, 0, 100);
        profile.MechanicalFriction.MaxOutputPercent = Math.Clamp(MechanicalFrictionMaxPercent, 0, 100);
        profile.MechanicalFriction.Curve = MechanicalFrictionCurve;
        profile.LoadResistance.Enabled = LoadResistanceEnabled;
        profile.LoadResistance.StrengthPercent = Math.Clamp(LoadResistanceStrengthPercent, 0, 100);
        profile.LoadResistance.MaxOutputPercent = Math.Clamp(LoadResistanceMaxPercent, 0, 100);
        profile.LoadResistance.Curve = LoadResistanceCurve;
        profile.EngineVibration.Enabled = EngineVibrationEnabled;
        profile.EngineVibration.StrengthPercent = Math.Clamp(EngineVibrationStrengthPercent, 0, 100);
        profile.EngineVibration.MaxOutputPercent = Math.Clamp(EngineVibrationMaxPercent, 0, 100);
        profile.EngineVibration.Curve = EngineVibrationCurve;
        profile.SurfaceFeedback.Enabled = SurfaceFeedbackEnabled;
        profile.SurfaceFeedback.StrengthPercent = Math.Clamp(SurfaceFeedbackStrengthPercent, 0, 100);
        profile.SurfaceFeedback.MaxOutputPercent = Math.Clamp(SurfaceFeedbackMaxPercent, 0, 100);
        profile.SurfaceFeedback.Curve = SurfaceFeedbackCurve;
        profile.SlipFeedback.Enabled = SlipFeedbackEnabled;
        profile.SlipFeedback.StrengthPercent = Math.Clamp(SlipFeedbackStrengthPercent, 0, 100);
        profile.SlipFeedback.MaxOutputPercent = Math.Clamp(SlipFeedbackMaxPercent, 0, 100);
        profile.SlipFeedback.Curve = SlipFeedbackCurve;
        profile.WetnessFeedback.Enabled = WetnessFeedbackEnabled;
        profile.WetnessFeedback.StrengthPercent = Math.Clamp(WetnessFeedbackStrengthPercent, 0, 100);
        profile.WetnessFeedback.MaxOutputPercent = Math.Clamp(WetnessFeedbackMaxPercent, 0, 100);
        profile.WetnessFeedback.Curve = WetnessFeedbackCurve;
        profile.MotionFeedback.Enabled = MotionFeedbackEnabled;
        profile.MotionFeedback.StrengthPercent = Math.Clamp(MotionFeedbackStrengthPercent, 0, 100);
        profile.MotionFeedback.MaxOutputPercent = Math.Clamp(MotionFeedbackMaxPercent, 0, 100);
        profile.MotionFeedback.Curve = MotionFeedbackCurve;
        profile.BumpFeedback.Enabled = BumpFeedbackEnabled;
        profile.BumpFeedback.StrengthPercent = Math.Clamp(BumpFeedbackStrengthPercent, 0, 100);
        profile.BumpFeedback.MaxOutputPercent = Math.Clamp(BumpFeedbackMaxPercent, 0, 100);
        profile.BumpFeedback.Curve = BumpFeedbackCurve;
    }

    private GameplayFfbSettings GetRuntimeGameplaySettings()
    {
        if (!_gameplayFfbPausedByStopAll)
        {
            return _config.GameplayFfb;
        }

        return new GameplayFfbSettings
        {
            Enabled = false,
            SpeedSpring = _config.GameplayFfb.SpeedSpring,
            SpeedDamper = _config.GameplayFfb.SpeedDamper,
            MechanicalFriction = _config.GameplayFfb.MechanicalFriction,
            LoadResistance = _config.GameplayFfb.LoadResistance,
            EngineVibration = _config.GameplayFfb.EngineVibration,
            SurfaceFeedback = _config.GameplayFfb.SurfaceFeedback,
            SlipFeedback = _config.GameplayFfb.SlipFeedback,
            WetnessFeedback = _config.GameplayFfb.WetnessFeedback,
            MotionFeedback = _config.GameplayFfb.MotionFeedback,
            BumpFeedback = _config.GameplayFfb.BumpFeedback,
            VehicleCategoryEffectProfiles = _config.GameplayFfb.VehicleCategoryEffectProfiles
        };
    }

    private void OnGameplayApplyResultChanged(FfbApplyResult result)
    {
        Dispatcher.UIThread.Post(() =>
        {
            GameplayFfbRuntimeStatus = result.Status switch
            {
                FfbApplyStatus.Applied => "FFB enabled",
                FfbApplyStatus.AcquireFailed => "Device not acquired",
                _ when _gameplayFfbPausedByStopAll => "Paused by Stop All",
                _ when GameplayFfbEnabled => "FFB enabled",
                _ => "FFB disabled"
            };

            if (result.Status == FfbApplyStatus.AcquireFailed)
            {
                BackendStatus = result.Message;
            }
        });
    }

    private void OnGameplayOutputChanged(GameplayFfbOutput output)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ActiveGameplayEffects = output.ActiveEffectsText;
            ActiveVehicleCategory = output.IsActive ? output.ActiveCategory : ActiveVehicleCategory;
            SpringOutput = $"{output.SpringPercent}%";
            DamperOutput = $"{output.DamperPercent}%";
            FrictionOutput = $"{output.FrictionPercent}%";
            EngineVibrationOutput = output.EngineVibrationPercent > 0
                ? $"{output.EngineVibrationPercent}% @ {output.EngineVibrationHz} Hz"
                : "0%";
                            SurfaceFeedbackOutput = output.SurfaceVibrationPercent > 0
                                ? $"{output.SurfaceVibrationPercent}% @ {output.SurfaceVibrationHz} Hz"
                                : "0%";
            if (output.SurfaceVibrationPercent == 0 && output.IsActive)
            {
                SurfaceFeedbackOutput = $"{SurfaceFeedbackOutput} ({SurfaceType}, {SpeedKmh})";
            }
            WetnessFeedbackOutput = WetnessFeedbackEnabled
                ? WetnessAndRain
                : "0%";
            SlipFeedbackOutput = output.SlipVibrationPercent > 0
                ? $"{output.SlipVibrationPercent}% @ {output.SlipVibrationHz} Hz"
                : "0%";
            if (output.SlipVibrationPercent == 0 && output.IsActive)
            {
                SlipFeedbackOutput = $"{SlipFeedbackOutput} (slip {WheelSlip})";
            }
            BumpFeedbackOutput = output.BumpImpulsePercent != 0
                ? $"{output.BumpImpulsePercent}% / {output.BumpDurationMs} ms"
                : "0%";
            LoadFactor = output.LoadFactor.ToString("0.00");
            TelemetryFade = $"{output.TelemetryFade * 100:0}%";
        });
    }

    private void RefreshCommandStates()
    {
        OnPropertyChanged(nameof(CanRunEffects));
        OnPropertyChanged(nameof(FfbReadyText));
        OnPropertyChanged(nameof(IsFfbReady));
        OnPropertyChanged(nameof(IsFfbInactive));
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
        RawTelemetryPreview = LimitRawTelemetryPreview(state.LastRawPacket);
        TelemetryParseStatus = string.IsNullOrWhiteSpace(state.LastParseError)
            ? "Last packet parsed"
            : $"Parse error: {state.LastParseError}";

        var packet = state.LastPacket;
        if (packet is null)
        {
            CurrentVehicle = "No vehicle";
            VehicleType = "Unknown";
            VehicleCategory = "Unknown";
            ActiveVehicleCategory = "Unknown";
            SpeedKmh = "-";
            SteeringAngle = "-";
            Rpm = "-";
            EngineStatus = "-";
            Mass = "-";
            TotalMass = "-";
            MassAndTotal = "- / -";
            IsOnField = "-";
            SurfaceType = "-";
            SurfaceAttribute = "-";
            WetnessAndRain = "- / -";
            WheelSlip = "-";
            GroundContactRatio = "-";
            WheelTireTypes = "-";
            WheelTireProfile = "-";
            Attitude = "- / - / -";
            LocalAcceleration = "- / - / -";
            BumpImpulse = "-";
            GameState = "-";
        }
        else
        {
            CurrentVehicle = string.IsNullOrWhiteSpace(packet.VehicleName) ? "No vehicle" : packet.VehicleName;
            VehicleType = string.IsNullOrWhiteSpace(packet.VehicleType) ? "Unknown" : packet.VehicleType;
            VehicleCategory = string.IsNullOrWhiteSpace(packet.VehicleCategory) ? "Unknown" : packet.VehicleCategory;
            ActiveVehicleCategory = VehicleCategory;
            SpeedKmh = FormatNumber(packet.SpeedKmh, "0.0 km/h");
            SteeringAngle = FormatNumber(packet.SteeringAngle, "0.000");
            Rpm = FormatNumber(packet.Rpm, "0 rpm");
            EngineStatus = packet.EngineStarted is null ? "-" : packet.EngineStarted.Value ? "Started" : "Stopped";
            Mass = FormatNumber(packet.Mass, "0 kg");
            TotalMass = FormatNumber(packet.TotalMass, "0 kg");
            MassAndTotal = $"{Mass} / {TotalMass}";
            IsOnField = packet.IsOnField is null ? "-" : packet.IsOnField.Value ? "Yes" : "No";
            SurfaceType = string.IsNullOrWhiteSpace(packet.SurfaceType) ? "-" : packet.SurfaceType;
            SurfaceAttribute = FormatNumber(packet.SurfaceAttribute, "0");
            WetnessAndRain = $"{FormatNumber(packet.GroundWetness, "0.00")} / {FormatNumber(packet.RainScale, "0.00")}";
            WheelSlip = $"{FormatNumber(packet.WheelSlip, "0.00")} / {FormatNumber(packet.MaxWheelSlip, "0.00")}";
            GroundContactRatio = FormatNumber(packet.GroundContactRatio, "0%");
            WheelTireTypes = string.IsNullOrWhiteSpace(packet.WheelTireTypes) ? "-" : packet.WheelTireTypes;
            WheelTireProfile = string.IsNullOrWhiteSpace(packet.WheelTireProfile) ? "-" : packet.WheelTireProfile;
            Attitude = $"{FormatNumber(packet.PitchDeg, "0.0 deg")} / {FormatNumber(packet.RollDeg, "0.0 deg")} / {FormatNumber(packet.SlopeDeg, "0.0 deg")}";
            LocalAcceleration = $"{FormatNumber(packet.LocalAccelerationX, "0.00")} / {FormatNumber(packet.LocalAccelerationY, "0.00")} / {FormatNumber(packet.LocalAccelerationZ, "0.00")}";
            BumpImpulse = FormatNumber(packet.BumpImpulse, "0.00");
            GameState = string.IsNullOrWhiteSpace(packet.GameState) ? "-" : packet.GameState;
        }

        OnPropertyChanged(nameof(TelemetryReadyText));
        OnPropertyChanged(nameof(IsTelemetryConnected));
        OnPropertyChanged(nameof(IsTelemetryWaiting));
        OnPropertyChanged(nameof(IsTelemetryLost));
    }

    private static string FormatNumber(double? value, string format)
    {
        return value is null ? "-" : value.Value.ToString(format);
    }

    private static string LimitRawTelemetryPreview(string raw)
    {
        if (raw.Length <= MaxRawTelemetryPreviewLength)
        {
            return raw;
        }

        return $"{raw[..MaxRawTelemetryPreviewLength]}... truncated ({raw.Length} chars)";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _safety.StopAll("view model disposed");
        _effectStatusWriter.WriteZero(ActiveVehicleCategory);
        _gameplayFfb.Dispose();
        _telemetryReceiver.StateChanged -= OnTelemetryStateChanged;
        _telemetryReceiver.Dispose();
        _panicHotkey.Dispose();
        _backend.Dispose();
        _log.Dispose();
    }
}
