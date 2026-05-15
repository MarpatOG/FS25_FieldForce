using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Services;

public sealed partial class GameplayFfbCalculator
{
    public static double CalculateSteeringLoadSpeedScale(double speedKmh)
    {
        const double fullEffectSpeedKmh = 10.0;
        const double cappedSpeedKmh = 40.0;
        return Math.Clamp(Math.Min(Math.Max(0, speedKmh), cappedSpeedKmh) / fullEffectSpeedKmh, 0, 1);
    }

    internal static double CalculateSpeedEffect(SpeedConditionSettings settings, double speedKmh, double fade)
    {
        if (!settings.Enabled)
        {
            return 0;
        }

        var speedRatio = Math.Clamp(speedKmh / Math.Max(1, settings.SpeedReferenceKmh), 0, 1);
        var curve = ApplyCurve(speedRatio, settings.Curve);
        var floor = Math.Clamp(settings.StandstillFloor, 0, 1);
        var normalized = Math.Clamp(floor + ((1 - floor) * curve), 0, 1);
        return CalculateMaxCapped(settings, fade) * normalized;
    }

    public static class SpeedSpringLayer
    {
        public static LayerContribution<SteeringContribution> Calculate(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var spring = CalculateSpeedEffect(profile.SpeedSpring, features.SpeedKmh, context.TelemetryFade);
            return new(new SteeringContribution(nameof(SpeedSpringLayer), spring, 0, 0, 0, 0, 1), 1.0);
        }
    }

    public static class SpeedDamperLayer
    {
        public static LayerContribution<SteeringContribution> Calculate(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var damper = CalculateSpeedEffect(profile.SpeedDamper, features.SpeedKmh, context.TelemetryFade);
            return new(new SteeringContribution(nameof(SpeedDamperLayer), 0, damper, 0, 0, 0, 1), 1.0);
        }
    }

    public static class MechanicalFrictionLayer
    {
        public static LayerContribution<SteeringContribution> Calculate(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var friction = CalculateMechanicalFriction(profile.MechanicalFriction, CalculateLoadRatio(features.LoadFactor), features.SurfaceClass is "field" or "wetField", context.TelemetryFade);
            return new(new SteeringContribution(nameof(MechanicalFrictionLayer), 0, 0, friction, 0, 0, 1), 1.0);
        }

        private static double CalculateMechanicalFriction(MechanicalFrictionSettings settings, double loadRatio, bool surfaceActive, double fade)
        {
            if (!settings.Enabled)
            {
                return 0;
            }

            var normalized = Math.Clamp(
                Math.Clamp(settings.BaseFriction, 0, 1) +
                (Math.Clamp(settings.LoadInfluence, 0, 2) * ApplyCurve(loadRatio, settings.Curve)) +
                (surfaceActive ? Math.Clamp(settings.FieldInfluence, 0, 1) : 0),
                0,
                1);

            return CalculateMaxCapped(settings, fade) * normalized;
        }
    }

    public static class SurfaceSteeringLayer
    {
        public static LayerContribution<SteeringContribution> Calculate(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var confidence = features.SurfaceConfidence;
            if (features.SurfaceClass is not ("field" or "wetField") || !profile.SurfaceFeedback.Enabled || features.SpeedKmh < Math.Max(0, profile.SurfaceFeedback.MinSpeedKmh))
            {
                return new(Zero(nameof(SurfaceSteeringLayer)), confidence);
            }

            var baseSpring = CalculateSpeedEffect(profile.SpeedSpring, features.SpeedKmh, context.TelemetryFade);
            var baseDamper = CalculateSpeedEffect(profile.SpeedDamper, features.SpeedKmh, context.TelemetryFade);
            var baseFriction = MechanicalFrictionLayer.Calculate(features, profile, context).Value.FrictionAdd;
            var springGain = 1 + (profile.SurfaceFeedback.FieldSpringModifierPercent / 100.0);
            var damperGain = 1 + (profile.SurfaceFeedback.FieldDamperModifierPercent / 100.0);
            var frictionGain = 1 + (profile.SurfaceFeedback.FieldFrictionModifierPercent / 100.0);

            var wetnessEffect = CalculateWetnessEffect(profile.WetnessFeedback, features.Wetness, context.TelemetryFade);
            if (wetnessEffect > 0)
            {
                springGain *= Lerp(1, 0.92, wetnessEffect);
                damperGain *= 1 + wetnessEffect * profile.WetnessFeedback.DamperModifierPercent / 100.0;
            }

            return new(new SteeringContribution(
                nameof(SurfaceSteeringLayer),
                baseSpring * (springGain - 1),
                baseDamper * (damperGain - 1),
                baseFriction * (frictionGain - 1),
                0,
                0,
                1), confidence);
        }
    }

    public static class LoadResistanceLayer
    {
        public static LayerContribution<SteeringContribution> Calculate(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            if (!profile.LoadResistance.Enabled)
            {
                return new(Zero(nameof(LoadResistanceLayer)), features.LoadConfidence);
            }

            var loadResistance =
                (CalculateMaxCapped(profile.LoadResistance, context.TelemetryFade) / 100.0) *
                CalculateSteeringLoadSpeedScale(features.SpeedKmh) *
                ApplyCurve(CalculateLoadRatio(features.LoadFactor), profile.LoadResistance.Curve);
            var baseSpring = CalculateSpeedEffect(profile.SpeedSpring, features.SpeedKmh, context.TelemetryFade);
            var baseDamper = CalculateSpeedEffect(profile.SpeedDamper, features.SpeedKmh, context.TelemetryFade);
            var baseFriction = MechanicalFrictionLayer.Calculate(features, profile, context).Value.FrictionAdd;
            return new(new SteeringContribution(
                nameof(LoadResistanceLayer),
                profile.LoadResistance.AffectsSpring ? baseSpring * loadResistance * Math.Clamp(profile.LoadResistance.SpringScale, 0, 2) : 0,
                profile.LoadResistance.AffectsDamper ? baseDamper * loadResistance * Math.Clamp(profile.LoadResistance.DamperScale, 0, 2) : 0,
                profile.LoadResistance.AffectsFriction ? baseFriction * loadResistance * Math.Clamp(profile.LoadResistance.FrictionScale, 0, 2) : 0,
                0,
                0,
                1), features.LoadConfidence);
        }
    }

    public static class MotionFeedbackLayer
    {
        public static LayerContribution<SteeringContribution> Calculate(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var settings = profile.MotionFeedback;
            if (!settings.Enabled)
            {
                return new(Zero(nameof(MotionFeedbackLayer)), 1.0);
            }

            var motionRatio = Math.Max(features.YawRateRatio * features.SpeedRatio, features.SlopeRatio);
            if (motionRatio <= 0)
            {
                return new(Zero(nameof(MotionFeedbackLayer)), 1.0);
            }

            var weighted = (CalculateMaxCapped(settings, context.TelemetryFade) / 100.0) * ApplyCurve(motionRatio, settings.Curve);
            var baseSpring = CalculateSpeedEffect(profile.SpeedSpring, features.SpeedKmh, context.TelemetryFade);
            var baseDamper = CalculateSpeedEffect(profile.SpeedDamper, features.SpeedKmh, context.TelemetryFade);
            return new(new SteeringContribution(
                nameof(MotionFeedbackLayer),
                baseSpring * weighted * Math.Clamp(settings.SpringModifierPercent, -100, 100) / 100.0,
                baseDamper * weighted * Math.Clamp(settings.DamperModifierPercent, -100, 100) / 100.0,
                0,
                0,
                0,
                1), 1.0);
        }
    }

    public static class ContactReliefLayer
    {
        public static LayerContribution<SteeringContribution> Calculate(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var contactLoss = (1 - features.ContactRatio) * features.ContactConfidence;
            var slipRelief = profile.SlipFeedback.Enabled
                ? Math.Clamp(
                    (features.Slip - profile.SlipFeedback.MinSlip) /
                    Math.Max(0.01, profile.SlipFeedback.FullSlip - profile.SlipFeedback.MinSlip),
                    0,
                    1) * 0.30
                : 0;
            return new(new SteeringContribution(
                nameof(ContactReliefLayer),
                0,
                0,
                0,
                Math.Clamp((contactLoss * 0.65) + slipRelief, 0, 0.85),
                Math.Clamp(((contactLoss * 0.65) + slipRelief) * 0.5, 0, 0.7),
                1), Math.Max(features.ContactConfidence, features.Slip > 0 ? 0.8 : 0));
        }
    }

    public static class SpeedStabilityLayer
    {
        public static LayerContribution<SteeringContribution> Calculate(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            if (!profile.SpeedDamper.Enabled)
            {
                return new(Zero(nameof(SpeedStabilityLayer)), 0);
            }

            var speedScale = CalculateSteeringLoadSpeedScale(features.SpeedKmh);
            var speedDamping = speedScale * 2.0;
            var rateDamping = Math.Clamp(Math.Abs(features.SteeringRate) / 2.0, 0, 1) * 8.0 * context.DeviceProfile.SteeringRateDamperScale * speedScale;
            var antiOscillation = speedScale > 0.45 && Math.Abs(features.SteeringAngle) < 0.04 ? 3.0 * speedScale : 0.0;
            return new(new SteeringContribution(nameof(SpeedStabilityLayer), 0, speedDamping + rateDamping + antiOscillation, 0, 0, 0, 1), 1.0);
        }
    }

    public static class HillStandstillLoadLayer
    {
        public static LayerContribution<SteeringContribution> Calculate(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var settings = profile.HillStandstillLoad;
            var slopeDeg = features.SlopeRatio * Math.Max(0.1, profile.MotionFeedback.FullPitchDeg);
            var slopeRatio = slopeDeg <= settings.MinSlopeDeg
                ? 0
                : Math.Clamp((slopeDeg - settings.MinSlopeDeg) / Math.Max(0.1, settings.FullSlopeDeg - settings.MinSlopeDeg), 0, 1);
            if (!settings.Enabled || features.SpeedKmh > MovingSpeedThresholdKmh || slopeRatio <= 0)
            {
                return new(Zero(nameof(HillStandstillLoadLayer)), 1.0);
            }

            var loadScale = Math.Clamp(0.75 + (CalculateLoadRatio(features.LoadFactor) * 0.65), 0.75, 1.4);
            var weighted = (CalculateMaxCapped(settings, context.TelemetryFade) / 100.0) * ApplyCurve(slopeRatio, settings.Curve) * loadScale;
            var baseSpring = CalculateSpeedEffect(profile.SpeedSpring, 0, context.TelemetryFade);
            var baseDamper = CalculateSpeedEffect(profile.SpeedDamper, 0, context.TelemetryFade);
            return new(new SteeringContribution(
                nameof(HillStandstillLoadLayer),
                Math.Max(8, baseSpring) * weighted * 0.85,
                Math.Max(6, baseDamper) * weighted * 1.15,
                MechanicalFrictionLayer.Calculate(features, profile, context).Value.FrictionAdd * weighted * 0.75,
                0,
                0,
                1), 1.0);
        }
    }

    public static class SideSlopeBiasLayer
    {
        public static LayerContribution<SteeringContribution> Calculate(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var settings = profile.SideSlopeBias;
            if (!settings.Enabled || features.RollRatio <= 0 || features.RollDirection == 0)
            {
                return new(Zero(nameof(SideSlopeBiasLayer)), 1.0);
            }

            var shaped = ApplyCurve(features.RollRatio, settings.Curve);
            var max = CalculateMaxCapped(settings, context.TelemetryFade);
            var load = (max / 100.0) * shaped;
            return new(new SteeringContribution(
                nameof(SideSlopeBiasLayer),
                0,
                Math.Max(4, CalculateSpeedEffect(profile.SpeedDamper, features.SpeedKmh, context.TelemetryFade)) * load * 0.45,
                Math.Max(4, MechanicalFrictionLayer.Calculate(features, profile, context).Value.FrictionAdd) * load * 0.35,
                0,
                0,
                1,
                features.RollDirection * max * shaped), 1.0);
        }
    }

    public static class ImplementBiasLayer
    {
        public static LayerContribution<SteeringContribution> Calculate(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var settings = profile.ImplementBias;
            if (!settings.Enabled || features.AttachedMassRatio <= settings.MinAttachedMassRatio)
            {
                return new(Zero(nameof(ImplementBiasLayer)), 1.0);
            }

            var massRatio = Math.Clamp(
                (features.AttachedMassRatio - settings.MinAttachedMassRatio) /
                Math.Max(0.01, settings.FullAttachedMassRatio - settings.MinAttachedMassRatio),
                0,
                1);
            var shaped = ApplyCurve(massRatio, settings.Curve);
            var max = CalculateMaxCapped(settings, context.TelemetryFade);
            var load = (max / 100.0) * shaped;
            var offset = Math.Abs(features.ImplementLateralOffsetRatio) > 0.01
                ? Math.Clamp(features.ImplementLateralOffsetRatio, -1, 1) * max * shaped
                : 0;
            return new(new SteeringContribution(
                nameof(ImplementBiasLayer),
                Math.Max(6, CalculateSpeedEffect(profile.SpeedSpring, features.SpeedKmh, context.TelemetryFade)) * load * 0.18,
                Math.Max(5, CalculateSpeedEffect(profile.SpeedDamper, features.SpeedKmh, context.TelemetryFade)) * load * 0.55,
                Math.Max(4, MechanicalFrictionLayer.Calculate(features, profile, context).Value.FrictionAdd) * load * 0.55,
                0,
                0,
                1,
                offset), 1.0);
        }
    }

    private static SteeringContribution Zero(string source)
    {
        return new SteeringContribution(source, 0, 0, 0, 0, 0, 1);
    }
}
