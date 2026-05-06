using System.Text.Json.Serialization;

namespace FS25FfbBridge.App.Models;

public sealed class TelemetryPacket
{
    [JsonPropertyName("timestamp")]
    public double? Timestamp { get; set; }

    [JsonPropertyName("gameState")]
    public string? GameState { get; set; }

    [JsonPropertyName("isPlayerInVehicle")]
    public bool? IsPlayerInVehicle { get; set; }

    [JsonPropertyName("vehicleName")]
    public string? VehicleName { get; set; }

    [JsonPropertyName("vehicleType")]
    public string? VehicleType { get; set; }

    [JsonPropertyName("speedKmh")]
    public double? SpeedKmh { get; set; }

    [JsonPropertyName("steeringAngle")]
    public double? SteeringAngle { get; set; }

    [JsonPropertyName("rpm")]
    public double? Rpm { get; set; }

    [JsonPropertyName("engineStarted")]
    public bool? EngineStarted { get; set; }

    [JsonPropertyName("mass")]
    public double? Mass { get; set; }

    [JsonPropertyName("totalMass")]
    public double? TotalMass { get; set; }

    [JsonPropertyName("isOnField")]
    public bool? IsOnField { get; set; }

    [JsonPropertyName("surfaceType")]
    public string? SurfaceType { get; set; }

    [JsonPropertyName("surfaceAttribute")]
    public double? SurfaceAttribute { get; set; }

    [JsonPropertyName("groundWetness")]
    public double? GroundWetness { get; set; }

    [JsonPropertyName("rainScale")]
    public double? RainScale { get; set; }

    [JsonPropertyName("wheelSlip")]
    public double? WheelSlip { get; set; }

    [JsonPropertyName("maxWheelSlip")]
    public double? MaxWheelSlip { get; set; }

    [JsonPropertyName("groundContactRatio")]
    public double? GroundContactRatio { get; set; }

    [JsonPropertyName("pitchDeg")]
    public double? PitchDeg { get; set; }

    [JsonPropertyName("rollDeg")]
    public double? RollDeg { get; set; }

    [JsonPropertyName("yawRateDegPerSec")]
    public double? YawRateDegPerSec { get; set; }

    [JsonPropertyName("slopeDeg")]
    public double? SlopeDeg { get; set; }

    [JsonPropertyName("localAccelerationX")]
    public double? LocalAccelerationX { get; set; }

    [JsonPropertyName("localAccelerationY")]
    public double? LocalAccelerationY { get; set; }

    [JsonPropertyName("localAccelerationZ")]
    public double? LocalAccelerationZ { get; set; }

    [JsonPropertyName("bumpImpulse")]
    public double? BumpImpulse { get; set; }
}
