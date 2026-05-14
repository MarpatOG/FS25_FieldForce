using System.Text.Json.Serialization;

namespace FS25FfbBridge.App.Models;

public sealed class TelemetryPacketV1
{
    public const string ExpectedProtocolName = "FS25_REAL_FFB_TELEMETRY";
    public const string ExpectedProtocolVersion = "1.3.0";
    public const string LegacyProtocolVersion = "1.2.0";
    public const string LegacyProtocolVersionV1_1 = "1.1.0";

    [JsonPropertyName("protocol")]
    public TelemetryProtocolV1? Protocol { get; set; }

    [JsonPropertyName("frame")]
    public TelemetryFrameV1? Frame { get; set; }

    [JsonPropertyName("game")]
    public TelemetryGameV1? Game { get; set; }

    [JsonPropertyName("player")]
    public TelemetryPlayerV1? Player { get; set; }

    [JsonPropertyName("vehicle")]
    public TelemetryVehicleV1? Vehicle { get; set; }

    [JsonPropertyName("controls")]
    public TelemetryControlsV1? Controls { get; set; }

    [JsonPropertyName("motion")]
    public TelemetryMotionV1? Motion { get; set; }

    [JsonPropertyName("steering")]
    public TelemetrySteeringV1? Steering { get; set; }

    [JsonPropertyName("engine")]
    public TelemetryEngineV1? Engine { get; set; }

    [JsonPropertyName("transmission")]
    public TelemetryTransmissionV1? Transmission { get; set; }

    [JsonPropertyName("events")]
    public TelemetryEventsV1? Events { get; set; }

    [JsonPropertyName("wheels")]
    public List<TelemetryWheelV1> Wheels { get; set; } = [];

    [JsonPropertyName("suspension")]
    public TelemetrySuspensionV1? Suspension { get; set; }

    [JsonPropertyName("surface")]
    public TelemetrySurfaceV1? Surface { get; set; }

    [JsonPropertyName("environment")]
    public TelemetryEnvironmentV1? Environment { get; set; }

    [JsonPropertyName("attachments")]
    public List<TelemetryAttachmentV1> Attachments { get; set; } = [];

    [JsonPropertyName("collisions")]
    public TelemetryCollisionsV1? Collisions { get; set; }

    [JsonPropertyName("diagnostics")]
    public TelemetryDiagnosticsV1? Diagnostics { get; set; }

    [JsonIgnore]
    public bool IsProtocolValid =>
        string.Equals(Protocol?.Name, ExpectedProtocolName, StringComparison.Ordinal) &&
        (string.Equals(Protocol?.Version, ExpectedProtocolVersion, StringComparison.Ordinal) ||
         string.Equals(Protocol?.Version, LegacyProtocolVersion, StringComparison.Ordinal) ||
         string.Equals(Protocol?.Version, LegacyProtocolVersionV1_1, StringComparison.Ordinal));

    [JsonIgnore]
    public bool IsPlayerInVehicle => Player?.IsInVehicle == true && Vehicle is not null;

    [JsonIgnore]
    public string? VehicleName => Vehicle?.Name;

    [JsonIgnore]
    public string? VehicleType => Vehicle?.Type;

    [JsonIgnore]
    public string? VehicleCategory => Vehicle?.Category;

    [JsonIgnore]
    public string? WheelTireTypes => Vehicle?.WheelTireTypes;

    [JsonIgnore]
    public string? WheelTireProfile => Vehicle?.WheelTireProfile;

    [JsonIgnore]
    public string? ActiveTireProfile => Wheels.Select(w => w.TireProfile).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? Vehicle?.WheelTireProfile;

    [JsonIgnore]
    public bool? IsArticulated => Vehicle?.IsArticulated;

    [JsonIgnore]
    public double? SpeedKmh => Motion?.SpeedKmh;

    [JsonIgnore]
    public double? SteeringAngle => Steering?.Angle;

    [JsonIgnore]
    public double? SteeringRate => Steering?.Rate;

    [JsonIgnore]
    public double? Rpm => Engine?.Rpm;

    [JsonIgnore]
    public bool? EngineStarted => Engine?.Started;

    [JsonIgnore]
    public bool? EngineRunning => Engine?.IsRunning ?? Engine?.Started;

    [JsonIgnore]
    public string? EngineState => Engine?.State;

    [JsonIgnore]
    public bool? EngineIsStarting => Engine?.IsStarting;

    [JsonIgnore]
    public double? EngineStartDurationMs => Engine?.StartDurationMs;

    [JsonIgnore]
    public double? EngineStartRemainingMs => Engine?.StartRemainingMs;

    [JsonIgnore]
    public double? Rpm01 => Engine?.Rpm01;

    [JsonIgnore]
    public double? MinRpm => Engine?.MinRpm;

    [JsonIgnore]
    public double? MaxRpm => Engine?.MaxRpm;

    [JsonIgnore]
    public double? EngineLoad01 => Engine?.Load01;

    [JsonIgnore]
    public double? MotorTorque => Engine?.Torque;

    [JsonIgnore]
    public double? MotorMaxTorque => Engine?.MaxTorque;

    [JsonIgnore]
    public string? MotorType => Engine?.MotorType;

    [JsonIgnore]
    public string? PowertrainType => Engine?.PowertrainType;

    [JsonIgnore]
    public IReadOnlyList<string> EnergySources => Engine?.EnergySources ?? [];

    [JsonIgnore]
    public double? MassT => Vehicle?.MassT;

    [JsonIgnore]
    public double? TotalMassT => Vehicle?.TotalMassT;

    [JsonIgnore]
    public double? MassKg => Vehicle?.MassT is null ? null : Vehicle.MassT.Value * 1000.0;

    [JsonIgnore]
    public double? TotalMassKg => Vehicle?.TotalMassT is null ? null : Vehicle.TotalMassT.Value * 1000.0;

    [JsonIgnore]
    public bool? IsOnField => Surface?.IsOnField;

    [JsonIgnore]
    public string? SurfaceType => Surface?.Type;

    [JsonIgnore]
    public double? SurfaceAttribute => Surface?.Attribute;

    [JsonIgnore]
    public double? GroundWetness => Environment?.GroundWetness;

    [JsonIgnore]
    public double? RainScale => Environment?.RainScale;

    [JsonIgnore]
    public double? WheelSlip => Wheels.Count == 0 ? null : Wheels.Where(w => IsValidFinite(w.Slip)).Select(w => w.Slip!.Value).DefaultIfEmpty().Average();

    [JsonIgnore]
    public double? MaxWheelSlip => Wheels.Count == 0 ? null : Wheels.Where(w => IsValidFinite(w.Slip)).Select(w => w.Slip!.Value).DefaultIfEmpty().Max();

    [JsonIgnore]
    public double? GroundContactRatio => Ratio(Wheels, w => w.HasGroundContact);

    [JsonIgnore]
    public double? SteeringGroundContactRatio => Ratio(Wheels.Where(w => w.IsSteering == true), w => w.HasGroundContact);

    [JsonIgnore]
    public double? SteeringWheelSlip => Wheels.Where(w => w.IsSteering == true && IsValidFinite(w.Slip)).Select(w => w.Slip!.Value).DefaultIfEmpty(double.NaN).Average() is var value && double.IsNaN(value) ? null : value;

    [JsonIgnore]
    public double? PitchDeg => Motion?.PitchDeg;

    [JsonIgnore]
    public double? RollDeg => Motion?.RollDeg;

    [JsonIgnore]
    public double? YawRateDegPerSec => Motion?.YawRateRadPerSec is null ? null : Motion.YawRateRadPerSec.Value * 180.0 / Math.PI;

    [JsonIgnore]
    public double? SlopeDeg => Motion?.SlopeDeg;

    [JsonIgnore]
    public double? LocalAccelerationX => Motion?.LocalAccelerationMps2?.X;

    [JsonIgnore]
    public double? LocalAccelerationY => Motion?.LocalAccelerationMps2?.Y;

    [JsonIgnore]
    public double? LocalAccelerationZ => Motion?.LocalAccelerationMps2?.Z;

    [JsonIgnore]
    public double? SuspensionImpulse => Suspension?.Impulse;

    [JsonIgnore]
    public double? VerticalImpactImpulse => Suspension?.VerticalImpactImpulse;

    [JsonIgnore]
    public double? BumpImpulse => Suspension?.VerticalImpactImpulse;

    [JsonIgnore]
    public double? LandingImpulse => Suspension?.LandingImpulse;

    [JsonIgnore]
    public double? CollisionImpulse => Collisions?.CollisionImpulse;

    [JsonIgnore]
    public double? LongitudinalJerkImpulse => Collisions?.LongitudinalJerkImpulse;

    [JsonIgnore]
    public double? LeftSuspensionImpulse => Suspension?.LeftImpulse;

    [JsonIgnore]
    public double? RightSuspensionImpulse => Suspension?.RightImpulse;

    [JsonIgnore]
    public double? Throttle => Controls?.Throttle;

    [JsonIgnore]
    public double? Brake => Controls?.Brake;

    [JsonIgnore]
    public double? Clutch => Controls?.Clutch;

    [JsonIgnore]
    public int? Gear => Transmission?.Gear;

    [JsonIgnore]
    public int? PreviousGear => Transmission?.PreviousGear;

    [JsonIgnore]
    public int? TargetGear => Transmission?.TargetGear;

    [JsonIgnore]
    public string? GearGroup => Transmission?.GearGroup;

    [JsonIgnore]
    public double? TransmissionClutch01 => Transmission?.Clutch01 ?? Controls?.Clutch;

    [JsonIgnore]
    public double? TransmissionBrake01 => Transmission?.Brake01 ?? Controls?.Brake;

    [JsonIgnore]
    public double? TransmissionThrottle01 => Transmission?.Throttle01 ?? Controls?.Throttle;

    [JsonIgnore]
    public long? EngineStartSeq => Events?.EngineStartSeq;

    [JsonIgnore]
    public long? EngineStopSeq => Events?.EngineStopSeq;

    [JsonIgnore]
    public long? GearChangeSeq => Events?.GearChangeSeq;

    [JsonIgnore]
    public string? GearChangeKind => Events?.GearChangeKind;

    [JsonIgnore]
    public double? GearChangeTimeMs => Events?.GearChangeTimeMs;

    [JsonIgnore]
    public string? GameState => Game?.State;

    public void ValidateContract()
    {
        if (!IsProtocolValid)
        {
            throw new InvalidDataException($"Unsupported telemetry protocol '{Protocol?.Name ?? "<null>"}' version '{Protocol?.Version ?? "<null>"}'.");
        }

        if (Vehicle is null)
        {
            if (Controls is not null || Motion is not null || Steering is not null || Engine is not null ||
                Transmission is not null || Suspension is not null || Surface is not null || Collisions is not null)
            {
                throw new InvalidDataException("Invalid no-vehicle packet: vehicle-dependent blocks must be null.");
            }

            if (Wheels.Count != 0 || Attachments.Count != 0)
            {
                throw new InvalidDataException("Invalid no-vehicle packet: wheels and attachments must be empty arrays.");
            }
        }
    }

    private static bool IsValidFinite(double? value)
    {
        return value is not null && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);
    }

    private static double? Ratio(IEnumerable<TelemetryWheelV1> wheels, Func<TelemetryWheelV1, bool?> selector)
    {
        var values = wheels.Select(selector).Where(value => value is not null).Select(value => value!.Value).ToArray();
        return values.Length == 0 ? null : values.Count(value => value) / (double)values.Length;
    }
}

public sealed class TelemetryProtocolV1
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

public sealed class TelemetryFrameV1
{
    [JsonPropertyName("sequence")]
    public long? Sequence { get; set; }

    [JsonPropertyName("dtMs")]
    public double? DtMs { get; set; }

    [JsonPropertyName("telemetryRateHz")]
    public double? TelemetryRateHz { get; set; }

    [JsonPropertyName("timestampMs")]
    public double? TimestampMs { get; set; }

    [JsonPropertyName("isDuplicate")]
    public bool? IsDuplicate { get; set; }

    [JsonPropertyName("isInterpolated")]
    public bool? IsInterpolated { get; set; }
}

public sealed class TelemetryGameV1
{
    [JsonPropertyName("state")]
    public string? State { get; set; }
}

public sealed class TelemetryPlayerV1
{
    [JsonPropertyName("isInVehicle")]
    public bool? IsInVehicle { get; set; }
}

public sealed class TelemetryVehicleV1
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("wheelTireTypes")]
    public string? WheelTireTypes { get; set; }

    [JsonPropertyName("wheelTireProfile")]
    public string? WheelTireProfile { get; set; }

    [JsonPropertyName("isArticulated")]
    public bool? IsArticulated { get; set; }

    [JsonPropertyName("massT")]
    public double? MassT { get; set; }

    [JsonPropertyName("totalMassT")]
    public double? TotalMassT { get; set; }
}

public sealed class TelemetryControlsV1
{
    [JsonPropertyName("throttle")]
    public double? Throttle { get; set; }

    [JsonPropertyName("brake")]
    public double? Brake { get; set; }

    [JsonPropertyName("clutch")]
    public double? Clutch { get; set; }
}

public sealed class TelemetryMotionV1
{
    [JsonPropertyName("speedMps")]
    public double? SpeedMps { get; set; }

    [JsonPropertyName("speedKmh")]
    public double? SpeedKmh { get; set; }

    [JsonPropertyName("pitchDeg")]
    public double? PitchDeg { get; set; }

    [JsonPropertyName("rollDeg")]
    public double? RollDeg { get; set; }

    [JsonPropertyName("yawRateRadPerSec")]
    public double? YawRateRadPerSec { get; set; }

    [JsonPropertyName("slopeDeg")]
    public double? SlopeDeg { get; set; }

    [JsonPropertyName("localAccelerationMps2")]
    public TelemetryVector3V1? LocalAccelerationMps2 { get; set; }
}

public sealed class TelemetryVector3V1
{
    [JsonPropertyName("x")]
    public double? X { get; set; }

    [JsonPropertyName("y")]
    public double? Y { get; set; }

    [JsonPropertyName("z")]
    public double? Z { get; set; }
}

public sealed class TelemetrySteeringV1
{
    [JsonPropertyName("angle")]
    public double? Angle { get; set; }

    [JsonPropertyName("rate")]
    public double? Rate { get; set; }
}

public sealed class TelemetryEngineV1
{
    [JsonPropertyName("isRunning")]
    public bool? IsRunning { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("isStarting")]
    public bool? IsStarting { get; set; }

    [JsonPropertyName("startDurationMs")]
    public double? StartDurationMs { get; set; }

    [JsonPropertyName("startRemainingMs")]
    public double? StartRemainingMs { get; set; }

    [JsonPropertyName("rpm")]
    public double? Rpm { get; set; }

    [JsonPropertyName("rpm01")]
    public double? Rpm01 { get; set; }

    [JsonPropertyName("minRpm")]
    public double? MinRpm { get; set; }

    [JsonPropertyName("maxRpm")]
    public double? MaxRpm { get; set; }

    [JsonPropertyName("load01")]
    public double? Load01 { get; set; }

    [JsonPropertyName("torque")]
    public double? Torque { get; set; }

    [JsonPropertyName("maxTorque")]
    public double? MaxTorque { get; set; }

    [JsonPropertyName("motorType")]
    public string? MotorType { get; set; }

    [JsonPropertyName("powertrainType")]
    public string? PowertrainType { get; set; }

    [JsonPropertyName("energySources")]
    public List<string> EnergySources { get; set; } = [];

    [JsonPropertyName("started")]
    public bool? Started { get; set; }
}

public sealed class TelemetryTransmissionV1
{
    [JsonPropertyName("gear")]
    public int? Gear { get; set; }

    [JsonPropertyName("previousGear")]
    public int? PreviousGear { get; set; }

    [JsonPropertyName("targetGear")]
    public int? TargetGear { get; set; }

    [JsonPropertyName("gearGroup")]
    public string? GearGroup { get; set; }

    [JsonPropertyName("clutch01")]
    public double? Clutch01 { get; set; }

    [JsonPropertyName("brake01")]
    public double? Brake01 { get; set; }

    [JsonPropertyName("throttle01")]
    public double? Throttle01 { get; set; }
}

public sealed class TelemetryEventsV1
{
    [JsonPropertyName("engineStartSeq")]
    public long? EngineStartSeq { get; set; }

    [JsonPropertyName("engineStopSeq")]
    public long? EngineStopSeq { get; set; }

    [JsonPropertyName("gearChangeSeq")]
    public long? GearChangeSeq { get; set; }

    [JsonPropertyName("gearChangeKind")]
    public string? GearChangeKind { get; set; }

    [JsonPropertyName("gearChangeTimeMs")]
    public double? GearChangeTimeMs { get; set; }
}

public sealed class TelemetryWheelV1
{
    [JsonPropertyName("index")]
    public int? Index { get; set; }

    [JsonPropertyName("side")]
    public string? Side { get; set; }

    [JsonPropertyName("isSteering")]
    public bool? IsSteering { get; set; }

    [JsonPropertyName("slip")]
    public double? Slip { get; set; }

    [JsonPropertyName("hasGroundContact")]
    public bool? HasGroundContact { get; set; }

    [JsonPropertyName("suspensionImpulse")]
    public double? SuspensionImpulse { get; set; }

    [JsonPropertyName("wheelType")]
    public string? WheelType { get; set; }

    [JsonPropertyName("tireType")]
    public string? TireType { get; set; }

    [JsonPropertyName("tireProfile")]
    public string? TireProfile { get; set; }

    [JsonPropertyName("surfaceType")]
    public string? SurfaceType { get; set; }

    [JsonPropertyName("surfaceAttribute")]
    public double? SurfaceAttribute { get; set; }

    [JsonPropertyName("groundType")]
    public string? GroundType { get; set; }

    [JsonPropertyName("groundDepth")]
    public double? GroundDepth { get; set; }

    [JsonPropertyName("isOnField")]
    public bool? IsOnField { get; set; }
}

public sealed class TelemetrySuspensionV1
{
    [JsonPropertyName("impulse")]
    public double? Impulse { get; set; }

    [JsonPropertyName("verticalImpactImpulse")]
    public double? VerticalImpactImpulse { get; set; }

    [JsonPropertyName("landingImpulse")]
    public double? LandingImpulse { get; set; }

    [JsonPropertyName("leftImpulse")]
    public double? LeftImpulse { get; set; }

    [JsonPropertyName("rightImpulse")]
    public double? RightImpulse { get; set; }
}

public sealed class TelemetrySurfaceV1
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("attribute")]
    public double? Attribute { get; set; }

    [JsonPropertyName("isOnField")]
    public bool? IsOnField { get; set; }
}

public sealed class TelemetryEnvironmentV1
{
    [JsonPropertyName("groundWetness")]
    public double? GroundWetness { get; set; }

    [JsonPropertyName("rainScale")]
    public double? RainScale { get; set; }
}

public sealed class TelemetryAttachmentV1
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("massT")]
    public double? MassT { get; set; }
}

public sealed class TelemetryCollisionsV1
{
    [JsonPropertyName("collisionImpulse")]
    public double? CollisionImpulse { get; set; }

    [JsonPropertyName("longitudinalJerkImpulse")]
    public double? LongitudinalJerkImpulse { get; set; }
}

public sealed class TelemetryDiagnosticsV1
{
    [JsonPropertyName("payloadBytes")]
    public int? PayloadBytes { get; set; }

    [JsonPropertyName("buildTimeMs")]
    public double? BuildTimeMs { get; set; }

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];
}
