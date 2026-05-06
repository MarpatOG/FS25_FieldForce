using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Services;

public sealed class GameplayFfbController : IDisposable
{
    private readonly TelemetryReceiverService _telemetryReceiver;
    private readonly IFfbBackend _backend;
    private readonly AppLogService _log;
    private readonly GameplayFfbCalculator _calculator = new();
    private readonly Func<GameplayFfbSettings> _settingsAccessor;
    private readonly Action<GameplayFfbOutput>? _outputChanged;
    private readonly Action<FfbApplyResult>? _applyResultChanged;
    private readonly EffectStatusWriter? _effectStatusWriter;
    private GameplayFfbOutput _lastOutput = GameplayFfbOutput.Zero;
    private bool _wasActive;
    private bool _disposed;

    public GameplayFfbController(
        TelemetryReceiverService telemetryReceiver,
        IFfbBackend backend,
        AppLogService log,
        Func<GameplayFfbSettings> settingsAccessor,
        Action<GameplayFfbOutput>? outputChanged = null,
        Action<FfbApplyResult>? applyResultChanged = null,
        EffectStatusWriter? effectStatusWriter = null)
    {
        _telemetryReceiver = telemetryReceiver;
        _backend = backend;
        _log = log;
        _settingsAccessor = settingsAccessor;
        _outputChanged = outputChanged;
        _applyResultChanged = applyResultChanged;
        _effectStatusWriter = effectStatusWriter;
        _telemetryReceiver.StateChanged += OnTelemetryStateChanged;
    }

    private void OnTelemetryStateChanged(TelemetryReceiverState state)
    {
        if (_disposed)
        {
            return;
        }

        var output = _calculator.Calculate(state, _settingsAccessor());
        _lastOutput = output;
        _outputChanged?.Invoke(output);

        if (!output.IsActive)
        {
            StopIfActive(output.TelemetryFade <= 0 ? "telemetry lost" : "gameplay inactive");
            _effectStatusWriter?.WriteZero(output.ActiveCategory);
            return;
        }

        var result = _backend.ApplyGameplayEffects(output);
        _applyResultChanged?.Invoke(result);
        if (result.Status == FfbApplyStatus.AcquireFailed)
        {
            _effectStatusWriter?.WriteZero(output.ActiveCategory);
            StopIfActive(result.Message);
            return;
        }

        if (_backend.HasSelectedFfbDevice)
        {
            _effectStatusWriter?.Write(output);
        }
        else
        {
            _effectStatusWriter?.WriteZero(output.ActiveCategory);
        }

        if (!_wasActive)
        {
            _wasActive = true;
            _log.Information("Gameplay FFB enabled");
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
        _effectStatusWriter?.WriteZero(_lastOutput.ActiveCategory);
        if (_lastOutput != GameplayFfbOutput.Zero)
        {
            _lastOutput = GameplayFfbOutput.Zero;
            _outputChanged?.Invoke(GameplayFfbOutput.Zero);
        }
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
        _effectStatusWriter?.WriteZero(_lastOutput.ActiveCategory);
    }
}
