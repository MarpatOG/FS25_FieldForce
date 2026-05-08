namespace FS25FfbBridge.App.Models;

public sealed record FfbFrameContext(
    TimeSpan DeltaTime,
    TimeSpan? TelemetryAge,
    double TelemetryFade,
    string VehicleCategory,
    DeviceHapticProfile DeviceProfile);

public sealed record LayerContribution<T>(
    T Value,
    double Confidence);

public sealed record SteeringModel(
    double Spring,
    double Damper,
    double Friction);

public sealed record SteeringModifiers(
    double SpringGain,
    double DamperGain,
    double FrictionGain,
    double SpringRelief,
    double DamperAdditive);

public sealed record ContinuousHaptics(
    double SurfacePercent,
    int SurfaceHz,
    double SlipPercent,
    int SlipHz,
    double EnginePercent,
    int EngineHz,
    double TerrainRumblePercent,
    int TerrainRumbleHz);

public sealed record EventPulse(
    FfbPulseKind Kind,
    double Percent,
    int DurationMs,
    int CooldownMs,
    double Direction,
    double Confidence);

public enum FfbPulseKind
{
    None,
    Bump,
    LeftSuspensionHit,
    RightSuspensionHit,
    Landing,
    Collision,
    EngineStartStop,
    DrivetrainJerk,
    GearShift
}

public sealed record TelemetryFeatures(
    double SpeedKmh,
    double SpeedRatio,
    double SteeringAngle,
    double SteeringRate,
    double YawRateRatio,
    double Slip,
    double ContactRatio,
    double ContactConfidence,
    string SurfaceClass,
    double SurfaceConfidence,
    double? Wetness,
    double LoadFactor,
    double LoadConfidence,
    double SlopeRatio,
    double SuspensionImpulse,
    double SuspensionConfidence,
    double VerticalImpactImpulse,
    double LandingImpulse,
    double CollisionImpulse,
    double LongitudinalJerkImpulse,
    double LeftSuspensionImpulse,
    double RightSuspensionImpulse,
    double RpmRatio,
    double DrivetrainConfidence);

public sealed record DeviceHapticProfile(
    string Name,
    int EngineVibrationCapPercent,
    int SurfaceHapticCapPercent,
    int SlipHapticCapPercent,
    int TerrainRumbleCapPercent,
    int BumpPulseCapPercent,
    int MaxBumpDurationMs,
    double SlewLimitPerSecond,
    double SteeringRateDamperScale)
{
    public static DeviceHapticProfile Generic { get; } = new(
        "Generic FFB",
        EngineVibrationCapPercent: 100,
        SurfaceHapticCapPercent: 100,
        SlipHapticCapPercent: 100,
        TerrainRumbleCapPercent: 100,
        BumpPulseCapPercent: 100,
        MaxBumpDurationMs: 250,
        SlewLimitPerSecond: 260,
        SteeringRateDamperScale: 1.0);

    public static DeviceHapticProfile LogitechMomo { get; } = new(
        "Logitech MOMO",
        EngineVibrationCapPercent: 14,
        SurfaceHapticCapPercent: 18,
        SlipHapticCapPercent: 18,
        TerrainRumbleCapPercent: 14,
        BumpPulseCapPercent: 34,
        MaxBumpDurationMs: 90,
        SlewLimitPerSecond: 150,
        SteeringRateDamperScale: 1.20);

    public static DeviceHapticProfile LogitechG25G27 { get; } = new(
        "Logitech G25/G27",
        EngineVibrationCapPercent: 24,
        SurfaceHapticCapPercent: 32,
        SlipHapticCapPercent: 32,
        TerrainRumbleCapPercent: 28,
        BumpPulseCapPercent: 48,
        MaxBumpDurationMs: 120,
        SlewLimitPerSecond: 190,
        SteeringRateDamperScale: 1.10);

    public static DeviceHapticProfile LogitechG29G920G923 { get; } = new(
        "Logitech G29/G920/G923",
        EngineVibrationCapPercent: 22,
        SurfaceHapticCapPercent: 30,
        SlipHapticCapPercent: 30,
        TerrainRumbleCapPercent: 26,
        BumpPulseCapPercent: 46,
        MaxBumpDurationMs: 120,
        SlewLimitPerSecond: 190,
        SteeringRateDamperScale: 1.10);

    public static DeviceHapticProfile Resolve(string? deviceProfileName)
    {
        if (string.IsNullOrWhiteSpace(deviceProfileName))
        {
            return Generic;
        }

        var name = deviceProfileName.Trim();
        if (name.Contains("momo", StringComparison.OrdinalIgnoreCase))
        {
            return LogitechMomo;
        }

        if (name.Contains("g25", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("g27", StringComparison.OrdinalIgnoreCase))
        {
            return LogitechG25G27;
        }

        if (name.Contains("g29", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("g920", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("g923", StringComparison.OrdinalIgnoreCase))
        {
            return LogitechG29G920G923;
        }

        return Generic;
    }
}
