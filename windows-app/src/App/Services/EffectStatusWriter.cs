using System.Text.Json;
using System.Text.Json.Serialization;
using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Services;

public sealed class EffectStatusWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly AppLogService _log;
    private readonly string _statusPath;
    private DateTimeOffset _lastWriteUtc = DateTimeOffset.MinValue;

    public EffectStatusWriter(AppLogService log)
    {
        _log = log;
        _statusPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games",
            "FarmingSimulator2025",
            "modSettings",
            "FS25_RealFfbTelemetry",
            "effectStatus.json");
    }

    public string StatusPath => _statusPath;

    public void Write(GameplayFfbOutput output, bool force = false)
    {
        var now = DateTimeOffset.UtcNow;
        if (!force && now - _lastWriteUtc < TimeSpan.FromMilliseconds(100))
        {
            return;
        }

        WriteCore(output, now);
    }

    public void WriteZero(string activeCategory = VehicleCategoryFfbProfile.Unknown)
    {
        WriteCore(GameplayFfbOutput.Zero with { ActiveCategory = activeCategory }, DateTimeOffset.UtcNow);
    }

    private void WriteCore(GameplayFfbOutput output, DateTimeOffset now)
    {
        try
        {
            var directory = Path.GetDirectoryName(_statusPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var status = new EffectStatusPacket(
                now.ToUnixTimeMilliseconds(),
                string.IsNullOrWhiteSpace(output.ActiveCategory) ? VehicleCategoryFfbProfile.Unknown : output.ActiveCategory,
                output.ActiveEffectsText,
                output.SpringPercent > 0,
                output.DamperPercent > 0,
                output.FrictionPercent > 0,
                output.SlewSmoothingActive,
                output.EngineVibrationPercent > 0,
                output.SurfaceVibrationPercent > 0,
                output.SlipVibrationPercent > 0,
                output.EventPulseKind == FfbPulseKind.Bump,
                output.EventPulseKind is FfbPulseKind.LeftSuspensionHit or FfbPulseKind.RightSuspensionHit,
                output.EventPulseKind == FfbPulseKind.Landing,
                output.EventPulseKind == FfbPulseKind.Collision,
                output.EventPulseKind is FfbPulseKind.DrivetrainJerk or FfbPulseKind.GearShift or FfbPulseKind.EngineStartStop,
                output.EventPulseKind == FfbPulseKind.GearShift,
                output.EventPulseKind == FfbPulseKind.DrivetrainJerk,
                output.EventPulseKind == FfbPulseKind.EngineStartStop,
                output.EngineUnderLoadActive,
                output.EngineLuggingActive,
                output.SpringPercent > 0 || output.FrictionPercent > 0,
                output.DamperPercent > 0,
                output.SurfaceVibrationPercent > 0 || output.TerrainRumblePercent > 0 || output.SlipVibrationPercent > 0,
                output.TerrainRumblePercent > 0,
                output.LoadResistanceActive || output.MotionFeedbackActive || output.HillStandstillLoadActive || output.SideSlopeBiasActive || output.ImplementBiasActive,
                output.EngineVibrationPercent > 0);

            File.WriteAllText(_statusPath, JsonSerializer.Serialize(status, JsonOptions));
            _lastWriteUtc = now;
        }
        catch (Exception ex)
        {
            _log.Warning("Effect status write failed: {Message}", ex.Message);
        }
    }

    private sealed record EffectStatusPacket(
        [property: JsonPropertyName("timestampMs")] long TimestampMs,
        [property: JsonPropertyName("activeCategory")] string ActiveCategory,
        [property: JsonPropertyName("activeEffectsText")] string ActiveEffectsText,
        [property: JsonPropertyName("speedSpring")] bool SpeedSpring,
        [property: JsonPropertyName("speedDamper")] bool SpeedDamper,
        [property: JsonPropertyName("friction")] bool Friction,
        [property: JsonPropertyName("slewSmoothing")] bool SlewSmoothing,
        [property: JsonPropertyName("rpmVibration")] bool RpmVibration,
        [property: JsonPropertyName("surfaceFeedback")] bool SurfaceFeedback,
        [property: JsonPropertyName("slipFeedback")] bool SlipFeedback,
        [property: JsonPropertyName("bump")] bool Bump,
        [property: JsonPropertyName("suspensionHit")] bool SuspensionHit,
        [property: JsonPropertyName("landing")] bool Landing,
        [property: JsonPropertyName("collision")] bool Collision,
        [property: JsonPropertyName("drivetrainPulse")] bool DrivetrainPulse,
        [property: JsonPropertyName("gearShiftPulse")] bool GearShiftPulse,
        [property: JsonPropertyName("clutchBrakeJerk")] bool ClutchBrakeJerk,
        [property: JsonPropertyName("engineStartStopPulse")] bool EngineStartStopPulse,
        [property: JsonPropertyName("engineUnderLoad")] bool EngineUnderLoad,
        [property: JsonPropertyName("engineLugging")] bool EngineLugging,
        [property: JsonPropertyName("steeringLoad")] bool SteeringLoad,
        [property: JsonPropertyName("speedStability")] bool SpeedStability,
        [property: JsonPropertyName("surfaceTraction")] bool SurfaceTraction,
        [property: JsonPropertyName("suspensionTerrain")] bool SuspensionTerrain,
        [property: JsonPropertyName("loadSlopeImplement")] bool LoadSlopeImplement,
        [property: JsonPropertyName("engineDrivetrain")] bool EngineDrivetrain);
}
