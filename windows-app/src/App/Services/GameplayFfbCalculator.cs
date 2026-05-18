using FieldForce.App.Models;

namespace FieldForce.App.Services;

public sealed partial class GameplayFfbCalculator
{
    private const double MovingSpeedThresholdKmh = 2.0;

    private const double ImpulseNoiseFloor = 0.04;

    private const double MinPulsePercentToEmit = 2.5;

    private const double MinEventPercentForSuppression = 5.0;

    private const double ImpactPulseGain = 3.0;

    private const double EngineStartRpmZeroThresholdMs = 1000;

    private const double EngineStartRpmTriggerThreshold = 10;

    private const double ElectricContinuousEngineScale = 0.0;

    private const double ElectricStartStopScale = 0.0;

    private const double ElectricGearShiftScale = 0.20;

    private const double ElectricDrivetrainJerkScale = 0.0;

    private const double HybridContinuousEngineScale = 0.45;

    private const double HybridStartStopScale = 0.45;

    private const double HybridGearShiftScale = 0.55;

    private const double HybridDrivetrainJerkScale = 0.55;

    private DrivetrainSample? _lastDrivetrainSample;

    private long? _lastEngineStartSeq;

    private long? _lastEngineStopSeq;

    private long? _lastGearChangeSeq;

    private SteeringModel? _lastSteeringModel;
    private bool _lastSlewLimited;

    private EngineStartStopVibrationState? _engineStartStopVibration;

    private string? _rpmStartVehicleName;

    private double _rpmStartZeroMs;

    private bool _rpmStartArmed;

    private bool _suppressRpmStartUntilEngineOff;

    private DateTimeOffset? _lastCalculateAt;

    public GameplayFfbOutput Calculate(TelemetryReceiverState state, GameplayFfbSettings settings)
    {
        return Calculate(state, settings, WheelProfileCatalog.ResolveById(settings.WheelProfileId).Haptics);
    }

    public GameplayFfbOutput Calculate(TelemetryReceiverState state, GameplayFfbSettings settings, DeviceHapticProfile deviceProfile)
    {
        if (!settings.Enabled)
        {
            _lastDrivetrainSample = null;
            ResetEngineEventState();
            _lastSteeringModel = null;
            _lastCalculateAt = null;
            return GameplayFfbOutput.Zero;
        }

        var packet = state.LastPacket;
        var fade = CalculateTelemetryFade(state.LastPacketAge);
        if (fade <= 0 || packet is null || !packet.IsPlayerInVehicle)
        {
            _lastDrivetrainSample = null;
            ResetEngineEventState();
            _lastSteeringModel = null;
            _lastCalculateAt = null;
            return GameplayFfbOutput.Zero;
        }

        var activeCategory = NormalizeVehicleCategory(packet.VehicleCategory);
        var profile = ResolveVehicleCategoryProfile(settings, activeCategory);
        var context = new FfbFrameContext(
            CalculateContextDeltaTime(packet),
            state.LastPacketAge,
            fade,
            activeCategory,
            deviceProfile);
        var features = TelemetryFeatureExtractor.Extract(packet, profile, settings.TireSurfaceTuning);

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
            SpeedStabilityLayer.Calculate(features, profile, context),
            HillStandstillLoadLayer.Calculate(features, profile, context),
            SideSlopeBiasLayer.Calculate(features, profile, context),
            ImplementBiasLayer.Calculate(features, profile, context));
        steering = SafetyFilters.Apply(steering);
        steering = SmoothSteering(steering, context, profile);

        var engineDrivetrainPulses = CalculateEngineDrivetrainPulses(packet, features, profile, context);
        var engineStartStopVibration = CalculateEngineStartStopVibrationContinuous(profile, context);
        var haptics = HapticMixer.CombineContinuous(
            SurfaceTractionLayer.CalculateContinuous(features, profile, context),
            EngineDrivetrainLayer.CalculateContinuous(packet, features, profile, context),
            engineStartStopVibration.Contribution,
            SuspensionTerrainLayer.CalculateContinuous(features, profile, context));
        var pulses = HapticMixer.CombinePulses(
            SuspensionTerrainLayer.CalculatePulses(packet, features, profile, context),
            engineDrivetrainPulses);

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
            bump is null ? 0 : Math.Clamp(bump.DurationMs, 20, bump.Kind == FfbPulseKind.EngineStartStop && bump.Percent > 0 ? 5000 : 250),
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
            CalculateSteeringSlipReliefActive(features, profile),
            engineStartStopVibration.Direction > 0 ? ClampPercent(engineStartStopVibration.Contribution.Value.EnginePercent) : 0,
            engineStartStopVibration.Direction > 0 ? engineStartStopVibration.DurationMs : 0,
            engineStartStopVibration.Direction > 0 ? engineStartStopVibration.Contribution.Value.EngineHz : 0,
            engineStartStopVibration.Direction < 0 ? ClampPercent(engineStartStopVibration.Contribution.Value.EnginePercent) : 0,
            engineStartStopVibration.Direction < 0 ? Math.Clamp(profile.EngineStartStopPulse.StopDurationMs, 40, 500) : 0,
            engineStartStopVibration.Direction < 0 ? engineStartStopVibration.Contribution.Value.EngineHz : 0,
            bump?.Kind == FfbPulseKind.GearShift ? ClampPercent(Math.Abs(bump.Percent)) : 0,
            bump?.Kind == FfbPulseKind.GearShift ? bump.DurationMs : 0,
            capped.Haptics.EnginePercent > 0 || engineStartStopVibration.Direction != 0 || bump?.Kind is FfbPulseKind.GearShift,
            features.EngineLugging,
            features.EngineUnderLoad,
            bump?.Kind == FfbPulseKind.GearShift,
            engineStartStopVibration.Direction != 0,
            ClampSignedPercent(capped.Steering.CenterOffsetPercent),
            _lastSlewLimited,
            CalculateHillStandstillLoadActive(features, profile, fade),
            CalculateSideSlopeBiasActive(features, profile, fade),
            CalculateImplementBiasActive(features, profile, fade));

        return output with
        {
            IsActive = output.SpringPercent > 0 ||
                       output.DamperPercent > 0 ||
                       output.FrictionPercent > 0 ||
                       output.EngineRpmVibrationPercent > 0 ||
                       output.SurfaceVibrationPercent > 0 ||
                       output.TerrainRumblePercent > 0 ||
                       output.SlipVibrationPercent > 0 ||
                       output.BumpImpulsePercent != 0 ||
                       output.CenterOffsetPercent != 0 ||
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
                Math.Abs(contribution.FrictionRelief) > epsilon ||
                Math.Abs(contribution.CenterOffsetAdd) > epsilon);
    }

    private static bool CalculateHillStandstillLoadActive(TelemetryFeatures features, GameplayFfbEffectProfile profile, double fade)
    {
        var slopeDeg = features.SlopeRatio * Math.Max(0.1, profile.MotionFeedback.FullPitchDeg);
        return profile.MotionFeedback.Enabled && profile.HillStandstillLoad.Enabled && fade > 0 && features.SpeedKmh <= MovingSpeedThresholdKmh && slopeDeg > profile.HillStandstillLoad.MinSlopeDeg;
    }

    private static bool CalculateSideSlopeBiasActive(TelemetryFeatures features, GameplayFfbEffectProfile profile, double fade)
    {
        return profile.MotionFeedback.Enabled && profile.SideSlopeBias.Enabled && fade > 0 && features.RollRatio > 0;
    }

    private static bool CalculateImplementBiasActive(TelemetryFeatures features, GameplayFfbEffectProfile profile, double fade)
    {
        return profile.ImplementBias.Enabled && fade > 0 && features.AttachedMassRatio > 0;
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
            FfbPulseKind.EngineStartStop => 4,
            FfbPulseKind.GearShift => 5,
            FfbPulseKind.DrivetrainJerk => 5,
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

    private TimeSpan CalculateContextDeltaTime(TelemetryPacketV1 packet)
    {
        var now = DateTimeOffset.UtcNow;
        var wallClockMs = _lastCalculateAt is null ? 0 : (now - _lastCalculateAt.Value).TotalMilliseconds;
        _lastCalculateAt = now;

        var frameDtMs = IsValidFinite(packet.Frame?.DtMs) ? packet.Frame!.DtMs!.Value : 1000.0 / 125.0;
        var deltaMs = Math.Max(frameDtMs, wallClockMs);
        return TimeSpan.FromMilliseconds(Math.Clamp(deltaMs, 1, 1000));
    }
}
