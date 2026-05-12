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
    public int IdleStrengthPercent { get; set; } = 6;
    public int LoadStrengthPercent { get; set; } = 18;
    public int LuggingBoostPercent { get; set; } = 5;
}

public sealed class GearShiftPulseSettings : FfbEffectSettings
{
    public int DurationMs { get; set; } = 55;
    public int CooldownMs { get; set; } = 300;
}

public sealed class EngineStartStopPulseSettings : FfbEffectSettings
{
    public int DurationMs { get; set; } = 120;
    public int StartFrequencyHz { get; set; } = 18;
    public int StopFrequencyHz { get; set; } = 12;
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

public class ImpulsePulseFeedbackSettings : FfbEffectSettings
{
    public double MinImpulse { get; set; }
    public double FullImpulse { get; set; }
    public int DurationMs { get; set; }
    public int CooldownMs { get; set; }
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

public sealed class DrivetrainPulseSettings : FfbEffectSettings
{
    public int DurationMs { get; set; } = 45;
    public int CooldownMs { get; set; } = 160;
}

public class GameplayFfbEffectProfile
{
    public const int SpeedSpringStrengthDefault = 75;
    public const int SpeedSpringMaxOutputDefault = 80;
    public const double SpeedSpringStandstillFloorDefault = 0.22;
    public const double SpeedSpringReferenceKmhDefault = 28;
    public const int DefaultMaxOutputPercent = 65;

    public SpeedConditionSettings SpeedSpring { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = SpeedSpringStrengthDefault,
        MaxOutputPercent = SpeedSpringMaxOutputDefault,
        Curve = FfbCurveKind.Aggressive,
        StandstillFloor = SpeedSpringStandstillFloorDefault,
        SpeedReferenceKmh = SpeedSpringReferenceKmhDefault
    };

    public SpeedConditionSettings SpeedDamper { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 70,
        MaxOutputPercent = DefaultMaxOutputPercent,
        Curve = FfbCurveKind.Smooth,
        StandstillFloor = 0.04,
        SpeedReferenceKmh = 55
    };

    public MechanicalFrictionSettings MechanicalFriction { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 38,
        MaxOutputPercent = DefaultMaxOutputPercent,
        Curve = FfbCurveKind.Smooth,
        BaseFriction = 0.18,
        LoadInfluence = 0.80,
        FieldInfluence = 0.25
    };

    public LoadResistanceSettings LoadResistance { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 55,
        MaxOutputPercent = DefaultMaxOutputPercent,
        Curve = FfbCurveKind.Smooth,
        AffectsSpring = true,
        AffectsDamper = true,
        AffectsFriction = true,
        SpringScale = 0.35,
        DamperScale = 0.90,
        FrictionScale = 1.00
    };

    public EngineVibrationSettings EngineRpmVibration { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 15,
        MaxOutputPercent = 100,
        Curve = FfbCurveKind.Smooth,
        MinRpm = 500,
        MaxRpm = 2400,
        MinFrequencyHz = 12,
        MaxFrequencyHz = 30
    };

    public EngineVibrationSettings EngineVibration
    {
        get => EngineRpmVibration;
        set => EngineRpmVibration = value ?? new EngineVibrationSettings();
    }

    public GearShiftPulseSettings GearShiftPulse { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 35,
        MaxOutputPercent = 100,
        Curve = FfbCurveKind.Smooth,
        DurationMs = 55,
        CooldownMs = 300
    };

    public EngineStartStopPulseSettings EngineStartStopPulse { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 30,
        MaxOutputPercent = 100,
        Curve = FfbCurveKind.Smooth,
        DurationMs = 120,
        StartFrequencyHz = 18,
        StopFrequencyHz = 12
    };

    public int EngineDrivetrainMaxPercent { get; set; } = 12;

    public SurfaceFeedbackSettings SurfaceFeedback { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 35,
        MaxOutputPercent = DefaultMaxOutputPercent,
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
        StrengthPercent = 31,
        MaxOutputPercent = DefaultMaxOutputPercent,
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
        StrengthPercent = 22,
        MaxOutputPercent = DefaultMaxOutputPercent,
        Curve = FfbCurveKind.Smooth,
        MinWetness = 0.05,
        DamperModifierPercent = 18,
        SurfaceVibrationModifierPercent = 25
    };

    public MotionFeedbackSettings MotionFeedback { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 16,
        MaxOutputPercent = DefaultMaxOutputPercent,
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
        StrengthPercent = 34,
        MaxOutputPercent = DefaultMaxOutputPercent,
        Curve = FfbCurveKind.Aggressive,
        MinImpulse = 0.28,
        FullImpulse = 1.2,
        DurationMs = 65,
        CooldownMs = 150
    };

    public SuspensionHitFeedbackSettings SuspensionHitFeedback { get; set; } = new();

    public LandingFeedbackSettings LandingFeedback { get; set; } = new();

    public CollisionFeedbackSettings CollisionFeedback { get; set; } = new();

    public TerrainRumbleSettings TerrainRumble { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 28,
        MaxOutputPercent = DefaultMaxOutputPercent,
        Curve = FfbCurveKind.Smooth,
        MinImpulse = 0.08,
        FullImpulse = 0.60,
        MinFrequencyHz = 8,
        MaxFrequencyHz = 14
    };

    public DrivetrainPulseSettings DrivetrainPulse { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 18,
        MaxOutputPercent = DefaultMaxOutputPercent,
        Curve = FfbCurveKind.Smooth,
        DurationMs = 45,
        CooldownMs = 160
    };

    public static Dictionary<string, GameplayFfbEffectProfile> CreateCategoryDefaults(GameplayFfbEffectProfile baseProfile)
    {
        return CreateCategoryProfiles(baseProfile, VehicleCategoryFfbProfile.CreateDefaults(), applyLegacyMultipliers: true);
    }

    public static Dictionary<string, GameplayFfbEffectProfile> CreateCategoryProfiles(
        GameplayFfbEffectProfile baseProfile,
        Dictionary<string, VehicleCategoryFfbProfile> legacyProfiles,
        bool applyLegacyMultipliers)
    {
        var result = new Dictionary<string, GameplayFfbEffectProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in VehicleCategoryFfbProfile.Categories)
        {
            var profile = Clone(baseProfile);
            if (applyLegacyMultipliers &&
                legacyProfiles.TryGetValue(category, out var legacyProfile) &&
                legacyProfile is not null)
            {
                ApplyLegacyMultipliers(profile, legacyProfile);
            }

            ApplyOverallOutputCap(profile, baseProfile.SpeedSpring.MaxOutputPercent);
            result[category] = profile;
        }

        return result;
    }

    public static GameplayFfbEffectProfile Clone(GameplayFfbEffectProfile settings)
    {
        return new GameplayFfbEffectProfile
        {
            SpeedSpring = CloneSpeed(settings.SpeedSpring),
            SpeedDamper = CloneSpeed(settings.SpeedDamper),
            MechanicalFriction = new MechanicalFrictionSettings
            {
                Enabled = settings.MechanicalFriction.Enabled,
                StrengthPercent = settings.MechanicalFriction.StrengthPercent,
                MaxOutputPercent = settings.MechanicalFriction.MaxOutputPercent,
                Curve = settings.MechanicalFriction.Curve,
                BaseFriction = settings.MechanicalFriction.BaseFriction,
                LoadInfluence = settings.MechanicalFriction.LoadInfluence,
                FieldInfluence = settings.MechanicalFriction.FieldInfluence
            },
            LoadResistance = new LoadResistanceSettings
            {
                Enabled = settings.LoadResistance.Enabled,
                StrengthPercent = settings.LoadResistance.StrengthPercent,
                MaxOutputPercent = settings.LoadResistance.MaxOutputPercent,
                Curve = settings.LoadResistance.Curve,
                AffectsSpring = settings.LoadResistance.AffectsSpring,
                AffectsDamper = settings.LoadResistance.AffectsDamper,
                AffectsFriction = settings.LoadResistance.AffectsFriction,
                SpringScale = settings.LoadResistance.SpringScale,
                DamperScale = settings.LoadResistance.DamperScale,
                FrictionScale = settings.LoadResistance.FrictionScale
            },
            EngineRpmVibration = new EngineVibrationSettings
            {
                Enabled = settings.EngineRpmVibration.Enabled,
                StrengthPercent = settings.EngineRpmVibration.StrengthPercent,
                MaxOutputPercent = settings.EngineRpmVibration.MaxOutputPercent,
                Curve = settings.EngineRpmVibration.Curve,
                MinRpm = settings.EngineRpmVibration.MinRpm,
                MaxRpm = settings.EngineRpmVibration.MaxRpm,
                MinFrequencyHz = settings.EngineRpmVibration.MinFrequencyHz,
                MaxFrequencyHz = settings.EngineRpmVibration.MaxFrequencyHz,
                IdleStrengthPercent = settings.EngineRpmVibration.IdleStrengthPercent,
                LoadStrengthPercent = settings.EngineRpmVibration.LoadStrengthPercent,
                LuggingBoostPercent = settings.EngineRpmVibration.LuggingBoostPercent
            },
            GearShiftPulse = new GearShiftPulseSettings
            {
                Enabled = settings.GearShiftPulse.Enabled,
                StrengthPercent = settings.GearShiftPulse.StrengthPercent,
                MaxOutputPercent = settings.GearShiftPulse.MaxOutputPercent,
                Curve = settings.GearShiftPulse.Curve,
                DurationMs = settings.GearShiftPulse.DurationMs,
                CooldownMs = settings.GearShiftPulse.CooldownMs
            },
            EngineStartStopPulse = new EngineStartStopPulseSettings
            {
                Enabled = settings.EngineStartStopPulse.Enabled,
                StrengthPercent = settings.EngineStartStopPulse.StrengthPercent,
                MaxOutputPercent = settings.EngineStartStopPulse.MaxOutputPercent,
                Curve = settings.EngineStartStopPulse.Curve,
                DurationMs = settings.EngineStartStopPulse.DurationMs,
                StartFrequencyHz = settings.EngineStartStopPulse.StartFrequencyHz,
                StopFrequencyHz = settings.EngineStartStopPulse.StopFrequencyHz
            },
            EngineDrivetrainMaxPercent = settings.EngineDrivetrainMaxPercent,
            SurfaceFeedback = new SurfaceFeedbackSettings
            {
                Enabled = settings.SurfaceFeedback.Enabled,
                StrengthPercent = settings.SurfaceFeedback.StrengthPercent,
                MaxOutputPercent = settings.SurfaceFeedback.MaxOutputPercent,
                Curve = settings.SurfaceFeedback.Curve,
                MinSpeedKmh = settings.SurfaceFeedback.MinSpeedKmh,
                FieldFrequencyMinHz = settings.SurfaceFeedback.FieldFrequencyMinHz,
                FieldFrequencyMaxHz = settings.SurfaceFeedback.FieldFrequencyMaxHz,
                FieldSpringModifierPercent = settings.SurfaceFeedback.FieldSpringModifierPercent,
                FieldDamperModifierPercent = settings.SurfaceFeedback.FieldDamperModifierPercent,
                FieldFrictionModifierPercent = settings.SurfaceFeedback.FieldFrictionModifierPercent
            },
            SlipFeedback = new SlipFeedbackSettings
            {
                Enabled = settings.SlipFeedback.Enabled,
                StrengthPercent = settings.SlipFeedback.StrengthPercent,
                MaxOutputPercent = settings.SlipFeedback.MaxOutputPercent,
                Curve = settings.SlipFeedback.Curve,
                MinSlip = settings.SlipFeedback.MinSlip,
                FullSlip = settings.SlipFeedback.FullSlip,
                MinSpeedKmh = settings.SlipFeedback.MinSpeedKmh,
                MinFrequencyHz = settings.SlipFeedback.MinFrequencyHz,
                MaxFrequencyHz = settings.SlipFeedback.MaxFrequencyHz
            },
            WetnessFeedback = new WetnessFeedbackSettings
            {
                Enabled = settings.WetnessFeedback.Enabled,
                StrengthPercent = settings.WetnessFeedback.StrengthPercent,
                MaxOutputPercent = settings.WetnessFeedback.MaxOutputPercent,
                Curve = settings.WetnessFeedback.Curve,
                MinWetness = settings.WetnessFeedback.MinWetness,
                DamperModifierPercent = settings.WetnessFeedback.DamperModifierPercent,
                SurfaceVibrationModifierPercent = settings.WetnessFeedback.SurfaceVibrationModifierPercent
            },
            MotionFeedback = new MotionFeedbackSettings
            {
                Enabled = settings.MotionFeedback.Enabled,
                StrengthPercent = settings.MotionFeedback.StrengthPercent,
                MaxOutputPercent = settings.MotionFeedback.MaxOutputPercent,
                Curve = settings.MotionFeedback.Curve,
                FullRollDeg = settings.MotionFeedback.FullRollDeg,
                FullPitchDeg = settings.MotionFeedback.FullPitchDeg,
                FullYawRateDegPerSec = settings.MotionFeedback.FullYawRateDegPerSec,
                FullAcceleration = settings.MotionFeedback.FullAcceleration,
                SpringModifierPercent = settings.MotionFeedback.SpringModifierPercent,
                DamperModifierPercent = settings.MotionFeedback.DamperModifierPercent
            },
            BumpFeedback = new BumpFeedbackSettings
            {
                Enabled = settings.BumpFeedback.Enabled,
                StrengthPercent = settings.BumpFeedback.StrengthPercent,
                MaxOutputPercent = settings.BumpFeedback.MaxOutputPercent,
                Curve = settings.BumpFeedback.Curve,
                MinImpulse = settings.BumpFeedback.MinImpulse,
                FullImpulse = settings.BumpFeedback.FullImpulse,
                DurationMs = settings.BumpFeedback.DurationMs,
                CooldownMs = settings.BumpFeedback.CooldownMs
            },
            SuspensionHitFeedback = CloneImpulse(settings.SuspensionHitFeedback, new SuspensionHitFeedbackSettings()),
            LandingFeedback = CloneImpulse(settings.LandingFeedback, new LandingFeedbackSettings()),
            CollisionFeedback = CloneImpulse(settings.CollisionFeedback, new CollisionFeedbackSettings()),
            TerrainRumble = new TerrainRumbleSettings
            {
                Enabled = settings.TerrainRumble.Enabled,
                StrengthPercent = settings.TerrainRumble.StrengthPercent,
                MaxOutputPercent = settings.TerrainRumble.MaxOutputPercent,
                Curve = settings.TerrainRumble.Curve,
                MinImpulse = settings.TerrainRumble.MinImpulse,
                FullImpulse = settings.TerrainRumble.FullImpulse,
                MinFrequencyHz = settings.TerrainRumble.MinFrequencyHz,
                MaxFrequencyHz = settings.TerrainRumble.MaxFrequencyHz
            },
            DrivetrainPulse = new DrivetrainPulseSettings
            {
                Enabled = settings.DrivetrainPulse.Enabled,
                StrengthPercent = settings.DrivetrainPulse.StrengthPercent,
                MaxOutputPercent = settings.DrivetrainPulse.MaxOutputPercent,
                Curve = settings.DrivetrainPulse.Curve,
                DurationMs = settings.DrivetrainPulse.DurationMs,
                CooldownMs = settings.DrivetrainPulse.CooldownMs
            }
        };
    }

    public static void NormalizeEffectSettings(GameplayFfbEffectProfile settings)
    {
        settings.SpeedSpring ??= new SpeedConditionSettings();
        settings.SpeedDamper ??= new SpeedConditionSettings();
        settings.MechanicalFriction ??= new MechanicalFrictionSettings();
        settings.LoadResistance ??= new LoadResistanceSettings();
        settings.EngineRpmVibration ??= settings.EngineVibration ?? new EngineVibrationSettings();
        settings.EngineRpmVibration.IdleStrengthPercent = Math.Clamp(settings.EngineRpmVibration.IdleStrengthPercent, 0, 100);
        settings.EngineRpmVibration.LoadStrengthPercent = Math.Clamp(settings.EngineRpmVibration.LoadStrengthPercent, 0, 100);
        settings.EngineRpmVibration.LuggingBoostPercent = Math.Clamp(settings.EngineRpmVibration.LuggingBoostPercent, 0, 100);
        settings.GearShiftPulse ??= new GearShiftPulseSettings();
        settings.EngineStartStopPulse ??= new EngineStartStopPulseSettings();
        settings.GearShiftPulse.CooldownMs = Math.Clamp(settings.GearShiftPulse.CooldownMs, 100, 700);
        settings.EngineDrivetrainMaxPercent = Math.Clamp(settings.EngineDrivetrainMaxPercent, 0, 100);
        settings.SurfaceFeedback ??= new SurfaceFeedbackSettings();
        settings.SlipFeedback ??= new SlipFeedbackSettings();
        settings.WetnessFeedback ??= new WetnessFeedbackSettings();
        settings.MotionFeedback ??= new MotionFeedbackSettings();
        settings.BumpFeedback ??= new BumpFeedbackSettings();
        settings.SuspensionHitFeedback ??= new SuspensionHitFeedbackSettings();
        settings.LandingFeedback ??= new LandingFeedbackSettings();
        settings.CollisionFeedback ??= new CollisionFeedbackSettings();
        settings.TerrainRumble ??= new TerrainRumbleSettings();
        settings.DrivetrainPulse ??= new DrivetrainPulseSettings();
    }

    public static void ApplyCurrentSpeedSpringPreset(GameplayFfbEffectProfile settings)
    {
        settings.SpeedSpring.Enabled = true;
        settings.SpeedSpring.StrengthPercent = SpeedSpringStrengthDefault;
        settings.SpeedSpring.MaxOutputPercent = SpeedSpringMaxOutputDefault;
        settings.SpeedSpring.Curve = FfbCurveKind.Aggressive;
        settings.SpeedSpring.StandstillFloor = SpeedSpringStandstillFloorDefault;
        settings.SpeedSpring.SpeedReferenceKmh = SpeedSpringReferenceKmhDefault;
    }

    public static void ApplyCurrentSuspensionTerrainPreset(GameplayFfbEffectProfile settings)
    {
        settings.BumpFeedback.Enabled = true;
        settings.BumpFeedback.StrengthPercent = 34;
        settings.BumpFeedback.MaxOutputPercent = DefaultMaxOutputPercent;
        settings.BumpFeedback.Curve = FfbCurveKind.Aggressive;
        settings.BumpFeedback.MinImpulse = 0.28;
        settings.BumpFeedback.FullImpulse = 1.2;
        settings.BumpFeedback.DurationMs = 65;
        settings.BumpFeedback.CooldownMs = 150;

        settings.SuspensionHitFeedback.Enabled = true;
        settings.SuspensionHitFeedback.StrengthPercent = 30;
        settings.SuspensionHitFeedback.MaxOutputPercent = DefaultMaxOutputPercent;
        settings.SuspensionHitFeedback.Curve = FfbCurveKind.Aggressive;
        settings.SuspensionHitFeedback.MinImpulse = 0.26;
        settings.SuspensionHitFeedback.FullImpulse = 1.05;
        settings.SuspensionHitFeedback.DurationMs = 50;
        settings.SuspensionHitFeedback.CooldownMs = 85;

        settings.TerrainRumble.Enabled = true;
        settings.TerrainRumble.StrengthPercent = 28;
        settings.TerrainRumble.MaxOutputPercent = DefaultMaxOutputPercent;
        settings.TerrainRumble.Curve = FfbCurveKind.Smooth;
        settings.TerrainRumble.MinImpulse = 0.08;
        settings.TerrainRumble.FullImpulse = 0.60;
        settings.TerrainRumble.MinFrequencyHz = 8;
        settings.TerrainRumble.MaxFrequencyHz = 14;
    }

    public static void ApplyOverallOutputCap(GameplayFfbEffectProfile settings, int overallCapPercent)
    {
        var overallCap = Math.Clamp(overallCapPercent, 0, 100);
        ApplyOverallOutputCap(settings.SpeedSpring, overallCap);
        ApplyOverallOutputCap(settings.SpeedDamper, overallCap);
        ApplyOverallOutputCap(settings.MechanicalFriction, overallCap);
        ApplyOverallOutputCap(settings.LoadResistance, overallCap);
        ApplyOverallOutputCap(settings.EngineRpmVibration, overallCap);
        ApplyOverallOutputCap(settings.GearShiftPulse, overallCap);
        ApplyOverallOutputCap(settings.EngineStartStopPulse, overallCap);
        ApplyOverallOutputCap(settings.SurfaceFeedback, overallCap);
        ApplyOverallOutputCap(settings.SlipFeedback, overallCap);
        ApplyOverallOutputCap(settings.WetnessFeedback, overallCap);
        ApplyOverallOutputCap(settings.MotionFeedback, overallCap);
        ApplyOverallOutputCap(settings.BumpFeedback, overallCap);
        ApplyOverallOutputCap(settings.SuspensionHitFeedback, overallCap);
        ApplyOverallOutputCap(settings.LandingFeedback, overallCap);
        ApplyOverallOutputCap(settings.CollisionFeedback, overallCap);
        ApplyOverallOutputCap(settings.TerrainRumble, overallCap);
        ApplyOverallOutputCap(settings.DrivetrainPulse, overallCap);
    }

    private static void ApplyOverallOutputCap(FfbEffectSettings settings, int overallCap)
    {
        var currentMax = Math.Clamp(settings.StrengthPercent, 0, 100) *
                         (Math.Clamp(settings.MaxOutputPercent, 0, 100) / 100.0);
        settings.StrengthPercent = overallCap == 0
            ? 0
            : ClampSettingPercent(currentMax * 100 / overallCap);
        settings.MaxOutputPercent = overallCap;
    }

    private static void ApplyLegacyMultipliers(GameplayFfbEffectProfile settings, VehicleCategoryFfbProfile profile)
    {
        ApplySpeedProfile(settings.SpeedSpring, profile.SpeedSpringStrengthMultiplier, profile.SpeedSpringMaxMultiplier, profile.SpeedSpringReferenceMultiplier);
        ApplySpeedProfile(settings.SpeedDamper, profile.SpeedDamperStrengthMultiplier, profile.SpeedDamperMaxMultiplier, profile.SpeedDamperReferenceMultiplier);
        ApplyEffectProfile(settings.MechanicalFriction, profile.MechanicalFrictionStrengthMultiplier, profile.MechanicalFrictionMaxMultiplier);
        ApplyEffectProfile(settings.LoadResistance, profile.LoadResistanceStrengthMultiplier, profile.LoadResistanceMaxMultiplier);
        ApplyEffectProfile(settings.EngineVibration, profile.EngineVibrationStrengthMultiplier, profile.EngineVibrationMaxMultiplier);
        ApplyEffectProfile(settings.SurfaceFeedback, profile.SurfaceFeedbackStrengthMultiplier, profile.SurfaceFeedbackMaxMultiplier);
        ApplyEffectProfile(settings.SlipFeedback, profile.SlipFeedbackStrengthMultiplier, profile.SlipFeedbackMaxMultiplier);
        ApplyEffectProfile(settings.BumpFeedback, profile.BumpFeedbackStrengthMultiplier, profile.BumpFeedbackMaxMultiplier);
        ApplyEffectProfile(settings.SuspensionHitFeedback, profile.BumpFeedbackStrengthMultiplier, profile.BumpFeedbackMaxMultiplier);
        ApplyEffectProfile(settings.LandingFeedback, profile.BumpFeedbackStrengthMultiplier, profile.BumpFeedbackMaxMultiplier);
        ApplyEffectProfile(settings.CollisionFeedback, profile.BumpFeedbackStrengthMultiplier, profile.BumpFeedbackMaxMultiplier);
        ApplyEffectProfile(settings.TerrainRumble, profile.BumpFeedbackStrengthMultiplier, profile.BumpFeedbackMaxMultiplier);
    }

    private static void ApplyEffectProfile(FfbEffectSettings settings, double strengthMultiplier, double maxMultiplier)
    {
        settings.StrengthPercent = ClampSettingPercent(settings.StrengthPercent * SanitizeMultiplier(strengthMultiplier));
        settings.MaxOutputPercent = ClampSettingPercent(settings.MaxOutputPercent * SanitizeMultiplier(maxMultiplier));
    }

    private static void ApplySpeedProfile(SpeedConditionSettings settings, double strengthMultiplier, double maxMultiplier, double speedReferenceMultiplier)
    {
        ApplyEffectProfile(settings, strengthMultiplier, maxMultiplier);
        settings.SpeedReferenceKmh = Math.Clamp(settings.SpeedReferenceKmh * SanitizeMultiplier(speedReferenceMultiplier), 1, 300);
    }

    private static double SanitizeMultiplier(double value)
    {
        return double.IsFinite(value) && value >= 0 ? Math.Clamp(value, 0, 3) : 1;
    }

    private static int ClampSettingPercent(double value)
    {
        return Math.Clamp((int)Math.Round(value), 0, 100);
    }

    private static SpeedConditionSettings CloneSpeed(SpeedConditionSettings settings)
    {
        return new SpeedConditionSettings
        {
            Enabled = settings.Enabled,
            StrengthPercent = settings.StrengthPercent,
            MaxOutputPercent = settings.MaxOutputPercent,
            Curve = settings.Curve,
            StandstillFloor = settings.StandstillFloor,
            SpeedReferenceKmh = settings.SpeedReferenceKmh
        };
    }

    private static T CloneImpulse<T>(T settings, T fallback)
        where T : ImpulsePulseFeedbackSettings
    {
        fallback.Enabled = settings.Enabled;
        fallback.StrengthPercent = settings.StrengthPercent;
        fallback.MaxOutputPercent = settings.MaxOutputPercent;
        fallback.Curve = settings.Curve;
        fallback.MinImpulse = settings.MinImpulse;
        fallback.FullImpulse = settings.FullImpulse;
        fallback.DurationMs = settings.DurationMs;
        fallback.CooldownMs = settings.CooldownMs;
        return fallback;
    }
}

public sealed class GameplayFfbSettings : GameplayFfbEffectProfile
{
    public GameplayFfbSettings()
    {
        VehicleCategoryEffectProfiles = GameplayFfbEffectProfile.CreateCategoryDefaults(this);
    }

    public bool Enabled { get; set; } = true;

    public string DeviceHapticProfileName { get; set; } = "Logitech MOMO Racing Wheel";

    public Dictionary<string, GameplayFfbEffectProfile> VehicleCategoryEffectProfiles { get; set; }

    public Dictionary<string, VehicleCategoryFfbProfile> VehicleCategoryProfiles { get; set; } = VehicleCategoryFfbProfile.CreateDefaults();
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

    public static IReadOnlyList<string> Categories { get; } =
    [
        TractorWheeled,
        TractorTracked,
        Harvester,
        Truck,
        LoaderTelehandler,
        LightVehicle,
        Unknown
    ];

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
                SurfaceFeedbackStrengthMultiplier = 0.70,
                SurfaceFeedbackMaxMultiplier = 0.80
            },
            [LoaderTelehandler] = new()
            {
                SpeedDamperReferenceMultiplier = 0.85,
                MechanicalFrictionStrengthMultiplier = 1.20,
                BumpFeedbackStrengthMultiplier = 1.20
            },
            [LightVehicle] = new()
            {
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
