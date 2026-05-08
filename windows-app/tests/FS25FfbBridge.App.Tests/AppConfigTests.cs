using System.Text.Json;
using FS25FfbBridge.App.Models;
using FS25FfbBridge.App.Services;

namespace FS25FfbBridge.App.Tests;

public sealed class AppConfigTests
{
    [Fact]
    public void Defaults_use_current_gameplay_effect_profile()
    {
        var config = new AppConfig();

        Assert.Equal(12, config.EffectsProfileVersion);
        Assert.Equal("Logitech MOMO Racing Wheel", config.DeviceProfileName);
        Assert.Equal(270, config.RotationDegrees);
        Assert.Equal(40, config.GlobalForceLimitPercent);
        Assert.Equal(35, config.DeviceForceLimitPercent);
        Assert.Equal(125, config.TelemetryFfbUpdateRateHz);
        Assert.Equal(100, config.TelemetryUiRefreshMs);
        Assert.True(config.GameplayFfb.Enabled);
        Assert.True(config.GameplayFfb.SpeedSpring.Enabled);
        Assert.True(config.GameplayFfb.SpeedDamper.Enabled);
        Assert.True(config.GameplayFfb.MechanicalFriction.Enabled);
        Assert.Equal(75, config.GameplayFfb.SpeedSpring.StrengthPercent);
        Assert.Equal(80, config.GameplayFfb.SpeedSpring.MaxOutputPercent);
        Assert.Equal(28, config.GameplayFfb.SpeedSpring.SpeedReferenceKmh);
        Assert.Equal(0.22, config.GameplayFfb.SpeedSpring.StandstillFloor);
        Assert.Equal(FfbCurveKind.Aggressive, config.GameplayFfb.SpeedSpring.Curve);
        Assert.Equal(70, config.GameplayFfb.SpeedDamper.StrengthPercent);
        Assert.Equal(65, config.GameplayFfb.SpeedDamper.MaxOutputPercent);
        Assert.Equal(55, config.GameplayFfb.SpeedDamper.SpeedReferenceKmh);
        Assert.Equal(38, config.GameplayFfb.MechanicalFriction.StrengthPercent);
        Assert.Equal(65, config.GameplayFfb.MechanicalFriction.MaxOutputPercent);
        Assert.Equal(0.18, config.GameplayFfb.MechanicalFriction.BaseFriction);
        Assert.Equal(55, config.GameplayFfb.LoadResistance.StrengthPercent);
        Assert.Equal(65, config.GameplayFfb.LoadResistance.MaxOutputPercent);
        Assert.True(config.GameplayFfb.LoadResistance.AffectsFriction);
        Assert.Equal(31, config.GameplayFfb.EngineVibration.StrengthPercent);
        Assert.Equal(65, config.GameplayFfb.EngineVibration.MaxOutputPercent);
        Assert.Equal(12, config.GameplayFfb.EngineVibration.MinFrequencyHz);
        Assert.Equal(30, config.GameplayFfb.EngineVibration.MaxFrequencyHz);
        Assert.Equal(35, config.GameplayFfb.SurfaceFeedback.StrengthPercent);
        Assert.Equal(65, config.GameplayFfb.SurfaceFeedback.MaxOutputPercent);
        Assert.Equal(0.2, config.GameplayFfb.SurfaceFeedback.MinSpeedKmh);
        Assert.Equal(8, config.GameplayFfb.SurfaceFeedback.FieldFrequencyMinHz);
        Assert.Equal(24, config.GameplayFfb.SurfaceFeedback.FieldFrequencyMaxHz);
        Assert.True(config.GameplayFfb.SlipFeedback.Enabled);
        Assert.Equal(31, config.GameplayFfb.SlipFeedback.StrengthPercent);
        Assert.Equal(65, config.GameplayFfb.SlipFeedback.MaxOutputPercent);
        Assert.Equal(0.12, config.GameplayFfb.SlipFeedback.MinSlip);
        Assert.Equal(0.5, config.GameplayFfb.SlipFeedback.MinSpeedKmh);
        Assert.True(config.GameplayFfb.WetnessFeedback.Enabled);
        Assert.Equal(22, config.GameplayFfb.WetnessFeedback.StrengthPercent);
        Assert.Equal(65, config.GameplayFfb.WetnessFeedback.MaxOutputPercent);
        Assert.True(config.GameplayFfb.MotionFeedback.Enabled);
        Assert.Equal(16, config.GameplayFfb.MotionFeedback.StrengthPercent);
        Assert.Equal(65, config.GameplayFfb.MotionFeedback.MaxOutputPercent);
        Assert.True(config.GameplayFfb.BumpFeedback.Enabled);
        Assert.Equal(34, config.GameplayFfb.BumpFeedback.StrengthPercent);
        Assert.Equal(65, config.GameplayFfb.BumpFeedback.MaxOutputPercent);
        Assert.Equal(0.28, config.GameplayFfb.BumpFeedback.MinImpulse);
        Assert.Equal(65, config.GameplayFfb.BumpFeedback.DurationMs);
        Assert.Equal(150, config.GameplayFfb.BumpFeedback.CooldownMs);
        Assert.Equal(30, config.GameplayFfb.SuspensionHitFeedback.StrengthPercent);
        Assert.Equal(34, config.GameplayFfb.LandingFeedback.StrengthPercent);
        Assert.Equal(40, config.GameplayFfb.CollisionFeedback.StrengthPercent);
        Assert.Equal(28, config.GameplayFfb.TerrainRumble.StrengthPercent);
        Assert.Equal(18, config.GameplayFfb.DrivetrainPulse.StrengthPercent);
        Assert.Contains(VehicleCategoryFfbProfile.TractorWheeled, config.GameplayFfb.VehicleCategoryEffectProfiles.Keys);
        Assert.DoesNotContain(VehicleCategoryFfbProfile.HeavyTractorTracked, config.GameplayFfb.VehicleCategoryEffectProfiles.Keys);
        Assert.All(config.GameplayFfb.VehicleCategoryEffectProfiles.Values, profile =>
        {
            Assert.Equal(75, profile.SpeedSpring.StrengthPercent);
            Assert.Equal(80, profile.SpeedSpring.MaxOutputPercent);
            Assert.Equal(28, profile.SpeedSpring.SpeedReferenceKmh);
            Assert.Equal(0.22, profile.SpeedSpring.StandstillFloor);
            Assert.Equal(FfbCurveKind.Aggressive, profile.SpeedSpring.Curve);
        });
        Assert.Equal(68, config.GameplayFfb.VehicleCategoryEffectProfiles[VehicleCategoryFfbProfile.TractorTracked].SpeedDamper.StrengthPercent);
    }

    [Fact]
    public void Effect_settings_round_trip_through_json()
    {
        var config = new AppConfig();
        config.GameplayFfb.SpeedSpring.StrengthPercent = 42;
        config.GameplayFfb.MechanicalFriction.BaseFriction = 0.25;
        config.GameplayFfb.SlipFeedback.MinSlip = 0.2;
        config.GameplayFfb.BumpFeedback.DurationMs = 120;
        config.GameplayFfb.SurfaceFeedback.Enabled = false;

        var json = JsonSerializer.Serialize(config);
        var decoded = JsonSerializer.Deserialize<AppConfig>(json);

        Assert.NotNull(decoded);
        Assert.Equal(42, decoded!.GameplayFfb.SpeedSpring.StrengthPercent);
        Assert.Equal(0.25, decoded.GameplayFfb.MechanicalFriction.BaseFriction);
        Assert.Equal(0.2, decoded.GameplayFfb.SlipFeedback.MinSlip);
        Assert.Equal(120, decoded.GameplayFfb.BumpFeedback.DurationMs);
        Assert.False(decoded.GameplayFfb.SurfaceFeedback.Enabled);
    }

    [Fact]
    public void Config_v4_migration_preserves_user_effect_settings_and_adds_category_effect_profiles()
    {
        var path = Path.Combine(Path.GetTempPath(), "FS25FfbBridge.Tests", Guid.NewGuid().ToString("N"), "config.json");
        var store = new ConfigStore(path);
        var oldConfig = new AppConfig
        {
            EffectsProfileVersion = 4
        };
        oldConfig.GameplayFfb.SpeedSpring.StrengthPercent = 42;
        oldConfig.GameplayFfb.SpeedDamper.MaxOutputPercent = 51;
        oldConfig.GameplayFfb.VehicleCategoryProfiles = [];

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(oldConfig));

        var migrated = store.Load();

        Assert.Equal(12, migrated.EffectsProfileVersion);
        Assert.Equal(75, migrated.GameplayFfb.SpeedSpring.StrengthPercent);
        Assert.Equal(45, migrated.GameplayFfb.SpeedDamper.StrengthPercent);
        Assert.Equal(80, migrated.GameplayFfb.SpeedDamper.MaxOutputPercent);
        Assert.Contains(VehicleCategoryFfbProfile.TractorTracked, migrated.GameplayFfb.VehicleCategoryEffectProfiles.Keys);
        Assert.Contains(VehicleCategoryFfbProfile.Unknown, migrated.GameplayFfb.VehicleCategoryEffectProfiles.Keys);
        Assert.Equal(75, migrated.GameplayFfb.VehicleCategoryEffectProfiles[VehicleCategoryFfbProfile.TractorWheeled].SpeedSpring.StrengthPercent);
    }

    [Fact]
    public void Config_v5_migration_applies_legacy_category_multipliers_once()
    {
        var path = Path.Combine(Path.GetTempPath(), "FS25FfbBridge.Tests", Guid.NewGuid().ToString("N"), "config.json");
        var store = new ConfigStore(path);
        var oldConfig = new AppConfig
        {
            EffectsProfileVersion = 5
        };
        oldConfig.GameplayFfb.SpeedDamper.StrengthPercent = 50;
        oldConfig.GameplayFfb.VehicleCategoryProfiles[VehicleCategoryFfbProfile.Truck].SpeedDamperStrengthMultiplier = 1.4;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(oldConfig));

        var migrated = store.Load();
        store.Save(migrated);
        var loadedAgain = store.Load();

        Assert.Equal(12, loadedAgain.EffectsProfileVersion);
        Assert.Equal(57, loadedAgain.GameplayFfb.VehicleCategoryEffectProfiles[VehicleCategoryFfbProfile.Truck].SpeedDamper.StrengthPercent);
        Assert.Equal(41, loadedAgain.GameplayFfb.SpeedDamper.StrengthPercent);
    }

    [Fact]
    public void Config_v7_migration_restores_previous_speed_spring_to_every_profile()
    {
        var path = Path.Combine(Path.GetTempPath(), "FS25FfbBridge.Tests", Guid.NewGuid().ToString("N"), "config.json");
        var store = new ConfigStore(path);
        var oldConfig = new AppConfig
        {
            EffectsProfileVersion = 7
        };
        oldConfig.GameplayFfb.SpeedSpring.StrengthPercent = 90;
        oldConfig.GameplayFfb.VehicleCategoryEffectProfiles[VehicleCategoryFfbProfile.Truck].SpeedSpring.StrengthPercent = 90;
        oldConfig.GameplayFfb.VehicleCategoryEffectProfiles[VehicleCategoryFfbProfile.LightVehicle].SpeedSpring.MaxOutputPercent = 10;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(oldConfig));

        var migrated = store.Load();

        Assert.Equal(12, migrated.EffectsProfileVersion);
        Assert.Equal(75, migrated.GameplayFfb.SpeedSpring.StrengthPercent);
        Assert.All(migrated.GameplayFfb.VehicleCategoryEffectProfiles.Values, profile =>
        {
            Assert.Equal(75, profile.SpeedSpring.StrengthPercent);
            Assert.Equal(80, profile.SpeedSpring.MaxOutputPercent);
            Assert.Equal(28, profile.SpeedSpring.SpeedReferenceKmh);
            Assert.Equal(0.22, profile.SpeedSpring.StandstillFloor);
            Assert.Equal(FfbCurveKind.Aggressive, profile.SpeedSpring.Curve);
        });
    }

    [Fact]
    public void Config_v10_migration_updates_suspension_terrain_settings_and_adds_split_pulse_settings()
    {
        var path = Path.Combine(Path.GetTempPath(), "FS25FfbBridge.Tests", Guid.NewGuid().ToString("N"), "config.json");
        var store = new ConfigStore(path);
        var oldConfig = new AppConfig
        {
            EffectsProfileVersion = 10
        };
        oldConfig.GameplayFfb.BumpFeedback.MinImpulse = 0.12;
        oldConfig.GameplayFfb.BumpFeedback.CooldownMs = 90;
        oldConfig.GameplayFfb.BumpFeedback.StrengthPercent = 44;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(oldConfig));

        var migrated = store.Load();

        Assert.Equal(12, migrated.EffectsProfileVersion);
        Assert.Equal(0.28, migrated.GameplayFfb.BumpFeedback.MinImpulse);
        Assert.Equal(150, migrated.GameplayFfb.BumpFeedback.CooldownMs);
        Assert.Equal(34, migrated.GameplayFfb.BumpFeedback.StrengthPercent);
        Assert.True(migrated.GameplayFfb.SuspensionHitFeedback.Enabled);
        Assert.True(migrated.GameplayFfb.LandingFeedback.Enabled);
        Assert.True(migrated.GameplayFfb.CollisionFeedback.Enabled);
        Assert.True(migrated.GameplayFfb.TerrainRumble.Enabled);
        Assert.True(migrated.GameplayFfb.DrivetrainPulse.Enabled);
    }

    [Fact]
    public void Config_v11_migration_applies_suspension_terrain_preset_to_every_profile()
    {
        var path = Path.Combine(Path.GetTempPath(), "FS25FfbBridge.Tests", Guid.NewGuid().ToString("N"), "config.json");
        var store = new ConfigStore(path);
        var oldConfig = new AppConfig
        {
            EffectsProfileVersion = 11
        };
        oldConfig.GameplayFfb.BumpFeedback.StrengthPercent = 44;
        oldConfig.GameplayFfb.TerrainRumble.StrengthPercent = 12;
        oldConfig.GameplayFfb.VehicleCategoryEffectProfiles[VehicleCategoryFfbProfile.Truck].BumpFeedback.MinImpulse = 0.10;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(oldConfig));

        var migrated = store.Load();

        Assert.Equal(12, migrated.EffectsProfileVersion);
        Assert.Equal(34, migrated.GameplayFfb.BumpFeedback.StrengthPercent);
        Assert.Equal(28, migrated.GameplayFfb.TerrainRumble.StrengthPercent);
        Assert.All(migrated.GameplayFfb.VehicleCategoryEffectProfiles.Values, profile =>
        {
            Assert.Equal(0.28, profile.BumpFeedback.MinImpulse);
            Assert.Equal(0.26, profile.SuspensionHitFeedback.MinImpulse);
            Assert.Equal(28, profile.TerrainRumble.StrengthPercent);
        });
    }
}
