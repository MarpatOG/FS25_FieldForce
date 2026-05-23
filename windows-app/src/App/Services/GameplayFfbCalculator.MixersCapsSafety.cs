using FieldForce.App.Models;

namespace FieldForce.App.Services;

public sealed partial class GameplayFfbCalculator
{
    public static class SteeringContributionMixer
    {
        public static SteeringModel Combine(params LayerContribution<SteeringContribution>[] layers)
        {
            var spring = 0.0;
            var damper = 0.0;
            var friction = 0.0;
            var centerOffset = 0.0;
            var springRelief = 0.0;
            var frictionRelief = 0.0;

            foreach (var layer in layers)
            {
                var confidence = Math.Clamp(layer.Confidence, 0, 1);
                var value = layer.Value;

                spring += value.SpringAdd * confidence;
                damper += value.DamperAdd * confidence;
                friction += value.FrictionAdd * confidence;
                centerOffset += value.CenterOffsetAdd * confidence;
                springRelief += value.SpringRelief * confidence;
                frictionRelief += value.FrictionRelief * confidence;
            }

            springRelief = Math.Clamp(springRelief, 0, 0.75);
            frictionRelief = Math.Clamp(frictionRelief, 0, 0.6);

            return new SteeringModel(
                spring * (1 - springRelief),
                damper,
                friction * (1 - frictionRelief),
                centerOffset);
        }
    }

    public static class HapticMixer
    {
        public static ContinuousHaptics CombineContinuous(params LayerContribution<ContinuousHaptics>[] layers)
        {
            var result = new ContinuousHaptics(0, 0, 0, 0, 0, 0, 0, 0);

            foreach (var layer in layers)
            {
                var confidence = Math.Clamp(layer.Confidence, 0, 1);
                var value = layer.Value;

                var surfaceCandidate = value.SurfacePercent * confidence;
                if (surfaceCandidate > result.SurfacePercent)
                {
                    result = result with
                    {
                        SurfacePercent = surfaceCandidate,
                        SurfaceHz = value.SurfaceHz
                    };
                }

                var slipCandidate = value.SlipPercent * confidence;
                if (slipCandidate > result.SlipPercent)
                {
                    result = result with
                    {
                        SlipPercent = slipCandidate,
                        SlipHz = value.SlipHz
                    };
                }

                var engineCandidate = value.EnginePercent * confidence;
                if (engineCandidate > result.EnginePercent)
                {
                    result = result with
                    {
                        EnginePercent = engineCandidate,
                        EngineHz = value.EngineHz
                    };
                }

                var terrainCandidate = value.TerrainRumblePercent * confidence;
                if (terrainCandidate > result.TerrainRumblePercent)
                {
                    result = result with
                    {
                        TerrainRumblePercent = terrainCandidate,
                        TerrainRumbleHz = value.TerrainRumbleHz
                    };
                }
            }

            return result;
        }

        public static IReadOnlyList<EventPulse> CombinePulses(params IReadOnlyList<EventPulse>[] pulseGroups)
        {
            return pulseGroups.SelectMany(p => p).Where(p => p.Confidence > 0).ToArray();
        }
    }

    public static class DeviceCaps
    {
        public static (SteeringModel Steering, ContinuousHaptics Haptics, IReadOnlyList<EventPulse> Pulses) Apply(
            SteeringModel steering,
            ContinuousHaptics haptics,
            IReadOnlyList<EventPulse> pulses,
            DeviceHapticProfile profile,
            bool engineStartStopActive = false)
        {
            var engineCapPercent = engineStartStopActive
                ? Math.Max(profile.EngineVibrationCapPercent, profile.EngineDrivetrainPulseCapPercent)
                : profile.EngineVibrationCapPercent;
            var cappedHaptics = haptics with
            {
                EnginePercent = Math.Min(haptics.EnginePercent, engineCapPercent),
                SurfacePercent = Math.Min(haptics.SurfacePercent, profile.SurfaceHapticCapPercent),
                SlipPercent = Math.Min(haptics.SlipPercent, profile.SlipHapticCapPercent),
                TerrainRumblePercent = Math.Min(haptics.TerrainRumblePercent, profile.TerrainRumbleCapPercent)
            };
            var cappedPulses = pulses
                .Select(p => p with
                {
                    Percent = CapPulsePercent(p, profile),
                    DurationMs = p.Kind is FfbPulseKind.GearShift or FfbPulseKind.EngineStartStop
                        ? p.DurationMs
                        : Math.Min(p.DurationMs, profile.MaxBumpDurationMs)
                })
                .ToArray();

            return (steering, cappedHaptics, cappedPulses);
        }

        private static double CapPulsePercent(EventPulse pulse, DeviceHapticProfile profile)
        {
            var magnitude = Math.Abs(pulse.Percent);
            var sign = Math.Sign(pulse.Percent == 0 ? 1 : pulse.Percent);
            var cap = pulse.Kind is FfbPulseKind.GearShift or FfbPulseKind.DrivetrainJerk or FfbPulseKind.EngineStartStop
                ? profile.EngineDrivetrainPulseCapPercent
                : profile.BumpPulseCapPercent;
            var floor = pulse.Kind is FfbPulseKind.GearShift or FfbPulseKind.DrivetrainJerk or FfbPulseKind.EngineStartStop
                ? magnitude
                : profile.Name.Contains("MOMO", StringComparison.OrdinalIgnoreCase) && magnitude > 0
                    ? Math.Max(magnitude, 6)
                    : magnitude;

            return sign * Math.Min(floor, cap);
        }
    }

    public static class SafetyFilters
    {
        public static SteeringModel Apply(SteeringModel steering)
        {
            return new SteeringModel(
                Math.Clamp(steering.Spring, 0, 100),
                Math.Clamp(steering.Damper, 0, 100),
                Math.Clamp(steering.Friction, 0, 100),
                Math.Clamp(steering.CenterOffsetPercent, -100, 100));
        }
    }

    private SteeringModel SmoothSteering(SteeringModel current, FfbFrameContext context, GameplayFfbEffectProfile profile)
    {
        _lastSlewLimited = false;

        var strength = Math.Clamp(profile.SlewSmoothing.StrengthPercent, 0, 100) / 100.0;
        if (_lastSteeringModel is null || !profile.SlewSmoothing.Enabled || strength <= 0)
        {
            _lastSteeringModel = current;
            return current;
        }

        var previous = _lastSteeringModel;
        var dt = Math.Clamp(context.DeltaTime.TotalSeconds, 0.001, 0.25);
        var smoothingSec = Lerp(0.015, 0.095, strength);
        var alpha = 1 - Math.Exp(-dt / Math.Max(0.001, smoothingSec));

        var smoothed = new SteeringModel(
            SmoothAxis(previous.Spring, current.Spring, alpha),
            SmoothAxis(previous.Damper, current.Damper, alpha),
            SmoothAxis(previous.Friction, current.Friction, alpha),
            SmoothAxis(previous.CenterOffsetPercent, current.CenterOffsetPercent, alpha));

        _lastSteeringModel = smoothed;
        return smoothed;
    }

    private double SmoothAxis(double previous, double target, double alpha)
    {
        var delta = target - previous;
        if (Math.Abs(delta) < 0.35)
        {
            return target;
        }

        _lastSlewLimited = true;
        return Lerp(previous, target, alpha);
    }

    private static double Lerp(double from, double to, double ratio)
    {
        return from + ((to - from) * Math.Clamp(ratio, 0, 1));
    }
}
