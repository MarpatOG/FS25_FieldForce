using FieldForce.App.Models;

namespace FieldForce.App.Services;

public sealed partial class GameplayFfbCalculator
{
    private static double CalculateLoadFactor(double? mass, double? totalMass)
    {
        if (mass is null || totalMass is null || mass <= 0 || totalMass <= 0)
        {
            return 1;
        }

        return Math.Clamp(totalMass.Value / mass.Value, 1, 4);
    }

    private static double CalculateLoadRatio(double loadFactor)
    {
        return Math.Clamp((loadFactor - 1) / 2, 0, 1);
    }

    private static string NormalizeSurfaceType(TelemetryPacketV1 packet, TireSurfaceTuningSettings? tuning)
    {
        var value = packet.SurfaceType?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return packet.IsOnField == true ? "field" : "unknownMixed";
        }

        var normalized = (tuning ?? new TireSurfaceTuningSettings()).ResolveSurfaceAlias(value);
        return normalized == "unknownMixed" && packet.IsOnField == true ? "field" : normalized;
    }

    private static bool IsOffRoadSurface(string surfaceType, bool? isOnField)
    {
        return surfaceType is "field" or "wetField" or "grass" or "dirt" or "gravel" or "mud" or "snow" or "shallowWater" ||
               (surfaceType == "unknown" && isOnField == true);
    }

    private static bool IsOffRoadSurface(TelemetryFeatures features)
    {
        return features.SurfaceClass is "field" or "wetField" or "grass" or "dirt" or "gravel" or "mud" or "snow" or "shallowWater" or "plowedField" or "cultivatedField";
    }

    private static bool IsRoadSurface(string surfaceType) => surfaceType is "asphalt";

    private static bool IsUnknownMixedSurface(TelemetryFeatures features)
    {
        return features.SurfaceClass == "unknownMixed";
    }

    private static double CalculateHapticLoadScale(TelemetryFeatures features)
    {
        if (!IsOffRoadSurface(features))
        {
            return 1;
        }

        var extraLoad = Math.Max(0, features.LoadFactor - 1);
        return Math.Clamp(1 + (Math.Sqrt(extraLoad) * 0.24), 1, 1.42);
    }

    private static double? CalculateWetness(TelemetryPacketV1 packet)
    {
        if (packet.GroundWetness is null && packet.RainScale is null)
        {
            return null;
        }

        return Math.Clamp(Math.Max(packet.GroundWetness ?? 0, packet.RainScale ?? 0), 0, 1);
    }

    private static double NormalizeAbs(double? value, double fullScale)
    {
        if (value is null)
        {
            return 0;
        }

        return Math.Clamp(Math.Abs(value.Value) / Math.Max(0.01, fullScale), 0, 1);
    }

    private static double NormalizeVector(double? x, double? y, double? z, double fullScale)
    {
        if (x is null && y is null && z is null)
        {
            return 0;
        }

        var magnitude = Math.Sqrt(Math.Pow(x ?? 0, 2) + Math.Pow(y ?? 0, 2) + Math.Pow(z ?? 0, 2));
        return Math.Clamp(magnitude / Math.Max(0.01, fullScale), 0, 1);
    }

    private static bool IsValidFinite(double? value)
    {
        return value is not null && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);
    }

    private static double? FirstValid(params double?[] values)
    {
        foreach (var value in values)
        {
            if (IsValidFinite(value))
            {
                return value;
            }
        }

        return null;
    }

    private static double NormalizeImpulse(double? value)
    {
        if (!IsValidFinite(value))
        {
            return 0;
        }

        var magnitude = Math.Abs(value!.Value);
        return magnitude < ImpulseNoiseFloor ? 0 : Math.Clamp(magnitude, 0, 2);
    }

    private static double MaxValidImpulse(params double?[] values)
    {
        var max = 0.0;
        foreach (var value in values)
        {
            max = Math.Max(max, NormalizeImpulse(value));
        }

        return max;
    }

    private static int CountValidImpulses(params double?[] values)
    {
        return values.Count(value => NormalizeImpulse(value) > 0);
    }

    public static class TelemetryFeatureExtractor
    {
        public static TelemetryFeatures Extract(TelemetryPacketV1 packet, GameplayFfbEffectProfile profile, TireSurfaceTuningSettings? tuning = null)
        {
            tuning = TireSurfaceTuningSettings.CreateNormalized(tuning);
            var rawSpeed = Math.Max(0, packet.SpeedKmh ?? 0);
            var speed = rawSpeed < MovingSpeedThresholdKmh ? 0 : rawSpeed;
            var surfaceType = NormalizeSurfaceType(packet, tuning);
            var surfaceClass = IsOffRoadSurface(surfaceType, packet.IsOnField)
                ? (surfaceType == "unknown" ? "field" : surfaceType)
                : IsRoadSurface(surfaceType) ? "road" : "unknownMixed";
            var tireProfile = ResolveTireProfile(packet);
            var multiplier = ResolveTireSurfaceMultiplier(packet, tuning, tireProfile, surfaceType);
            var loadFactor = CalculateLoadFactor(packet.MassKg, packet.TotalMassKg);
            var steeringContact = FirstValid(packet.SteeringGroundContactRatio, packet.GroundContactRatio);
            var contactConfidence = IsValidFinite(packet.SteeringGroundContactRatio) ? 1.0 : IsValidFinite(packet.GroundContactRatio) ? 0.55 : 0.0;
            var suspension = MaxValidImpulse(packet.SuspensionImpulse, packet.BumpImpulse, packet.VerticalImpactImpulse);
            var suspensionConfidence = CountValidImpulses(packet.SuspensionImpulse, packet.BumpImpulse, packet.VerticalImpactImpulse) > 0 ? 1.0 : 0.0;
            var verticalImpact = MaxValidImpulse(packet.VerticalImpactImpulse, packet.BumpImpulse, packet.SuspensionImpulse);
            var landing = NormalizeImpulse(packet.LandingImpulse);
            var collision = NormalizeImpulse(packet.CollisionImpulse);
            var longitudinalJerk = packet.LongitudinalJerkImpulse ??
                                    Math.Clamp(Math.Abs(packet.LocalAccelerationZ ?? packet.LocalAccelerationX ?? 0) / 9.81, 0, 2);
            var slip = Math.Max(packet.SteeringWheelSlip ?? 0, packet.MaxWheelSlip ?? packet.WheelSlip ?? 0);
            var minRpm = Math.Max(0, profile.EngineVibration.MinRpm);
            var telemetryMinRpm = IsValidFinite(packet.MinRpm) ? packet.MinRpm!.Value : minRpm;
            var maxRpm = Math.Max(telemetryMinRpm + 1, IsValidFinite(packet.MaxRpm) ? packet.MaxRpm!.Value : profile.EngineVibration.MaxRpm);
            var rpmRatio = IsValidFinite(packet.Rpm01)
                ? Math.Clamp(packet.Rpm01!.Value, 0, 1)
                : packet.Rpm is null ? 0 : Math.Clamp((packet.Rpm.Value - telemetryMinRpm) / (maxRpm - telemetryMinRpm), 0, 1);
            var torqueLoad = IsValidFinite(packet.MotorTorque) && IsValidFinite(packet.MotorMaxTorque) && packet.MotorMaxTorque > 0
                ? Math.Abs(packet.MotorTorque!.Value) / Math.Max(1, Math.Abs(packet.MotorMaxTorque!.Value))
                : (double?)null;
            var throttleDemand = Math.Clamp(FirstValid(packet.TransmissionThrottle01, packet.Throttle) ?? 0, 0, 1);
            var brakeDemand = Math.Clamp(FirstValid(packet.TransmissionBrake01, packet.Brake) ?? 0, 0, 1);
            var brakeOnlyStandstill = speed <= 0 && brakeDemand > 0.05 && throttleDemand < 0.05;
            var rawLoadRatio = FirstValid(packet.EngineLoad01, torqueLoad, packet.TransmissionThrottle01, packet.Throttle) ?? 0;
            var loadRatio = Math.Clamp(brakeOnlyStandstill ? torqueLoad ?? 0 : rawLoadRatio, 0, 1);
            var heavyEngine = IsHeavyEngine(packet);
            var powertrainType = NormalizePowertrainType(packet.PowertrainType);
            var lugging = packet.EngineRunning == true && loadRatio >= 0.65 && rpmRatio <= (heavyEngine ? 0.36 : 0.30);
            var rollDeg = IsValidFinite(packet.RollDeg) ? packet.RollDeg!.Value : 0;
            var rollAbs = Math.Abs(rollDeg);
            var rollRatio = rollAbs <= profile.SideSlopeBias.MinRollDeg
                ? 0
                : Math.Clamp((rollAbs - profile.SideSlopeBias.MinRollDeg) / Math.Max(0.1, profile.SideSlopeBias.FullRollDeg - profile.SideSlopeBias.MinRollDeg), 0, 1);
            var downhillRollDirection = -Math.Sign(rollDeg);
            var accelerationRatio = NormalizeVector(packet.LocalAccelerationX, packet.LocalAccelerationY, packet.LocalAccelerationZ, profile.MotionFeedback.FullAcceleration);
            var ownMassT = IsValidFinite(packet.MassT) && packet.MassT > 0 ? packet.MassT!.Value : 0;
            var attachedMassT = packet.Attachments
                .Where(a => IsValidFinite(a.MassT))
                .Sum(a => Math.Max(0, a.MassT!.Value));
            var weightedOffsetMass = packet.Attachments
                .Where(a => IsValidFinite(a.MassT) && IsValidFinite(a.LateralOffsetM))
                .Sum(a => Math.Max(0, a.MassT!.Value) * a.LateralOffsetM!.Value);
            var weightedOffsetM = attachedMassT > 0 ? weightedOffsetMass / attachedMassT : 0;
            var attachedMassRatio = ownMassT > 0 ? Math.Clamp(attachedMassT / ownMassT, 0, 4) : CalculateLoadRatio(loadFactor);
            var implementLateralOffsetRatio = Math.Clamp(weightedOffsetM / Math.Max(0.1, profile.ImplementBias.FullLateralOffsetM), -1, 1);

            return new TelemetryFeatures(
                speed,
                Math.Clamp(speed / Math.Max(1, profile.SpeedSpring.SpeedReferenceKmh), 0, 1),
                packet.SteeringAngle ?? 0,
                packet.SteeringRate ?? 0,
                NormalizeAbs(packet.YawRateDegPerSec, profile.MotionFeedback.FullYawRateDegPerSec),
                Math.Clamp(slip, 0, 1),
                Math.Clamp(steeringContact ?? 1, 0, 1),
                contactConfidence,
                surfaceClass,
                packet.SurfaceType is not null ? 1.0 : packet.IsOnField is not null ? 0.7 : 0.0,
                CalculateFeatureWetness(packet, surfaceType),
                loadFactor,
                packet.MassKg is not null && packet.TotalMassKg is not null ? 1.0 : 0.0,
                NormalizeAbs(packet.CalculatedSlopeDeg, profile.MotionFeedback.FullPitchDeg),
                suspension,
                suspensionConfidence,
                verticalImpact,
                landing,
                collision,
                Math.Clamp(Math.Abs(longitudinalJerk), 0, 2),
                NormalizeImpulse(packet.LeftSuspensionImpulse),
                NormalizeImpulse(packet.RightSuspensionImpulse),
                packet.IsArticulated == true,
                rpmRatio,
                packet.TransmissionThrottle01 is not null || packet.TransmissionBrake01 is not null || packet.TransmissionClutch01 is not null || packet.Gear is not null ? 1.0 : 0.0,
                loadRatio,
                lugging,
                packet.EngineRunning == true && loadRatio >= 0.25,
                powertrainType,
                heavyEngine,
                tireProfile,
                multiplier,
                rollRatio,
                downhillRollDirection,
                accelerationRatio,
                attachedMassRatio,
                implementLateralOffsetRatio);
        }

        private static string ResolveTireProfile(TelemetryPacketV1 packet)
        {
            var profile = packet.Wheels
                .Select(w => w.TireProfile)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? packet.WheelTireProfile;
            return TireSurfaceTuningSettings.NormalizeTireProfile(profile);
        }

        private static double ResolveTireSurfaceMultiplier(TelemetryPacketV1 packet, TireSurfaceTuningSettings tuning, string tireProfile, string surfaceType)
        {
            if (tireProfile != "mixed")
            {
                return tuning.GetMultiplierPercent(tireProfile, surfaceType) / 100.0;
            }

            var wheelProfiles = packet.Wheels
                .Select(w => TireSurfaceTuningSettings.NormalizeTireProfile(w.TireProfile ?? w.TireType))
                .Where(profile => profile is not "unknown" and not "mixed")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (wheelProfiles.Length == 0)
            {
                return tuning.GetMultiplierPercent("mixed", surfaceType) / 100.0;
            }

            return wheelProfiles.Average(profile => tuning.GetMultiplierPercent(profile, surfaceType) / 100.0);
        }

        private static string NormalizePowertrainType(string? powertrainType)
        {
            return powertrainType?.Trim().ToLowerInvariant() switch
            {
                "combustion" => "combustion",
                "electric" => "electric",
                "hybrid" => "hybrid",
                _ => "unknown"
            };
        }

        private static bool IsHeavyEngine(TelemetryPacketV1 packet)
        {
            var motorType = packet.MotorType ?? "";
            if (motorType.Contains("heavy", StringComparison.OrdinalIgnoreCase) ||
                motorType.Contains("diesel", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return packet.VehicleCategory is VehicleCategoryFfbProfile.TractorTracked or VehicleCategoryFfbProfile.Harvester;
        }
    }

    private static double? CalculateFeatureWetness(TelemetryPacketV1 packet, string surfaceType)
    {
        return CalculateWetness(packet) ?? (surfaceType == "wetField" ? 0.6 : null);
    }
}
