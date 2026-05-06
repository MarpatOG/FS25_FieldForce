using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Services;

public sealed class SafetyManager
{
    private readonly IFfbBackend _backend;
    private readonly AppLogService _log;

    public SafetyManager(IFfbBackend backend, AppLogService log)
    {
        _backend = backend;
        _log = log;
    }

    public void StartTestEffect(FfbEffectKind kind)
    {
        _backend.StartTestEffect(kind);
    }

    public void StopAll(string reason)
    {
        _backend.StopAllEffects(reason);
        _log.Warning("Safety event: Stop all effects ({Reason})", reason);
    }

    public void OnAppClosing() => StopAll("app closing");
    public void OnPanicHotkey() => StopAll("panic hotkey");
    public void OnDeviceDisconnect() => StopAll("device disconnect");
}
