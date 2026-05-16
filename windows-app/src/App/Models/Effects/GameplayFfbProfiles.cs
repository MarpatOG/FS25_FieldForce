namespace FS25FfbBridge.App.Models;

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

    public SlewSmoothingSettings SlewSmoothing { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 45
    };

    public HillStandstillLoadSettings HillStandstillLoad { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 18,
        MaxOutputPercent = DefaultMaxOutputPercent,
        Curve = FfbCurveKind.Smooth,
        MinSlopeDeg = 3,
        FullSlopeDeg = 16
    };

    public SideSlopeBiasSettings SideSlopeBias { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 12,
        MaxOutputPercent = DefaultMaxOutputPercent,
        Curve = FfbCurveKind.Smooth,
        MinRollDeg = 3,
        FullRollDeg = 15
    };

    public ImplementBiasSettings ImplementBias { get; set; } = new()
    {
        Enabled = true,
        StrengthPercent = 14,
        MaxOutputPercent = DefaultMaxOutputPercent,
        Curve = FfbCurveKind.Smooth,
        MinAttachedMassRatio = 0.10,
        FullAttachedMassRatio = 1.0,
        FullLateralOffsetM = 1.5
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
        StartDurationMs = 3000,
        StopDurationMs = 120,
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
        return CreateLogitechMomoRacingCategoryDefaults();
    }

    public static void ApplyLogitechMomoRacingPreset(GameplayFfbEffectProfile settings)
    {
        ApplyLogitechMomoCategoryDefaults(settings, VehicleCategoryFfbProfile.TractorWheeled);
    }

    public static Dictionary<string, GameplayFfbEffectProfile> CreateLogitechMomoRacingCategoryDefaults()
    {
        var result = new Dictionary<string, GameplayFfbEffectProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in VehicleCategoryFfbProfile.Categories)
        {
            var profile = new GameplayFfbEffectProfile();
            ApplyLogitechMomoCategoryDefaults(profile, category);
            result[category] = profile;
        }

        return result;
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
            SlewSmoothing = new SlewSmoothingSettings
            {
                Enabled = settings.SlewSmoothing.Enabled,
                StrengthPercent = settings.SlewSmoothing.StrengthPercent
            },
            HillStandstillLoad = new HillStandstillLoadSettings
            {
                Enabled = settings.HillStandstillLoad.Enabled,
                StrengthPercent = settings.HillStandstillLoad.StrengthPercent,
                MaxOutputPercent = settings.HillStandstillLoad.MaxOutputPercent,
                Curve = settings.HillStandstillLoad.Curve,
                MinSlopeDeg = settings.HillStandstillLoad.MinSlopeDeg,
                FullSlopeDeg = settings.HillStandstillLoad.FullSlopeDeg
            },
            SideSlopeBias = new SideSlopeBiasSettings
            {
                Enabled = settings.SideSlopeBias.Enabled,
                StrengthPercent = settings.SideSlopeBias.StrengthPercent,
                MaxOutputPercent = settings.SideSlopeBias.MaxOutputPercent,
                Curve = settings.SideSlopeBias.Curve,
                MinRollDeg = settings.SideSlopeBias.MinRollDeg,
                FullRollDeg = settings.SideSlopeBias.FullRollDeg
            },
            ImplementBias = new ImplementBiasSettings
            {
                Enabled = settings.ImplementBias.Enabled,
                StrengthPercent = settings.ImplementBias.StrengthPercent,
                MaxOutputPercent = settings.ImplementBias.MaxOutputPercent,
                Curve = settings.ImplementBias.Curve,
                MinAttachedMassRatio = settings.ImplementBias.MinAttachedMassRatio,
                FullAttachedMassRatio = settings.ImplementBias.FullAttachedMassRatio,
                FullLateralOffsetM = settings.ImplementBias.FullLateralOffsetM
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
                StartDurationMs = settings.EngineStartStopPulse.StartDurationMs,
                StopDurationMs = settings.EngineStartStopPulse.StopDurationMs,
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
        settings.SlewSmoothing ??= new SlewSmoothingSettings();
        settings.SlewSmoothing.StrengthPercent = Math.Clamp(settings.SlewSmoothing.StrengthPercent, 0, 100);
        settings.HillStandstillLoad ??= new HillStandstillLoadSettings();
        settings.HillStandstillLoad.MinSlopeDeg = Math.Clamp(settings.HillStandstillLoad.MinSlopeDeg, 0, 45);
        settings.HillStandstillLoad.FullSlopeDeg = Math.Clamp(settings.HillStandstillLoad.FullSlopeDeg, Math.Max(0.1, settings.HillStandstillLoad.MinSlopeDeg), 60);
        settings.SideSlopeBias ??= new SideSlopeBiasSettings();
        settings.SideSlopeBias.MinRollDeg = Math.Clamp(settings.SideSlopeBias.MinRollDeg, 0, 45);
        settings.SideSlopeBias.FullRollDeg = Math.Clamp(settings.SideSlopeBias.FullRollDeg, Math.Max(0.1, settings.SideSlopeBias.MinRollDeg), 60);
        settings.ImplementBias ??= new ImplementBiasSettings();
        settings.ImplementBias.MinAttachedMassRatio = Math.Clamp(settings.ImplementBias.MinAttachedMassRatio, 0, 4);
        settings.ImplementBias.FullAttachedMassRatio = Math.Clamp(settings.ImplementBias.FullAttachedMassRatio, Math.Max(0.01, settings.ImplementBias.MinAttachedMassRatio), 4);
        settings.ImplementBias.FullLateralOffsetM = Math.Clamp(settings.ImplementBias.FullLateralOffsetM, 0.1, 10);
        settings.EngineRpmVibration ??= settings.EngineVibration ?? new EngineVibrationSettings();
        settings.EngineRpmVibration.IdleStrengthPercent = Math.Clamp(settings.EngineRpmVibration.IdleStrengthPercent, 0, 100);
        settings.EngineRpmVibration.LoadStrengthPercent = Math.Clamp(settings.EngineRpmVibration.LoadStrengthPercent, 0, 100);
        settings.EngineRpmVibration.LuggingBoostPercent = Math.Clamp(settings.EngineRpmVibration.LuggingBoostPercent, 0, 100);
        settings.GearShiftPulse ??= new GearShiftPulseSettings();
        settings.EngineStartStopPulse ??= new EngineStartStopPulseSettings();
        settings.EngineStartStopPulse.StartDurationMs = Math.Clamp(settings.EngineStartStopPulse.StartDurationMs, 40, 5000);
        settings.EngineStartStopPulse.StopDurationMs = Math.Clamp(settings.EngineStartStopPulse.StopDurationMs, 40, 500);
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

    private static void ApplyLogitechMomoCategoryDefaults(GameplayFfbEffectProfile settings, string category)
    {
        var values = LogitechMomoCategoryValues.For(category);
        ApplySpeed(settings.SpeedSpring, values.SpeedSpring, values.SpeedSpringCurve, maxOutputPercent: 100);
        ApplyEffect(settings.MechanicalFriction, values.MechanicalFriction, values.MechanicalFrictionCurve);
        ApplySpeed(settings.SpeedDamper, values.SpeedDamper, values.SpeedDamperCurve);
        ApplyEffect(settings.SurfaceFeedback, values.SurfaceFeedback, values.SurfaceFeedbackCurve);
        ApplyEffect(settings.SlipFeedback, values.SlipFeedback, values.SlipFeedbackCurve);
        ApplyEffect(settings.WetnessFeedback, values.Wetness, values.WetnessCurve);
        ApplyEffect(settings.BumpFeedback, values.Bump, values.BumpCurve);
        ApplyEffect(settings.SuspensionHitFeedback, values.SuspensionHit, values.SuspensionHitCurve);
        ApplyEffect(settings.CollisionFeedback, values.CollisionPulse, values.CollisionPulseCurve);
        ApplyEffect(settings.LandingFeedback, values.LandingPulse, values.LandingPulseCurve);
        ApplyEffect(settings.TerrainRumble, values.TerrainRumble, values.TerrainRumbleCurve);
        ApplyEffect(settings.LoadResistance, values.LoadResistance, values.LoadResistanceCurve);
        ApplyEffect(settings.MotionFeedback, values.Motion, values.MotionCurve);
        ApplyEffect(settings.HillStandstillLoad, values.HillStandstillLoad, values.HillStandstillLoadCurve);
        ApplyEffect(settings.SideSlopeBias, values.SideSlopeBias, values.SideSlopeBiasCurve);
        ApplyEffect(settings.ImplementBias, values.ImplementBias, values.ImplementBiasCurve);

        settings.EngineRpmVibration.Enabled = true;
        settings.EngineRpmVibration.IdleStrengthPercent = values.RpmIdle;
        settings.EngineRpmVibration.LoadStrengthPercent = values.RpmLoad;
        settings.EngineRpmVibration.LuggingBoostPercent = values.LuggingBoost;
        settings.EngineRpmVibration.StrengthPercent = Math.Max(values.RpmIdle, values.RpmLoad);
        settings.EngineRpmVibration.MaxOutputPercent = 100;
        settings.EngineRpmVibration.Curve = FfbCurveKind.Smooth;

        ApplyEffect(settings.GearShiftPulse, values.GearShiftPulse, values.GearShiftPulseCurve, maxOutputPercent: 100);
        settings.GearShiftPulse.CooldownMs = values.GearCooldownMs;
        ApplyEffect(settings.DrivetrainPulse, values.ClutchBrakeJerk, values.ClutchBrakeJerkCurve, maxOutputPercent: 100);
        ApplyEffect(settings.EngineStartStopPulse, values.EngineStartStop, values.EngineStartStopCurve, maxOutputPercent: 100);
        settings.EngineDrivetrainMaxPercent = values.EngineCapPercent;
    }

    private static void ApplyEffect(FfbEffectSettings settings, int strengthPercent, FfbCurveKind curve, int maxOutputPercent = DefaultMaxOutputPercent)
    {
        settings.Enabled = true;
        settings.StrengthPercent = strengthPercent;
        settings.MaxOutputPercent = maxOutputPercent;
        settings.Curve = curve;
    }

    private static void ApplySpeed(SpeedConditionSettings settings, int strengthPercent, FfbCurveKind curve, int maxOutputPercent = DefaultMaxOutputPercent)
    {
        ApplyEffect(settings, strengthPercent, curve, maxOutputPercent);
    }

    public static void ApplyOverallOutputCap(GameplayFfbEffectProfile settings, int overallCapPercent)
    {
        var overallCap = Math.Clamp(overallCapPercent, 0, 100);
        ApplyOverallOutputCap(settings.SpeedSpring, overallCap);
        ApplyOverallOutputCap(settings.SpeedDamper, overallCap);
        ApplyOverallOutputCap(settings.MechanicalFriction, overallCap);
        ApplyOverallOutputCap(settings.LoadResistance, overallCap);
        ApplyOverallOutputCap(settings.HillStandstillLoad, overallCap);
        ApplyOverallOutputCap(settings.SideSlopeBias, overallCap);
        ApplyOverallOutputCap(settings.ImplementBias, overallCap);
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
        ApplyLogitechMomoRacingPreset(this);
        VehicleCategoryEffectProfiles = GameplayFfbEffectProfile.CreateCategoryDefaults(this);
    }

    public bool Enabled { get; set; } = true;

    public string WheelProfileId { get; set; } = WheelProfileCatalog.LogitechMomoRacingId;

    public string DeviceHapticProfileName { get; set; } = "Logitech Momo Racing";

    public TireSurfaceTuningSettings TireSurfaceTuning { get; set; } = new();

    public Dictionary<string, GameplayFfbEffectProfile> VehicleCategoryEffectProfiles { get; set; }

    public Dictionary<string, VehicleCategoryFfbProfile> VehicleCategoryProfiles { get; set; } = VehicleCategoryFfbProfile.CreateDefaults();
}

internal sealed record LogitechMomoCategoryValues(
    int SpeedSpring,
    FfbCurveKind SpeedSpringCurve,
    int MechanicalFriction,
    FfbCurveKind MechanicalFrictionCurve,
    int SpeedDamper,
    FfbCurveKind SpeedDamperCurve,
    int SurfaceFeedback,
    FfbCurveKind SurfaceFeedbackCurve,
    int SlipFeedback,
    FfbCurveKind SlipFeedbackCurve,
    int Wetness,
    FfbCurveKind WetnessCurve,
    int Bump,
    FfbCurveKind BumpCurve,
    int SuspensionHit,
    FfbCurveKind SuspensionHitCurve,
    int CollisionPulse,
    FfbCurveKind CollisionPulseCurve,
    int LandingPulse,
    FfbCurveKind LandingPulseCurve,
    int TerrainRumble,
    FfbCurveKind TerrainRumbleCurve,
    int LoadResistance,
    FfbCurveKind LoadResistanceCurve,
    int Motion,
    FfbCurveKind MotionCurve,
    int HillStandstillLoad,
    FfbCurveKind HillStandstillLoadCurve,
    int SideSlopeBias,
    FfbCurveKind SideSlopeBiasCurve,
    int ImplementBias,
    FfbCurveKind ImplementBiasCurve,
    int RpmIdle,
    int RpmLoad,
    int LuggingBoost,
    int GearShiftPulse,
    FfbCurveKind GearShiftPulseCurve,
    int ClutchBrakeJerk,
    FfbCurveKind ClutchBrakeJerkCurve,
    int EngineStartStop,
    FfbCurveKind EngineStartStopCurve,
    int EngineCapPercent,
    int GearCooldownMs)
{
    public static LogitechMomoCategoryValues For(string category)
    {
        return category switch
        {
            VehicleCategoryFfbProfile.TractorTracked => new(
                42, S, 38, S, 45, L, 28, S, 18, S, 24, S, 20, S, 24, S, 42, A, 28, A, 38, S, 48, S,
                18, S, 38, S, 22, S, 36, S, 16, 28, 28, 18, A, 16, A, 24, A, 42, 550),
            VehicleCategoryFfbProfile.Harvester => new(
                48, S, 34, S, 42, L, 25, S, 18, S, 22, S, 18, S, 24, S, 40, A, 26, A, 30, S, 42, S,
                22, S, 30, S, 28, S, 26, S, 20, 32, 24, 16, A, 14, A, 26, A, 45, 650),
            VehicleCategoryFfbProfile.Truck => new(
                45, S, 24, L, 38, L, 22, S, 24, A, 24, S, 22, S, 28, A, 45, A, 30, A, 22, S, 30, S,
                22, S, 24, S, 18, S, 18, S, 16, 24, 20, 26, A, 26, A, 22, A, 38, 400),
            VehicleCategoryFfbProfile.LoaderTelehandler => new(
                50, S, 36, S, 34, L, 35, S, 30, A, 30, S, 28, A, 36, A, 48, A, 36, A, 36, S, 45, S,
                28, S, 34, S, 30, S, 32, S, 18, 30, 28, 22, A, 24, A, 26, A, 45, 450),
            VehicleCategoryFfbProfile.LightVehicle => new(
                38, S, 18, L, 28, L, 30, S, 34, A, 28, S, 30, A, 34, A, 40, A, 34, A, 28, S, 18, S,
                30, S, 16, S, 22, S, 10, S, 14, 20, 16, 24, A, 24, A, 18, A, 35, 350),
            VehicleCategoryFfbProfile.Unknown => new(
                45, S, 25, S, 32, L, 28, S, 24, A, 25, S, 22, S, 28, A, 40, A, 30, A, 28, S, 32, S,
                22, S, 25, S, 22, S, 24, S, 16, 25, 22, 20, A, 20, A, 22, A, 40, 500),
            _ => new(
                55, S, 28, S, 35, L, 32, S, 26, A, 30, S, 24, S, 32, A, 45, A, 34, A, 30, S, 40, S,
                24, S, 34, S, 26, S, 34, S, 18, 30, 30, 24, A, 22, A, 26, A, 45, 450)
        };
    }

    private static FfbCurveKind S => FfbCurveKind.Smooth;
    private static FfbCurveKind L => FfbCurveKind.Linear;
    private static FfbCurveKind A => FfbCurveKind.Aggressive;
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
