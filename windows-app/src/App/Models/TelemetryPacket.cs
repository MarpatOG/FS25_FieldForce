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
}
