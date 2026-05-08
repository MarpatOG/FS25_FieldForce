using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Services;

public sealed class GameplayFfbCalculator
{
    private const double MovingSpeedThresholdKmh = 2.0;
    private DrivetrainSample? _lastDrivetrainSample;
    private SteeringModel? _lastSteeringModel;

    public GameplayFfbOutput Calculate(TelemetryReceiverState state, GameplayFfbSettings settings)
    {
        return Calculate(state, settings, DeviceHapticProfile.Resolve(settings.DeviceHapticProfileName));
    }

    public GameplayFfbOutput Calculate(TelemetryReceiverState state, GameplayFfbSettings settings, DeviceHapticProfile deviceProfile)
    {
        if (!settings.Enabled)
        {
            _lastDrivetrainSample = null;
            _lastSteeringModel = null;
            return GameplayFfbOutput.Zero;
        }

        var packet = state.LastPacket;
        var fade = CalculateTelemetryFade(state.LastPacketAge);
        if (fade <= 0 || packet is null || packet.IsPlayerInVehicle != true)
        {
            _lastDrivetrainSample = null;
            _lastSteeringModel = null;
            return GameplayFfbOutput.Zero;
        }

        var activeCategory = NormalizeVehicleCategory(packet.VehicleCategory);
        var profile = ResolveVehicleCategoryProfile(settings, activeCategory);
        var context = new FfbFrameContext(
            TimeSpan.FromSeconds(1.0 / 125.0),
            state.LastPacketAge,
            fade,
            activeCategory,
            deviceProfile);
        var features = TelemetryFeatureExtractor.Extract(packet, profile);

        var loadResistance = LoadResistanceLayer.Calculate(features, profile, context);
        var motionFeedback = MotionFeedbackLayer.Calculate(features, profile, context);
        var contactRelief = ContactReliefLayer.Calculate(features, profile, context);
        var steering = SteeringContributionMixer.Combine(
            SpeedSpringLayer.Calculate(features, profile, context),
            SpeedDamperLayer.Calculate(features, profile, context),
            MechanicalFrictionLayer.Calculate(features, profile, context),
            SurfaceSteeringLayer.Calculate(features, profile, context),
            loadResistance,
            motionFeedback,
            contactRelief,
            SpeedStabilityLayer.Calculate(features, profile, context));
        steering = SafetyFilters.Apply(steering);
        steering = SmoothSteering(steering, context);

        var haptics = HapticMixer.CombineContinuous(
            SurfaceTractionLayer.CalculateContinuous(features, profile, context),
            EngineDrivetrainLayer.CalculateContinuous(packet, features, profile, context),
            SuspensionTerrainLayer.CalculateContinuous(features, profile, context));
        var pulses = HapticMixer.CombinePulses(
            SuspensionTerrainLayer.CalculatePulses(packet, features, profile, context),
            CalculateDrivetrainPulses(packet, features, profile, context));

        var capped = DeviceCaps.Apply(steering, haptics, pulses, context.DeviceProfile);
        var bump = SelectFramePulse(capped.Pulses);

        var output = new GameplayFfbOutput(
            ClampPercent(capped.Steering.Spring),
            ClampPercent(capped.Steering.Damper),
            ClampPercent(capped.Steering.Friction),
            ClampPercent(capped.Haptics.EnginePercent),
            capped.Haptics.EnginePercent > 0 ? capped.Haptics.EngineHz : 0,
            ClampPercent(capped.Haptics.SurfacePercent),
            capped.Haptics.SurfacePercent > 0 ? capped.Haptics.SurfaceHz : 0,
            ClampPercent(capped.Haptics.TerrainRumblePercent),
            capped.Haptics.TerrainRumblePercent > 0 ? capped.Haptics.TerrainRumbleHz : 0,
            ClampPercent(capped.Haptics.SlipPercent),
            capped.Haptics.SlipPercent > 0 ? capped.Haptics.SlipHz : 0,
            bump is null ? 0 : ClampSignedPercent(bump.Percent * Math.Sign(bump.Direction == 0 ? bump.Percent : bump.Direction)),
            bump is null ? 0 : Math.Clamp(bump.DurationMs, 20, 250),
            bump is null ? 0 : Math.Clamp(bump.CooldownMs, 20, 500),
            features.LoadFactor,
            fade,
            true,
            activeCategory,
            capped.Haptics.TerrainRumblePercent > 0,
            bump is not null,
            bump?.Kind ?? FfbPulseKind.None,
            HasActiveSteeringContribution(loadResistance.Value, loadResistance.Confidence),
            HasActiveSteeringContribution(motionFeedback.Value, motionFeedback.Confidence),
            CalculateContactReliefActive(features),
            CalculateAntiOscillationActive(features, profile),
            CalculateWetnessEffect(profile.WetnessFeedback, features.Wetness, fade) > 0,
            CalculateSteeringSlipReliefActive(features, profile));

        return output with
        {
            IsActive = output.SpringPercent > 0 ||
                       output.DamperPercent > 0 ||
                       output.FrictionPercent > 0 ||
                       output.EngineVibrationPercent > 0 ||
                       output.SurfaceVibrationPercent > 0 ||
                       output.TerrainRumblePercent > 0 ||
                       output.SlipVibrationPercent > 0 ||
                       output.BumpImpulsePercent != 0 ||
                       output.TerrainRumbleActive
        };
    }

    private static GameplayFfbEffectProfile ResolveVehicleCategoryProfile(GameplayFfbSettings settings, string activeCategory)
    {
        if (settings.VehicleCategoryEffectProfiles.TryGetValue(activeCategory, out var profile) && profile is not null)
        {
            return profile;
        }

        if (settings.VehicleCategoryEffectProfiles.TryGetValue(VehicleCategoryFfbProfile.Unknown, out var unknownProfile) && unknownProfile is not null)
        {
            return unknownProfile;
        }

        return settings;
    }

    private static string NormalizeVehicleCategory(string? vehicleCategory)
    {
        if (string.IsNullOrWhiteSpace(vehicleCategory))
        {
            return VehicleCategoryFfbProfile.Unknown;
        }

        var value = vehicleCategory.Trim();
        return value switch
        {
            VehicleCategoryFfbProfile.TractorWheeled or
            VehicleCategoryFfbProfile.TractorTracked or
            VehicleCategoryFfbProfile.Harvester or
            VehicleCategoryFfbProfile.Truck or
            VehicleCategoryFfbProfile.LoaderTelehandler or
            VehicleCategoryFfbProfile.LightVehicle or
            VehicleCategoryFfbProfile.Unknown => value,
            VehicleCategoryFfbProfile.HeavyTractorWheeled => VehicleCategoryFfbProfile.TractorWheeled,
            VehicleCategoryFfbProfile.HeavyTractorTracked => VehicleCategoryFfbProfile.TractorTracked,
            _ => VehicleCategoryFfbProfile.Unknown
        };
    }

    public static double CalculateTelemetryFade(TimeSpan? lastPacketAge)
    {
        if (lastPacketAge is null)
        {
            return 0;
        }

        var ms = Math.Max(0, lastPacketAge.Value.TotalMilliseconds);
        if (ms <= 300)
        {
            return 1 - (ms / 300 * 0.05);
        }

        if (ms <= 1000)
        {
            return 0.95 * (1 - ((ms - 300) / 700));
        }

        return 0;
    }

    private static bool HasActiveSteeringContribution(SteeringContribution contribution, double confidence)
    {
        const double epsilon = 0.0001;
        return confidence > 0 &&
               (Math.Abs(contribution.SpringAdd) > epsilon ||
                Math.Abs(contribution.DamperAdd) > epsilon ||
                Math.Abs(contribution.FrictionAdd) > epsilon ||
                Math.Abs(contribution.SpringRelief) > epsilon ||
                Math.Abs(contribution.FrictionRelief) > epsilon);
    }

    private static bool CalculateContactReliefActive(TelemetryFeatures features)
    {
        return features.ContactConfidence > 0 && (1 - features.ContactRatio) * features.ContactConfidence > 0.0001;
    }

    private static bool CalculateAntiOscillationActive(TelemetryFeatures features, GameplayFfbEffectProfile profile)
    {
        return profile.SpeedDamper.Enabled && CalculateSteeringLoadSpeedScale(features.SpeedKmh) > 0.45 && Math.Abs(features.SteeringAngle) < 0.04;
    }

    private static bool CalculateSteeringSlipReliefActive(TelemetryFeatures features, GameplayFfbEffectProfile profile)
    {
        return profile.SlipFeedback.Enabled &&
               features.Slip > Math.Clamp(profile.SlipFeedback.MinSlip, 0, 1);
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

    internal static double CalculateMaxCapped(FfbEffectSettings settings, double fade)
    {
        return Math.Clamp(settings.StrengthPercent, 0, 100) *
               (Math.Clamp(settings.MaxOutputPercent, 0, 100) / 100.0) *
               Math.Clamp(fade, 0, 1);
    }

    internal static double ApplyCurve(double ratio, FfbCurveKind curve)
    {
        ratio = Math.Clamp(ratio, 0, 1);
        return curve switch
        {
            FfbCurveKind.Linear => ratio,
            FfbCurveKind.Aggressive => Math.Pow(ratio, 0.65),
            _ => ratio * ratio * (3 - (2 * ratio))
        };
    }

    internal static int Quantize(int value, int step)
    {
        return Math.Max(step, (int)Math.Round(value / (double)step) * step);
    }

    private static EventPulse? SelectFramePulse(IReadOnlyList<EventPulse> pulses)
    {
        return pulses
            .Where(p => p.Confidence > 0 && Math.Abs(p.Percent) > 0)
            .Where(p => p.Kind != FfbPulseKind.Collision || Math.Abs(p.Percent) >= 18)
            .OrderBy(p => PulsePriority(p.Kind))
            .ThenByDescending(p => Math.Abs(p.Percent))
            .FirstOrDefault();
    }

    private static int PulsePriority(FfbPulseKind kind)
    {
        return kind switch
        {
            FfbPulseKind.Collision => 0,
            FfbPulseKind.Landing => 1,
            FfbPulseKind.LeftSuspensionHit or FfbPulseKind.RightSuspensionHit => 2,
            FfbPulseKind.Bump => 3,
            FfbPulseKind.GearShift => 4,
            FfbPulseKind.DrivetrainJerk => 5,
            FfbPulseKind.EngineStartStop => 5,
            _ => 99
        };
    }

    private static int ClampPercent(double value)
    {
        return Math.Clamp((int)Math.Round(value), 0, 100);
    }

    private static int ClampSignedPercent(double value)
    {
        return Math.Clamp((int)Math.Round(value), -100, 100);
    }

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

    public static double CalculateSteeringLoadSpeedScale(double speedKmh)
    {
        const double fullEffectSpeedKmh = 10.0;
        const double cappedSpeedKmh = 40.0;
        return Math.Clamp(Math.Min(Math.Max(0, speedKmh), cappedSpeedKmh) / fullEffectSpeedKmh, 0, 1);
    }

    private static string NormalizeSurfaceType(TelemetryPacket packet)
    {
        var value = packet.SurfaceType?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return packet.IsOnField == true ? "field" : "unknown";
        }

        return value.Equals("wetField", StringComparison.OrdinalIgnoreCase)
            ? "wetField"
            : value.ToLowerInvariant();
    }

    private static bool IsOffRoadSurface(string surfaceType, bool? isOnField)
    {
        return surfaceType is "field" or "wetField" or "grass" or "dirt" or "gravel" or "mud" or "snow" or "shallowwater" ||
               (surfaceType == "unknown" && isOnField == true);
    }

    private static bool IsOffRoadSurface(TelemetryFeatures features)
    {
        return features.SurfaceClass is "field" or "wetField" or "grass" or "dirt" or "gravel" or "mud" or "snow" or "shallowwater";
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

    private static double? CalculateWetness(TelemetryPacket packet)
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

    public static class TelemetryFeatureExtractor
    {
        public static TelemetryFeatures Extract(TelemetryPacket packet, GameplayFfbEffectProfile profile)
        {
            var rawSpeed = Math.Max(0, packet.SpeedKmh ?? 0);
            var speed = rawSpeed < MovingSpeedThresholdKmh ? 0 : rawSpeed;
            var surfaceType = NormalizeSurfaceType(packet);
            var surfaceClass = IsOffRoadSurface(surfaceType, packet.IsOnField)
                ? (surfaceType == "unknown" ? "field" : surfaceType)
                : "road";
            var loadFactor = CalculateLoadFactor(packet.Mass, packet.TotalMass);
            var steeringContact = packet.SteeringGroundContactRatio ?? packet.GroundContactRatio;
            var contactConfidence = packet.SteeringGroundContactRatio is not null ? 1.0 : packet.GroundContactRatio is not null ? 0.55 : 0.0;
            var suspension = packet.SuspensionImpulse ?? packet.BumpImpulse;
            var suspensionConfidence = packet.SuspensionImpulse is not null ? 1.0 : packet.BumpImpulse is not null ? 0.55 : 0.0;
            var verticalImpact = packet.VerticalImpactImpulse ?? packet.SuspensionImpulse ?? packet.BumpImpulse;
            var landing = packet.LandingImpulse ?? 0;
            var collision = packet.CollisionImpulse ?? 0;
            var longitudinalJerk = packet.LongitudinalJerkImpulse ??
                                    Math.Clamp(Math.Abs(packet.LocalAccelerationZ ?? packet.LocalAccelerationX ?? 0) / 9.81, 0, 2);
            var slip = Math.Max(packet.SteeringWheelSlip ?? 0, packet.MaxWheelSlip ?? packet.WheelSlip ?? 0);
            var minRpm = Math.Max(0, profile.EngineVibration.MinRpm);
            var maxRpm = Math.Max(minRpm + 1, profile.EngineVibration.MaxRpm);

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
                packet.Mass is not null && packet.TotalMass is not null ? 1.0 : 0.0,
                Math.Max(NormalizeAbs(packet.PitchDeg, profile.MotionFeedback.FullPitchDeg), NormalizeAbs(packet.SlopeDeg, profile.MotionFeedback.FullPitchDeg)),
                Math.Clamp(Math.Abs(suspension ?? 0), 0, 2),
                suspensionConfidence,
                Math.Clamp(Math.Abs(verticalImpact ?? 0), 0, 2),
                Math.Clamp(Math.Abs(landing), 0, 2),
                Math.Clamp(Math.Abs(collision), 0, 2),
                Math.Clamp(Math.Abs(longitudinalJerk), 0, 2),
                Math.Clamp(Math.Abs(packet.LeftSuspensionImpulse ?? 0), 0, 2),
                Math.Clamp(Math.Abs(packet.RightSuspensionImpulse ?? 0), 0, 2),
                packet.Rpm is null ? 0 : Math.Clamp((packet.Rpm.Value - minRpm) / (maxRpm - minRpm), 0, 1),
                packet.Throttle is not null || packet.Brake is not null || packet.Clutch is not null || packet.Gear is not null ? 1.0 : 0.0);
        }
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

    public static class SurfaceTractionLayer
    {
        public static LayerContribution<ContinuousHaptics> CalculateContinuous(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var slip = CalculateSlipFeedback(features.Slip, features.SpeedKmh, profile.SlipFeedback, context.TelemetryFade, out var slipHz);
            if (features.SurfaceClass is not ("field" or "wetField") || !profile.SurfaceFeedback.Enabled || features.SpeedKmh < Math.Max(0, profile.SurfaceFeedback.MinSpeedKmh))
            {
                return new(new ContinuousHaptics(0, 0, slip, slipHz, 0, 0, 0, 0), Math.Max(features.SurfaceConfidence, slip > 0 ? 1.0 : 0.0));
            }

            var surface = CalculateMaxCapped(profile.SurfaceFeedback, context.TelemetryFade);
            var wetnessEffect = CalculateWetnessEffect(profile.WetnessFeedback, features.Wetness, context.TelemetryFade);
            if (wetnessEffect > 0)
            {
                surface *= 1 + (wetnessEffect * profile.WetnessFeedback.SurfaceVibrationModifierPercent / 100.0);
            }

            var hz = CalculateSurfaceFrequency(profile.SurfaceFeedback, features.SpeedKmh, profile.SpeedDamper.SpeedReferenceKmh);
            return new(new ContinuousHaptics(surface, hz, slip, slipHz, 0, 0, 0, 0), features.SurfaceConfidence);
        }

        private static double CalculateSlipFeedback(double slip, double speedKmh, SlipFeedbackSettings settings, double fade, out int hz)
        {
            hz = 0;
            if (!settings.Enabled || speedKmh < Math.Max(0, settings.MinSpeedKmh))
            {
                return 0;
            }

            var minSlip = Math.Clamp(settings.MinSlip, 0, 1);
            var fullSlip = Math.Max(minSlip + 0.01, settings.FullSlip);
            if (slip <= minSlip)
            {
                return 0;
            }

            var ratio = Math.Clamp((slip - minSlip) / (fullSlip - minSlip), 0, 1);
            var curve = ApplyCurve(ratio, settings.Curve);
            var minHz = Math.Clamp(settings.MinFrequencyHz, 4, 60);
            var maxHz = Math.Clamp(settings.MaxFrequencyHz, minHz, 60);
            hz = Quantize((int)Math.Round(minHz + ((maxHz - minHz) * curve)), 2);
            return CalculateMaxCapped(settings, fade) * curve;
        }

        private static int CalculateSurfaceFrequency(SurfaceFeedbackSettings settings, double speedKmh, double speedReferenceKmh)
        {
            var minHz = Math.Clamp(settings.FieldFrequencyMinHz, 4, 45);
            var maxHz = Math.Clamp(settings.FieldFrequencyMaxHz, minHz, 45);
            var ratio = Math.Clamp(speedKmh / Math.Max(1, speedReferenceKmh), 0, 1);
            return Quantize((int)Math.Round(minHz + ((maxHz - minHz) * ApplyCurve(ratio, settings.Curve))), 2);
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

    private static SteeringContribution Zero(string source)
    {
        return new SteeringContribution(source, 0, 0, 0, 0, 0, 1);
    }

    public static class SuspensionTerrainLayer
    {
        public static LayerContribution<ContinuousHaptics> CalculateContinuous(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var settings = profile.TerrainRumble;
            if (!settings.Enabled)
            {
                return new(new ContinuousHaptics(0, 0, 0, 0, 0, 0, 0, 0), features.SuspensionConfidence);
            }

            var offRoad = IsOffRoadSurface(features);
            var minImpulse = offRoad
                ? Math.Clamp(settings.MinImpulse, 0, 10)
                : Math.Max(Math.Clamp(settings.MinImpulse, 0, 10), 0.42);
            var fullImpulse = Math.Max(minImpulse + 0.01, offRoad ? settings.FullImpulse : Math.Max(settings.FullImpulse, 1.15));
            var impulse = Math.Min(features.SuspensionImpulse, fullImpulse);
            if (impulse <= minImpulse)
            {
                return new(new ContinuousHaptics(0, 0, 0, 0, 0, 0, 0, 0), features.SuspensionConfidence);
            }

            var ratio = Math.Clamp((impulse - minImpulse) / (fullImpulse - minImpulse), 0, 1);
            var curve = ApplyCurve(ratio, settings.Curve);
            var minHz = Math.Clamp(settings.MinFrequencyHz, 4, 60);
            var maxHz = Math.Clamp(settings.MaxFrequencyHz, minHz, 60);
            var hz = Quantize((int)Math.Round(minHz + ((maxHz - minHz) * curve)), 2);
            var surfaceScale = offRoad ? 1.10 : 0.14;
            var rumble = CalculateMaxCapped(settings, context.TelemetryFade) * curve * surfaceScale * CalculateHapticLoadScale(features);
            return new(new ContinuousHaptics(0, 0, 0, 0, 0, 0, rumble, hz), features.SuspensionConfidence);
        }

        public static IReadOnlyList<EventPulse> CalculatePulses(TelemetryPacket packet, TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var pulses = new List<EventPulse>();
            if (ShouldAllowCollisionPulse(features))
            {
                TryAddImpulsePulse(
                    pulses,
                    FfbPulseKind.Collision,
                    features.CollisionImpulse,
                    profile.CollisionFeedback,
                    context,
                    DirectionFromHorizontalImpact(packet),
                    CalculateCollisionSurfaceScale(features),
                    CalculateCollisionMinImpulse(features, profile.CollisionFeedback));
            }

            TryAddImpulsePulse(pulses, FfbPulseKind.Landing, features.LandingImpulse, profile.LandingFeedback, context, 1);

            var sideKind = SelectSidePulseKind(features);
            if (sideKind is not FfbPulseKind.Bump)
            {
                var sideImpulse = Math.Max(features.LeftSuspensionImpulse, features.RightSuspensionImpulse);
                TryAddImpulsePulse(pulses, sideKind, sideImpulse, profile.SuspensionHitFeedback, context, sideKind == FfbPulseKind.LeftSuspensionHit ? -1 : 1, CalculatePulseSurfaceScale(features), CalculatePulseMinImpulse(features, profile.SuspensionHitFeedback));
            }

            if (ShouldAllowRoadBump(features))
            {
                var direction = Math.Sign(packet.LocalAccelerationX ?? packet.SteeringAngle ?? 1);
                TryAddImpulsePulse(pulses, FfbPulseKind.Bump, features.VerticalImpactImpulse, profile.BumpFeedback, context, direction == 0 ? 1 : direction, CalculatePulseSurfaceScale(features), CalculatePulseMinImpulse(features, profile.BumpFeedback));
            }

            return pulses;
        }

        private static void TryAddImpulsePulse(
            List<EventPulse> pulses,
            FfbPulseKind kind,
            double impulse,
            ImpulsePulseFeedbackSettings settings,
            FfbFrameContext context,
            double direction,
            double outputScale = 1,
            double? minImpulseOverride = null)
        {
            if (!settings.Enabled)
            {
                return;
            }

            var minImpulse = Math.Clamp(minImpulseOverride ?? settings.MinImpulse, 0, 10);
            var fullImpulse = Math.Max(minImpulse + 0.01, settings.FullImpulse);
            var magnitude = Math.Abs(impulse);
            if (magnitude <= minImpulse)
            {
                return;
            }

            var ratio = Math.Clamp((magnitude - minImpulse) / (fullImpulse - minImpulse), 0, 1);
            if (direction == 0)
            {
                direction = 1;
            }

            pulses.Add(new EventPulse(
                kind,
                CalculateMaxCapped(settings, context.TelemetryFade) * ApplyCurve(ratio, settings.Curve) * Math.Max(0, outputScale),
                Math.Clamp(settings.DurationMs, 20, 250),
                Math.Clamp(settings.CooldownMs, 20, 500),
                direction,
                1.0));
        }

        private static double CalculatePulseSurfaceScale(TelemetryFeatures features)
        {
            return (IsOffRoadSurface(features) ? 1.05 : 0.18) * CalculateHapticLoadScale(features);
        }

        private static double CalculatePulseMinImpulse(TelemetryFeatures features, ImpulsePulseFeedbackSettings settings)
        {
            return IsOffRoadSurface(features)
                ? Math.Clamp(settings.MinImpulse, 0, 10)
                : Math.Max(Math.Clamp(settings.MinImpulse, 0, 10), 0.58);
        }

        private static double CalculateCollisionSurfaceScale(TelemetryFeatures features)
        {
            return IsOffRoadSurface(features) ? 0.55 : 0.32;
        }

        private static double CalculateCollisionMinImpulse(TelemetryFeatures features, ImpulsePulseFeedbackSettings settings)
        {
            return IsOffRoadSurface(features)
                ? Math.Max(Math.Clamp(settings.MinImpulse, 0, 10), 1.25)
                : Math.Max(Math.Clamp(settings.MinImpulse, 0, 10), 1.75);
        }

        private static bool ShouldAllowCollisionPulse(TelemetryFeatures features)
        {
            if (features.CollisionImpulse <= 0)
            {
                return false;
            }

            var minCollision = IsOffRoadSurface(features) ? 1.25 : 1.75;

            if (features.CollisionImpulse < minCollision)
            {
                return false;
            }

            if (features.VerticalImpactImpulse > features.CollisionImpulse * 0.75)
            {
                return false;
            }

            if (!IsOffRoadSurface(features) &&
                features.ContactConfidence > 0 &&
                features.ContactRatio > 0.75 &&
                features.CollisionImpulse < 2.0)
            {
                return false;
            }

            return true;
        }

        private static bool ShouldAllowRoadBump(TelemetryFeatures features)
        {
            if (features.CollisionImpulse > 0 || features.LandingImpulse > 0)
            {
                return false;
            }

            if (features.ContactConfidence > 0 && features.ContactRatio < 0.15)
            {
                return false;
            }

            var sideConfirmation = Math.Max(features.LeftSuspensionImpulse, features.RightSuspensionImpulse) >= 0.10;
            if (features.LongitudinalJerkImpulse >= 0.35 && !sideConfirmation)
            {
                return false;
            }

            return true;
        }

        private static double DirectionFromHorizontalImpact(TelemetryPacket packet)
        {
            var accel = packet.LocalAccelerationZ ?? packet.LocalAccelerationX ?? 1;
            var direction = Math.Sign(accel);
            return direction == 0 ? 1 : direction;
        }

        private static FfbPulseKind SelectSidePulseKind(TelemetryFeatures features)
        {
            var minSideImpulse = IsOffRoadSurface(features) ? 0.26 : 0.58;
            var dominance = IsOffRoadSurface(features) ? 1.45 : 1.80;
            if (features.LeftSuspensionImpulse >= minSideImpulse &&
                features.LeftSuspensionImpulse > features.RightSuspensionImpulse * dominance)
            {
                return FfbPulseKind.LeftSuspensionHit;
            }

            if (features.RightSuspensionImpulse >= minSideImpulse &&
                features.RightSuspensionImpulse > features.LeftSuspensionImpulse * dominance)
            {
                return FfbPulseKind.RightSuspensionHit;
            }

            return FfbPulseKind.Bump;
        }
    }

    public static class EngineDrivetrainLayer
    {
        public static LayerContribution<ContinuousHaptics> CalculateContinuous(TelemetryPacket packet, TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var hz = 0;
            if (!profile.EngineVibration.Enabled || packet.EngineStarted != true || packet.Rpm is null || features.RpmRatio <= 0)
            {
                return new(new ContinuousHaptics(0, 0, 0, 0, 0, 0, 0, 0), packet.Rpm is null ? 0 : 1);
            }

            hz = Quantize((int)Math.Round(profile.EngineVibration.MinFrequencyHz + ((profile.EngineVibration.MaxFrequencyHz - profile.EngineVibration.MinFrequencyHz) * features.RpmRatio)), 2);
            var percent = CalculateMaxCapped(profile.EngineVibration, context.TelemetryFade) * Math.Clamp(0.35 + (0.65 * ApplyCurve(features.RpmRatio, profile.EngineVibration.Curve)), 0, 1);
            return new(new ContinuousHaptics(0, 0, 0, 0, percent, hz, 0, 0), 1.0);
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
                EnginePercent = Math.Min(haptics.EnginePercent, profile.EngineVibrationCapPercent),
                SurfacePercent = Math.Min(haptics.SurfacePercent, profile.SurfaceHapticCapPercent),
                SlipPercent = Math.Min(haptics.SlipPercent, profile.SlipHapticCapPercent),
                TerrainRumblePercent = Math.Min(haptics.TerrainRumblePercent, profile.TerrainRumbleCapPercent)
            };
            var cappedPulses = pulses
                .Select(p => p with
                {
                    Percent = Math.Min(Math.Abs(p.Percent), profile.BumpPulseCapPercent),
                    DurationMs = Math.Min(p.DurationMs, profile.MaxBumpDurationMs)
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

    private IReadOnlyList<EventPulse> CalculateDrivetrainPulses(TelemetryPacket packet, TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
    {
        if (!profile.DrivetrainPulse.Enabled || features.DrivetrainConfidence < 1.0)
        {
            _lastDrivetrainSample = null;
            return [];
        }

        var current = new DrivetrainSample(packet.VehicleName, packet.EngineStarted, packet.Throttle, packet.Brake, packet.Clutch, packet.Gear);
        var previous = _lastDrivetrainSample;
        _lastDrivetrainSample = current;
        if (previous is null || !string.Equals(previous.VehicleName, current.VehicleName, StringComparison.Ordinal))
        {
            return [];
        }

        var pulseRatio = 0.0;
        var kind = FfbPulseKind.DrivetrainJerk;
        if (previous.EngineStarted != current.EngineStarted)
        {
            pulseRatio = 0.55;
            kind = FfbPulseKind.EngineStartStop;
        }

        if (previous.Gear is not null && current.Gear is not null && previous.Gear != current.Gear)
        {
            pulseRatio = Math.Max(pulseRatio, 0.70);
            kind = FfbPulseKind.GearShift;
        }

        var throttleDelta = Math.Abs((current.Throttle ?? 0) - (previous.Throttle ?? 0));
        var brakeDelta = Math.Abs((current.Brake ?? 0) - (previous.Brake ?? 0));
        var pedalDelta = Math.Max(throttleDelta, brakeDelta);
        if (pedalDelta >= 0.35)
        {
            pulseRatio = Math.Max(pulseRatio, Math.Clamp((pedalDelta - 0.35) / 0.65, 0.25, 1.0));
            kind = FfbPulseKind.DrivetrainJerk;
        }

        if (features.LongitudinalJerkImpulse >= 0.35 &&
            features.VerticalImpactImpulse < profile.BumpFeedback.MinImpulse &&
            features.CollisionImpulse < profile.CollisionFeedback.MinImpulse)
        {
            pulseRatio = Math.Max(pulseRatio, Math.Clamp((features.LongitudinalJerkImpulse - 0.35) / 0.85, 0.25, 1.0));
            kind = FfbPulseKind.DrivetrainJerk;
        }

        if (pulseRatio <= 0)
        {
            return [];
        }

        var direction = (current.Brake ?? 0) > (previous.Brake ?? 0) ? -1 : 1;
        return
        [
            new EventPulse(
                kind,
                CalculateMaxCapped(profile.DrivetrainPulse, context.TelemetryFade) * ApplyCurve(pulseRatio, profile.DrivetrainPulse.Curve),
                Math.Clamp(profile.DrivetrainPulse.DurationMs, 20, 160),
                Math.Clamp(profile.DrivetrainPulse.CooldownMs, 20, 500),
                direction,
                1.0)
        ];
    }

    private static double? CalculateFeatureWetness(TelemetryPacket packet, string surfaceType)
    {
        return CalculateWetness(packet) ?? (surfaceType == "wetField" ? 0.6 : null);
    }

    private static double CalculateWetnessEffect(WetnessFeedbackSettings settings, double? wetness, double fade)
    {
        if (!settings.Enabled || wetness is null)
        {
            return 0;
        }

        var minWetness = Math.Clamp(settings.MinWetness, 0, 1);
        var value = Math.Clamp(wetness.Value, 0, 1);
        if (value <= minWetness)
        {
            return 0;
        }

        var ratio = Math.Clamp((value - minWetness) / Math.Max(0.01, 1 - minWetness), 0, 1);
        return Math.Clamp(CalculateMaxCapped(settings, fade) / 100.0, 0, 1) * ApplyCurve(ratio, settings.Curve);
    }

    private sealed record DrivetrainSample(string? VehicleName, bool? EngineStarted, double? Throttle, double? Brake, double? Clutch, int? Gear);
}
