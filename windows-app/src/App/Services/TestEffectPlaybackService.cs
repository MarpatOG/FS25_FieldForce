using FieldForce.App.Models;

namespace FieldForce.App.Services;

public sealed class TestEffectPlaybackService : IDisposable
{
    public static readonly TimeSpan TestDuration = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(50);
    private readonly IFfbBackend _backend;
    private readonly AppLogService _log;
    private readonly Func<GameplayFfbEffectProfile> _profileAccessor;
    private readonly Func<string> _categoryAccessor;
    private readonly Action<GameplayFfbOutput>? _outputChanged;
    private readonly object _lock = new();
    private CancellationTokenSource? _playbackCancellation;
    private bool _disposed;

    public TestEffectPlaybackService(
        IFfbBackend backend,
        AppLogService log,
        Func<GameplayFfbEffectProfile> profileAccessor,
        Func<string> categoryAccessor,
        Action<GameplayFfbOutput>? outputChanged = null)
    {
        _backend = backend;
        _log = log;
        _profileAccessor = profileAccessor;
        _categoryAccessor = categoryAccessor;
        _outputChanged = outputChanged;
    }

    public async Task StartBasicAsync(FfbEffectKind kind, Action<bool>? playbackStateChanged = null)
    {
        var token = BeginPlayback(playbackStateChanged);
        try
        {
            _backend.StartTestEffect(kind);
            await Task.Delay(TestDuration, token).ConfigureAwait(false);
            if (!token.IsCancellationRequested)
            {
                _backend.StopAllEffects("basic test finished");
                _outputChanged?.Invoke(GameplayFfbOutput.Zero);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            EndPlayback(token, playbackStateChanged);
        }
    }

    public async Task StartModAsync(TestFfbEffectKind kind, Action<bool>? playbackStateChanged = null)
    {
        var token = BeginPlayback(playbackStateChanged);
        try
        {
            var started = DateTimeOffset.UtcNow;
            while (!token.IsCancellationRequested)
            {
                var elapsed = DateTimeOffset.UtcNow - started;
                if (elapsed >= TestDuration)
                {
                    break;
                }

                var output = CreateOutput(kind, _profileAccessor(), _categoryAccessor(), elapsed, TestDuration);
                _backend.ApplyGameplayEffects(output);
                _outputChanged?.Invoke(output);
                await Task.Delay(FrameInterval, token).ConfigureAwait(false);
            }

            if (!token.IsCancellationRequested)
            {
                _backend.StopGameplayEffects("mod test finished");
                _outputChanged?.Invoke(GameplayFfbOutput.Zero);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            EndPlayback(token, playbackStateChanged);
        }
    }

    public void StopAll(string reason)
    {
        lock (_lock)
        {
            _playbackCancellation?.Cancel();
            _playbackCancellation?.Dispose();
            _playbackCancellation = null;
        }

        _backend.StopAllEffects(reason);
        _outputChanged?.Invoke(GameplayFfbOutput.Zero);
    }

    private CancellationToken BeginPlayback(Action<bool>? playbackStateChanged)
    {
        CancellationTokenSource cancellation;
        lock (_lock)
        {
            _playbackCancellation?.Cancel();
            _playbackCancellation?.Dispose();
            _playbackCancellation = new CancellationTokenSource();
            cancellation = _playbackCancellation;
        }

        _backend.StopAllEffects("starting test effect");
        playbackStateChanged?.Invoke(true);
        return cancellation.Token;
    }

    private void EndPlayback(CancellationToken token, Action<bool>? playbackStateChanged)
    {
        var endedCurrent = false;
        lock (_lock)
        {
            if (_playbackCancellation is not null && _playbackCancellation.Token == token)
            {
                _playbackCancellation.Dispose();
                _playbackCancellation = null;
                endedCurrent = true;
            }
        }

        if (endedCurrent)
        {
            playbackStateChanged?.Invoke(false);
        }
    }

    private static GameplayFfbOutput CreateOutput(TestFfbEffectKind kind, GameplayFfbEffectProfile profile, string category, TimeSpan elapsed, TimeSpan duration)
    {
        var seconds = elapsed.TotalSeconds;
        var fade = CalculateEnvelope(seconds, duration.TotalSeconds);
        var pulseOn = (int)(seconds * 4) % 2 == 0;
        var sweep = 12 + (int)Math.Round(28 * Math.Clamp((seconds - 1) / 5.0, 0, 1));
        var bumpPulse = pulseOn ? Strength(profile.BumpFeedback) : 0;

        return kind switch
        {
            TestFfbEffectKind.SpeedSpring => Active(category, spring: Strength(profile.SpeedSpring, fade)),
            TestFfbEffectKind.SpeedDamper => Active(category, damper: Strength(profile.SpeedDamper, fade)),
            TestFfbEffectKind.MechanicalFriction => Active(category, friction: Strength(profile.MechanicalFriction, fade)),
            TestFfbEffectKind.EngineRpmVibration => Active(category, engine: Strength(profile.EngineVibration, fade), engineHz: sweep),
            TestFfbEffectKind.SurfaceFeedback => Active(category, surface: Strength(profile.SurfaceFeedback, fade), surfaceHz: sweep),
            TestFfbEffectKind.SlipFeedback => Active(category, slip: Strength(profile.SlipFeedback, fade), slipHz: sweep),
            TestFfbEffectKind.TerrainRumble => Active(category, terrain: Strength(profile.TerrainRumble, fade), terrainHz: 6 + (sweep / 6), terrainActive: true),
            TestFfbEffectKind.BumpFeedback => Pulse(category, bumpPulse, profile.BumpFeedback.DurationMs, FfbPulseKind.Bump),
            TestFfbEffectKind.SuspensionHitFeedback => Pulse(category, pulseOn ? Strength(profile.SuspensionHitFeedback) : 0, profile.SuspensionHitFeedback.DurationMs, FfbPulseKind.LeftSuspensionHit),
            TestFfbEffectKind.CollisionFeedback => Pulse(category, pulseOn ? Strength(profile.CollisionFeedback) : 0, profile.CollisionFeedback.DurationMs, FfbPulseKind.Collision),
            TestFfbEffectKind.LandingFeedback => Pulse(category, pulseOn ? Strength(profile.LandingFeedback) : 0, profile.LandingFeedback.DurationMs, FfbPulseKind.Landing),
            TestFfbEffectKind.GearShiftPulse => Pulse(category, pulseOn ? Strength(profile.GearShiftPulse) : 0, profile.GearShiftPulse.DurationMs, FfbPulseKind.GearShift),
            TestFfbEffectKind.DrivetrainPulse => Pulse(category, pulseOn ? Strength(profile.DrivetrainPulse) : 0, profile.DrivetrainPulse.DurationMs, FfbPulseKind.DrivetrainJerk),
            TestFfbEffectKind.EngineStartStopPulse => Active(category, engineStart: pulseOn ? Strength(profile.EngineStartStopPulse) : 0, engineStartMs: profile.EngineStartStopPulse.StartDurationMs, engineStartHz: profile.EngineStartStopPulse.StartFrequencyHz),
            _ => GameplayFfbOutput.Zero
        };
    }

    private static double CalculateEnvelope(double seconds, double durationSeconds)
    {
        if (seconds < 1)
        {
            return Math.Clamp(seconds, 0, 1);
        }

        if (seconds > durationSeconds - 1)
        {
            return Math.Clamp(durationSeconds - seconds, 0, 1);
        }

        return 1;
    }

    private static int Strength(FfbEffectSettings settings, double scale = 1)
    {
        if (!settings.Enabled)
        {
            return 0;
        }

        return Math.Clamp((int)Math.Round(settings.StrengthPercent * scale), 0, 100);
    }

    private static GameplayFfbOutput Active(
        string category,
        int spring = 0,
        int damper = 0,
        int friction = 0,
        int engine = 0,
        int engineHz = 0,
        int surface = 0,
        int surfaceHz = 0,
        int terrain = 0,
        int terrainHz = 0,
        int slip = 0,
        int slipHz = 0,
        bool terrainActive = false,
        int engineStart = 0,
        int engineStartMs = 0,
        int engineStartHz = 0)
    {
        return new GameplayFfbOutput(
            spring,
            damper,
            friction,
            engine,
            engineHz,
            surface,
            surfaceHz,
            terrain,
            terrainHz,
            slip,
            slipHz,
            0,
            0,
            0,
            1,
            1,
            spring > 0 || damper > 0 || friction > 0 || engine > 0 || surface > 0 || terrain > 0 || slip > 0 || engineStart > 0,
            category,
            terrainActive,
            engineStart > 0,
            engineStart > 0 ? FfbPulseKind.EngineStartStop : FfbPulseKind.None,
            EngineStartPulsePercent: engineStart,
            EngineStartPulseDurationMs: engineStartMs,
            EngineStartPulseHz: engineStartHz,
            EngineStartStopPulseActive: engineStart > 0);
    }

    private static GameplayFfbOutput Pulse(string category, int percent, int durationMs, FfbPulseKind kind)
    {
        return new GameplayFfbOutput(
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            percent,
            Math.Clamp(durationMs, 40, 500),
            0,
            1,
            1,
            percent > 0,
            category,
            EventPulseActive: percent > 0,
            EventPulseKind: kind);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopAll("test playback disposed");
        _log.Information("Test effect playback disposed");
    }
}
