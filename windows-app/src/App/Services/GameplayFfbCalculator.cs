using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Services;

public sealed class GameplayFfbCalculator
{
    private const double SpeedReferenceKmh = 40;
    private const double MinRpm = 500;
    private const double MaxRpm = 2400;

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
        var speedRatio = Math.Clamp(speed / SpeedReferenceKmh, 0, 1);
        var loadFactor = CalculateLoadFactor(packet.Mass, packet.TotalMass);
        var loadInfluence = CalculateLoadInfluence(settings.LoadResistance, loadFactor);
        var onField = packet.IsOnField == true;

        var spring = CalculateSpeedEffect(settings.SpeedSpring, speedRatio, fade, standstillFloor: 0.02);
        var damper = CalculateSpeedEffect(settings.SpeedDamper, speedRatio, fade, standstillFloor: 0.03);

        if (settings.LoadResistance.Enabled)
        {
            if (settings.LoadResistance.AffectsSpring)
            {
                spring *= 1 + loadInfluence;
            }

            if (settings.LoadResistance.AffectsDamper)
            {
                damper *= 1 + loadInfluence;
            }
        }

        if (onField && settings.SurfaceFeedback.Enabled)
        {
            spring *= 1 + (settings.SurfaceFeedback.FieldSpringModifierPercent / 100.0);
            damper *= 1 + (settings.SurfaceFeedback.FieldDamperModifierPercent / 100.0);
        }

        var engine = CalculateEngineVibration(packet.Rpm, packet.EngineStarted, settings.EngineVibration, fade, out var engineHz);
        var surface = onField && settings.SurfaceFeedback.Enabled
            ? CalculateMaxCapped(settings.SurfaceFeedback, fade)
            : 0;

        var output = new GameplayFfbOutput(
            ClampPercent(spring),
            ClampPercent(damper),
            ClampPercent(engine),
            engine > 0 ? engineHz : 0,
            ClampPercent(surface),
            surface > 0 ? Math.Clamp(settings.SurfaceFeedback.FieldFrequencyHz, 4, 45) : 0,
            loadFactor,
            fade,
            true);

        return output with
        {
            IsActive = output.SpringPercent > 0 ||
                       output.DamperPercent > 0 ||
                       output.EngineVibrationPercent > 0 ||
                       output.SurfaceVibrationPercent > 0
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

    private static double CalculateSpeedEffect(FfbEffectSettings settings, double speedRatio, double fade, double standstillFloor)
    {
        if (!settings.Enabled)
        {
            return 0;
        }

        var curve = ApplyCurve(speedRatio, settings.Curve);
        var normalized = Math.Clamp(standstillFloor + ((1 - standstillFloor) * curve), 0, 1);
        return CalculateMaxCapped(settings, fade) * normalized;
    }

    private static double CalculateEngineVibration(double? rpm, bool? engineStarted, EngineVibrationSettings settings, double fade, out int hz)
    {
        hz = 0;
        if (!settings.Enabled || engineStarted != true || rpm is null || rpm < MinRpm)
        {
            return 0;
        }

        var rpmRatio = Math.Clamp((rpm.Value - MinRpm) / (MaxRpm - MinRpm), 0, 1);
        hz = Quantize((int)Math.Round(settings.MinFrequencyHz + ((settings.MaxFrequencyHz - settings.MinFrequencyHz) * rpmRatio)), 2);
        return CalculateMaxCapped(settings, fade) * Math.Clamp(0.35 + (0.65 * ApplyCurve(rpmRatio, settings.Curve)), 0, 1);
    }

    private static double CalculateLoadInfluence(LoadResistanceSettings settings, double loadFactor)
    {
        if (!settings.Enabled)
        {
            return 0;
        }

        var ratio = Math.Clamp((loadFactor - 1) / 2, 0, 1);
        return (settings.StrengthPercent / 100.0) * (settings.MaxOutputPercent / 100.0) * ApplyCurve(ratio, settings.Curve);
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

    private static int Quantize(int value, int step)
    {
        return Math.Max(step, (int)Math.Round(value / (double)step) * step);
    }
}
