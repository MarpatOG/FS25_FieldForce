namespace FS25FfbBridge.App.Models;

public class FfbEffectSettings
{
    public bool Enabled { get; set; } = true;
    public int StrengthPercent { get; set; } = 50;
    public int MaxOutputPercent { get; set; } = 50;
    public FfbCurveKind Curve { get; set; } = FfbCurveKind.Smooth;
}

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

public sealed class EngineVibrationSettings : FfbEffectSettings
{
    public int MinRpm { get; set; } = 500;
    public int MaxRpm { get; set; } = 2400;
    public int MinFrequencyHz { get; set; } = 16;
    public int MaxFrequencyHz { get; set; } = 34;
}

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

public sealed class MotionFeedbackSettings : FfbEffectSettings
{
    public double FullRollDeg { get; set; } = 12;
    public double FullPitchDeg { get; set; } = 12;
    public double FullYawRateDegPerSec { get; set; } = 45;
    public double FullAcceleration { get; set; } = 5;
    public int SpringModifierPercent { get; set; } = 20;
    public int DamperModifierPercent { get; set; } = 25;
}

public sealed class BumpFeedbackSettings : FfbEffectSettings
{
    public double MinImpulse { get; set; } = 0.12;
    public double FullImpulse { get; set; } = 1.0;
    public int DurationMs { get; set; } = 80;
    public int CooldownMs { get; set; } = 90;
}

public sealed class GameplayFfbSettings
{
    public bool Enabled { get; set; } = true;

    public Dictionary<string, VehicleCategoryFfbProfile> VehicleCategoryProfiles { get; set; } = VehicleCategoryFfbProfile.CreateDefaults();

    public SpeedConditionSettings SpeedSpring { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 60,
        MaxOutputPercent = 65,
        Curve = FfbCurveKind.Smooth,
        StandstillFloor = 0.04,
        SpeedReferenceKmh = 50
    };

    public SpeedConditionSettings SpeedDamper { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 65,
        MaxOutputPercent = 70,
        Curve = FfbCurveKind.Smooth,
        StandstillFloor = 0.04,
        SpeedReferenceKmh = 55
    };

    public MechanicalFrictionSettings MechanicalFriction { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 45,
        MaxOutputPercent = 55,
        Curve = FfbCurveKind.Smooth,
        BaseFriction = 0.18,
        LoadInfluence = 0.80,
        FieldInfluence = 0.25
    };

    public LoadResistanceSettings LoadResistance { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 60,
        MaxOutputPercent = 60,
        Curve = FfbCurveKind.Smooth,
        AffectsSpring = true,
        AffectsDamper = true,
        AffectsFriction = true,
        SpringScale = 0.35,
        DamperScale = 0.90,
        FrictionScale = 1.00
    };

    public EngineVibrationSettings EngineVibration { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 45,
        MaxOutputPercent = 45,
        Curve = FfbCurveKind.Smooth,
        MinRpm = 500,
        MaxRpm = 2400,
        MinFrequencyHz = 12,
        MaxFrequencyHz = 30
    };

    public SurfaceFeedbackSettings SurfaceFeedback { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 45,
        MaxOutputPercent = 50,
        Curve = FfbCurveKind.Smooth,
        MinSpeedKmh = 0.2,
        FieldFrequencyMinHz = 8,
        FieldFrequencyMaxHz = 24,
        FieldSpringModifierPercent = -10,
        FieldDamperModifierPercent = 10,
        FieldFrictionModifierPercent = 15
    };

    public SlipFeedbackSettings SlipFeedback { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 45,
        MaxOutputPercent = 45,
        Curve = FfbCurveKind.Smooth,
        MinSlip = 0.12,
        FullSlip = 0.65,
        MinSpeedKmh = 0.5,
        MinFrequencyHz = 18,
        MaxFrequencyHz = 38
    };

    public WetnessFeedbackSettings WetnessFeedback { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 35,
        MaxOutputPercent = 40,
        Curve = FfbCurveKind.Smooth,
        MinWetness = 0.05,
        DamperModifierPercent = 18,
        SurfaceVibrationModifierPercent = 25
    };

    public MotionFeedbackSettings MotionFeedback { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 30,
        MaxOutputPercent = 35,
        Curve = FfbCurveKind.Smooth,
        FullRollDeg = 12,
        FullPitchDeg = 12,
        FullYawRateDegPerSec = 45,
        FullAcceleration = 5,
        SpringModifierPercent = 20,
        DamperModifierPercent = 25
    };

    public BumpFeedbackSettings BumpFeedback { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 55,
        MaxOutputPercent = 45,
        Curve = FfbCurveKind.Aggressive,
        MinImpulse = 0.12,
        FullImpulse = 1.0,
        DurationMs = 80,
        CooldownMs = 90
    };
}

public sealed class VehicleCategoryFfbProfile
{
    public const string TractorWheeled = "TractorWheeled";
    public const string TractorTracked = "TractorTracked";
    public const string HeavyTractorWheeled = "HeavyTractorWheeled";
    public const string HeavyTractorTracked = "HeavyTractorTracked";
    public const string Harvester = "Harvester";
    public const string Truck = "Truck";
    public const string LoaderTelehandler = "LoaderTelehandler";
    public const string LightVehicle = "LightVehicle";
    public const string Unknown = "Unknown";

    public double SpeedSpringStrengthMultiplier { get; set; } = 1.0;
    public double SpeedSpringMaxMultiplier { get; set; } = 1.0;
    public double SpeedSpringReferenceMultiplier { get; set; } = 1.0;
    public double SpeedDamperStrengthMultiplier { get; set; } = 1.0;
    public double SpeedDamperMaxMultiplier { get; set; } = 1.0;
    public double SpeedDamperReferenceMultiplier { get; set; } = 1.0;
    public double MechanicalFrictionStrengthMultiplier { get; set; } = 1.0;
    public double MechanicalFrictionMaxMultiplier { get; set; } = 1.0;
    public double LoadResistanceStrengthMultiplier { get; set; } = 1.0;
    public double LoadResistanceMaxMultiplier { get; set; } = 1.0;
    public double EngineVibrationStrengthMultiplier { get; set; } = 1.0;
    public double EngineVibrationMaxMultiplier { get; set; } = 1.0;
    public double SurfaceFeedbackStrengthMultiplier { get; set; } = 1.0;
    public double SurfaceFeedbackMaxMultiplier { get; set; } = 1.0;
    public double SlipFeedbackStrengthMultiplier { get; set; } = 1.0;
    public double SlipFeedbackMaxMultiplier { get; set; } = 1.0;
    public double BumpFeedbackStrengthMultiplier { get; set; } = 1.0;
    public double BumpFeedbackMaxMultiplier { get; set; } = 1.0;

    public static Dictionary<string, VehicleCategoryFfbProfile> CreateDefaults()
    {
        return new(StringComparer.OrdinalIgnoreCase)
        {
            [TractorWheeled] = new(),
            [TractorTracked] = new()
            {
                SpeedSpringStrengthMultiplier = 0.85,
                SpeedSpringMaxMultiplier = 0.90,
                SpeedDamperStrengthMultiplier = 1.20,
                MechanicalFrictionStrengthMultiplier = 1.18,
                LoadResistanceStrengthMultiplier = 1.20,
                SlipFeedbackStrengthMultiplier = 0.70
            },
            [HeavyTractorWheeled] = new()
            {
                SpeedDamperStrengthMultiplier = 1.15,
                MechanicalFrictionStrengthMultiplier = 1.15,
                LoadResistanceStrengthMultiplier = 1.20,
                EngineVibrationStrengthMultiplier = 0.90
            },
            [HeavyTractorTracked] = new()
            {
                SpeedSpringStrengthMultiplier = 0.75,
                SpeedSpringMaxMultiplier = 0.85,
                SpeedDamperStrengthMultiplier = 1.35,
                SpeedDamperMaxMultiplier = 1.10,
                MechanicalFrictionStrengthMultiplier = 1.35,
                LoadResistanceStrengthMultiplier = 1.35,
                LoadResistanceMaxMultiplier = 1.10,
                EngineVibrationStrengthMultiplier = 0.90,
                SlipFeedbackStrengthMultiplier = 0.60
            },
            [Harvester] = new()
            {
                SpeedDamperStrengthMultiplier = 1.25,
                MechanicalFrictionStrengthMultiplier = 1.20,
                EngineVibrationStrengthMultiplier = 0.85,
                BumpFeedbackStrengthMultiplier = 0.75
            },
            [Truck] = new()
            {
                SpeedSpringStrengthMultiplier = 1.15,
                SpeedSpringMaxMultiplier = 1.10,
                SurfaceFeedbackStrengthMultiplier = 0.70,
                SurfaceFeedbackMaxMultiplier = 0.80
            },
            [LoaderTelehandler] = new()
            {
                SpeedSpringReferenceMultiplier = 0.75,
                SpeedDamperReferenceMultiplier = 0.85,
                MechanicalFrictionStrengthMultiplier = 1.20,
                BumpFeedbackStrengthMultiplier = 1.20
            },
            [LightVehicle] = new()
            {
                SpeedSpringMaxMultiplier = 0.70,
                SpeedSpringReferenceMultiplier = 0.75,
                SpeedDamperMaxMultiplier = 0.70,
                SpeedDamperReferenceMultiplier = 0.75,
                MechanicalFrictionMaxMultiplier = 0.75,
                LoadResistanceStrengthMultiplier = 0.60,
                LoadResistanceMaxMultiplier = 0.70,
                SurfaceFeedbackMaxMultiplier = 0.75,
                SlipFeedbackMaxMultiplier = 0.75,
                BumpFeedbackMaxMultiplier = 0.75
            },
            [Unknown] = new()
        };
    }
}
