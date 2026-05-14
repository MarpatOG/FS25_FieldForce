namespace FS25FfbBridge.App.Models;

public sealed class BumpFeedbackSettings : ImpulsePulseFeedbackSettings
{
    public BumpFeedbackSettings()
    {
        MinImpulse = 0.28;
        FullImpulse = 1.2;
        DurationMs = 65;
        CooldownMs = 150;
    }
}

public sealed class SuspensionHitFeedbackSettings : ImpulsePulseFeedbackSettings
{
    public SuspensionHitFeedbackSettings()
    {
        Enabled = true;
        StrengthPercent = 30;
        MaxOutputPercent = 65;
        Curve = FfbCurveKind.Aggressive;
        MinImpulse = 0.26;
        FullImpulse = 1.05;
        DurationMs = 50;
        CooldownMs = 85;
    }
}

public sealed class LandingFeedbackSettings : ImpulsePulseFeedbackSettings
{
    public LandingFeedbackSettings()
    {
        Enabled = true;
        StrengthPercent = 34;
        MaxOutputPercent = 65;
        Curve = FfbCurveKind.Aggressive;
        MinImpulse = 0.35;
        FullImpulse = 1.4;
        DurationMs = 70;
        CooldownMs = 180;
    }
}

public sealed class CollisionFeedbackSettings : ImpulsePulseFeedbackSettings
{
    public CollisionFeedbackSettings()
    {
        Enabled = true;
        StrengthPercent = 40;
        MaxOutputPercent = 65;
        Curve = FfbCurveKind.Aggressive;
        MinImpulse = 0.45;
        FullImpulse = 1.6;
        DurationMs = 90;
        CooldownMs = 350;
    }
}

public sealed class TerrainRumbleSettings : FfbEffectSettings
{
    public double MinImpulse { get; set; } = 0.08;
    public double FullImpulse { get; set; } = 0.60;
    public int MinFrequencyHz { get; set; } = 8;
    public int MaxFrequencyHz { get; set; } = 14;
}
