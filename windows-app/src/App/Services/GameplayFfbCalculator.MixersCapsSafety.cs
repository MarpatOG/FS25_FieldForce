using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Services;

public sealed partial class GameplayFfbCalculator
{
    public static class SteeringContributionMixer
    {
        public static SteeringModel Combine(params LayerContribution<SteeringContribution>[] layers)
        {
            var spring = 0.0;
            var damper = 0.0;
            var friction = 0.0;
            var springRelief = 0.0;
            var frictionRelief = 0.0;

            foreach (var layer in layers)
            {
                var confidence = Math.Clamp(layer.Confidence, 0, 1);
                var value = layer.Value;

                spring += value.SpringAdd * confidence;
                damper += value.DamperAdd * confidence;
                friction += value.FrictionAdd * confidence;
                springRelief += value.SpringRelief * confidence;
                frictionRelief += value.FrictionRelief * confidence;
            }

            springRelief = Math.Clamp(springRelief, 0, 0.75);
            frictionRelief = Math.Clamp(frictionRelief, 0, 0.6);

            return new SteeringModel(
                spring * (1 - springRelief),
                damper,
                friction * (1 - frictionRelief));
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
            DeviceHapticProfile profile)
        {
            var cappedHaptics = haptics with
            {
                EnginePercent = haptics.EnginePercent,
                SurfacePercent = Math.Min(haptics.SurfacePercent, profile.SurfaceHapticCapPercent),
                SlipPercent = Math.Min(haptics.SlipPercent, profile.SlipHapticCapPercent),
                TerrainRumblePercent = Math.Min(haptics.TerrainRumblePercent, profile.TerrainRumbleCapPercent)
            };
            var cappedPulses = pulses
                .Select(p => p with
                {
                    Percent = p.Kind is FfbPulseKind.GearShift or FfbPulseKind.EngineStartStop
                        ? p.Percent
                        : Math.Sign(p.Percent == 0 ? 1 : p.Percent) * Math.Min(
                        profile.Name.Contains("MOMO", StringComparison.OrdinalIgnoreCase) && Math.Abs(p.Percent) > 0
                            ? Math.Max(Math.Abs(p.Percent), 6)
                            : Math.Abs(p.Percent),
                        profile.BumpPulseCapPercent),
                    DurationMs = p.Kind is FfbPulseKind.GearShift or FfbPulseKind.EngineStartStop
                        ? p.DurationMs
                        : Math.Min(p.DurationMs, profile.MaxBumpDurationMs)
                })
                .ToArray();

            return (steering, cappedHaptics, cappedPulses);
        }
    }

    public static class SafetyFilters
    {
        public static SteeringModel Apply(SteeringModel steering)
        {
            return new SteeringModel(
                Math.Clamp(steering.Spring, 0, 100),
                Math.Clamp(steering.Damper, 0, 100),
                Math.Clamp(steering.Friction, 0, 100));
        }
    }

    private SteeringModel SmoothSteering(SteeringModel current, FfbFrameContext context)
    {
        const double alpha = 0.18;

        if (_lastSteeringModel is null)
        {
            _lastSteeringModel = current;
            return current;
        }

        var previous = _lastSteeringModel;

        var smoothed = new SteeringModel(
            Lerp(previous.Spring, current.Spring, alpha),
            Lerp(previous.Damper, current.Damper, alpha),
            Lerp(previous.Friction, current.Friction, alpha));

        _lastSteeringModel = smoothed;
        return smoothed;
    }

    private static double Lerp(double from, double to, double ratio)
    {
        return from + ((to - from) * Math.Clamp(ratio, 0, 1));
    }
}
