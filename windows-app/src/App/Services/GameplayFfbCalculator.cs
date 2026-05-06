using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Services;

public sealed class GameplayFfbCalculator
{
    public GameplayFfbOutput Calculate(TelemetryReceiverState state, GameplayFfbSettings settings)
    {
        if (!settings.Enabled)
        {
            return GameplayFfbOutput.Zero;
        }

        var packet = state.LastPacket;
        var fade = CalculateTelemetryFade(state.LastPacketAge);

        if (fade <= 0 ||
            packet is null ||
            packet.IsPlayerInVehicle != true)
        {
            return GameplayFfbOutput.Zero;
        }

        var speed = Math.Max(0, packet.SpeedKmh ?? 0);
        var loadFactor = CalculateLoadFactor(packet.Mass, packet.TotalMass);
        var loadResistance = CalculateLoadResistance(settings.LoadResistance, loadFactor);
        var loadRatio = CalculateLoadRatio(loadFactor);
        var surfaceType = NormalizeSurfaceType(packet);
        var fieldSurface = IsFieldSurface(surfaceType, packet.IsOnField);
        var wetness = CalculateWetness(packet);
        var surfaceActive = IsSurfaceActive(settings.SurfaceFeedback, speed, fieldSurface);
        var wetnessActive = settings.WetnessFeedback.Enabled &&
                            wetness is not null &&
                            wetness.Value >= Math.Clamp(settings.WetnessFeedback.MinWetness, 0, 1);

        var spring = CalculateSpeedEffect(settings.SpeedSpring, speed, fade);
        var damper = CalculateSpeedEffect(settings.SpeedDamper, speed, fade);
        var friction = CalculateMechanicalFriction(settings.MechanicalFriction, loadRatio, surfaceActive, fade);

        if (settings.LoadResistance.Enabled)
        {
            if (settings.LoadResistance.AffectsSpring)
            {
                spring *= 1 + (loadResistance * Math.Clamp(settings.LoadResistance.SpringScale, 0, 2));
            }

            if (settings.LoadResistance.AffectsDamper)
            {
                damper *= 1 + (loadResistance * Math.Clamp(settings.LoadResistance.DamperScale, 0, 2));
            }

            if (settings.LoadResistance.AffectsFriction)
            {
                friction *= 1 + (loadResistance * Math.Clamp(settings.LoadResistance.FrictionScale, 0, 2));
            }
        }

        if (surfaceActive)
        {
            spring *= 1 + (settings.SurfaceFeedback.FieldSpringModifierPercent / 100.0);
            damper *= 1 + (settings.SurfaceFeedback.FieldDamperModifierPercent / 100.0);
            friction *= 1 + (settings.SurfaceFeedback.FieldFrictionModifierPercent / 100.0);
        }

        if (wetnessActive)
        {
            var wetnessRatio = ApplyCurve(wetness!.Value, settings.WetnessFeedback.Curve);
            var wetnessCap = CalculateMaxCapped(settings.WetnessFeedback, fade) / 100.0;
            damper *= 1 + (wetnessRatio * wetnessCap * settings.WetnessFeedback.DamperModifierPercent / 100.0);
        }

        var motionRatio = CalculateMotionRatio(packet, settings.MotionFeedback);
        if (motionRatio > 0)
        {
            var motionCap = CalculateMaxCapped(settings.MotionFeedback, fade) / 100.0;
            spring *= 1 + (motionRatio * motionCap * settings.MotionFeedback.SpringModifierPercent / 100.0);
            damper *= 1 + (motionRatio * motionCap * settings.MotionFeedback.DamperModifierPercent / 100.0);
        }

        var engine = CalculateEngineVibration(packet.Rpm, packet.EngineStarted, settings.EngineVibration, fade, out var engineHz);
        var surface = surfaceActive
            ? CalculateMaxCapped(settings.SurfaceFeedback, fade)
            : 0;
        if (surface > 0 && wetnessActive)
        {
            var wetnessRatio = ApplyCurve(wetness!.Value, settings.WetnessFeedback.Curve);
            var wetnessCap = CalculateMaxCapped(settings.WetnessFeedback, fade) / 100.0;
            surface *= 1 + (wetnessRatio * wetnessCap * settings.WetnessFeedback.SurfaceVibrationModifierPercent / 100.0);
        }

        var surfaceHz = surface > 0
            ? CalculateSurfaceFrequency(settings.SurfaceFeedback, speed, settings.SpeedDamper.SpeedReferenceKmh)
            : 0;
        var slip = CalculateSlipFeedback(packet.MaxWheelSlip ?? packet.WheelSlip, speed, settings.SlipFeedback, fade, out var slipHz);
        var bump = CalculateBumpImpulse(packet, settings.BumpFeedback, fade, out var bumpDurationMs);

        var output = new GameplayFfbOutput(
            ClampPercent(spring),
            ClampPercent(damper),
            ClampPercent(friction),
            ClampPercent(engine),
            engine > 0 ? engineHz : 0,
            ClampPercent(surface),
            surfaceHz,
            ClampPercent(slip),
            slip > 0 ? slipHz : 0,
            ClampSignedPercent(bump),
            bump != 0 ? bumpDurationMs : 0,
            bump != 0 ? Math.Clamp(settings.BumpFeedback.CooldownMs, 20, 500) : 0,
            loadFactor,
            fade,
            true);

        return output with
        {
            IsActive = output.SpringPercent > 0 ||
                       output.DamperPercent > 0 ||
                       output.FrictionPercent > 0 ||
                       output.EngineVibrationPercent > 0 ||
                       output.SurfaceVibrationPercent > 0 ||
                       output.SlipVibrationPercent > 0 ||
                       output.BumpImpulsePercent != 0
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

    private static double CalculateSpeedEffect(SpeedConditionSettings settings, double speedKmh, double fade)
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

    private static double CalculateEngineVibration(double? rpm, bool? engineStarted, EngineVibrationSettings settings, double fade, out int hz)
    {
        hz = 0;
        var minRpm = Math.Max(0, settings.MinRpm);
        var maxRpm = Math.Max(minRpm + 1, settings.MaxRpm);
        if (!settings.Enabled || engineStarted != true || rpm is null || rpm < minRpm)
        {
            return 0;
        }

        var rpmRatio = Math.Clamp((rpm.Value - minRpm) / (maxRpm - minRpm), 0, 1);
        hz = Quantize((int)Math.Round(settings.MinFrequencyHz + ((settings.MaxFrequencyHz - settings.MinFrequencyHz) * rpmRatio)), 2);
        return CalculateMaxCapped(settings, fade) * Math.Clamp(0.35 + (0.65 * ApplyCurve(rpmRatio, settings.Curve)), 0, 1);
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

    private static double CalculateSlipFeedback(double? slip, double speedKmh, SlipFeedbackSettings settings, double fade, out int hz)
    {
        hz = 0;
        if (!settings.Enabled || slip is null || speedKmh < Math.Max(0, settings.MinSpeedKmh))
        {
            return 0;
        }

        var minSlip = Math.Clamp(settings.MinSlip, 0, 1);
        var fullSlip = Math.Max(minSlip + 0.01, settings.FullSlip);
        if (slip.Value <= minSlip)
        {
            return 0;
        }

        var ratio = Math.Clamp((slip.Value - minSlip) / (fullSlip - minSlip), 0, 1);
        var curve = ApplyCurve(ratio, settings.Curve);
        var minHz = Math.Clamp(settings.MinFrequencyHz, 4, 60);
        var maxHz = Math.Clamp(settings.MaxFrequencyHz, minHz, 60);
        hz = Quantize((int)Math.Round(minHz + ((maxHz - minHz) * curve)), 2);
        return CalculateMaxCapped(settings, fade) * curve;
    }

    private static double CalculateMotionRatio(TelemetryPacket packet, MotionFeedbackSettings settings)
    {
        if (!settings.Enabled)
        {
            return 0;
        }

        var roll = NormalizeAbs(packet.RollDeg, settings.FullRollDeg);
        var pitch = Math.Max(NormalizeAbs(packet.PitchDeg, settings.FullPitchDeg), NormalizeAbs(packet.SlopeDeg, settings.FullPitchDeg));
        var yaw = NormalizeAbs(packet.YawRateDegPerSec, settings.FullYawRateDegPerSec);
        var acceleration = NormalizeVector(packet.LocalAccelerationX, packet.LocalAccelerationY, packet.LocalAccelerationZ, settings.FullAcceleration);

        return Math.Clamp((roll * 0.30) + (pitch * 0.25) + (yaw * 0.20) + (acceleration * 0.25), 0, 1);
    }

    private static double CalculateBumpImpulse(TelemetryPacket packet, BumpFeedbackSettings settings, double fade, out int durationMs)
    {
        durationMs = 0;
        if (!settings.Enabled || packet.BumpImpulse is null)
        {
            return 0;
        }

        var minImpulse = Math.Clamp(settings.MinImpulse, 0, 10);
        var fullImpulse = Math.Max(minImpulse + 0.01, settings.FullImpulse);
        var impulse = Math.Abs(packet.BumpImpulse.Value);
        if (impulse <= minImpulse)
        {
            return 0;
        }

        var ratio = Math.Clamp((impulse - minImpulse) / (fullImpulse - minImpulse), 0, 1);
        durationMs = Math.Clamp(settings.DurationMs, 20, 250);
        var sign = Math.Sign(packet.LocalAccelerationX ?? packet.SteeringAngle ?? packet.BumpImpulse.Value);
        if (sign == 0)
        {
            sign = 1;
        }

        return sign * CalculateMaxCapped(settings, fade) * ApplyCurve(ratio, settings.Curve);
    }

    private static double CalculateLoadResistance(LoadResistanceSettings settings, double loadFactor)
    {
        if (!settings.Enabled)
        {
            return 0;
        }

        return (settings.StrengthPercent / 100.0) *
               (settings.MaxOutputPercent / 100.0) *
               ApplyCurve(CalculateLoadRatio(loadFactor), settings.Curve);
    }

    private static double CalculateLoadRatio(double loadFactor)
    {
        return Math.Clamp((loadFactor - 1) / 2, 0, 1);
    }

    private static double CalculateLoadFactor(double? mass, double? totalMass)
    {
        if (mass is null || totalMass is null || mass <= 0 || totalMass <= 0)
        {
            return 1;
        }

        return Math.Clamp(totalMass.Value / mass.Value, 1, 4);
    }

    private static double CalculateMaxCapped(FfbEffectSettings settings, double fade)
    {
        return Math.Clamp(settings.StrengthPercent, 0, 100) *
               (Math.Clamp(settings.MaxOutputPercent, 0, 100) / 100.0) *
               Math.Clamp(fade, 0, 1);
    }

    private static bool IsSurfaceActive(SurfaceFeedbackSettings settings, double speedKmh, bool onField)
    {
        return onField &&
               settings.Enabled &&
               speedKmh >= Math.Max(0, settings.MinSpeedKmh);
    }

    private static int CalculateSurfaceFrequency(SurfaceFeedbackSettings settings, double speedKmh, double speedReferenceKmh)
    {
        var minHz = Math.Clamp(settings.FieldFrequencyMinHz, 4, 45);
        var maxHz = Math.Clamp(settings.FieldFrequencyMaxHz, minHz, 45);
        var ratio = Math.Clamp(speedKmh / Math.Max(1, speedReferenceKmh), 0, 1);
        return Quantize((int)Math.Round(minHz + ((maxHz - minHz) * ApplyCurve(ratio, settings.Curve))), 2);
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

    private static double ApplyCurve(double ratio, FfbCurveKind curve)
    {
        ratio = Math.Clamp(ratio, 0, 1);
        return curve switch
        {
            FfbCurveKind.Linear => ratio,
            FfbCurveKind.Aggressive => Math.Pow(ratio, 0.65),
            _ => ratio * ratio * (3 - (2 * ratio))
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

    private static int Quantize(int value, int step)
    {
        return Math.Max(step, (int)Math.Round(value / (double)step) * step);
    }
}
