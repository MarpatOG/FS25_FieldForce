using System.Collections.ObjectModel;
using Avalonia.Media;
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
    private readonly EffectsProfileStore _effectsProfileStore;
    private readonly IFfbBackend _backend;
    private readonly TelemetryReceiverService _telemetryReceiver;
    private readonly GameplayFfbController _gameplayFfb;
    private readonly EffectStatusWriter _effectStatusWriter;
    private readonly SafetyManager _safety;
    private readonly AppLogService _log;
    private readonly PanicHotkeyService _panicHotkey;
    private readonly TelemetryCaptureLogService _telemetryCapture;
    private readonly TelemetryCaptureHotkeyService _telemetryCaptureHotkey;
    private AppConfig _config;
    private IntPtr _windowHandle;
    private bool _loadingConfig;
    private bool _syncingEffectCategoryFromActiveVehicle;
    private bool _effectCategoryPinnedByUser;
    private bool _gameplayFfbPausedByStopAll;
    private bool _gameplayFfbPausedByReload;
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
    private string _activeTireSurfaceMultiplier = "50%";

    [ObservableProperty]
    private string _newSurfaceAliasRaw = "";

    [ObservableProperty]
    private string _selectedSurfaceAliasTarget = "unknownMixed";

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
    private bool _telemetryCaptureRecording;

    [ObservableProperty]
    private string _telemetryCaptureStatus = "Not recording";

    [ObservableProperty]
    private string _telemetryCapturePath = "-";

    public string TelemetryCaptureButtonText => TelemetryCaptureRecording ? "Stop Capture" : "Start Capture";

    [ObservableProperty]
    private bool _gameplayFfbEnabled;

    [ObservableProperty]
    private bool _effectOverlayEnabled;

    [ObservableProperty]
    private bool _effectOverlayClickThrough;

    [ObservableProperty]
    private int _effectOverlayX;

    [ObservableProperty]
    private int _effectOverlayY;

    [ObservableProperty]
    private bool _speedSpringEnabled;

    [ObservableProperty]
    private int _speedSpringStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _speedSpringCurve;

    [ObservableProperty]
    private bool _speedDamperEnabled;

    [ObservableProperty]
    private int _speedDamperStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _speedDamperCurve;

    [ObservableProperty]
    private bool _mechanicalFrictionEnabled;

    [ObservableProperty]
    private int _mechanicalFrictionStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _mechanicalFrictionCurve;

    [ObservableProperty]
    private bool _loadResistanceEnabled;

    [ObservableProperty]
    private int _loadResistanceStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _loadResistanceCurve;

    [ObservableProperty]
    private bool _slewSmoothingEnabled;

    [ObservableProperty]
    private int _slewSmoothingStrengthPercent;

    [ObservableProperty]
    private bool _hillStandstillLoadEnabled;

    [ObservableProperty]
    private int _hillStandstillLoadStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _hillStandstillLoadCurve;

    [ObservableProperty]
    private bool _sideSlopeBiasEnabled;

    [ObservableProperty]
    private int _sideSlopeBiasStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _sideSlopeBiasCurve;

    [ObservableProperty]
    private bool _implementBiasEnabled;

    [ObservableProperty]
    private int _implementBiasStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _implementBiasCurve;

    [ObservableProperty]
    private bool _engineVibrationEnabled;

    [ObservableProperty]
    private int _engineVibrationStrengthPercent;

    [ObservableProperty]
    private int _engineIdleStrengthPercent;

    [ObservableProperty]
    private int _engineLoadStrengthPercent;

    [ObservableProperty]
    private int _engineLuggingBoostPercent;

    [ObservableProperty]
    private FfbCurveKind _engineVibrationCurve;

    [ObservableProperty]
    private bool _gearShiftPulseEnabled;

    [ObservableProperty]
    private int _gearShiftPulseStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _gearShiftPulseCurve;

    [ObservableProperty]
    private int _gearShiftPulseCooldownMs;

    [ObservableProperty]
    private bool _engineStartStopPulseEnabled;

    [ObservableProperty]
    private int _engineStartStopPulseStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _engineStartStopPulseCurve;

    [ObservableProperty]
    private int _engineDrivetrainMaxPercent;

    [ObservableProperty]
    private bool _surfaceFeedbackEnabled;

    [ObservableProperty]
    private int _surfaceFeedbackStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _surfaceFeedbackCurve;

    [ObservableProperty]
    private bool _slipFeedbackEnabled;

    [ObservableProperty]
    private int _slipFeedbackStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _slipFeedbackCurve;

    [ObservableProperty]
    private bool _wetnessFeedbackEnabled;

    [ObservableProperty]
    private int _wetnessFeedbackStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _wetnessFeedbackCurve;

    [ObservableProperty]
    private bool _motionFeedbackEnabled;

    [ObservableProperty]
    private int _motionFeedbackStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _motionFeedbackCurve;

    [ObservableProperty]
    private bool _bumpFeedbackEnabled;

    [ObservableProperty]
    private int _bumpFeedbackStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _bumpFeedbackCurve;

    [ObservableProperty]
    private bool _suspensionHitFeedbackEnabled;

    [ObservableProperty]
    private int _suspensionHitFeedbackStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _suspensionHitFeedbackCurve;

    [ObservableProperty]
    private bool _landingFeedbackEnabled;

    [ObservableProperty]
    private int _landingFeedbackStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _landingFeedbackCurve;

    [ObservableProperty]
    private bool _collisionFeedbackEnabled;

    [ObservableProperty]
    private int _collisionFeedbackStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _collisionFeedbackCurve;

    [ObservableProperty]
    private bool _terrainRumbleEnabled;

    [ObservableProperty]
    private int _terrainRumbleStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _terrainRumbleCurve;

    [ObservableProperty]
    private bool _drivetrainPulseEnabled;

    [ObservableProperty]
    private int _drivetrainPulseStrengthPercent;

    [ObservableProperty]
    private FfbCurveKind _drivetrainPulseCurve;

    [ObservableProperty]
    private string _activeGameplayEffects = "None";

    [ObservableProperty]
    private string _activeVehicleCategory = "Unknown";

    [ObservableProperty]
    private bool _speedSpringActive;

    [ObservableProperty]
    private bool _speedDamperActive;

    [ObservableProperty]
    private bool _mechanicalFrictionActive;

    [ObservableProperty]
    private bool _rpmVibrationActive;

    [ObservableProperty]
    private bool _surfaceFeedbackActive;

    [ObservableProperty]
    private bool _slipFeedbackActive;

    [ObservableProperty]
    private bool _bumpFeedbackActive;

    [ObservableProperty]
    private bool _suspensionHitActive;

    [ObservableProperty]
    private bool _leftSuspensionHitActive;

    [ObservableProperty]
    private bool _rightSuspensionHitActive;

    [ObservableProperty]
    private bool _landingFeedbackActive;

    [ObservableProperty]
    private bool _collisionFeedbackActive;

    [ObservableProperty]
    private bool _drivetrainPulseActive;

    [ObservableProperty]
    private bool _loadResistanceActive;

    [ObservableProperty]
    private bool _motionFeedbackActive;

    [ObservableProperty]
    private bool _slewSmoothingActive;

    [ObservableProperty]
    private bool _hillStandstillLoadActive;

    [ObservableProperty]
    private bool _sideSlopeBiasActive;

    [ObservableProperty]
    private bool _implementBiasActive;

    [ObservableProperty]
    private bool _contactReliefControlsActive;

    [ObservableProperty]
    private bool _antiOscillationActive;

    [ObservableProperty]
    private bool _wetnessFeedbackActive;

    [ObservableProperty]
    private bool _steeringSlipReliefActive;

    [ObservableProperty]
    private bool _terrainRumbleActive;

    [ObservableProperty]
    private bool _gearShiftPulseActive;

    [ObservableProperty]
    private bool _clutchBrakeJerkActive;

    [ObservableProperty]
    private bool _engineStartStopActive;

    [ObservableProperty]
    private bool _engineUnderLoadActive;

    [ObservableProperty]
    private bool _engineLuggingActive;

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
    private string _terrainRumbleOutput = "0%";

    [ObservableProperty]
    private string _loadFactor = "1.00";

    [ObservableProperty]
    private string _telemetryFade = "0%";

    [ObservableProperty]
    private string _selectedEffectCategory = VehicleCategoryFfbProfile.TractorWheeled;

    [ObservableProperty]
    private EffectCategoryOption? _selectedEffectCategoryOption;

    public MainWindowViewModel()
        : this(new ConfigStore(), null, null, null)
    {
    }

    public MainWindowViewModel(ConfigStore configStore, IFfbBackend? backend, TelemetryReceiverService? telemetryReceiver, AppLogService? log)
    {
        _configStore = configStore;
        var configDirectory = Path.GetDirectoryName(_configStore.ConfigPath);
        _effectsProfileStore = string.IsNullOrWhiteSpace(configDirectory)
            ? new EffectsProfileStore()
            : new EffectsProfileStore(Path.Combine(configDirectory, "effect-profiles"));
        _log = log ?? new AppLogService();
        _backend = backend ?? new DirectInputFfbBackend(_log);
        _telemetryReceiver = telemetryReceiver ?? new TelemetryReceiverService(_log);
        _safety = new SafetyManager(_backend, _log);
        _effectStatusWriter = new EffectStatusWriter(_log);
        _panicHotkey = new PanicHotkeyService(_log);
        _panicHotkey.Pressed += HandlePanicHotkey;
        _panicHotkey.Register();
        _telemetryCapture = new TelemetryCaptureLogService(_log);
        _telemetryCaptureHotkey = new TelemetryCaptureHotkeyService(_log);
        _telemetryCaptureHotkey.Pressed += HandleTelemetryCaptureHotkey;
        _telemetryCaptureHotkey.Register();
        _config = _configStore.Load();
        _config.GameplayFfb = _effectsProfileStore.Load(_config.GameplayFfb.WheelProfileId, _config.GameplayFfb);
        UseGlobalForceLimitOnly(_config.GameplayFfb);
        _loadingConfig = true;
        _globalForceLimitPercent = _config.GlobalForceLimitPercent;
        _effectOverlayEnabled = _config.EffectOverlayEnabled;
        _effectOverlayClickThrough = _config.EffectOverlayClickThrough;
        _effectOverlayX = _config.EffectOverlayX;
        _effectOverlayY = _config.EffectOverlayY;
        GameplayFfbEnabled = _config.GameplayFfb.Enabled;
        LoadGameplaySettingsIntoUi(GetSelectedCategoryProfile(SelectedEffectCategory));
        _loadingConfig = false;
        _configPath = _configStore.ConfigPath;
        _logPath = _log.LogPath;
        Logs = _log.Entries;
        LogEvents = _log.EventEntries;
        EffectCategoryOptions = VehicleCategoryFfbProfile.Categories
            .Select(category => new EffectCategoryOption(category, GetVehicleCategoryDisplayName(category)))
            .ToArray();
        SelectedEffectCategoryOption = EffectCategoryOptions.FirstOrDefault(option => option.Id == SelectedEffectCategory);
        LoadTireSurfaceTuningIntoUi();

        _backend.UpdateForceLimits(GlobalForceLimitPercent, 100);
        _telemetryReceiver.StateChanged += OnTelemetryStateChanged;
        _telemetryReceiver.FfbStateChanged += OnTelemetryFfbStateChanged;
        _telemetryReceiver.Start(
            _config.TelemetryHost,
            _config.TelemetryPort,
            _config.TelemetryLostTimeoutMs,
            _config.TelemetryFilePath,
            ffbUpdateRateHz: _config.TelemetryFfbUpdateRateHz,
            uiRefreshMs: _config.TelemetryUiRefreshMs,
            transportMode: _config.TelemetryTransportMode);
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
    public ObservableCollection<AppLogEntry> LogEvents { get; }
    public ObservableCollection<TireSurfaceMatrixRow> TireSurfaceMatrixRows { get; } = [];
    public ObservableCollection<SurfaceAliasRow> SurfaceAliasRows { get; } = [];
    public IReadOnlyList<string> TireSurfaceTargets => TireSurfaceTuningSettings.SurfaceTypes;
    public IReadOnlyList<string> EffectCategories => VehicleCategoryFfbProfile.Categories;
    public IReadOnlyList<EffectCategoryOption> EffectCategoryOptions { get; }
    public IReadOnlyList<FfbCurveKind> CurveKinds { get; } =
    [
        FfbCurveKind.Smooth,
        FfbCurveKind.Linear,
        FfbCurveKind.Aggressive
    ];
    public string PanicHotkey => _config.PanicHotkey;
    public string TelemetryCaptureHotkey => "Ctrl+Alt+L";
    public bool CanRunEffects => SelectedDevice?.IsForceFeedbackCapable == true && _backend.HasSelectedFfbDevice;
    public string DeviceCountText => $"{Devices.Count} device(s)";
    public string FfbReadyText => $"FFB: {FfbStatus}";
    public string TelemetryReadyText => $"Telemetry: {TelemetryStatusText}";
    public string WheelReadyText => $"Wheel: {WheelStatus}";
    public bool IsTelemetryConnected => TelemetryStatus == "Connected";
    public bool IsTelemetryWaiting => TelemetryStatus == "Waiting";
    public bool IsTelemetryLost => TelemetryStatus == "Lost";
    public bool IsFfbReady => CanRunEffects;
    public bool IsFfbInactive => !CanRunEffects;
    public bool IsFfbOperational => GameplayFfbEnabled && CanRunEffects && !_gameplayFfbPausedByStopAll;
    public bool IsFfbBlocked => !IsFfbOperational;
    public string WheelStatus => SelectedDevice is null
        ? "not selected"
        : CanRunEffects
            ? $"{SelectedDevice.DisplayName} ready"
            : $"{SelectedDevice.DisplayName} selected";
    public string WheelReason => SelectedDevice is null
        ? DeviceCapabilityStatus
        : SelectedDeviceStatus;
    public string WheelNextAction => SelectedDevice is null
        ? Devices.Count == 0 ? "Connect a wheel and press Scan." : "Select your force feedback wheel."
        : CanRunEffects ? "Run a gentle test effect or start FS25 telemetry." : "Choose a device that reports force feedback support.";
    public string TelemetryStatusText => TelemetryStatus switch
    {
        "Connected" => "receiving data",
        "Lost" => "signal lost",
        _ => "waiting for FS25"
    };
    public string TelemetryReason => TelemetryStatus switch
    {
        "Connected" => $"{PacketRate} from {LastPacketSource}; last packet {LastPacketAge}.",
        "Lost" => $"No fresh packet for {LastPacketAge}.",
        _ => "No telemetry packets received yet."
    };
    public string TelemetryNextAction => TelemetryStatus switch
    {
        "Connected" => CurrentVehicle == "No vehicle" ? "Enter a vehicle in FS25." : $"Drive {CurrentVehicle} to feel live effects.",
        "Lost" => "Return to FS25 or check the telemetry plugin.",
        _ => "Start FS25, load a save, and enter a vehicle."
    };
    public string FfbStatus => !GameplayFfbEnabled || _gameplayFfbPausedByStopAll
        ? "off"
        : CanRunEffects
            ? "ready"
            : "waiting for wheel";
    public string FfbReason => !GameplayFfbEnabled
        ? "Gameplay force feedback is disabled from the header FFB button."
        : _gameplayFfbPausedByStopAll
            ? GameplayFfbRuntimeStatus
            : CanRunEffects
                ? GameplayFfbRuntimeStatus
                : "A force feedback wheel must be selected before effects can run.";
    public string FfbNextAction => !GameplayFfbEnabled
        ? "Use the FFB button in the header when you are ready."
        : _gameplayFfbPausedByStopAll
            ? "Press the FFB button in the header or select the wheel again before driving."
            : CanRunEffects
                ? "Keep the force limits low until the wheel feels comfortable."
                : "Scan and select your wheel on the Device tab.";

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

    partial void OnEffectOverlayEnabledChanged(bool value)
    {
        if (_loadingConfig)
        {
            return;
        }

        _config.EffectOverlayEnabled = value;
        _configStore.Save(_config);
    }

    partial void OnEffectOverlayClickThroughChanged(bool value)
    {
        if (_loadingConfig)
        {
            return;
        }

        _config.EffectOverlayClickThrough = value;
        _configStore.Save(_config);
    }

    public void SaveEffectOverlayPosition(int x, int y)
    {
        if (_disposed)
        {
            return;
        }

        EffectOverlayX = Math.Clamp(x, -10000, 10000);
        EffectOverlayY = Math.Clamp(y, -10000, 10000);
        _config.EffectOverlayX = EffectOverlayX;
        _config.EffectOverlayY = EffectOverlayY;
        _configStore.Save(_config);
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
        RefreshDashboardStatusProperties();

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
    private void ToggleGameplayFfb()
    {
        if (!GameplayFfbEnabled || _gameplayFfbPausedByStopAll)
        {
            _gameplayFfbPausedByStopAll = false;
            GameplayFfbEnabled = true;
            GameplayFfbRuntimeStatus = CanRunEffects ? "FFB enabled" : "Device not acquired";
            SaveGameplaySettingsFromUi();
            RefreshDashboardStatusProperties();
            return;
        }

        GameplayFfbEnabled = false;
    }

    [RelayCommand]
    private void ReloadBridge()
    {
        try
        {
            _log.Information("Reload requested");
            _gameplayFfbPausedByReload = true;
            _safety.StopAll("reload");
            OnGameplayOutputChanged(GameplayFfbOutput.Zero);
            _effectStatusWriter.WriteZero(ActiveVehicleCategory);
            _telemetryReceiver.Stop();
            var scanFailed = false;
            try
            {
                ScanDevices();
            }
            catch (Exception ex)
            {
                scanFailed = true;
                _log.Error(ex, "Reload device scan failed");
                SelectedDeviceStatus = "Reload scan failed";
                BackendStatus = $"Reload scan failed: {ex.Message}";
            }

            _telemetryReceiver.Start(
                _config.TelemetryHost,
                _config.TelemetryPort,
                _config.TelemetryLostTimeoutMs,
                _config.TelemetryFilePath,
                ffbUpdateRateHz: _config.TelemetryFfbUpdateRateHz,
                uiRefreshMs: _config.TelemetryUiRefreshMs,
                transportMode: _config.TelemetryTransportMode);
            GameplayFfbRuntimeStatus = GameplayFfbEnabled ? "Reloaded" : "FFB disabled";
            if (!scanFailed)
            {
                BackendStatus = SelectedDevice is null ? "Reloaded; waiting for device" : BackendStatus;
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Reload failed");
            GameplayFfbRuntimeStatus = "Reload failed";
            BackendStatus = $"Reload failed: {ex.Message}";
        }
        finally
        {
            _gameplayFfbPausedByReload = false;
            RefreshCommandStates();
            RefreshDashboardStatusProperties();
        }
    }

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

    [RelayCommand]
    private void CopyEffectStrengthsToAllCategories()
    {
        SaveGameplaySettingsToProfile(GetSelectedCategoryProfile(SelectedEffectCategory));
        var source = GetSelectedCategoryProfile(SelectedEffectCategory);
        foreach (var category in VehicleCategoryFfbProfile.Categories)
        {
            if (category == SelectedEffectCategory)
            {
                continue;
            }

            CopyEffectStrengths(source, GetSelectedCategoryProfile(category));
        }

        SaveGameplayProfile();
        _log.Information("Effect strengths copied from {Category} to all categories", SelectedEffectCategory);
    }

    [RelayCommand]
    private void ResetMatrixDefaults()
    {
        _config.GameplayFfb.TireSurfaceTuning.Matrix = TireSurfaceTuningSettings.CreateDefaultMatrix();
        LoadTireSurfaceTuningIntoUi();
        SaveGameplayProfile();
    }

    [RelayCommand]
    private void ResetSelectedSurface(TireSurfaceMatrixRow? row)
    {
        if (row is null)
        {
            return;
        }

        var defaults = TireSurfaceTuningSettings.CreateDefaultMatrix();
        foreach (var profile in TireSurfaceTuningSettings.TireProfiles)
        {
            row.Set(profile, defaults[profile][row.SurfaceType], notify: true);
        }

        SaveTireSurfaceMatrixFromUi();
    }

    [RelayCommand]
    private void SaveAlias()
    {
        var raw = NewSurfaceAliasRaw.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        _config.GameplayFfb.TireSurfaceTuning.SurfaceAliases[raw] = TireSurfaceTuningSettings.NormalizeSurfaceType(SelectedSurfaceAliasTarget);
        NewSurfaceAliasRaw = "";
        LoadSurfaceAliasesIntoUi();
        SaveGameplayProfile();
    }

    [RelayCommand]
    private void ToggleTelemetryCapture()
    {
        if (TelemetryCaptureRecording)
        {
            _telemetryCapture.Stop();
            TelemetryCaptureRecording = false;
            TelemetryCaptureStatus = $"Stopped at {_telemetryCapture.SampleCount} samples";
            TelemetryCapturePath = _telemetryCapture.CurrentNdjsonPath ?? "-";
            OnPropertyChanged(nameof(TelemetryCaptureButtonText));
            return;
        }

        var path = _telemetryCapture.Start();
        TelemetryCaptureRecording = true;
        TelemetryCapturePath = path;
        TelemetryCaptureStatus = "Recording 0 samples";
        OnPropertyChanged(nameof(TelemetryCaptureButtonText));
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

    private void SyncSelectedEffectCategoryWithActiveVehicle(string? category)
    {
        if (_effectCategoryPinnedByUser)
        {
            return;
        }

        var normalized = VehicleCategoryFfbProfile.Categories.Contains(category ?? string.Empty)
            ? category!
            : VehicleCategoryFfbProfile.Unknown;
        if (SelectedEffectCategory == normalized)
        {
            return;
        }

        _syncingEffectCategoryFromActiveVehicle = true;
        try
        {
            SelectedEffectCategory = normalized;
        }
        finally
        {
            _syncingEffectCategoryFromActiveVehicle = false;
        }
    }

    private void SelectDevice(DeviceInfo? device)
    {
        if (device is null)
        {
            SelectedDeviceStatus = "No FFB device selected";
            DeviceCapabilityStatus = "No selected device";
            RefreshCommandStates();
            RefreshDashboardStatusProperties();
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

        var selected = _backend.SelectDevice(device, _windowHandle, GlobalForceLimitPercent, 100);
        if (selected)
        {
            var wheelProfile = WheelProfileCatalog.Resolve(device);
            SelectedDeviceStatus = $"{device.DisplayName} acquired ({wheelProfile.DisplayName})";
            BackendStatus = "DirectInput FFB ready";
            _config.SelectedDeviceStableId = device.StableId;
            _config.WheelProfileId = wheelProfile.Id;
            _config.DeviceProfileName = wheelProfile.DisplayName;
            _config.RotationDegrees = wheelProfile.RotationDegrees;
            _config.RecommendedMode = wheelProfile.RecommendedMode;
            _config.GlobalForceLimitPercent = wheelProfile.DefaultGlobalForceLimitPercent;
            GlobalForceLimitPercent = wheelProfile.DefaultGlobalForceLimitPercent;
            _backend.UpdateForceLimits(GlobalForceLimitPercent, 100);
            _config.GameplayFfb = _effectsProfileStore.Load(wheelProfile.Id, _config.GameplayFfb);
            _config.GameplayFfb.WheelProfileId = wheelProfile.Id;
            _config.GameplayFfb.DeviceHapticProfileName = wheelProfile.DisplayName;
            _loadingConfig = true;
            GameplayFfbEnabled = _config.GameplayFfb.Enabled;
            LoadGameplaySettingsIntoUi(GetSelectedCategoryProfile(SelectedEffectCategory));
            _loadingConfig = false;
            _configStore.Save(_config);
        }
        else
        {
            SelectedDeviceStatus = $"{device.DisplayName} could not be acquired";
            BackendStatus = "DirectInput acquisition failed";
        }

        RefreshCommandStates();
        RefreshDashboardStatusProperties();
    }

    private void SaveForceLimits()
    {
        _config.GlobalForceLimitPercent = GlobalForceLimitPercent;
        _config.DeviceForceLimitPercent = 100;
        _backend.UpdateForceLimits(GlobalForceLimitPercent, 100);
        _configStore.Save(_config);
        _log.Information("Force limit updated: global={GlobalLimit}%", GlobalForceLimitPercent);
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

        RefreshDashboardStatusProperties();
    }
    partial void OnSelectedEffectCategoryChanged(string value)
    {
        if (SelectedEffectCategoryOption?.Id != value)
        {
            SelectedEffectCategoryOption = EffectCategoryOptions.FirstOrDefault(option => option.Id == value);
        }

        if (_loadingConfig)
        {
            return;
        }

        if (!_syncingEffectCategoryFromActiveVehicle)
        {
            _effectCategoryPinnedByUser = true;
        }

        _loadingConfig = true;
        LoadGameplaySettingsIntoUi(GetSelectedCategoryProfile(value));
        _loadingConfig = false;
    }

    partial void OnSelectedEffectCategoryOptionChanged(EffectCategoryOption? value)
    {
        if (value is not null && SelectedEffectCategory != value.Id)
        {
            SelectedEffectCategory = value.Id;
        }
    }
    partial void OnSpeedSpringEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedSpringStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedSpringCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedDamperEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedDamperStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSpeedDamperCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnMechanicalFrictionEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnMechanicalFrictionStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnMechanicalFrictionCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnLoadResistanceEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnLoadResistanceStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnLoadResistanceCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnSlewSmoothingEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnSlewSmoothingStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnHillStandstillLoadEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnHillStandstillLoadStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnHillStandstillLoadCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnSideSlopeBiasEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnSideSlopeBiasStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSideSlopeBiasCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnImplementBiasEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnImplementBiasStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnImplementBiasCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnEngineVibrationEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnEngineVibrationStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnEngineIdleStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnEngineLoadStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnEngineLuggingBoostPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnEngineVibrationCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnGearShiftPulseEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnGearShiftPulseStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnGearShiftPulseCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnGearShiftPulseCooldownMsChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnEngineStartStopPulseEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnEngineStartStopPulseStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnEngineStartStopPulseCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnEngineDrivetrainMaxPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSurfaceFeedbackEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnSurfaceFeedbackStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSurfaceFeedbackCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnSlipFeedbackEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnSlipFeedbackStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSlipFeedbackCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnWetnessFeedbackEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnWetnessFeedbackStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnWetnessFeedbackCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnMotionFeedbackEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnMotionFeedbackStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnMotionFeedbackCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnBumpFeedbackEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnBumpFeedbackStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnBumpFeedbackCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnSuspensionHitFeedbackEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnSuspensionHitFeedbackStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnSuspensionHitFeedbackCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnLandingFeedbackEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnLandingFeedbackStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnLandingFeedbackCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnCollisionFeedbackEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnCollisionFeedbackStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnCollisionFeedbackCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnTerrainRumbleEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnTerrainRumbleStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnTerrainRumbleCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();
    partial void OnDrivetrainPulseEnabledChanged(bool value) => SaveGameplaySettingsFromUi();
    partial void OnDrivetrainPulseStrengthPercentChanged(int value) => SaveGameplaySettingsFromUi();
    partial void OnDrivetrainPulseCurveChanged(FfbCurveKind value) => SaveGameplaySettingsFromUi();

    private void LoadGameplaySettingsIntoUi(GameplayFfbSettings settings)
    {
        GameplayFfbEnabled = settings.Enabled;
        LoadGameplaySettingsIntoUi(GetSelectedCategoryProfile(SelectedEffectCategory));
    }

    private void LoadGameplaySettingsIntoUi(GameplayFfbEffectProfile settings)
    {
        SpeedSpringEnabled = settings.SpeedSpring.Enabled;
        SpeedSpringStrengthPercent = settings.SpeedSpring.StrengthPercent;
        SpeedSpringCurve = settings.SpeedSpring.Curve;
        SpeedDamperEnabled = settings.SpeedDamper.Enabled;
        SpeedDamperStrengthPercent = settings.SpeedDamper.StrengthPercent;
        SpeedDamperCurve = settings.SpeedDamper.Curve;
        MechanicalFrictionEnabled = settings.MechanicalFriction.Enabled;
        MechanicalFrictionStrengthPercent = settings.MechanicalFriction.StrengthPercent;
        MechanicalFrictionCurve = settings.MechanicalFriction.Curve;
        LoadResistanceEnabled = settings.LoadResistance.Enabled;
        LoadResistanceStrengthPercent = settings.LoadResistance.StrengthPercent;
        LoadResistanceCurve = settings.LoadResistance.Curve;
        SlewSmoothingEnabled = settings.SlewSmoothing.Enabled;
        SlewSmoothingStrengthPercent = settings.SlewSmoothing.StrengthPercent;
        HillStandstillLoadEnabled = settings.HillStandstillLoad.Enabled;
        HillStandstillLoadStrengthPercent = settings.HillStandstillLoad.StrengthPercent;
        HillStandstillLoadCurve = settings.HillStandstillLoad.Curve;
        SideSlopeBiasEnabled = settings.SideSlopeBias.Enabled;
        SideSlopeBiasStrengthPercent = settings.SideSlopeBias.StrengthPercent;
        SideSlopeBiasCurve = settings.SideSlopeBias.Curve;
        ImplementBiasEnabled = settings.ImplementBias.Enabled;
        ImplementBiasStrengthPercent = settings.ImplementBias.StrengthPercent;
        ImplementBiasCurve = settings.ImplementBias.Curve;
        EngineVibrationEnabled = settings.EngineVibration.Enabled;
        EngineVibrationStrengthPercent = settings.EngineVibration.StrengthPercent;
        EngineIdleStrengthPercent = settings.EngineVibration.IdleStrengthPercent;
        EngineLoadStrengthPercent = settings.EngineVibration.LoadStrengthPercent;
        EngineLuggingBoostPercent = settings.EngineVibration.LuggingBoostPercent;
        EngineVibrationCurve = settings.EngineVibration.Curve;
        GearShiftPulseEnabled = settings.GearShiftPulse.Enabled;
        GearShiftPulseStrengthPercent = settings.GearShiftPulse.StrengthPercent;
        GearShiftPulseCurve = settings.GearShiftPulse.Curve;
        GearShiftPulseCooldownMs = settings.GearShiftPulse.CooldownMs;
        EngineStartStopPulseEnabled = settings.EngineStartStopPulse.Enabled;
        EngineStartStopPulseStrengthPercent = settings.EngineStartStopPulse.StrengthPercent;
        EngineStartStopPulseCurve = settings.EngineStartStopPulse.Curve;
        EngineDrivetrainMaxPercent = settings.EngineDrivetrainMaxPercent;
        SurfaceFeedbackEnabled = settings.SurfaceFeedback.Enabled;
        SurfaceFeedbackStrengthPercent = settings.SurfaceFeedback.StrengthPercent;
        SurfaceFeedbackCurve = settings.SurfaceFeedback.Curve;
        SlipFeedbackEnabled = settings.SlipFeedback.Enabled;
        SlipFeedbackStrengthPercent = settings.SlipFeedback.StrengthPercent;
        SlipFeedbackCurve = settings.SlipFeedback.Curve;
        WetnessFeedbackEnabled = settings.WetnessFeedback.Enabled;
        WetnessFeedbackStrengthPercent = settings.WetnessFeedback.StrengthPercent;
        WetnessFeedbackCurve = settings.WetnessFeedback.Curve;
        MotionFeedbackEnabled = settings.MotionFeedback.Enabled;
        MotionFeedbackStrengthPercent = settings.MotionFeedback.StrengthPercent;
        MotionFeedbackCurve = settings.MotionFeedback.Curve;
        BumpFeedbackEnabled = settings.BumpFeedback.Enabled;
        BumpFeedbackStrengthPercent = settings.BumpFeedback.StrengthPercent;
        BumpFeedbackCurve = settings.BumpFeedback.Curve;
        SuspensionHitFeedbackEnabled = settings.SuspensionHitFeedback.Enabled;
        SuspensionHitFeedbackStrengthPercent = settings.SuspensionHitFeedback.StrengthPercent;
        SuspensionHitFeedbackCurve = settings.SuspensionHitFeedback.Curve;
        LandingFeedbackEnabled = settings.LandingFeedback.Enabled;
        LandingFeedbackStrengthPercent = settings.LandingFeedback.StrengthPercent;
        LandingFeedbackCurve = settings.LandingFeedback.Curve;
        CollisionFeedbackEnabled = settings.CollisionFeedback.Enabled;
        CollisionFeedbackStrengthPercent = settings.CollisionFeedback.StrengthPercent;
        CollisionFeedbackCurve = settings.CollisionFeedback.Curve;
        TerrainRumbleEnabled = settings.TerrainRumble.Enabled;
        TerrainRumbleStrengthPercent = settings.TerrainRumble.StrengthPercent;
        TerrainRumbleCurve = settings.TerrainRumble.Curve;
        DrivetrainPulseEnabled = settings.DrivetrainPulse.Enabled;
        DrivetrainPulseStrengthPercent = settings.DrivetrainPulse.StrengthPercent;
        DrivetrainPulseCurve = settings.DrivetrainPulse.Curve;
    }

    private void SaveGameplaySettingsFromUi()
    {
        if (_loadingConfig)
        {
            return;
        }

        UseGlobalForceLimitOnly(_config.GameplayFfb);
        _config.GameplayFfb.Enabled = GameplayFfbEnabled;
        SaveGameplaySettingsToProfile(GetSelectedCategoryProfile(SelectedEffectCategory));
        UseGlobalForceLimitOnly(_config.GameplayFfb);
        SaveGameplayProfile();
        _log.Information("Gameplay FFB settings updated");
    }

    private void SaveGameplayProfile()
    {
        _config.GameplayFfb.TireSurfaceTuning = TireSurfaceTuningSettings.CreateNormalized(_config.GameplayFfb.TireSurfaceTuning);
        _effectsProfileStore.Save(_config.GameplayFfb.WheelProfileId, _config.GameplayFfb);
        _configStore.Save(_config);
    }

    private void LoadTireSurfaceTuningIntoUi()
    {
        _config.GameplayFfb.TireSurfaceTuning = TireSurfaceTuningSettings.CreateNormalized(_config.GameplayFfb.TireSurfaceTuning);
        TireSurfaceMatrixRows.Clear();
        foreach (var surface in TireSurfaceTuningSettings.SurfaceTypes)
        {
            var row = new TireSurfaceMatrixRow(surface);
            foreach (var profile in TireSurfaceTuningSettings.TireProfiles)
            {
                row.Set(profile, _config.GameplayFfb.TireSurfaceTuning.GetMultiplierPercent(profile, surface), notify: false);
            }

            row.Changed += SaveTireSurfaceMatrixFromUi;
            TireSurfaceMatrixRows.Add(row);
        }

        LoadSurfaceAliasesIntoUi();
    }

    private void LoadSurfaceAliasesIntoUi()
    {
        SurfaceAliasRows.Clear();
        foreach (var alias in _config.GameplayFfb.TireSurfaceTuning.SurfaceAliases.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var row = new SurfaceAliasRow(alias.Key, alias.Value, TireSurfaceTargets);
            row.Changed += SaveSurfaceAliasesFromUi;
            SurfaceAliasRows.Add(row);
        }
    }

    private void SaveTireSurfaceMatrixFromUi()
    {
        if (_loadingConfig)
        {
            return;
        }

        var matrix = TireSurfaceTuningSettings.CreateDefaultMatrix();
        foreach (var row in TireSurfaceMatrixRows)
        {
            foreach (var profile in TireSurfaceTuningSettings.TireProfiles)
            {
                matrix[profile][row.SurfaceType] = row.Get(profile);
            }
        }

        _config.GameplayFfb.TireSurfaceTuning.Matrix = matrix;
        SaveGameplayProfile();
    }

    private void SaveSurfaceAliasesFromUi()
    {
        if (_loadingConfig)
        {
            return;
        }

        _config.GameplayFfb.TireSurfaceTuning.SurfaceAliases = SurfaceAliasRows
            .Where(row => !string.IsNullOrWhiteSpace(row.Raw))
            .ToDictionary(row => row.Raw.Trim(), row => TireSurfaceTuningSettings.NormalizeSurfaceType(row.Normalized), StringComparer.OrdinalIgnoreCase);
        SaveGameplayProfile();
    }

    private static void CopyEffectStrengths(GameplayFfbEffectProfile source, GameplayFfbEffectProfile target)
    {
        target.SpeedSpring.StrengthPercent = source.SpeedSpring.StrengthPercent;
        target.SpeedDamper.StrengthPercent = source.SpeedDamper.StrengthPercent;
        target.MechanicalFriction.StrengthPercent = source.MechanicalFriction.StrengthPercent;
        target.LoadResistance.StrengthPercent = source.LoadResistance.StrengthPercent;
        target.SlewSmoothing.StrengthPercent = source.SlewSmoothing.StrengthPercent;
        target.HillStandstillLoad.StrengthPercent = source.HillStandstillLoad.StrengthPercent;
        target.SideSlopeBias.StrengthPercent = source.SideSlopeBias.StrengthPercent;
        target.ImplementBias.StrengthPercent = source.ImplementBias.StrengthPercent;
        target.EngineVibration.StrengthPercent = source.EngineVibration.StrengthPercent;
        target.EngineVibration.IdleStrengthPercent = source.EngineVibration.IdleStrengthPercent;
        target.EngineVibration.LoadStrengthPercent = source.EngineVibration.LoadStrengthPercent;
        target.EngineVibration.LuggingBoostPercent = source.EngineVibration.LuggingBoostPercent;
        target.GearShiftPulse.StrengthPercent = source.GearShiftPulse.StrengthPercent;
        target.EngineStartStopPulse.StrengthPercent = source.EngineStartStopPulse.StrengthPercent;
        target.SurfaceFeedback.StrengthPercent = source.SurfaceFeedback.StrengthPercent;
        target.SlipFeedback.StrengthPercent = source.SlipFeedback.StrengthPercent;
        target.WetnessFeedback.StrengthPercent = source.WetnessFeedback.StrengthPercent;
        target.MotionFeedback.StrengthPercent = source.MotionFeedback.StrengthPercent;
        target.BumpFeedback.StrengthPercent = source.BumpFeedback.StrengthPercent;
        target.SuspensionHitFeedback.StrengthPercent = source.SuspensionHitFeedback.StrengthPercent;
        target.LandingFeedback.StrengthPercent = source.LandingFeedback.StrengthPercent;
        target.CollisionFeedback.StrengthPercent = source.CollisionFeedback.StrengthPercent;
        target.TerrainRumble.StrengthPercent = source.TerrainRumble.StrengthPercent;
        target.DrivetrainPulse.StrengthPercent = source.DrivetrainPulse.StrengthPercent;
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
        profile.SpeedSpring.MaxOutputPercent = 100;
        profile.SpeedSpring.Curve = SpeedSpringCurve;
        profile.SpeedDamper.Enabled = SpeedDamperEnabled;
        profile.SpeedDamper.StrengthPercent = Math.Clamp(SpeedDamperStrengthPercent, 0, 100);
        profile.SpeedDamper.MaxOutputPercent = 100;
        profile.SpeedDamper.Curve = SpeedDamperCurve;
        profile.MechanicalFriction.Enabled = MechanicalFrictionEnabled;
        profile.MechanicalFriction.StrengthPercent = Math.Clamp(MechanicalFrictionStrengthPercent, 0, 100);
        profile.MechanicalFriction.MaxOutputPercent = 100;
        profile.MechanicalFriction.Curve = MechanicalFrictionCurve;
        profile.LoadResistance.Enabled = LoadResistanceEnabled;
        profile.LoadResistance.StrengthPercent = Math.Clamp(LoadResistanceStrengthPercent, 0, 100);
        profile.LoadResistance.MaxOutputPercent = 100;
        profile.LoadResistance.Curve = LoadResistanceCurve;
        profile.SlewSmoothing.Enabled = SlewSmoothingEnabled;
        profile.SlewSmoothing.StrengthPercent = Math.Clamp(SlewSmoothingStrengthPercent, 0, 100);
        profile.HillStandstillLoad.Enabled = HillStandstillLoadEnabled;
        profile.HillStandstillLoad.StrengthPercent = Math.Clamp(HillStandstillLoadStrengthPercent, 0, 100);
        profile.HillStandstillLoad.MaxOutputPercent = 100;
        profile.HillStandstillLoad.Curve = HillStandstillLoadCurve;
        profile.SideSlopeBias.Enabled = SideSlopeBiasEnabled;
        profile.SideSlopeBias.StrengthPercent = Math.Clamp(SideSlopeBiasStrengthPercent, 0, 100);
        profile.SideSlopeBias.MaxOutputPercent = 100;
        profile.SideSlopeBias.Curve = SideSlopeBiasCurve;
        profile.ImplementBias.Enabled = ImplementBiasEnabled;
        profile.ImplementBias.StrengthPercent = Math.Clamp(ImplementBiasStrengthPercent, 0, 100);
        profile.ImplementBias.MaxOutputPercent = 100;
        profile.ImplementBias.Curve = ImplementBiasCurve;
        profile.EngineVibration.Enabled = EngineVibrationEnabled;
        profile.EngineVibration.IdleStrengthPercent = Math.Clamp(EngineIdleStrengthPercent, 0, 100);
        profile.EngineVibration.LoadStrengthPercent = Math.Clamp(EngineLoadStrengthPercent, 0, 100);
        profile.EngineVibration.LuggingBoostPercent = Math.Clamp(EngineLuggingBoostPercent, 0, 100);
        profile.EngineVibration.StrengthPercent = Math.Clamp(Math.Max(profile.EngineVibration.IdleStrengthPercent, profile.EngineVibration.LoadStrengthPercent), 0, 100);
        profile.EngineVibration.MaxOutputPercent = 100;
        profile.EngineVibration.Curve = EngineVibrationCurve;
        profile.GearShiftPulse.Enabled = GearShiftPulseEnabled;
        profile.GearShiftPulse.StrengthPercent = Math.Clamp(GearShiftPulseStrengthPercent, 0, 100);
        profile.GearShiftPulse.MaxOutputPercent = 100;
        profile.GearShiftPulse.Curve = GearShiftPulseCurve;
        profile.GearShiftPulse.CooldownMs = Math.Clamp(GearShiftPulseCooldownMs, 100, 700);
        profile.EngineStartStopPulse.Enabled = EngineStartStopPulseEnabled;
        profile.EngineStartStopPulse.StrengthPercent = Math.Clamp(EngineStartStopPulseStrengthPercent, 0, 100);
        profile.EngineStartStopPulse.MaxOutputPercent = 100;
        profile.EngineStartStopPulse.Curve = EngineStartStopPulseCurve;
        profile.EngineDrivetrainMaxPercent = Math.Clamp(EngineDrivetrainMaxPercent, 0, 100);
        profile.SurfaceFeedback.Enabled = SurfaceFeedbackEnabled;
        profile.SurfaceFeedback.StrengthPercent = Math.Clamp(SurfaceFeedbackStrengthPercent, 0, 100);
        profile.SurfaceFeedback.MaxOutputPercent = 100;
        profile.SurfaceFeedback.Curve = SurfaceFeedbackCurve;
        profile.SlipFeedback.Enabled = SlipFeedbackEnabled;
        profile.SlipFeedback.StrengthPercent = Math.Clamp(SlipFeedbackStrengthPercent, 0, 100);
        profile.SlipFeedback.MaxOutputPercent = 100;
        profile.SlipFeedback.Curve = SlipFeedbackCurve;
        profile.WetnessFeedback.Enabled = WetnessFeedbackEnabled;
        profile.WetnessFeedback.StrengthPercent = Math.Clamp(WetnessFeedbackStrengthPercent, 0, 100);
        profile.WetnessFeedback.MaxOutputPercent = 100;
        profile.WetnessFeedback.Curve = WetnessFeedbackCurve;
        profile.MotionFeedback.Enabled = MotionFeedbackEnabled;
        profile.MotionFeedback.StrengthPercent = Math.Clamp(MotionFeedbackStrengthPercent, 0, 100);
        profile.MotionFeedback.MaxOutputPercent = 100;
        profile.MotionFeedback.Curve = MotionFeedbackCurve;
        profile.BumpFeedback.Enabled = BumpFeedbackEnabled;
        profile.BumpFeedback.StrengthPercent = Math.Clamp(BumpFeedbackStrengthPercent, 0, 100);
        profile.BumpFeedback.MaxOutputPercent = 100;
        profile.BumpFeedback.Curve = BumpFeedbackCurve;
        profile.SuspensionHitFeedback.Enabled = SuspensionHitFeedbackEnabled;
        profile.SuspensionHitFeedback.StrengthPercent = Math.Clamp(SuspensionHitFeedbackStrengthPercent, 0, 100);
        profile.SuspensionHitFeedback.MaxOutputPercent = 100;
        profile.SuspensionHitFeedback.Curve = SuspensionHitFeedbackCurve;
        profile.LandingFeedback.Enabled = LandingFeedbackEnabled;
        profile.LandingFeedback.StrengthPercent = Math.Clamp(LandingFeedbackStrengthPercent, 0, 100);
        profile.LandingFeedback.MaxOutputPercent = 100;
        profile.LandingFeedback.Curve = LandingFeedbackCurve;
        profile.CollisionFeedback.Enabled = CollisionFeedbackEnabled;
        profile.CollisionFeedback.StrengthPercent = Math.Clamp(CollisionFeedbackStrengthPercent, 0, 100);
        profile.CollisionFeedback.MaxOutputPercent = 100;
        profile.CollisionFeedback.Curve = CollisionFeedbackCurve;
        profile.TerrainRumble.Enabled = TerrainRumbleEnabled;
        profile.TerrainRumble.StrengthPercent = Math.Clamp(TerrainRumbleStrengthPercent, 0, 100);
        profile.TerrainRumble.MaxOutputPercent = 100;
        profile.TerrainRumble.Curve = TerrainRumbleCurve;
        profile.DrivetrainPulse.Enabled = DrivetrainPulseEnabled;
        profile.DrivetrainPulse.StrengthPercent = Math.Clamp(DrivetrainPulseStrengthPercent, 0, 100);
        profile.DrivetrainPulse.MaxOutputPercent = 100;
        profile.DrivetrainPulse.Curve = DrivetrainPulseCurve;
    }

    private static void UseGlobalForceLimitOnly(GameplayFfbSettings settings)
    {
        ApplyNoProfileOutputCap(settings);
        foreach (var profile in settings.VehicleCategoryEffectProfiles.Values)
        {
            ApplyNoProfileOutputCap(profile);
        }
    }

    private static void ApplyNoProfileOutputCap(GameplayFfbEffectProfile profile)
    {
        profile.SpeedSpring.MaxOutputPercent = 100;
        profile.SpeedDamper.MaxOutputPercent = 100;
        profile.MechanicalFriction.MaxOutputPercent = 100;
        profile.LoadResistance.MaxOutputPercent = 100;
        profile.HillStandstillLoad.MaxOutputPercent = 100;
        profile.SideSlopeBias.MaxOutputPercent = 100;
        profile.ImplementBias.MaxOutputPercent = 100;
        profile.EngineVibration.MaxOutputPercent = 100;
        profile.GearShiftPulse.MaxOutputPercent = 100;
        profile.EngineStartStopPulse.MaxOutputPercent = 100;
        profile.SurfaceFeedback.MaxOutputPercent = 100;
        profile.SlipFeedback.MaxOutputPercent = 100;
        profile.WetnessFeedback.MaxOutputPercent = 100;
        profile.MotionFeedback.MaxOutputPercent = 100;
        profile.BumpFeedback.MaxOutputPercent = 100;
        profile.SuspensionHitFeedback.MaxOutputPercent = 100;
        profile.LandingFeedback.MaxOutputPercent = 100;
        profile.CollisionFeedback.MaxOutputPercent = 100;
        profile.TerrainRumble.MaxOutputPercent = 100;
        profile.DrivetrainPulse.MaxOutputPercent = 100;
    }

    private GameplayFfbSettings GetRuntimeGameplaySettings()
    {
        if (!_gameplayFfbPausedByStopAll && !_gameplayFfbPausedByReload)
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
            SlewSmoothing = _config.GameplayFfb.SlewSmoothing,
            HillStandstillLoad = _config.GameplayFfb.HillStandstillLoad,
            SideSlopeBias = _config.GameplayFfb.SideSlopeBias,
            ImplementBias = _config.GameplayFfb.ImplementBias,
            EngineVibration = _config.GameplayFfb.EngineVibration,
            GearShiftPulse = _config.GameplayFfb.GearShiftPulse,
            EngineStartStopPulse = _config.GameplayFfb.EngineStartStopPulse,
            EngineDrivetrainMaxPercent = _config.GameplayFfb.EngineDrivetrainMaxPercent,
            SurfaceFeedback = _config.GameplayFfb.SurfaceFeedback,
            SlipFeedback = _config.GameplayFfb.SlipFeedback,
            WetnessFeedback = _config.GameplayFfb.WetnessFeedback,
            MotionFeedback = _config.GameplayFfb.MotionFeedback,
            BumpFeedback = _config.GameplayFfb.BumpFeedback,
            SuspensionHitFeedback = _config.GameplayFfb.SuspensionHitFeedback,
            LandingFeedback = _config.GameplayFfb.LandingFeedback,
            CollisionFeedback = _config.GameplayFfb.CollisionFeedback,
            TerrainRumble = _config.GameplayFfb.TerrainRumble,
            DrivetrainPulse = _config.GameplayFfb.DrivetrainPulse,
            WheelProfileId = _config.GameplayFfb.WheelProfileId,
            DeviceHapticProfileName = _config.GameplayFfb.DeviceHapticProfileName,
            TireSurfaceTuning = _config.GameplayFfb.TireSurfaceTuning,
            VehicleCategoryEffectProfiles = _config.GameplayFfb.VehicleCategoryEffectProfiles
        };
    }

    private void OnGameplayApplyResultChanged(FfbApplyResult result)
    {
        Dispatcher.UIThread.Post(() =>
        {
            GameplayFfbRuntimeStatus = result.Status switch
            {
                FfbApplyStatus.Applied when GameplayFfbEnabled => "FFB enabled",
                FfbApplyStatus.AcquireFailed => "Device not acquired",
                _ when _gameplayFfbPausedByStopAll => "Paused by Stop All",
                _ when GameplayFfbEnabled => "FFB enabled",
                _ => "FFB disabled"
            };

            if (result.Status == FfbApplyStatus.AcquireFailed)
            {
                BackendStatus = result.Message;
            }

            RefreshDashboardStatusProperties();
        });
    }

    private void OnGameplayOutputChanged(GameplayFfbOutput output)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ActiveGameplayEffects = output.ActiveEffectsText;
            ActiveVehicleCategory = output.IsActive ? output.ActiveCategory : ActiveVehicleCategory;
            SpeedSpringActive = output.SpringPercent > 0;
            SpeedDamperActive = output.DamperPercent > 0;
            MechanicalFrictionActive = output.FrictionPercent > 0;
            RpmVibrationActive = output.EngineVibrationPercent > 0;
            SurfaceFeedbackActive = output.SurfaceVibrationPercent > 0;
            SlipFeedbackActive = output.SlipVibrationPercent > 0;
            BumpFeedbackActive = output.EventPulseKind == FfbPulseKind.Bump;
            LeftSuspensionHitActive = output.EventPulseKind == FfbPulseKind.LeftSuspensionHit;
            RightSuspensionHitActive = output.EventPulseKind == FfbPulseKind.RightSuspensionHit;
            SuspensionHitActive = LeftSuspensionHitActive || RightSuspensionHitActive;
            LandingFeedbackActive = output.EventPulseKind == FfbPulseKind.Landing;
            CollisionFeedbackActive = output.EventPulseKind == FfbPulseKind.Collision;
            DrivetrainPulseActive = output.EventPulseKind is FfbPulseKind.DrivetrainJerk or FfbPulseKind.GearShift or FfbPulseKind.EngineStartStop;
            LoadResistanceActive = output.LoadResistanceActive;
            MotionFeedbackActive = output.MotionFeedbackActive;
            SlewSmoothingActive = output.SlewSmoothingActive;
            HillStandstillLoadActive = output.HillStandstillLoadActive;
            SideSlopeBiasActive = output.SideSlopeBiasActive;
            ImplementBiasActive = output.ImplementBiasActive;
            ContactReliefControlsActive = output.ContactReliefControlsActive;
            AntiOscillationActive = output.AntiOscillationActive;
            WetnessFeedbackActive = output.WetnessFeedbackActive;
            SteeringSlipReliefActive = output.SteeringSlipReliefActive;
            TerrainRumbleActive = output.TerrainRumblePercent > 0;
            GearShiftPulseActive = output.EventPulseKind == FfbPulseKind.GearShift;
            ClutchBrakeJerkActive = output.EventPulseKind == FfbPulseKind.DrivetrainJerk;
            EngineStartStopActive = output.EventPulseKind == FfbPulseKind.EngineStartStop;
            EngineUnderLoadActive = output.EngineUnderLoadActive;
            EngineLuggingActive = output.EngineLuggingActive;
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
                ? $"{output.EventPulseKind}: {output.BumpImpulsePercent}% / {output.BumpDurationMs} ms"
                : "0%";
            TerrainRumbleOutput = output.TerrainRumblePercent > 0
                ? $"{output.TerrainRumblePercent}% @ {output.TerrainRumbleHz} Hz"
                : "0%";
            LoadFactor = output.LoadFactor.ToString("0.00");
            TelemetryFade = $"{output.TelemetryFade * 100:0}%";
            RefreshDashboardStatusProperties();
        });
    }

    private void RefreshCommandStates()
    {
        OnPropertyChanged(nameof(CanRunEffects));
        OnPropertyChanged(nameof(FfbReadyText));
        OnPropertyChanged(nameof(IsFfbReady));
        OnPropertyChanged(nameof(IsFfbInactive));
        OnPropertyChanged(nameof(IsFfbOperational));
        OnPropertyChanged(nameof(IsFfbBlocked));
        RefreshDashboardStatusProperties();
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

    private void OnTelemetryFfbStateChanged(TelemetryReceiverState state)
    {
        _telemetryCapture.Record(state);
        if (_telemetryCapture.IsRecording && _telemetryCapture.SampleCount % 16 == 0)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_telemetryCapture.IsRecording)
                {
                    TelemetryCaptureStatus = $"Recording {_telemetryCapture.SampleCount} samples";
                    TelemetryCapturePath = _telemetryCapture.CurrentNdjsonPath ?? "-";
                }
            });
        }
    }

    private void HandleTelemetryCaptureHotkey()
    {
        Dispatcher.UIThread.Post(() => ToggleTelemetryCapture());
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
            ActiveTireSurfaceMultiplier = FormatTireSurfaceMultiplier(50);
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
            SyncSelectedEffectCategoryWithActiveVehicle(packet.VehicleCategory);
            SpeedKmh = FormatNumber(packet.SpeedKmh, "0.0 km/h");
            SteeringAngle = FormatNumber(packet.SteeringAngle, "0.000");
            Rpm = FormatNumber(packet.Rpm, "0 rpm");
            EngineStatus = packet.EngineStarted is null ? "-" : packet.EngineStarted.Value ? "Started" : "Stopped";
            Mass = FormatNumber(packet.MassKg, "0 kg");
            TotalMass = FormatNumber(packet.TotalMassKg, "0 kg");
            MassAndTotal = $"{Mass} / {TotalMass}";
            IsOnField = packet.IsOnField is null ? "-" : packet.IsOnField.Value ? "Yes" : "No";
            SurfaceType = string.IsNullOrWhiteSpace(packet.SurfaceType) ? "-" : packet.SurfaceType;
            SurfaceAttribute = FormatNumber(packet.SurfaceAttribute, "0");
            WetnessAndRain = $"{FormatNumber(packet.GroundWetness, "0.00")} / {FormatNumber(packet.RainScale, "0.00")}";
            WheelSlip = $"{FormatNumber(packet.WheelSlip, "0.00")} / {FormatNumber(packet.MaxWheelSlip, "0.00")}";
            GroundContactRatio = FormatNumber(packet.GroundContactRatio, "0%");
            WheelTireTypes = string.IsNullOrWhiteSpace(packet.WheelTireTypes) ? "-" : packet.WheelTireTypes;
            WheelTireProfile = string.IsNullOrWhiteSpace(packet.ActiveTireProfile) ? "-" : packet.ActiveTireProfile;
            ActiveTireSurfaceMultiplier = FormatTireSurfaceMultiplier(CalculateActiveTireSurfaceMultiplier(packet));
            Attitude = $"{FormatNumber(packet.PitchDeg, "0.0 deg")} / {FormatNumber(packet.RollDeg, "0.0 deg")} / {FormatNumber(packet.CalculatedSlopeDeg, "0.0 deg")}";
            LocalAcceleration = $"{FormatNumber(packet.LocalAccelerationX, "0.00")} / {FormatNumber(packet.LocalAccelerationY, "0.00")} / {FormatNumber(packet.LocalAccelerationZ, "0.00")}";
            BumpImpulse = FormatNumber(packet.VerticalImpactImpulse, "0.00");
            GameState = string.IsNullOrWhiteSpace(packet.GameState) ? "-" : packet.GameState;
        }

        OnPropertyChanged(nameof(TelemetryReadyText));
        OnPropertyChanged(nameof(IsTelemetryConnected));
        OnPropertyChanged(nameof(IsTelemetryWaiting));
        OnPropertyChanged(nameof(IsTelemetryLost));
        RefreshDashboardStatusProperties();
    }

    private void RefreshDashboardStatusProperties()
    {
        OnPropertyChanged(nameof(WheelReadyText));
        OnPropertyChanged(nameof(WheelStatus));
        OnPropertyChanged(nameof(WheelReason));
        OnPropertyChanged(nameof(WheelNextAction));
        OnPropertyChanged(nameof(TelemetryReadyText));
        OnPropertyChanged(nameof(TelemetryStatusText));
        OnPropertyChanged(nameof(TelemetryReason));
        OnPropertyChanged(nameof(TelemetryNextAction));
        OnPropertyChanged(nameof(FfbReadyText));
        OnPropertyChanged(nameof(FfbStatus));
        OnPropertyChanged(nameof(FfbReason));
        OnPropertyChanged(nameof(FfbNextAction));
    }

    public static string GetVehicleCategoryDisplayName(string category) => category switch
    {
        VehicleCategoryFfbProfile.TractorWheeled => "Wheeled tractor",
        VehicleCategoryFfbProfile.TractorTracked => "Tracked tractor",
        VehicleCategoryFfbProfile.HeavyTractorWheeled => "Heavy wheeled tractor",
        VehicleCategoryFfbProfile.HeavyTractorTracked => "Heavy tracked tractor",
        VehicleCategoryFfbProfile.Harvester => "Harvester",
        VehicleCategoryFfbProfile.Truck => "Truck",
        VehicleCategoryFfbProfile.LoaderTelehandler => "Loader / telehandler",
        VehicleCategoryFfbProfile.LightVehicle => "Light vehicle",
        VehicleCategoryFfbProfile.Unknown => "Unknown vehicle",
        _ => category
    };

    private static string FormatNumber(double? value, string format)
    {
        return value is null ? "-" : value.Value.ToString(format);
    }

    private int CalculateActiveTireSurfaceMultiplier(TelemetryPacketV1 packet)
    {
        var tuning = TireSurfaceTuningSettings.CreateNormalized(_config.GameplayFfb.TireSurfaceTuning);
        var surface = tuning.ResolveSurfaceAlias(packet.SurfaceType);
        var profile = TireSurfaceTuningSettings.NormalizeTireProfile(packet.ActiveTireProfile);
        return tuning.GetMultiplierPercent(profile, surface);
    }

    private static string FormatTireSurfaceMultiplier(int percent)
    {
        return $"{TireSurfaceMatrixRow.PercentToScale(percent):0.##}/10";
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
        _telemetryReceiver.FfbStateChanged -= OnTelemetryFfbStateChanged;
        _telemetryReceiver.Dispose();
        _telemetryCaptureHotkey.Pressed -= HandleTelemetryCaptureHotkey;
        _telemetryCaptureHotkey.Dispose();
        _telemetryCapture.Dispose();
        _panicHotkey.Dispose();
        _backend.Dispose();
        _log.Dispose();
    }
}

public sealed record EffectCategoryOption(string Id, string DisplayName);

public sealed partial class TireSurfaceMatrixRow : ObservableObject
{
    [ObservableProperty]
    private int _street;

    [ObservableProperty]
    private int _agricultural;

    [ObservableProperty]
    private int _mud;

    [ObservableProperty]
    private int _offRoad;

    [ObservableProperty]
    private int _tracked;

    [ObservableProperty]
    private int _mixed;

    [ObservableProperty]
    private int _unknown;

    public TireSurfaceMatrixRow(string surfaceType)
    {
        SurfaceType = surfaceType;
    }

    public event Action? Changed;

    public string SurfaceType { get; }

    public IBrush StreetBrush => CreateScaleBrush(Street);

    public IBrush AgriculturalBrush => CreateScaleBrush(Agricultural);

    public IBrush MudBrush => CreateScaleBrush(Mud);

    public IBrush OffRoadBrush => CreateScaleBrush(OffRoad);

    public IBrush TrackedBrush => CreateScaleBrush(Tracked);

    public IBrush MixedBrush => CreateScaleBrush(Mixed);

    public IBrush UnknownBrush => CreateScaleBrush(Unknown);

    public void Set(string profile, int value, bool notify)
    {
        SetScale(profile, PercentToScale(value), notify);
    }

    public void SetScale(string profile, int scale, bool notify)
    {
        scale = ClampScale(scale);
        var previous = GetScale(profile);
        switch (profile)
        {
            case "street":
                Street = scale;
                break;
            case "agricultural":
                Agricultural = scale;
                break;
            case "mud":
                Mud = scale;
                break;
            case "offRoad":
                OffRoad = scale;
                break;
            case "tracked":
                Tracked = scale;
                break;
            case "mixed":
                Mixed = scale;
                break;
            default:
                Unknown = scale;
                break;
        }

        if (notify && previous == scale)
        {
            Changed?.Invoke();
        }
    }

    public int Get(string profile) => ScaleToPercent(profile switch
    {
        "street" => Street,
        "agricultural" => Agricultural,
        "mud" => Mud,
        "offRoad" => OffRoad,
        "tracked" => Tracked,
        "mixed" => Mixed,
        _ => Unknown
    });

    private int GetScale(string profile) => profile switch
    {
        "street" => Street,
        "agricultural" => Agricultural,
        "mud" => Mud,
        "offRoad" => OffRoad,
        "tracked" => Tracked,
        "mixed" => Mixed,
        _ => Unknown
    };

    partial void OnStreetChanged(int value) => ClampAndNotify(nameof(Street), nameof(StreetBrush), value);
    partial void OnAgriculturalChanged(int value) => ClampAndNotify(nameof(Agricultural), nameof(AgriculturalBrush), value);
    partial void OnMudChanged(int value) => ClampAndNotify(nameof(Mud), nameof(MudBrush), value);
    partial void OnOffRoadChanged(int value) => ClampAndNotify(nameof(OffRoad), nameof(OffRoadBrush), value);
    partial void OnTrackedChanged(int value) => ClampAndNotify(nameof(Tracked), nameof(TrackedBrush), value);
    partial void OnMixedChanged(int value) => ClampAndNotify(nameof(Mixed), nameof(MixedBrush), value);
    partial void OnUnknownChanged(int value) => ClampAndNotify(nameof(Unknown), nameof(UnknownBrush), value);

    private void ClampAndNotify(string propertyName, string brushPropertyName, int value)
    {
        var clamped = ClampScale(value);
        if (clamped != value)
        {
            SetPropertyValue(propertyName, clamped);
            return;
        }

        OnPropertyChanged(brushPropertyName);
        Changed?.Invoke();
    }

    private void SetPropertyValue(string propertyName, int value)
    {
        switch (propertyName)
        {
            case nameof(Street):
                Street = value;
                break;
            case nameof(Agricultural):
                Agricultural = value;
                break;
            case nameof(Mud):
                Mud = value;
                break;
            case nameof(OffRoad):
                OffRoad = value;
                break;
            case nameof(Tracked):
                Tracked = value;
                break;
            case nameof(Mixed):
                Mixed = value;
                break;
            case nameof(Unknown):
                Unknown = value;
                break;
        }
    }

    public static int PercentToScale(int percent)
    {
        return Math.Clamp((int)Math.Round(percent / 20.0, MidpointRounding.AwayFromZero), 1, 10);
    }

    private static int ScaleToPercent(int scale)
    {
        return ClampScale(scale) * 20;
    }

    private static int ClampScale(int scale)
    {
        return Math.Clamp(scale, 1, 10);
    }

    private static IBrush CreateScaleBrush(int scale)
    {
        var ratio = 1 - ((ClampScale(scale) - 1) / 9.0);
        var red = Lerp(0xF6, 0xD9, ratio);
        var green = Lerp(0xD8, 0xEE, ratio);
        var blue = Lerp(0xD6, 0xDA, ratio);
        return new SolidColorBrush(Avalonia.Media.Color.FromRgb(red, green, blue));
    }

    private static byte Lerp(byte start, byte end, double ratio)
    {
        return (byte)Math.Round(start + ((end - start) * ratio));
    }
}

public sealed partial class SurfaceAliasRow : ObservableObject
{
    [ObservableProperty]
    private string _normalized;

    public SurfaceAliasRow(string raw, string normalized, IReadOnlyList<string> targets)
    {
        Raw = raw;
        _normalized = normalized;
        Targets = targets;
    }

    public event Action? Changed;

    public string Raw { get; }

    public IReadOnlyList<string> Targets { get; }

    partial void OnNormalizedChanged(string value) => Changed?.Invoke();
}
