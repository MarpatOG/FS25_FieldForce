using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Services;

public sealed class GameplayFfbCalculator
{
    private const double MovingSpeedThresholdKmh = 2.0;

    public GameplayFfbOutput Calculate(TelemetryReceiverState state, GameplayFfbSettings settings)
    {
        return Calculate(state, settings, DeviceHapticProfile.Resolve(settings.DeviceHapticProfileName));
    }

    public GameplayFfbOutput Calculate(TelemetryReceiverState state, GameplayFfbSettings settings, DeviceHapticProfile deviceProfile)
    {
        if (!settings.Enabled)
        {
            return GameplayFfbOutput.Zero;
        }

        var packet = state.LastPacket;
        var fade = CalculateTelemetryFade(state.LastPacketAge);
        if (fade <= 0 || packet is null || packet.IsPlayerInVehicle != true)
        {
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

        var steering = BaseSteeringModel.Calculate(features, profile, context).Value;
        var modifiers = SteeringModifierMixer.Combine(
            SurfaceTractionLayer.CalculateModifiers(features, profile, context),
            LoadSlopeImplementLayer.CalculateModifiers(features, profile, context),
            ContactTractionLayer.CalculateModifiers(features, profile, context));
        steering = SteeringModifierMixer.Apply(steering, modifiers);
        steering = SpeedStabilityLayer.Apply(steering, features, context).Value;
        steering = SafetyFilters.Apply(steering);

        var haptics = HapticMixer.CombineContinuous(
            SurfaceTractionLayer.CalculateContinuous(features, profile, context),
            EngineDrivetrainLayer.CalculateContinuous(packet, features, profile, context),
            SuspensionTerrainLayer.CalculateContinuous(features, profile, context));
        var pulses = HapticMixer.CombinePulses(
            SuspensionTerrainLayer.CalculatePulses(packet, features, profile, context),
            EngineDrivetrainLayer.CalculatePulses(packet, features, profile, context));

        var capped = DeviceCaps.Apply(steering, haptics, pulses, context.DeviceProfile);
        var bump = capped.Pulses.FirstOrDefault();

        var output = new GameplayFfbOutput(
            ClampPercent(capped.Steering.Spring),
            ClampPercent(capped.Steering.Damper),
            ClampPercent(capped.Steering.Friction),
            ClampPercent(capped.Haptics.EnginePercent),
            capped.Haptics.EnginePercent > 0 ? capped.Haptics.EngineHz : 0,
            ClampPercent(capped.Haptics.SurfacePercent),
            capped.Haptics.SurfacePercent > 0 ? capped.Haptics.SurfaceHz : 0,
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
            capped.Pulses.Count > 0);

        return output with
        {
            IsActive = output.SpringPercent > 0 ||
                       output.DamperPercent > 0 ||
                       output.FrictionPercent > 0 ||
                       output.EngineVibrationPercent > 0 ||
                       output.SurfaceVibrationPercent > 0 ||
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
            VehicleCategoryFfbProfile.HeavyTractorWheeled or
            VehicleCategoryFfbProfile.HeavyTractorTracked or
            VehicleCategoryFfbProfile.Harvester or
            VehicleCategoryFfbProfile.Truck or
            VehicleCategoryFfbProfile.LoaderTelehandler or
            VehicleCategoryFfbProfile.LightVehicle or
            VehicleCategoryFfbProfile.Unknown => value,
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

    private static bool IsFieldSurface(string surfaceType, bool? isOnField)
    {
        return surfaceType is "field" or "wetField" || (surfaceType == "unknown" && isOnField == true);
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
            var loadFactor = CalculateLoadFactor(packet.Mass, packet.TotalMass);
            var steeringContact = packet.SteeringGroundContactRatio ?? packet.GroundContactRatio;
            var contactConfidence = packet.SteeringGroundContactRatio is not null ? 1.0 : packet.GroundContactRatio is not null ? 0.55 : 0.0;
            var suspension = packet.SuspensionImpulse ?? packet.BumpImpulse;
            var suspensionConfidence = packet.SuspensionImpulse is not null ? 1.0 : packet.BumpImpulse is not null ? 0.55 : 0.0;
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
                IsFieldSurface(surfaceType, packet.IsOnField) ? surfaceType : "road",
                packet.SurfaceType is not null ? 1.0 : packet.IsOnField is not null ? 0.7 : 0.0,
                loadFactor,
                packet.Mass is not null && packet.TotalMass is not null ? 1.0 : 0.0,
                Math.Max(NormalizeAbs(packet.PitchDeg, profile.MotionFeedback.FullPitchDeg), NormalizeAbs(packet.SlopeDeg, profile.MotionFeedback.FullPitchDeg)),
                Math.Clamp(Math.Abs(suspension ?? 0), 0, 2),
                suspensionConfidence,
                Math.Clamp(Math.Abs(packet.LeftSuspensionImpulse ?? 0), 0, 2),
                Math.Clamp(Math.Abs(packet.RightSuspensionImpulse ?? 0), 0, 2),
                packet.Rpm is null ? 0 : Math.Clamp((packet.Rpm.Value - minRpm) / (maxRpm - minRpm), 0, 1),
                packet.Throttle is not null || packet.Brake is not null || packet.Clutch is not null || packet.Gear is not null ? 1.0 : 0.0);
        }
    }

    public static class BaseSteeringModel
    {
        public static LayerContribution<SteeringModel> Calculate(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var spring = CalculateSpeedEffect(profile.SpeedSpring, features.SpeedKmh, context.TelemetryFade);
            var yawAlign = 1 + (features.YawRateRatio * features.SpeedRatio * 0.08);
            var damper = CalculateSpeedEffect(profile.SpeedDamper, features.SpeedKmh, context.TelemetryFade);
            var friction = CalculateMechanicalFriction(profile.MechanicalFriction, CalculateLoadRatio(features.LoadFactor), features.SurfaceClass is "field" or "wetField", context.TelemetryFade);
            return new(new SteeringModel(spring * yawAlign, damper, friction), 1.0);
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

    public static class SurfaceTractionLayer
    {
        public static LayerContribution<SteeringModifiers> CalculateModifiers(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var modifiers = new SteeringModifiers(1, 1, 1, 0, 0);
            var confidence = features.SurfaceConfidence;
            if (features.SurfaceClass is not ("field" or "wetField") || !profile.SurfaceFeedback.Enabled || features.SpeedKmh < Math.Max(0, profile.SurfaceFeedback.MinSpeedKmh))
            {
                return new(modifiers, confidence);
            }

            modifiers = modifiers with
            {
                SpringGain = 1 + (profile.SurfaceFeedback.FieldSpringModifierPercent / 100.0),
                DamperGain = 1 + (profile.SurfaceFeedback.FieldDamperModifierPercent / 100.0),
                FrictionGain = 1 + (profile.SurfaceFeedback.FieldFrictionModifierPercent / 100.0)
            };

            var wetness = features.SurfaceClass == "wetField" ? 0.6 : 0;
            if (wetness > 0 && profile.WetnessFeedback.Enabled)
            {
                modifiers = modifiers with
                {
                    SpringGain = modifiers.SpringGain * 0.92,
                    DamperGain = modifiers.DamperGain * (1 + wetness * profile.WetnessFeedback.DamperModifierPercent / 100.0)
                };
            }

            return new(modifiers, confidence);
        }

        public static LayerContribution<ContinuousHaptics> CalculateContinuous(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var slip = CalculateSlipFeedback(features.Slip, features.SpeedKmh, profile.SlipFeedback, context.TelemetryFade, out var slipHz);
            if (features.SurfaceClass is not ("field" or "wetField") || !profile.SurfaceFeedback.Enabled || features.SpeedKmh < Math.Max(0, profile.SurfaceFeedback.MinSpeedKmh))
            {
                return new(new ContinuousHaptics(0, 0, slip, slipHz, 0, 0, 0, 0), Math.Max(features.SurfaceConfidence, slip > 0 ? 1.0 : 0.0));
            }

            var surface = CalculateMaxCapped(profile.SurfaceFeedback, context.TelemetryFade);
            if (features.SurfaceClass == "wetField" && profile.WetnessFeedback.Enabled)
            {
                surface *= 1 + (0.6 * profile.WetnessFeedback.SurfaceVibrationModifierPercent / 100.0);
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

    public static class LoadSlopeImplementLayer
    {
        public static LayerContribution<SteeringModifiers> CalculateModifiers(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            if (!profile.LoadResistance.Enabled)
            {
                return new(new SteeringModifiers(1, 1, 1, 0, 0), features.LoadConfidence);
            }

            var loadResistance = (profile.LoadResistance.StrengthPercent / 100.0) *
                                 (profile.LoadResistance.MaxOutputPercent / 100.0) *
                                 ApplyCurve(CalculateLoadRatio(features.LoadFactor), profile.LoadResistance.Curve);
            var slopeAssist = features.SlopeRatio * 0.12;
            return new(new SteeringModifiers(
                profile.LoadResistance.AffectsSpring ? 1 + (loadResistance * Math.Clamp(profile.LoadResistance.SpringScale, 0, 2)) + slopeAssist : 1,
                profile.LoadResistance.AffectsDamper ? 1 + (loadResistance * Math.Clamp(profile.LoadResistance.DamperScale, 0, 2)) + slopeAssist : 1,
                profile.LoadResistance.AffectsFriction ? 1 + (loadResistance * Math.Clamp(profile.LoadResistance.FrictionScale, 0, 2)) : 1,
                0,
                0), features.LoadConfidence);
        }
    }

    public static class ContactTractionLayer
    {
        public static LayerContribution<SteeringModifiers> CalculateModifiers(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var contactLoss = (1 - features.ContactRatio) * features.ContactConfidence;
            var slipRelief = Math.Clamp((features.Slip - profile.SlipFeedback.MinSlip) / Math.Max(0.01, profile.SlipFeedback.FullSlip - profile.SlipFeedback.MinSlip), 0, 1) * 0.30;
            return new(new SteeringModifiers(
                1,
                1,
                1,
                Math.Clamp((contactLoss * 0.65) + slipRelief, 0, 0.85),
                0), Math.Max(features.ContactConfidence, features.Slip > 0 ? 0.8 : 0));
        }
    }

    public static class SpeedStabilityLayer
    {
        public static LayerContribution<SteeringModel> Apply(SteeringModel steering, TelemetryFeatures features, FfbFrameContext context)
        {
            var speedDamping = features.SpeedRatio * 2.0;
            var rateDamping = Math.Clamp(Math.Abs(features.SteeringRate) / 2.0, 0, 1) * 8.0 * context.DeviceProfile.SteeringRateDamperScale;
            var antiOscillation = features.SpeedRatio > 0.45 && Math.Abs(features.SteeringAngle) < 0.04 ? 3.0 : 0.0;
            return new(steering with { Damper = steering.Damper + speedDamping + rateDamping + antiOscillation }, 1.0);
        }
    }

    public static class SteeringModifierMixer
    {
        public static SteeringModifiers Combine(params LayerContribution<SteeringModifiers>[] layers)
        {
            var result = new SteeringModifiers(1, 1, 1, 0, 0);
            foreach (var layer in layers)
            {
                var confidence = Math.Clamp(layer.Confidence, 0, 1);
                var value = layer.Value;
                result = new SteeringModifiers(
                    result.SpringGain * Lerp(1, value.SpringGain, confidence),
                    result.DamperGain * Lerp(1, value.DamperGain, confidence),
                    result.FrictionGain * Lerp(1, value.FrictionGain, confidence),
                    Math.Clamp(result.SpringRelief + (value.SpringRelief * confidence), 0, 0.95),
                    result.DamperAdditive + (value.DamperAdditive * confidence));
            }

            return result;
        }

        public static SteeringModel Apply(SteeringModel steering, SteeringModifiers modifiers)
        {
            return new SteeringModel(
                steering.Spring * Math.Max(0, modifiers.SpringGain) * (1 - Math.Clamp(modifiers.SpringRelief, 0, 0.95)),
                (steering.Damper * Math.Max(0, modifiers.DamperGain)) + modifiers.DamperAdditive,
                steering.Friction * Math.Max(0, modifiers.FrictionGain) * (1 - Math.Clamp(modifiers.SpringRelief * 0.5, 0, 0.7)));
        }
    }

    public static class SuspensionTerrainLayer
    {
        public static LayerContribution<ContinuousHaptics> CalculateContinuous(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var rumble = features.SuspensionImpulse > 0.08
                ? Math.Min(CalculateMaxCapped(profile.BumpFeedback, context.TelemetryFade) * 0.35 * features.SuspensionImpulse, 100)
                : 0;
            return new(new ContinuousHaptics(0, 0, 0, 0, 0, 0, rumble, rumble > 0 ? 10 : 0), features.SuspensionConfidence);
        }

        public static IReadOnlyList<EventPulse> CalculatePulses(TelemetryPacket packet, TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            if (!profile.BumpFeedback.Enabled)
            {
                return [];
            }

            var impulse = packet.SuspensionImpulse ?? packet.BumpImpulse;
            if (impulse is null)
            {
                return [];
            }

            var minImpulse = Math.Clamp(profile.BumpFeedback.MinImpulse, 0, 10);
            var fullImpulse = Math.Max(minImpulse + 0.01, profile.BumpFeedback.FullImpulse);
            var magnitude = Math.Abs(impulse.Value);
            if (magnitude <= minImpulse)
            {
                return [];
            }

            var ratio = Math.Clamp((magnitude - minImpulse) / (fullImpulse - minImpulse), 0, 1);
            var direction = Math.Sign(packet.LocalAccelerationX ?? packet.SteeringAngle ?? impulse.Value);
            if (direction == 0)
            {
                direction = 1;
            }

            return
            [
                new EventPulse(
                    SelectPulseKind(features),
                    CalculateMaxCapped(profile.BumpFeedback, context.TelemetryFade) * ApplyCurve(ratio, profile.BumpFeedback.Curve),
                    Math.Clamp(profile.BumpFeedback.DurationMs, 20, 250),
                    Math.Clamp(profile.BumpFeedback.CooldownMs, 20, 500),
                    direction,
                    features.SuspensionConfidence)
            ];
        }

        private static FfbPulseKind SelectPulseKind(TelemetryFeatures features)
        {
            if (features.LeftSuspensionImpulse > features.RightSuspensionImpulse * 1.25)
            {
                return FfbPulseKind.LeftSuspensionHit;
            }

            if (features.RightSuspensionImpulse > features.LeftSuspensionImpulse * 1.25)
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

        public static IReadOnlyList<EventPulse> CalculatePulses(TelemetryPacket packet, TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            if (features.DrivetrainConfidence < 1.0)
            {
                return [];
            }

            return [];
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
                result = result with
                {
                    SurfacePercent = Math.Max(result.SurfacePercent, value.SurfacePercent * confidence),
                    SurfaceHz = value.SurfacePercent > result.SurfacePercent ? value.SurfaceHz : result.SurfaceHz,
                    SlipPercent = Math.Max(result.SlipPercent, value.SlipPercent * confidence),
                    SlipHz = value.SlipPercent > result.SlipPercent ? value.SlipHz : result.SlipHz,
                    EnginePercent = Math.Max(result.EnginePercent, value.EnginePercent * confidence),
                    EngineHz = value.EnginePercent > result.EnginePercent ? value.EngineHz : result.EngineHz,
                    TerrainRumblePercent = Math.Max(result.TerrainRumblePercent, value.TerrainRumblePercent * confidence),
                    TerrainRumbleHz = value.TerrainRumblePercent > result.TerrainRumblePercent ? value.TerrainRumbleHz : result.TerrainRumbleHz
                };
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

    private static double Lerp(double from, double to, double ratio)
    {
        return from + ((to - from) * Math.Clamp(ratio, 0, 1));
    }
}
