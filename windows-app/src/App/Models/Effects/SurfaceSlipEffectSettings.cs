namespace FieldForce.App.Models;

public sealed class SurfaceFeedbackSettings : FfbEffectSettings
{
    public double MinSpeedKmh { get; set; } = 0.2;
    public int FieldFrequencyMinHz { get; set; } = 8;
    public int FieldFrequencyMaxHz { get; set; } = 24;
    public int FieldSpringModifierPercent { get; set; } = -10;
    public int FieldDamperModifierPercent { get; set; } = 10;
    public int FieldFrictionModifierPercent { get; set; } = 15;
}

public sealed class SlipFeedbackSettings : FfbEffectSettings
{
    public double MinSlip { get; set; } = 0.12;
    public double FullSlip { get; set; } = 0.65;
    public double MinSpeedKmh { get; set; } = 0.5;
    public int MinFrequencyHz { get; set; } = 18;
    public int MaxFrequencyHz { get; set; } = 38;
}

public sealed class WetnessFeedbackSettings : FfbEffectSettings
{
    public double MinWetness { get; set; } = 0.05;
    public int DamperModifierPercent { get; set; } = 18;
    public int SurfaceVibrationModifierPercent { get; set; } = 25;
}
