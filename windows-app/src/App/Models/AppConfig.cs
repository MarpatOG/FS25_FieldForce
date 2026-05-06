namespace FS25FfbBridge.App.Models;

public sealed class AppConfig
{
    public string? SelectedDeviceStableId { get; set; }
    public int GlobalForceLimitPercent { get; set; } = 40;
    public int DeviceForceLimitPercent { get; set; } = 35;
    public string PanicHotkey { get; set; } = "Ctrl+Alt+Pause";
    public string TelemetryHost { get; set; } = "127.0.0.1";
    public int TelemetryPort { get; set; } = 34325;
    public int TelemetryLostTimeoutMs { get; set; } = 1000;
    public int TelemetryStaleWarningMs { get; set; } = 300;
    public string? TelemetryFilePath { get; set; }
}
