namespace FS25FfbBridge.App.Models;

public sealed record GameplayFfbOutput(
    int SpringPercent,
    int DamperPercent,
    int FrictionPercent,
    int EngineRpmVibrationPercent,
    int EngineRpmVibrationHz,
    int SurfaceVibrationPercent,
    int SurfaceVibrationHz,
    int TerrainRumblePercent,
    int TerrainRumbleHz,
    int SlipVibrationPercent,
    int SlipVibrationHz,
    int BumpImpulsePercent,
    int BumpDurationMs,
    int BumpCooldownMs,
    double LoadFactor,
    double TelemetryFade,
    bool IsActive,
    string ActiveCategory = VehicleCategoryFfbProfile.Unknown,
    bool TerrainRumbleActive = false,
    bool EventPulseActive = false,
    FfbPulseKind EventPulseKind = FfbPulseKind.None,
    bool LoadResistanceActive = false,
    bool MotionFeedbackActive = false,
    bool ContactReliefControlsActive = false,
    bool AntiOscillationActive = false,
    bool WetnessFeedbackActive = false,
    bool SteeringSlipReliefActive = false,
    int EngineStartPulsePercent = 0,
    int EngineStartPulseDurationMs = 0,
    int EngineStartPulseHz = 0,
    int EngineStopPulsePercent = 0,
    int EngineStopPulseDurationMs = 0,
    int EngineStopPulseHz = 0,
    int GearShiftPulsePercent = 0,
    int GearShiftPulseDurationMs = 0,
    bool EngineDrivetrainActive = false,
    bool EngineLuggingActive = false,
    bool EngineUnderLoadActive = false,
    bool GearShiftPulseActive = false,
    bool EngineStartStopPulseActive = false)
{
    public static GameplayFfbOutput Zero { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, false);

    public int EngineVibrationPercent => EngineRpmVibrationPercent;

    public int EngineVibrationHz => EngineRpmVibrationHz;

    public string ActiveEffectsText
    {
        get
        {
            var active = new List<string>();
            if (SpringPercent > 0)
            {
                active.Add("Spring");
            }

            if (DamperPercent > 0)
            {
                active.Add("Damper");
            }

            if (FrictionPercent > 0)
            {
                active.Add("Friction");
            }

            if (EngineRpmVibrationPercent > 0)
            {
                active.Add("Engine");
            }

            if (SurfaceVibrationPercent > 0)
            {
                active.Add("Surface");
            }

            if (TerrainRumblePercent > 0)
            {
                active.Add("Terrain");
            }

            if (SlipVibrationPercent > 0)
            {
                active.Add("Slip");
            }

            if (BumpImpulsePercent != 0)
            {
                active.Add(EventPulseKind switch
                {
                    FfbPulseKind.LeftSuspensionHit => "Left suspension",
                    FfbPulseKind.RightSuspensionHit => "Right suspension",
                    FfbPulseKind.Landing => "Landing",
                    FfbPulseKind.Collision => "Collision",
                    FfbPulseKind.GearShift => "Gear shift",
                    FfbPulseKind.DrivetrainJerk => "Drivetrain",
                    FfbPulseKind.EngineStartStop => "Engine pulse",
                    _ => "Bump"
                });
            }

            return active.Count == 0 ? "None" : string.Join(", ", active);
        }
    }
}
