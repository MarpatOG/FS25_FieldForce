namespace FS25FfbBridge.App.Models;

public sealed record GameplayFfbOutput(
    int SpringPercent,
    int DamperPercent,
    int FrictionPercent,
    int EngineVibrationPercent,
    int EngineVibrationHz,
    int SurfaceVibrationPercent,
    int SurfaceVibrationHz,
    int SlipVibrationPercent,
    int SlipVibrationHz,
    int BumpImpulsePercent,
    int BumpDurationMs,
    int BumpCooldownMs,
    double LoadFactor,
    double TelemetryFade,
    bool IsActive,
    string ActiveCategory = VehicleCategoryFfbProfile.Unknown)
{
    public static GameplayFfbOutput Zero { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, false);

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

            if (EngineVibrationPercent > 0)
            {
                active.Add("Engine");
            }

            if (SurfaceVibrationPercent > 0)
            {
                active.Add("Surface");
            }

            if (SlipVibrationPercent > 0)
            {
                active.Add("Slip");
            }

            if (BumpImpulsePercent != 0)
            {
                active.Add("Bump");
            }

            return active.Count == 0 ? "None" : string.Join(", ", active);
        }
    }
}
