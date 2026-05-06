using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Services;

public sealed class GameplayFfbController : IDisposable
{
    private const double MinRpm = 500;
    private const double MaxRpm = 2400;
    private const int MinHz = 16;
    private const int MaxHz = 34;
    private const int MinMagnitude = 250;
    private const int MaxMagnitude = 1500;
    private static readonly TimeSpan TelemetryLostStopAge = TimeSpan.FromMilliseconds(500);

    private readonly TelemetryReceiverService _telemetryReceiver;
    private readonly IFfbBackend _backend;
    private readonly AppLogService _log;
    private bool _wasActive;
    private bool _disposed;

    public GameplayFfbController(TelemetryReceiverService telemetryReceiver, IFfbBackend backend, AppLogService log)
    {
        _telemetryReceiver = telemetryReceiver;
        _backend = backend;
        _log = log;
        _telemetryReceiver.StateChanged += OnTelemetryStateChanged;
    }

    private void OnTelemetryStateChanged(TelemetryReceiverState state)
    {
        if (_disposed)
        {
            return;
        }

        var packet = state.LastPacket;
        if (state.Status != TelemetryStatus.Connected ||
            state.LastPacketAge is null ||
            state.LastPacketAge > TelemetryLostStopAge ||
            packet is null ||
            packet.IsPlayerInVehicle != true ||
            packet.EngineStarted != true ||
            packet.Rpm is null ||
            packet.Rpm < MinRpm)
        {
            StopIfActive("telemetry inactive");
            return;
        }

        var rpmRatio = Math.Clamp((packet.Rpm.Value - MinRpm) / (MaxRpm - MinRpm), 0, 1);
        var hz = Quantize(MinHz + (int)Math.Round((MaxHz - MinHz) * rpmRatio), 2);
        var magnitude = Quantize(MinMagnitude + (int)Math.Round((MaxMagnitude - MinMagnitude) * rpmRatio), 100);

        _backend.ApplyRpmVibration(magnitude, hz);
        if (!_wasActive)
        {
            _wasActive = true;
            _log.Information("Gameplay RPM vibration enabled");
        }
    }

    private void StopIfActive(string reason)
    {
        if (!_wasActive)
        {
            return;
        }

        _wasActive = false;
        _backend.StopGameplayEffects(reason);
    }

    private static int Quantize(int value, int step)
    {
        return Math.Max(step, (int)Math.Round(value / (double)step) * step);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _telemetryReceiver.StateChanged -= OnTelemetryStateChanged;
        _backend.StopGameplayEffects("gameplay ffb controller disposed");
    }
}
