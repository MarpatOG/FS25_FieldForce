namespace FieldForce.App.Models;

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
    double Friction,
    double CenterOffsetPercent = 0);

public sealed record SteeringContribution(
    string Source,
    double SpringAdd,
    double DamperAdd,
    double FrictionAdd,
    double SpringRelief,
    double FrictionRelief,
    double Confidence,
    double CenterOffsetAdd = 0);

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

public sealed record DerivedImpactFeatures(
    double LocalAccelerationX,
    double LocalAccelerationY,
    double LocalAccelerationZ,
    double VerticalImpactImpulse,
    double HorizontalImpulse,
    double CollisionImpulse,
    double LandingImpulse,
    double SuspensionImpulse,
    double LeftSuspensionImpulse,
    double RightSuspensionImpulse,
    double BottomOutImpulse,
    double LeftBottomOutImpulse,
    double RightBottomOutImpulse,
    double SuspensionConfidence);

public enum FfbPulseKind
{
    None,
    Bump,
    LeftSuspensionHit,
    RightSuspensionHit,
    BottomOut,
    LeftBottomOut,
    RightBottomOut,
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
    bool IsArticulatedVehicle,
    double RpmRatio,
    double DrivetrainConfidence,
    double EngineLoadRatio = 0,
    bool EngineLugging = false,
    bool EngineUnderLoad = false,
    string PowertrainType = "unknown",
    bool HeavyEngine = false,
    string TireProfile = "unknown",
    double TireSurfaceMultiplier = 0.5,
    double RollRatio = 0,
    double RollDirection = 0,
    double AccelerationRatio = 0,
    double AttachedMassRatio = 0,
    double ImplementLateralOffsetRatio = 0,
    double BottomOutImpulse = 0,
    double LeftBottomOutImpulse = 0,
    double RightBottomOutImpulse = 0,
    bool UsesNewRoadSlopeModel = false,
    string RoadSlopeSource = "none",
    double RoadSlopeConfidence = 0,
    double MaxSuspensionVelocity = 0,
    double MaxTireLoad = 0,
    bool CompressionRatioAvailable = false);

public sealed record WheelProfile(
    string Id,
    string DisplayName,
    IReadOnlyList<string> Aliases,
    int RotationDegrees,
    string RecommendedMode,
    int DefaultGlobalForceLimitPercent,
    DeviceHapticProfile Haptics);

public static class WheelProfileCatalog
{
    public const string LogitechMomoRacingId = "logitech-momo-racing";
    public const string GenericId = "generic-ffb-wheel";

    public static IReadOnlyList<WheelProfile> Profiles { get; } =
    [
        new(LogitechMomoRacingId, "Logitech Momo Racing",
            ["logitech momo racing wheel", "momo racing", "momo force", "momo racing force"],
            270, "Override / 270 degrees / low global force", 40, DeviceHapticProfile.LogitechMomo),
        new("logitech-driving-force-gt", "Logitech Driving Force GT",
            ["logitech driving force gt", "driving force gt", "dfgt"],
            900, "Override / 900 degrees / low global force", 45, DeviceHapticProfile.LogitechDrivingForce),
        new("logitech-driving-force-pro", "Logitech Driving Force Pro",
            ["logitech driving force pro", "driving force pro", "dfp"],
            900, "Override / 900 degrees / low global force", 42, DeviceHapticProfile.LogitechDrivingForce),
        new("logitech-driving-force-ex", "Logitech Driving Force EX",
            ["logitech driving force ex", "driving force ex", "df ex"],
            180, "Override / 180 degrees / low global force", 38, DeviceHapticProfile.LogitechDrivingForceEx),
        new("logitech-g25", "Logitech G25",
            ["logitech g25", "g25 racing wheel", "g25"],
            900, "Override / 900 degrees / moderate global force", 50, DeviceHapticProfile.LogitechG25G27),
        new("logitech-g27", "Logitech G27",
            ["logitech g27", "g27 racing wheel", "g27"],
            900, "Override / 900 degrees / moderate global force", 50, DeviceHapticProfile.LogitechG25G27),
        new("logitech-g29", "Logitech G29",
            ["logitech g29", "g29 driving force racing wheel", "g29"],
            900, "Override / 900 degrees / moderate global force", 48, DeviceHapticProfile.LogitechG29G920G923),
        new("logitech-g920", "Logitech G920",
            ["logitech g920", "g920 driving force racing wheel", "g920"],
            900, "Override / 900 degrees / moderate global force", 48, DeviceHapticProfile.LogitechG29G920G923),
        new("logitech-g923", "Logitech G923",
            ["logitech g923", "g923 racing wheel", "g923 trueforce", "g923"],
            900, "Override / 900 degrees / moderate global force", 48, DeviceHapticProfile.LogitechG29G920G923),
        new(GenericId, "Generic FFB Wheel",
            ["generic ffb wheel", "generic ffb", "generic force feedback wheel"],
            900, "Default DirectInput mode / start with a low global force", 35, DeviceHapticProfile.Generic)
    ];

    public static WheelProfile Generic => Profiles.Single(profile => profile.Id == GenericId);

    public static WheelProfile ResolveById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Generic;
        }

        return Profiles.FirstOrDefault(profile => profile.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Generic;
    }

    public static WheelProfile Resolve(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Generic;
        }

        var normalized = Normalize(value);
        var byId = Profiles.FirstOrDefault(profile => profile.Id.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (byId is not null)
        {
            return byId;
        }

        return Profiles
            .Where(profile => profile.Id != GenericId)
            .FirstOrDefault(profile => profile.Aliases.Any(alias => normalized.Contains(Normalize(alias), StringComparison.OrdinalIgnoreCase))) ?? Generic;
    }

    public static WheelProfile Resolve(DeviceInfo device)
    {
        foreach (var value in new[] { device.ProductName, device.InstanceName, device.DisplayName })
        {
            var profile = Resolve(value);
            if (profile.Id != GenericId)
            {
                return profile;
            }
        }

        return Generic;
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}

public sealed record DeviceHapticProfile(
    string Name,
    int EngineVibrationCapPercent,
    int EngineDrivetrainPulseCapPercent,
    int SurfaceHapticCapPercent,
    int SlipHapticCapPercent,
    int TerrainRumbleCapPercent,
    int BumpPulseCapPercent,
    int MaxBumpDurationMs,
    double SlewLimitPerSecond,
    double SteeringRateDamperScale)
{
    public static DeviceHapticProfile Generic { get; } = new(
        "Generic FFB Wheel",
        EngineVibrationCapPercent: 100,
        EngineDrivetrainPulseCapPercent: 100,
        SurfaceHapticCapPercent: 100,
        SlipHapticCapPercent: 100,
        TerrainRumbleCapPercent: 100,
        BumpPulseCapPercent: 100,
        MaxBumpDurationMs: 250,
        SlewLimitPerSecond: 260,
        SteeringRateDamperScale: 1.0);

    public static DeviceHapticProfile LogitechDrivingForce { get; } = new(
        "Logitech Driving Force GT/Pro",
        EngineVibrationCapPercent: 18,
        EngineDrivetrainPulseCapPercent: 24,
        SurfaceHapticCapPercent: 24,
        SlipHapticCapPercent: 24,
        TerrainRumbleCapPercent: 20,
        BumpPulseCapPercent: 42,
        MaxBumpDurationMs: 130,
        SlewLimitPerSecond: 165,
        SteeringRateDamperScale: 1.15);

    public static DeviceHapticProfile LogitechDrivingForceEx { get; } = new(
        "Logitech Driving Force EX",
        EngineVibrationCapPercent: 14,
        EngineDrivetrainPulseCapPercent: 20,
        SurfaceHapticCapPercent: 18,
        SlipHapticCapPercent: 18,
        TerrainRumbleCapPercent: 14,
        BumpPulseCapPercent: 36,
        MaxBumpDurationMs: 130,
        SlewLimitPerSecond: 145,
        SteeringRateDamperScale: 1.20);

    public static DeviceHapticProfile LogitechMomo { get; } = new(
        "Logitech MOMO",
        EngineVibrationCapPercent: 14,
        EngineDrivetrainPulseCapPercent: 22,
        SurfaceHapticCapPercent: 18,
        SlipHapticCapPercent: 18,
        TerrainRumbleCapPercent: 14,
        BumpPulseCapPercent: 100,
        MaxBumpDurationMs: 160,
        SlewLimitPerSecond: 150,
        SteeringRateDamperScale: 1.20);

    public static DeviceHapticProfile LogitechG25G27 { get; } = new(
        "Logitech G25/G27",
        EngineVibrationCapPercent: 24,
        EngineDrivetrainPulseCapPercent: 28,
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
        EngineDrivetrainPulseCapPercent: 28,
        SurfaceHapticCapPercent: 30,
        SlipHapticCapPercent: 30,
        TerrainRumbleCapPercent: 26,
        BumpPulseCapPercent: 46,
        MaxBumpDurationMs: 120,
        SlewLimitPerSecond: 190,
        SteeringRateDamperScale: 1.10);

    public static DeviceHapticProfile Resolve(string? deviceProfileName)
    {
        return WheelProfileCatalog.Resolve(deviceProfileName).Haptics;
    }
}
