using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Services;

public interface IFfbBackend : IDisposable
{
    IReadOnlyList<DeviceInfo> ScanDevices();
    bool SelectDevice(DeviceInfo device, IntPtr windowHandle, int globalLimitPercent, int deviceLimitPercent, int? primaryFfbAxisOffset);
    void UpdateForceLimits(int globalLimitPercent, int deviceLimitPercent);
    void StartTestEffect(FfbEffectKind kind);
    FfbApplyResult ApplyGameplayEffects(GameplayFfbOutput output);
    void StopGameplayEffects(string reason);
    void StopAllEffects(string reason);
    DeviceInfo? SelectedDevice { get; }
    bool HasSelectedFfbDevice { get; }
}

public enum FfbApplyStatus
{
    Applied,
    Skipped,
    AcquireFailed
}

public sealed record FfbApplyResult(FfbApplyStatus Status, string Message)
{
    public static FfbApplyResult Applied { get; } = new(FfbApplyStatus.Applied, "applied");
    public static FfbApplyResult Skipped(string message) => new(FfbApplyStatus.Skipped, message);
    public static FfbApplyResult AcquireFailed(string message) => new(FfbApplyStatus.AcquireFailed, message);
}
