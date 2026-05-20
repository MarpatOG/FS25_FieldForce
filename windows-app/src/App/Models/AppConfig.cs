namespace FieldForce.App.Models;

public sealed class AppConfig
{
    public const int CurrentEffectsProfileVersion = 17;

    public int EffectsProfileVersion { get; set; } = CurrentEffectsProfileVersion;
    public string? SelectedDeviceStableId { get; set; }
    public int? PrimaryFfbAxisOffset { get; set; }
    public int GlobalForceLimitPercent { get; set; } = 40;
    public int DeviceForceLimitPercent { get; set; } = 35;
    public string WheelProfileId { get; set; } = WheelProfileCatalog.LogitechMomoRacingId;
    public string DeviceProfileName { get; set; } = "Logitech Momo Racing";
    public int RotationDegrees { get; set; } = 270;
    public string RecommendedMode { get; set; } = "Override";
    public KeybindsConfig Keybinds { get; set; } = new();
    public string TelemetryHost { get; set; } = "127.0.0.1";
    public int TelemetryPort { get; set; } = 34325;
    public string TelemetryTransportMode { get; set; } = "file";
    public int TelemetryLostTimeoutMs { get; set; } = 1000;
    public int TelemetryStaleWarningMs { get; set; } = 300;
    public int TelemetryFfbUpdateRateHz { get; set; } = 60;
    public int TelemetryUiRefreshMs { get; set; } = 100;
    public string? TelemetryFilePath { get; set; }
    public bool EffectOverlayEnabled { get; set; } = true;
    public bool EffectOverlayClickThrough { get; set; }
    public int EffectOverlayX { get; set; } = 40;
    public int EffectOverlayY { get; set; } = 40;
    public GameplayFfbSettings GameplayFfb { get; set; } = new();
}
