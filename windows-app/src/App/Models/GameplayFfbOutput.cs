namespace FS25FfbBridge.App.Models;

public sealed record GameplayFfbOutput(
    int SpringPercent,
    int DamperPercent,
    int EngineVibrationPercent,
    int EngineVibrationHz,
    int SurfaceVibrationPercent,
    int SurfaceVibrationHz,
    double LoadFactor,
    double TelemetryFade,
    bool IsActive)
{
    public static GameplayFfbOutput Zero { get; } = new(0, 0, 0, 0, 0, 0, 1, 0, false);

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

            if (EngineVibrationPercent > 0)
            {
                active.Add("Engine");
            }

            if (SurfaceVibrationPercent > 0)
            {
                active.Add("Surface");
            }

            return active.Count == 0 ? "None" : string.Join(", ", active);
        }
    }
}
