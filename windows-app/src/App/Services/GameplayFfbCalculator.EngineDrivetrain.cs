using FieldForce.App.Models;

namespace FieldForce.App.Services;

public sealed partial class GameplayFfbCalculator
{
    private const double TelemetryLongitudinalJerkPulseThreshold = 0.85;

    public static class EngineDrivetrainLayer
    {
        public static LayerContribution<ContinuousHaptics> CalculateContinuous(TelemetryPacketV1 packet, TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var powertrainScale = CalculateContinuousEngineScale(features.PowertrainType);
            if (!profile.EngineRpmVibration.Enabled || packet.EngineRunning != true || context.TelemetryFade <= 0 || powertrainScale <= 0)
            {
                return new(new ContinuousHaptics(0, 0, 0, 0, 0, 0, 0, 0), packet.Rpm is null && packet.Rpm01 is null ? 0 : 1);
            }

            var rpmRatio = Math.Clamp(features.RpmRatio, 0.05, 1);
            var frequencyRatio = features.HeavyEngine ? Math.Sqrt(rpmRatio) : rpmRatio;
            var hz = Quantize((int)Math.Round(profile.EngineRpmVibration.MinFrequencyHz + ((profile.EngineRpmVibration.MaxFrequencyHz - profile.EngineRpmVibration.MinFrequencyHz) * frequencyRatio)), 2);
            var rpmMagnitude = Math.Clamp(0.32 + (0.68 * ApplyCurve(rpmRatio, profile.EngineRpmVibration.Curve)), 0, 1);
            var idle = Math.Clamp(profile.EngineRpmVibration.IdleStrengthPercent, 0, 100);
            var loaded = Math.Clamp(profile.EngineRpmVibration.LoadStrengthPercent, 0, 100);
            var basePercent = Lerp(idle, loaded, ApplyCurve(features.EngineLoadRatio, profile.EngineRpmVibration.Curve));
            if (features.EngineLugging)
            {
                basePercent += Math.Clamp(profile.EngineRpmVibration.LuggingBoostPercent, 0, 100);
            }

            var outputCapScale = Math.Clamp(profile.EngineRpmVibration.MaxOutputPercent, 0, 100) / 100.0;
            var percent = basePercent * outputCapScale * context.TelemetryFade * rpmMagnitude;
            percent = Math.Min(percent, Math.Clamp(profile.EngineDrivetrainMaxPercent, 0, 100)) * powertrainScale;
            return new(new ContinuousHaptics(0, 0, 0, 0, percent, hz, 0, 0), 1.0);
        }

        public static int StartPulseHz(GameplayFfbEffectProfile profile)
        {
            return Quantize(Math.Clamp(profile.EngineStartStopPulse.StartFrequencyHz, 6, 60), 2);
        }

        public static int StopPulseHz(GameplayFfbEffectProfile profile)
        {
            return Quantize(Math.Clamp(profile.EngineStartStopPulse.StopFrequencyHz, 6, 60), 2);
        }
    }

    private IReadOnlyList<EventPulse> CalculateEngineDrivetrainPulses(TelemetryPacketV1 packet, TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
    {
        if (context.TelemetryFade <= 0)
        {
            _lastDrivetrainSample = null;
            return [];
        }

        var current = new DrivetrainSample(packet.VehicleName, packet.EngineRunning, IsEngineStarting(packet), packet.TransmissionThrottle01, packet.TransmissionBrake01, packet.TransmissionClutch01, packet.Gear, packet.EngineStartSeq, packet.EngineStopSeq, packet.GearChangeSeq, features.LongitudinalJerkImpulse);
        var previous = _lastDrivetrainSample;
        _lastDrivetrainSample = current;
        if (previous is null || !string.Equals(previous.VehicleName, current.VehicleName, StringComparison.Ordinal))
        {
            _lastEngineStartSeq = current.EngineStartSeq;
            _lastEngineStopSeq = current.EngineStopSeq;
            _lastGearChangeSeq = current.GearChangeSeq;
            ResetRpmStartDetection(packet.VehicleName);
            return [];
        }

        var pulses = new List<EventPulse>();
        if (current.EngineStarted == false && current.EngineIsStarting != true)
        {
            _suppressRpmStartUntilEngineOff = false;
        }

        var engineStartedBySeq = IsNewSeq(current.EngineStartSeq, ref _lastEngineStartSeq);
        var engineStartedByStartingState = DetectEngineStartByStartingState(previous, current);
        var engineStartedByStarter = engineStartedBySeq || engineStartedByStartingState;
        if (engineStartedByStarter)
        {
            _suppressRpmStartUntilEngineOff = true;
            ResetRpmStartDetection(packet.VehicleName);
        }

        var engineStartedByRpm = !engineStartedByStarter &&
                                 !_suppressRpmStartUntilEngineOff &&
                                 current.EngineStartSeq is null &&
                                 (DetectEngineStartByRunningTransition(previous, current, packet) || DetectEngineStartByRpm(packet, context));
        if (profile.EngineStartStopPulse.Enabled && (engineStartedByStarter || engineStartedByRpm))
        {
            StartEngineStartStopVibration(profile, context, direction: 1, packet.EngineStartDurationMs, CalculateStartStopScale(features.PowertrainType));
        }

        if (profile.EngineStartStopPulse.Enabled && IsNewSeq(current.EngineStopSeq, ref _lastEngineStopSeq))
        {
            StartEngineStartStopVibration(profile, context, direction: -1, powertrainScale: CalculateStartStopScale(features.PowertrainType));
        }

        var gearChangedBySeq = profile.GearShiftPulse.Enabled && IsNewSeq(current.GearChangeSeq, ref _lastGearChangeSeq);
        var gearChangedByFallback = profile.GearShiftPulse.Enabled &&
                                    current.GearChangeSeq is null &&
                                    previous.Gear is not null &&
                                    current.Gear is not null &&
                                    previous.Gear != current.Gear;
        if (gearChangedBySeq || gearChangedByFallback)
        {
            var gearShiftPulse = CreateGearShiftPulse(profile, features, context, CalculateGearShiftScale(features.PowertrainType));
            if (gearShiftPulse.Confidence > 0)
            {
                pulses.Add(gearShiftPulse);
            }
        }

        var drivetrainJerkScale = CalculateDrivetrainJerkScale(features.PowertrainType);
        if (profile.DrivetrainPulse.Enabled && features.DrivetrainConfidence >= 1.0 && drivetrainJerkScale > 0)
        {
            var throttleDelta = Math.Abs((current.Throttle ?? 0) - (previous.Throttle ?? 0));
            var brakeDelta = features.SpeedKmh > 0
                ? Math.Abs((current.Brake ?? 0) - (previous.Brake ?? 0))
                : 0;
            var pedalDelta = Math.Max(throttleDelta, brakeDelta);
            var pulseRatio = pedalDelta >= 0.35
                ? Math.Clamp((pedalDelta - 0.35) / 0.65, 0.25, 1.0)
                : 0;

            if (ShouldCreateTelemetryLongitudinalJerkPulse(features, current, previous) &&
                features.VerticalImpactImpulse < profile.BumpFeedback.MinImpulse &&
                features.CollisionImpulse < profile.CollisionFeedback.MinImpulse)
            {
                pulseRatio = Math.Max(pulseRatio, Math.Clamp((features.LongitudinalJerkImpulse - TelemetryLongitudinalJerkPulseThreshold) / (2.0 - TelemetryLongitudinalJerkPulseThreshold), 0.25, 1.0));
            }

            if (pulseRatio > 0)
            {
                var direction = (current.Brake ?? 0) > (previous.Brake ?? 0) ? -1 : 1;
                pulses.Add(new EventPulse(
                    FfbPulseKind.DrivetrainJerk,
                    CalculateMaxCapped(profile.DrivetrainPulse, context.TelemetryFade) * ApplyCurve(pulseRatio, profile.DrivetrainPulse.Curve) * drivetrainJerkScale,
                    Math.Clamp(profile.DrivetrainPulse.DurationMs, 20, 160),
                    Math.Clamp(profile.DrivetrainPulse.CooldownMs, 20, 500),
                    direction,
                    1.0));
            }
        }

        return pulses;
    }

    private static bool ShouldCreateTelemetryLongitudinalJerkPulse(TelemetryFeatures features, DrivetrainSample current, DrivetrainSample previous)
    {
        return features.SpeedKmh > 0 &&
               (IsOffRoadSurface(features) || IsUnknownMixedSurface(features)) &&
               current.LongitudinalJerkImpulse >= TelemetryLongitudinalJerkPulseThreshold &&
               previous.LongitudinalJerkImpulse < TelemetryLongitudinalJerkPulseThreshold;
    }

    private static bool IsNewSeq(long? seq, ref long? lastSeq)
    {
        if (seq is null)
        {
            return false;
        }

        var isNew = lastSeq is not null && seq.Value > lastSeq.Value;
        lastSeq = seq;
        return isNew;
    }

    private static bool IsEngineStarting(TelemetryPacketV1 packet)
    {
        return packet.EngineIsStarting == true ||
               string.Equals(packet.EngineState, "starting", StringComparison.OrdinalIgnoreCase);
    }

    private bool DetectEngineStartByRpm(TelemetryPacketV1 packet, FfbFrameContext context)
    {
        var vehicleName = packet.VehicleName ?? "";
        if (!string.Equals(_rpmStartVehicleName, vehicleName, StringComparison.Ordinal))
        {
            _rpmStartVehicleName = vehicleName;
            _rpmStartZeroMs = 0;
            _rpmStartArmed = false;
        }

        if (!IsValidFinite(packet.Rpm))
        {
            return false;
        }

        var rpm = packet.Rpm!.Value;
        if (rpm <= 0)
        {
            _rpmStartZeroMs += Math.Max(1, context.DeltaTime.TotalMilliseconds);
            if (_rpmStartZeroMs > EngineStartRpmZeroThresholdMs)
            {
                _rpmStartArmed = true;
            }

            return false;
        }

        if (rpm > EngineStartRpmTriggerThreshold)
        {
            var shouldTrigger = _rpmStartArmed;
            _rpmStartZeroMs = 0;
            _rpmStartArmed = false;
            return shouldTrigger;
        }

        if (!_rpmStartArmed)
        {
            _rpmStartZeroMs = 0;
        }

        return false;
    }

    private static bool DetectEngineStartByRunningTransition(DrivetrainSample previous, DrivetrainSample current, TelemetryPacketV1 packet)
    {
        return previous.EngineStarted == false &&
               current.EngineStarted == true &&
               IsValidFinite(packet.Rpm) &&
               packet.Rpm!.Value > EngineStartRpmTriggerThreshold;
    }

    private static bool DetectEngineStartByStartingState(DrivetrainSample previous, DrivetrainSample current)
    {
        return previous.EngineIsStarting != true && current.EngineIsStarting == true;
    }

    private void ResetRpmStartDetection(string? vehicleName)
    {
        _rpmStartVehicleName = vehicleName ?? "";
        _rpmStartZeroMs = 0;
        _rpmStartArmed = false;
    }

    private void StartEngineStartStopVibration(GameplayFfbEffectProfile profile, FfbFrameContext context, int direction, double? telemetryStartDurationMs = null, double powertrainScale = 1.0)
    {
        var settings = profile.EngineStartStopPulse;
        var percent = Math.Min(CalculateMaxCapped(settings, context.TelemetryFade) * 0.85, Math.Clamp(profile.EngineDrivetrainMaxPercent, 0, 100)) * Math.Clamp(powertrainScale, 0, 1);
        var durationMs = direction > 0
            ? ResolveEngineStartDurationMs(settings, telemetryStartDurationMs)
            : Math.Clamp(settings.StopDurationMs, 40, 500);
        var hz = direction > 0
            ? EngineDrivetrainLayer.StartPulseHz(profile)
            : EngineDrivetrainLayer.StopPulseHz(profile);
        _engineStartStopVibration = percent > 0
            ? new EngineStartStopVibrationState(direction, durationMs, durationMs, percent, hz)
            : null;
    }

    private static int ResolveEngineStartDurationMs(EngineStartStopPulseSettings settings, double? telemetryStartDurationMs)
    {
        var configuredStartDurationMs = Math.Clamp(settings.StartDurationMs, 40, 5000);
        if (IsValidFinite(telemetryStartDurationMs))
        {
            return (int)Math.Round(Math.Clamp(telemetryStartDurationMs!.Value, 650, Math.Max(650, configuredStartDurationMs)));
        }

        return configuredStartDurationMs;
    }

    private EngineStartStopVibrationFrame CalculateEngineStartStopVibrationContinuous(GameplayFfbEffectProfile profile, FfbFrameContext context)
    {
        if (_engineStartStopVibration is null || context.TelemetryFade <= 0)
        {
            return EngineStartStopVibrationFrame.Inactive;
        }

        var state = _engineStartStopVibration;
        var elapsedMs = Math.Max(1, context.DeltaTime.TotalMilliseconds);
        var remainingRatio = Math.Clamp(state.RemainingMs / Math.Max(1.0, state.DurationMs), 0, 1);
        var fadeOut = state.Direction > 0
            ? Math.Clamp(remainingRatio * 1.25, 0, 1)
            : remainingRatio;
        var percent = Math.Min(state.Percent * fadeOut, Math.Clamp(profile.EngineDrivetrainMaxPercent, 0, 100));

        var nextRemaining = state.RemainingMs - elapsedMs;
        _engineStartStopVibration = nextRemaining > 0
            ? state with { RemainingMs = nextRemaining }
            : null;

        var contribution = new LayerContribution<ContinuousHaptics>(
            new ContinuousHaptics(0, 0, 0, 0, percent, state.Hz, 0, 0),
            percent > 0 ? 1.0 : 0.0);
        return percent > 0
            ? new EngineStartStopVibrationFrame(contribution, state.Direction, (int)Math.Round(state.DurationMs))
            : EngineStartStopVibrationFrame.Inactive;
    }

    private static EventPulse CreateGearShiftPulse(GameplayFfbEffectProfile profile, TelemetryFeatures features, FfbFrameContext context, double powertrainScale = 1.0)
    {
        var settings = profile.GearShiftPulse;
        var loadScale = Math.Clamp(0.70 + (features.EngineLoadRatio * 0.55), 0.65, 1.25);
        var percent = Math.Min(CalculateMaxCapped(settings, context.TelemetryFade) * loadScale, Math.Clamp(profile.EngineDrivetrainMaxPercent, 0, 100)) * Math.Clamp(powertrainScale, 0, 1);
        return new EventPulse(
            FfbPulseKind.GearShift,
            percent,
            Math.Clamp(settings.DurationMs, 25, 160),
            Math.Clamp(settings.CooldownMs, 100, 700),
            1,
            percent > 0 ? 1.0 : 0.0);
    }

    private static double CalculateContinuousEngineScale(string powertrainType)
    {
        return powertrainType switch
        {
            "electric" => ElectricContinuousEngineScale,
            "hybrid" => HybridContinuousEngineScale,
            _ => 1.0
        };
    }

    private static double CalculateStartStopScale(string powertrainType)
    {
        return powertrainType switch
        {
            "electric" => ElectricStartStopScale,
            "hybrid" => HybridStartStopScale,
            _ => 1.0
        };
    }

    private static double CalculateGearShiftScale(string powertrainType)
    {
        return powertrainType switch
        {
            "electric" => ElectricGearShiftScale,
            "hybrid" => HybridGearShiftScale,
            _ => 1.0
        };
    }

    private static double CalculateDrivetrainJerkScale(string powertrainType)
    {
        return powertrainType switch
        {
            "electric" => ElectricDrivetrainJerkScale,
            "hybrid" => HybridDrivetrainJerkScale,
            _ => 1.0
        };
    }

    private void ResetEngineEventState()
    {
        _lastEngineStartSeq = null;
        _lastEngineStopSeq = null;
        _lastGearChangeSeq = null;
        _engineStartStopVibration = null;
        _rpmStartVehicleName = null;
        _rpmStartZeroMs = 0;
        _rpmStartArmed = false;
        _suppressRpmStartUntilEngineOff = false;
    }

    private sealed record DrivetrainSample(string? VehicleName, bool? EngineStarted, bool EngineIsStarting, double? Throttle, double? Brake, double? Clutch, int? Gear, long? EngineStartSeq, long? EngineStopSeq, long? GearChangeSeq, double LongitudinalJerkImpulse);

    private sealed record EngineStartStopVibrationState(int Direction, double DurationMs, double RemainingMs, double Percent, int Hz);

    private sealed record EngineStartStopVibrationFrame(LayerContribution<ContinuousHaptics> Contribution, int Direction, int DurationMs)
    {
        public static EngineStartStopVibrationFrame Inactive { get; } = new(
            new LayerContribution<ContinuousHaptics>(new ContinuousHaptics(0, 0, 0, 0, 0, 0, 0, 0), 0),
            0,
            0);
    }
}
