using FS25FfbBridge.App.Models;
using SharpGen.Runtime;
using Vortice.DirectInput;

namespace FS25FfbBridge.App.Services;

public sealed class DirectInputFfbBackend : IFfbBackend
{
    private const int DirectInputMax = 10000;
    private const int InfiniteDuration = -1;
    private const int DirectionPositive = 10000;
    private const int DirectionNegative = -10000;
    private const int MomoTestConditionCoefficient = 10000;
    private const int MomoTestConditionSaturation = 10000;
    private const int MomoSpringDeadBand = 100;
    private const int GameplaySpringDeadBand = 0;
    private const int MomoConstantMagnitude = 10000;
    private const int MomoVibrationMagnitude = 9000;
    private const int MomoVibrationHz = 24;
    private const int MinimumGameplayPulseMagnitude = 5000;
    private const int MinimumEngineDrivetrainPulseMagnitude = 1600;
    private static readonly TimeSpan ConstantTestDuration = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan VibrationTestDuration = TimeSpan.FromSeconds(3);

    private readonly AppLogService _log;
    private readonly IDirectInput8 _directInput;
    private readonly object _effectLock = new();
    private readonly List<IDirectInputEffect> _activeEffects = [];
    private IDirectInputEffect? _gameplaySpringEffect;
    private IDirectInputEffect? _gameplayDamperEffect;
    private IDirectInputEffect? _gameplayFrictionEffect;
    private IDirectInputEffect? _gameplayEngineEffect;
    private IDirectInputEffect? _gameplaySurfaceEffect;
    private IDirectInputEffect? _gameplayTerrainRumbleEffect;
    private IDirectInputEffect? _gameplaySlipEffect;
    private IDirectInputEffect? _gameplayBumpEffect;
    private IDirectInputDevice8? _device;
    private int _primaryAxisOffset;
    private int _globalLimitPercent = 40;
    private int _deviceLimitPercent = 40;
    private readonly Dictionary<FfbPulseKind, DateTimeOffset> _lastPulseAtByKind = [];
    private DateTimeOffset _lastAcquireWarningAt = DateTimeOffset.MinValue;
    private GameplayFfbOutput _lastGameplayOutput = GameplayFfbOutput.Zero;

    public DirectInputFfbBackend(AppLogService log)
    {
        _log = log;
        _directInput = DInput.DirectInput8Create();
    }

    public DeviceInfo? SelectedDevice { get; private set; }
    public bool HasSelectedFfbDevice => _device is not null && SelectedDevice?.IsForceFeedbackCapable == true;

    public IReadOnlyList<DeviceInfo> ScanDevices()
    {
        var devices = new List<DeviceInfo>();
        foreach (var instance in _directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly))
        {
            IDirectInputDevice8? candidate = null;
            try
            {
                candidate = _directInput.CreateDevice(instance.InstanceGuid);
                var capabilities = candidate.Capabilities;
                var axes = candidate.GetObjects(DeviceObjectTypeFlags.Axis)
                    .Select(axis => string.IsNullOrWhiteSpace(axis.Name) ? axis.ObjectType.ToString() : axis.Name)
                    .Distinct()
                    .ToArray();
                var effects = SafeGetEffects(candidate);
                var supportsFfb = capabilities.Flags.HasFlag(DeviceFlags.ForceFeedback) || effects.Count > 0 || instance.ForceFeedbackDriverGuid != Guid.Empty;
                var stableId = BuildStableId(instance);

                devices.Add(new DeviceInfo(
                    stableId,
                    instance.InstanceGuid,
                    instance.ProductGuid,
                    instance.InstanceName,
                    instance.ProductName,
                    instance.Type.ToString(),
                    supportsFfb,
                    axes,
                    effects));

                _log.Information("Device detected: {DeviceName} ({StableId}), FFB={Ffb}", instance.ProductName, stableId, supportsFfb);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Device scan failed for {DeviceName}", instance.ProductName);
            }
            finally
            {
                candidate?.Dispose();
            }
        }

        return devices;
    }

    public bool SelectDevice(DeviceInfo device, IntPtr windowHandle, int globalLimitPercent, int deviceLimitPercent)
    {
        StopAllEffects("selecting device");
        ReleaseDevice();

        if (!device.IsForceFeedbackCapable)
        {
            _log.Warning("Device selection rejected because {DeviceName} does not report FFB support", device.DisplayName);
            return false;
        }

        try
        {
            _device = _directInput.CreateDevice(device.InstanceGuid);
            _device.SetDataFormat<RawJoystickState>();
            _device.SetCooperativeLevel(windowHandle, CooperativeLevel.Exclusive | CooperativeLevel.Background);
            _device.Acquire().CheckError();
            _device.SendForceFeedbackCommand(ForceFeedbackCommand.Reset);
            _device.SendForceFeedbackCommand(ForceFeedbackCommand.SetActuatorsOn);

            var actuator = _device.GetObjects(DeviceObjectTypeFlags.ForceFeedbackActuator).FirstOrDefault();
            var axis = _device.GetObjects(DeviceObjectTypeFlags.Axis).FirstOrDefault();
            _primaryAxisOffset = actuator is not null && actuator.Offset != 0
                ? actuator.Offset
                : axis?.Offset ?? 0;

            SelectedDevice = device;
            UpdateForceLimits(globalLimitPercent, deviceLimitPercent);
            _log.Information("Device selected: {DeviceName} ({StableId}); primary FFB axis offset={AxisOffset}", device.DisplayName, device.StableId, _primaryAxisOffset);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Could not acquire DirectInput FFB device {DeviceName}", device.DisplayName);
            ReleaseDevice();
            return false;
        }
    }

    public void UpdateForceLimits(int globalLimitPercent, int deviceLimitPercent)
    {
        _globalLimitPercent = Math.Clamp(globalLimitPercent, 0, 100);
        _deviceLimitPercent = Math.Clamp(deviceLimitPercent, 0, 100);
    }

    public void StartTestEffect(FfbEffectKind kind)
    {
        lock (_effectLock)
        {
            if (_device is null || SelectedDevice is null)
            {
                _log.Warning("Effect start ignored because no FFB device is selected");
                return;
            }

            StopGameplayEffectsCore("starting " + kind);
            StopActiveEffectsCore();

            try
            {
                var effect = kind switch
                {
                    FfbEffectKind.Spring => CreateEffectWithAcquireRetry(() => CreateConditionEffect(EffectGuid.Spring, MomoTestConditionCoefficient, MomoTestConditionSaturation, MomoSpringDeadBand), "test spring"),
                    FfbEffectKind.Damper => CreateEffectWithAcquireRetry(() => CreateConditionEffect(EffectGuid.Damper, MomoTestConditionCoefficient, MomoTestConditionSaturation, deadBand: 0), "test damper"),
                    FfbEffectKind.ConstantLeft => CreateEffectWithAcquireRetry(() => CreateConstantEffect(MomoConstantMagnitude, DirectionForLogicalForce(-1), ConstantTestDuration), "test constant left"),
                    FfbEffectKind.ConstantRight => CreateEffectWithAcquireRetry(() => CreateConstantEffect(MomoConstantMagnitude, DirectionForLogicalForce(1), ConstantTestDuration), "test constant right"),
                    FfbEffectKind.LowVibration => CreateEffectWithAcquireRetry(() => CreatePeriodicEffect(EffectGuid.Sine, MomoVibrationMagnitude, MomoVibrationHz, VibrationTestDuration), "test vibration"),
                    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
                };

                effect.Download().CheckError();
                effect.Start(1).CheckError();
                _activeEffects.Add(effect);
                _log.Information("Effect started: {EffectKind}", kind);
            }
            catch (Exception ex)
            {
                if (ex is SharpGenException sharpGenException && IsAcquireFailure(sharpGenException))
                {
                    LogAcquireFailure(sharpGenException, $"test {kind}");
                }
                else
                {
                    _log.Error(ex, "Effect start failed: {EffectKind}", kind);
                }

                StopAllEffectsCore("effect start failure");
            }
        }
    }

    public FfbApplyResult ApplyGameplayEffects(GameplayFfbOutput output)
    {
        lock (_effectLock)
        {
            if (_device is null || SelectedDevice is null)
            {
                return FfbApplyResult.Skipped("no selected FFB device");
            }

            if (!output.IsActive)
            {
                StopGameplayEffectsCore("gameplay output zero");
                return FfbApplyResult.Skipped("gameplay output zero");
            }

            var quantized = QuantizeOutput(output);
            if (IsSameGameplayOutput(_lastGameplayOutput, quantized))
            {
                return FfbApplyResult.Skipped("unchanged");
            }

            var previous = _lastGameplayOutput;
            FfbApplyResult? result = null;
            if (previous.SpringPercent != quantized.SpringPercent)
            {
                result ??= ApplyConditionEffect(ref _gameplaySpringEffect, EffectGuid.Spring, quantized.SpringPercent, deadBand: GameplaySpringDeadBand, "spring");
            }

            if (previous.DamperPercent != quantized.DamperPercent)
            {
                result ??= ApplyConditionEffect(ref _gameplayDamperEffect, EffectGuid.Damper, quantized.DamperPercent, deadBand: 0, "damper");
            }

            if (previous.FrictionPercent != quantized.FrictionPercent)
            {
                result ??= ApplyConditionEffect(ref _gameplayFrictionEffect, EffectGuid.Friction, quantized.FrictionPercent, deadBand: 0, "friction");
            }

            if (previous.EngineVibrationPercent != quantized.EngineVibrationPercent ||
                previous.EngineVibrationHz != quantized.EngineVibrationHz)
            {
                result ??= ApplyPeriodicEffect(ref _gameplayEngineEffect, quantized.EngineVibrationPercent, quantized.EngineVibrationHz, "engine vibration");
            }

            if (previous.SurfaceVibrationPercent != quantized.SurfaceVibrationPercent ||
                previous.SurfaceVibrationHz != quantized.SurfaceVibrationHz)
            {
                result ??= ApplyPeriodicEffect(ref _gameplaySurfaceEffect, quantized.SurfaceVibrationPercent, quantized.SurfaceVibrationHz, "surface feedback");
            }

            if (previous.TerrainRumblePercent != quantized.TerrainRumblePercent ||
                previous.TerrainRumbleHz != quantized.TerrainRumbleHz)
            {
                result ??= ApplyPeriodicEffect(ref _gameplayTerrainRumbleEffect, quantized.TerrainRumblePercent, quantized.TerrainRumbleHz, "terrain rumble");
            }

            if (previous.SlipVibrationPercent != quantized.SlipVibrationPercent ||
                previous.SlipVibrationHz != quantized.SlipVibrationHz)
            {
                result ??= ApplyPeriodicEffect(ref _gameplaySlipEffect, quantized.SlipVibrationPercent, quantized.SlipVibrationHz, "slip feedback");
            }

            result ??= ApplyBumpEffect(quantized);

            if (result?.Status == FfbApplyStatus.AcquireFailed)
            {
                StopGameplayEffectsCore(result.Message);
                return result;
            }

            _lastGameplayOutput = quantized;
            _log.Information(
                "Gameplay FFB updated: result=applied, spring={Spring}%, damper={Damper}%, friction={Friction}%, engine={Engine}%/{EngineHz}Hz, surface={Surface}%/{SurfaceHz}Hz, terrain={Terrain}%/{TerrainHz}Hz, slip={Slip}%/{SlipHz}Hz, pulse={PulseKind}/{Bump}%/{Duration}ms, load={Load:0.00}, fade={Fade:0.00}",
                quantized.SpringPercent,
                quantized.DamperPercent,
                quantized.FrictionPercent,
                quantized.EngineVibrationPercent,
                quantized.EngineVibrationHz,
                quantized.SurfaceVibrationPercent,
                quantized.SurfaceVibrationHz,
                quantized.TerrainRumblePercent,
                quantized.TerrainRumbleHz,
                quantized.SlipVibrationPercent,
                quantized.SlipVibrationHz,
                quantized.EventPulseKind,
                quantized.BumpImpulsePercent,
                quantized.BumpDurationMs,
                quantized.LoadFactor,
                quantized.TelemetryFade);
            return FfbApplyResult.Applied;
        }
    }

    public void StopGameplayEffects(string reason)
    {
        lock (_effectLock)
        {
            StopGameplayEffectsCore(reason);
        }
    }

    public void StopAllEffects(string reason)
    {
        lock (_effectLock)
        {
            StopAllEffectsCore(reason);
        }
    }

    private void StopAllEffectsCore(string reason)
    {
        StopGameplayEffectsCore(reason);
        StopActiveEffectsCore();

        try
        {
            _device?.SendForceFeedbackCommand(ForceFeedbackCommand.StopAll);
        }
        catch (SharpGenException ex) when (IsNotExclusiveAcquired(ex))
        {
            _log.Warning("StopAll command skipped because the DirectInput device is not exclusively acquired");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "StopAll command failed");
        }

        _log.Information("Effect stopped: all effects ({Reason})", reason);
    }

    private void StopActiveEffectsCore()
    {
        foreach (var effect in _activeEffects.ToArray())
        {
            try
            {
                effect.Stop();
                effect.Unload();
                effect.Dispose();
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Effect stop/unload failed");
            }
        }

        _activeEffects.Clear();
    }

    private void StopGameplayEffectsCore(string reason)
    {
        if (_gameplaySpringEffect is null &&
            _gameplayDamperEffect is null &&
            _gameplayFrictionEffect is null &&
            _gameplayEngineEffect is null &&
            _gameplaySurfaceEffect is null &&
            _gameplayTerrainRumbleEffect is null &&
            _gameplaySlipEffect is null &&
            _gameplayBumpEffect is null)
        {
            _lastGameplayOutput = GameplayFfbOutput.Zero;
            return;
        }

        StopAndDisposeEffect(ref _gameplaySpringEffect, "gameplay spring");
        StopAndDisposeEffect(ref _gameplayDamperEffect, "gameplay damper");
        StopAndDisposeEffect(ref _gameplayFrictionEffect, "gameplay friction");
        StopAndDisposeEffect(ref _gameplayEngineEffect, "gameplay engine vibration");
        StopAndDisposeEffect(ref _gameplaySurfaceEffect, "gameplay surface feedback");
        StopAndDisposeEffect(ref _gameplayTerrainRumbleEffect, "gameplay terrain rumble");
        StopAndDisposeEffect(ref _gameplaySlipEffect, "gameplay slip feedback");
        StopAndDisposeEffect(ref _gameplayBumpEffect, "gameplay bump feedback");
        _lastGameplayOutput = GameplayFfbOutput.Zero;
        _log.Information("Gameplay FFB stopped ({Reason})", reason);
    }

    public void Dispose()
    {
        StopAllEffects("backend disposed");
        ReleaseDevice();
        _directInput.Dispose();
    }

    private IDirectInputEffect CreateConditionEffect(Guid effectGuid, int coefficient, int saturation, int deadBand)
    {
        var cappedCoefficient = ScaleMagnitude(coefficient);
        var cappedSaturation = ScaleMagnitude(saturation);
        _log.Information("Preparing condition effect: guid={EffectGuid}, axis={AxisOffset}, coefficient={Coefficient}, saturation={Saturation}", effectGuid, _primaryAxisOffset, cappedCoefficient, cappedSaturation);
        var parameters = BaseParameters(TimeSpan.FromMilliseconds(-1));
        parameters.Parameters = new ConditionSet
        {
            Conditions =
            [
                new Condition
                {
                    Offset = 0,
                    PositiveCoefficient = cappedCoefficient,
                    NegativeCoefficient = cappedCoefficient,
                    PositiveSaturation = cappedSaturation,
                    NegativeSaturation = cappedSaturation,
                    DeadBand = deadBand
                }
            ]
        };

        return _device!.CreateEffect(effectGuid, parameters);
    }

    private IDirectInputEffect CreateConstantEffect(int magnitude, int direction, TimeSpan duration, int minimumScaledMagnitude = 0)
    {
        var scaledMagnitude = minimumScaledMagnitude > 0
            ? ScaleMagnitudeForLimitsWithFloor(magnitude, _globalLimitPercent, _deviceLimitPercent, minimumScaledMagnitude)
            : ScaleMagnitude(magnitude);
        _log.Information("Preparing constant effect: axis={AxisOffset}, magnitude={Magnitude}, direction={Direction}", _primaryAxisOffset, scaledMagnitude, direction);
        var parameters = BaseParameters(duration, direction);
        parameters.Parameters = new ConstantForce
        {
            Magnitude = scaledMagnitude
        };

        return _device!.CreateEffect(EffectGuid.ConstantForce, parameters);
    }

    private IDirectInputEffect CreatePeriodicEffect(Guid effectGuid, int magnitude, int hz, TimeSpan duration)
    {
        var scaledMagnitude = ScaleMagnitude(magnitude);
        _log.Information("Preparing periodic effect: guid={EffectGuid}, axis={AxisOffset}, magnitude={Magnitude}, hz={Hz}", effectGuid, _primaryAxisOffset, scaledMagnitude, hz);
        var parameters = BaseParameters(duration);
        parameters.Parameters = new PeriodicForce
        {
            Magnitude = scaledMagnitude,
            Offset = 0,
            Phase = 0,
            Period = Math.Max(1, 1_000_000 / Math.Max(1, hz))
        };

        return _device!.CreateEffect(effectGuid, parameters);
    }

    private FfbApplyResult? ApplyConditionEffect(ref IDirectInputEffect? effect, Guid effectGuid, int percent, int deadBand, string label)
    {
        StopAndDisposeEffect(ref effect, label);
        if (percent <= 0)
        {
            return null;
        }

        try
        {
            var magnitude = PercentToDirectInputMagnitude(percent);
            effect = CreateEffectWithAcquireRetry(() => CreateConditionEffect(effectGuid, magnitude, magnitude, deadBand), label);
            effect.Download().CheckError();
            effect.Start(1).CheckError();
            return null;
        }
        catch (SharpGenException ex) when (IsAcquireFailure(ex))
        {
            var result = LogAcquireFailure(ex, $"gameplay {label}");
            StopAndDisposeEffect(ref effect, label);
            return result;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Gameplay condition effect update failed: {EffectLabel}", label);
            StopAndDisposeEffect(ref effect, label);
            return null;
        }
    }

    private FfbApplyResult? ApplyPeriodicEffect(ref IDirectInputEffect? effect, int percent, int hz, string label)
    {
        StopAndDisposeEffect(ref effect, label);
        if (percent <= 0 || hz <= 0)
        {
            return null;
        }

        try
        {
            effect = CreateEffectWithAcquireRetry(() => CreatePeriodicEffect(EffectGuid.Sine, PercentToDirectInputMagnitude(percent), hz, TimeSpan.MaxValue), label);
            effect.Download().CheckError();
            effect.Start(1).CheckError();
            return null;
        }
        catch (SharpGenException ex) when (IsAcquireFailure(ex))
        {
            var result = LogAcquireFailure(ex, $"gameplay {label}");
            StopAndDisposeEffect(ref effect, label);
            return result;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Gameplay periodic effect update failed: {EffectLabel}", label);
            StopAndDisposeEffect(ref effect, label);
            return null;
        }
    }

    private FfbApplyResult? ApplyBumpEffect(GameplayFfbOutput output)
    {
        if (output.BumpImpulsePercent == 0)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var cooldownMs = Math.Max(20, output.BumpCooldownMs);
        var kind = output.EventPulseKind == FfbPulseKind.None ? FfbPulseKind.Bump : output.EventPulseKind;
        _lastPulseAtByKind.TryGetValue(kind, out var lastPulseAt);
        if ((now - lastPulseAt).TotalMilliseconds < cooldownMs)
        {
            return null;
        }

        StopAndDisposeEffect(ref _gameplayBumpEffect, "bump feedback");

        try
        {
            var direction = DirectionForLogicalForce(output.BumpImpulsePercent);
            _gameplayBumpEffect = CreateEffectWithAcquireRetry(() => CreateConstantEffect(
                PercentToDirectInputMagnitude(Math.Abs(output.BumpImpulsePercent)),
                direction,
                TimeSpan.FromMilliseconds(ClampGameplayPulseDuration(output)),
                CalculateMinimumGameplayPulseMagnitude(kind)), "bump feedback");
            _gameplayBumpEffect.Download().CheckError();
            _gameplayBumpEffect.Start(1).CheckError();
            _lastPulseAtByKind[kind] = now;
            return null;
        }
        catch (SharpGenException ex) when (IsAcquireFailure(ex))
        {
            var result = LogAcquireFailure(ex, "gameplay bump feedback");
            StopAndDisposeEffect(ref _gameplayBumpEffect, "bump feedback");
            return result;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Gameplay bump effect update failed");
            StopAndDisposeEffect(ref _gameplayBumpEffect, "bump feedback");
            return null;
        }
    }

    private IDirectInputEffect CreateEffectWithAcquireRetry(Func<IDirectInputEffect> factory, string label)
    {
        EnsureDeviceAcquired();
        try
        {
            return factory();
        }
        catch (SharpGenException ex) when (IsAcquireFailure(ex))
        {
            _log.Warning("DirectInput effect create needs reacquire: {EffectLabel}", label);
            EnsureDeviceAcquired();
            return factory();
        }
    }

    private void EnsureDeviceAcquired()
    {
        if (_device is null)
        {
            return;
        }

        _device.Acquire().CheckError();
        _device.SendForceFeedbackCommand(ForceFeedbackCommand.SetActuatorsOn);
    }

    private FfbApplyResult LogAcquireFailure(SharpGenException ex, string operation)
    {
        var message = "DirectInput device is not exclusively acquired or input was lost";
        var now = DateTimeOffset.UtcNow;
        if ((now - _lastAcquireWarningAt).TotalSeconds >= 2)
        {
            _lastAcquireWarningAt = now;
            _log.Warning("{Operation} skipped: {Message}. FS25 or the wheel driver may be holding the device. Error={Error}", operation, message, ex.Message);
        }

        return FfbApplyResult.AcquireFailed(message);
    }

    private void StopAndDisposeEffect(ref IDirectInputEffect? effect, string label)
    {
        if (effect is null)
        {
            return;
        }

        try
        {
            effect.Stop();
            effect.Unload();
            effect.Dispose();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Effect stop/unload failed: {EffectLabel}", label);
        }
        finally
        {
            effect = null;
        }
    }

    private EffectParameters BaseParameters(TimeSpan duration, int direction = DirectionPositive)
    {
        var parameters = new EffectParameters
        {
            Flags = EffectFlags.ObjectOffsets | EffectFlags.Cartesian,
            Duration = duration == TimeSpan.MaxValue || duration.TotalMilliseconds < 0
                ? InfiniteDuration
                : Math.Max(1, (int)(duration.TotalMilliseconds * 1000)),
            Gain = DirectInputMax,
            TriggerButton = -1,
            TriggerRepeatInterval = 0,
            SamplePeriod = 0,
            StartDelay = 0,
            Axes = [_primaryAxisOffset],
            Directions = [direction]
        };

        return parameters;
    }

    private int ScaleMagnitude(int magnitude)
    {
        return ScaleMagnitudeForLimits(magnitude, _globalLimitPercent, _deviceLimitPercent);
    }

    internal static int ScaleMagnitudeForLimits(int magnitude, int globalLimitPercent, int deviceLimitPercent)
    {
        var limit = Math.Min(Math.Clamp(globalLimitPercent, 0, 100), Math.Clamp(deviceLimitPercent, 0, 100)) / 100.0;
        return Math.Clamp((int)Math.Round(Math.Abs(magnitude) * limit), 0, DirectInputMax);
    }

    internal static int ScaleMagnitudeForLimitsWithFloor(int magnitude, int globalLimitPercent, int deviceLimitPercent, int minimumMagnitude)
    {
        if (magnitude <= 0)
        {
            return 0;
        }

        var limit = Math.Min(Math.Clamp(globalLimitPercent, 0, 100), Math.Clamp(deviceLimitPercent, 0, 100)) / 100.0;
        var limitMagnitude = Math.Clamp((int)Math.Round(DirectInputMax * limit), 0, DirectInputMax);
        var floor = Math.Clamp(minimumMagnitude, 0, limitMagnitude);
        return Math.Min(Math.Max(ScaleMagnitudeForLimits(magnitude, globalLimitPercent, deviceLimitPercent), floor), limitMagnitude);
    }

    internal static int CalculateMinimumGameplayPulseMagnitude(FfbPulseKind kind)
    {
        return kind switch
        {
            FfbPulseKind.GearShift or FfbPulseKind.EngineStartStop => MinimumEngineDrivetrainPulseMagnitude,
            FfbPulseKind.DrivetrainJerk => 0,
            _ => MinimumGameplayPulseMagnitude
        };
    }

    private static int ClampGameplayPulseDuration(GameplayFfbOutput output)
    {
        var maxDurationMs = output.EventPulseKind == FfbPulseKind.EngineStartStop && output.BumpImpulsePercent > 0
            ? 5000
            : 250;
        return Math.Clamp(output.BumpDurationMs, 20, maxDurationMs);
    }

    private static int PercentToDirectInputMagnitude(int percent)
    {
        return Math.Clamp((int)Math.Round(DirectInputMax * (Math.Clamp(percent, 0, 100) / 100.0)), 0, DirectInputMax);
    }

    private static int DirectionForLogicalForce(int signedValue)
    {
        return signedValue < 0 ? DirectionPositive : DirectionNegative;
    }

    private static GameplayFfbOutput QuantizeOutput(GameplayFfbOutput output)
    {
        return output with
        {
            SpringPercent = QuantizePercent(output.SpringPercent, 1),
            DamperPercent = QuantizePercent(output.DamperPercent, 2),
            FrictionPercent = QuantizePercent(output.FrictionPercent, 2),
            EngineRpmVibrationPercent = QuantizePercent(output.EngineRpmVibrationPercent, 2),
            EngineRpmVibrationHz = QuantizePercent(output.EngineRpmVibrationHz, 2),
            SurfaceVibrationPercent = QuantizePercent(output.SurfaceVibrationPercent, 2),
            SurfaceVibrationHz = QuantizePercent(output.SurfaceVibrationHz, 2),
            TerrainRumblePercent = QuantizePercent(output.TerrainRumblePercent, 2),
            TerrainRumbleHz = QuantizePercent(output.TerrainRumbleHz, 2),
            SlipVibrationPercent = QuantizePercent(output.SlipVibrationPercent, 2),
            SlipVibrationHz = QuantizePercent(output.SlipVibrationHz, 2),
            BumpImpulsePercent = QuantizeSignedPercent(output.BumpImpulsePercent, 2)
        };
    }

    private static int QuantizePercent(int value, int step)
    {
        return Math.Clamp((int)Math.Round(value / (double)step) * step, 0, 100);
    }

    private static int QuantizeSignedPercent(int value, int step)
    {
        return Math.Clamp((int)Math.Round(value / (double)step) * step, -100, 100);
    }

    private static bool IsSameGameplayOutput(GameplayFfbOutput left, GameplayFfbOutput right)
    {
        return left.SpringPercent == right.SpringPercent &&
               left.DamperPercent == right.DamperPercent &&
               left.FrictionPercent == right.FrictionPercent &&
               left.EngineVibrationPercent == right.EngineVibrationPercent &&
               left.EngineVibrationHz == right.EngineVibrationHz &&
               left.SurfaceVibrationPercent == right.SurfaceVibrationPercent &&
               left.SurfaceVibrationHz == right.SurfaceVibrationHz &&
               left.TerrainRumblePercent == right.TerrainRumblePercent &&
               left.TerrainRumbleHz == right.TerrainRumbleHz &&
               left.SlipVibrationPercent == right.SlipVibrationPercent &&
               left.SlipVibrationHz == right.SlipVibrationHz &&
               left.BumpImpulsePercent == 0 &&
               right.BumpImpulsePercent == 0;
    }

    private void ReleaseDevice()
    {
        try
        {
            _device?.Unacquire();
            _device?.Dispose();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Device release failed");
        }
        finally
        {
            _device = null;
            SelectedDevice = null;
            _primaryAxisOffset = 0;
        }
    }

    private static IReadOnlyList<string> SafeGetEffects(IDirectInputDevice8 device)
    {
        try
        {
            return device.GetEffects()
                .Select(effect => string.IsNullOrWhiteSpace(effect.Name) ? effect.Guid.ToString() : effect.Name)
                .Distinct()
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static string BuildStableId(DeviceInstance instance)
    {
        var product = string.IsNullOrWhiteSpace(instance.ProductName) ? "unknown" : instance.ProductName.Trim();
        return $"{instance.ProductGuid:N}:{instance.InstanceGuid:N}:{product}";
    }

    private static bool IsNotExclusiveAcquired(SharpGenException ex)
    {
        return ex.Message.Contains("DIERR_NOTEXCLUSIVEACQUIRED", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("NotExclusiveAcquired", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAcquireFailure(SharpGenException ex)
    {
        return IsNotExclusiveAcquired(ex) ||
               ex.Message.Contains("DIERR_INPUTLOST", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("InputLost", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("DIERR_NOTACQUIRED", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("NotAcquired", StringComparison.OrdinalIgnoreCase);
    }
}
