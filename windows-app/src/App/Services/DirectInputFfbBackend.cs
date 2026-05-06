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
    private const int MomoConstantMagnitude = 10000;
    private const int MomoVibrationMagnitude = 9000;
    private const int MomoVibrationHz = 24;
    private static readonly TimeSpan ConstantTestDuration = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan VibrationTestDuration = TimeSpan.FromSeconds(3);

    private readonly AppLogService _log;
    private readonly IDirectInput8 _directInput;
    private readonly List<IDirectInputEffect> _activeEffects = [];
    private IDirectInputDevice8? _device;
    private int _primaryAxisOffset;
    private int _globalLimitPercent = 40;
    private int _deviceLimitPercent = 35;

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
        if (_device is null || SelectedDevice is null)
        {
            _log.Warning("Effect start ignored because no FFB device is selected");
            return;
        }

        StopAllEffects("starting " + kind);

        try
        {
            var effect = kind switch
            {
                FfbEffectKind.Spring => CreateConditionEffect(EffectGuid.Spring, MomoTestConditionCoefficient, MomoTestConditionSaturation, MomoSpringDeadBand),
                FfbEffectKind.Damper => CreateConditionEffect(EffectGuid.Damper, MomoTestConditionCoefficient, MomoTestConditionSaturation, deadBand: 0),
                FfbEffectKind.ConstantLeft => CreateConstantEffect(MomoConstantMagnitude, DirectionNegative, ConstantTestDuration),
                FfbEffectKind.ConstantRight => CreateConstantEffect(MomoConstantMagnitude, DirectionPositive, ConstantTestDuration),
                FfbEffectKind.LowVibration => CreatePeriodicEffect(EffectGuid.Sine, MomoVibrationMagnitude, MomoVibrationHz, VibrationTestDuration),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };

            effect.Download().CheckError();
            effect.Start(1).CheckError();
            _activeEffects.Add(effect);
            _log.Information("Effect started: {EffectKind}", kind);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Effect start failed: {EffectKind}", kind);
            StopAllEffects("effect start failure");
        }
    }

    public void StopAllEffects(string reason)
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

        try
        {
            _device?.SendForceFeedbackCommand(ForceFeedbackCommand.StopAll);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "StopAll command failed");
        }

        _log.Information("Effect stopped: all effects ({Reason})", reason);
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

    private IDirectInputEffect CreateConstantEffect(int magnitude, int direction, TimeSpan duration)
    {
        var scaledMagnitude = ScaleMagnitude(magnitude);
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
        var limit = Math.Min(_globalLimitPercent, _deviceLimitPercent) / 100.0;
        return Math.Clamp((int)Math.Round(Math.Abs(magnitude) * limit), 0, DirectInputMax);
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
}
