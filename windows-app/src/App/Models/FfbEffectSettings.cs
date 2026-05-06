namespace FS25FfbBridge.App.Models;

public class FfbEffectSettings
{
    public bool Enabled { get; set; } = true;
    public int StrengthPercent { get; set; } = 50;
    public int MaxOutputPercent { get; set; } = 50;
    public FfbCurveKind Curve { get; set; } = FfbCurveKind.Smooth;
}

public sealed class LoadResistanceSettings : FfbEffectSettings
{
    public bool AffectsSpring { get; set; } = true;
    public bool AffectsDamper { get; set; } = true;
}

public sealed class EngineVibrationSettings : FfbEffectSettings
{
    public int MinFrequencyHz { get; set; } = 16;
    public int MaxFrequencyHz { get; set; } = 34;
}

public sealed class SurfaceFeedbackSettings : FfbEffectSettings
{
    public int FieldFrequencyHz { get; set; } = 18;
    public int FieldSpringModifierPercent { get; set; } = -10;
    public int FieldDamperModifierPercent { get; set; } = 10;
}

public sealed class GameplayFfbSettings
{
    public bool Enabled { get; set; } = true;

    public FfbEffectSettings SpeedSpring { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 85,
        MaxOutputPercent = 90,
        Curve = FfbCurveKind.Smooth
    };

    public FfbEffectSettings SpeedDamper { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 90,
        MaxOutputPercent = 95,
        Curve = FfbCurveKind.Smooth
    };

    public LoadResistanceSettings LoadResistance { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 65,
        MaxOutputPercent = 65,
        Curve = FfbCurveKind.Smooth,
        AffectsSpring = true,
        AffectsDamper = true
    };

    public EngineVibrationSettings EngineVibration { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 55,
        MaxOutputPercent = 55,
        Curve = FfbCurveKind.Smooth,
        MinFrequencyHz = 16,
        MaxFrequencyHz = 34
    };

    public SurfaceFeedbackSettings SurfaceFeedback { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 60,
        MaxOutputPercent = 60,
        Curve = FfbCurveKind.Smooth,
        FieldFrequencyHz = 18,
        FieldSpringModifierPercent = -10,
        FieldDamperModifierPercent = 10
    };
}
