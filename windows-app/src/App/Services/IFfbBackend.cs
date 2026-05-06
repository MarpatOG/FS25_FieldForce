using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Services;

public interface IFfbBackend : IDisposable
{
    IReadOnlyList<DeviceInfo> ScanDevices();
    bool SelectDevice(DeviceInfo device, IntPtr windowHandle, int globalLimitPercent, int deviceLimitPercent);
    void UpdateForceLimits(int globalLimitPercent, int deviceLimitPercent);
    void StartTestEffect(FfbEffectKind kind);
    void ApplyRpmVibration(int magnitude, int hz);
    void StopGameplayEffects(string reason);
    void StopAllEffects(string reason);
    DeviceInfo? SelectedDevice { get; }
    bool HasSelectedFfbDevice { get; }
}
