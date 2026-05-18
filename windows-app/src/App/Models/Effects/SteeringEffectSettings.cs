namespace FieldForce.App.Models;

public sealed class SpeedConditionSettings : FfbEffectSettings
{
    public double StandstillFloor { get; set; } = 0.04;
    public double SpeedReferenceKmh { get; set; } = 50;
}

public sealed class MechanicalFrictionSettings : FfbEffectSettings
{
    public double BaseFriction { get; set; } = 0.18;
    public double LoadInfluence { get; set; } = 0.80;
    public double FieldInfluence { get; set; } = 0.25;
}

public sealed class LoadResistanceSettings : FfbEffectSettings
{
    public bool AffectsSpring { get; set; } = true;
    public bool AffectsDamper { get; set; } = true;
    public bool AffectsFriction { get; set; } = true;
    public double SpringScale { get; set; } = 0.35;
    public double DamperScale { get; set; } = 0.90;
    public double FrictionScale { get; set; } = 1.00;
}

public sealed class MotionFeedbackSettings : FfbEffectSettings
{
    public double FullRollDeg { get; set; } = 12;
    public double FullPitchDeg { get; set; } = 12;
    public double FullYawRateDegPerSec { get; set; } = 45;
    public double FullAcceleration { get; set; } = 5;
    public int SpringModifierPercent { get; set; } = 20;
    public int DamperModifierPercent { get; set; } = 25;
}

public sealed class SlewSmoothingSettings
{
    public bool Enabled { get; set; } = true;
    public int StrengthPercent { get; set; } = 45;
}

public sealed class HillStandstillLoadSettings : FfbEffectSettings
{
    public double MinSlopeDeg { get; set; } = 3;
    public double FullSlopeDeg { get; set; } = 16;
}

public sealed class SideSlopeBiasSettings : FfbEffectSettings
{
    public const double ConservativeMinRollDeg = 6;

    public double MinRollDeg { get; set; } = ConservativeMinRollDeg;
    public double FullRollDeg { get; set; } = 18;
}

public sealed class ImplementBiasSettings : FfbEffectSettings
{
    public double MinAttachedMassRatio { get; set; } = 0.10;
    public double FullAttachedMassRatio { get; set; } = 1.0;
    public double FullLateralOffsetM { get; set; } = 1.5;
}
