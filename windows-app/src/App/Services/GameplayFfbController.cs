using FieldForce.App.Models;

namespace FieldForce.App.Services;

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
    private readonly TimeSpan _minimumApplyInterval;
    private readonly TimeSpan _uiCallbackInterval = TimeSpan.FromMilliseconds(100);
    private GameplayFfbOutput _lastOutput = GameplayFfbOutput.Zero;
    private DateTimeOffset _lastApplyAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastOutputCallbackAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastApplyResultCallbackAt = DateTimeOffset.MinValue;
    private bool _wasActive;
    private bool _disposed;

    public GameplayFfbController(
        TelemetryReceiverService telemetryReceiver,
        IFfbBackend backend,
        AppLogService log,
        Func<GameplayFfbSettings> settingsAccessor,
        Action<GameplayFfbOutput>? outputChanged = null,
        Action<FfbApplyResult>? applyResultChanged = null,
        EffectStatusWriter? effectStatusWriter = null,
        int maxUpdateRateHz = 125)
    {
        _telemetryReceiver = telemetryReceiver;
        _backend = backend;
        _log = log;
        _settingsAccessor = settingsAccessor;
        _outputChanged = outputChanged;
        _applyResultChanged = applyResultChanged;
        _effectStatusWriter = effectStatusWriter;
        _minimumApplyInterval = TimeSpan.FromMilliseconds(1000d / Math.Clamp(maxUpdateRateHz, 1, 1000));
        _telemetryReceiver.FfbStateChanged += OnTelemetryStateChanged;
    }

    private void OnTelemetryStateChanged(TelemetryReceiverState state)
    {
        if (_disposed)
        {
            return;
        }

        var output = _calculator.Calculate(state, _settingsAccessor());
        _lastOutput = output;
        PublishOutput(output, immediate: !output.IsActive);

        if (!output.IsActive)
        {
            StopIfActive(output.TelemetryFade <= 0 ? "telemetry lost" : state.LastPacket?.GameplayFfbOperatorPauseReason ?? "gameplay inactive");
            _effectStatusWriter?.WriteZero(output.ActiveCategory);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - _lastApplyAt < _minimumApplyInterval)
        {
            return;
        }

        _lastApplyAt = now;
        var result = _backend.ApplyGameplayEffects(output);
        PublishApplyResult(result, immediate: result.Status == FfbApplyStatus.AcquireFailed);
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

    private void PublishOutput(GameplayFfbOutput output, bool immediate)
    {
        var now = DateTimeOffset.UtcNow;
        if (!immediate && now - _lastOutputCallbackAt < _uiCallbackInterval)
        {
            return;
        }

        _lastOutputCallbackAt = now;
        _outputChanged?.Invoke(output);
    }

    private void PublishApplyResult(FfbApplyResult result, bool immediate)
    {
        var now = DateTimeOffset.UtcNow;
        if (!immediate && now - _lastApplyResultCallbackAt < _uiCallbackInterval)
        {
            return;
        }

        _lastApplyResultCallbackAt = now;
        _applyResultChanged?.Invoke(result);
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
            PublishOutput(GameplayFfbOutput.Zero, immediate: true);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _telemetryReceiver.FfbStateChanged -= OnTelemetryStateChanged;
        _backend.StopGameplayEffects("gameplay ffb controller disposed");
        _effectStatusWriter?.WriteZero(_lastOutput.ActiveCategory);
    }
}
