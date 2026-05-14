namespace FS25FfbBridge.App.Models;

public class FfbEffectSettings
{
    public bool Enabled { get; set; } = true;
    public int StrengthPercent { get; set; } = 50;
    public int MaxOutputPercent { get; set; } = 50;
    public FfbCurveKind Curve { get; set; } = FfbCurveKind.Smooth;
}

public class ImpulsePulseFeedbackSettings : FfbEffectSettings
{
    public double MinImpulse { get; set; }
    public double FullImpulse { get; set; }
    public int DurationMs { get; set; }
    public int CooldownMs { get; set; }
}
