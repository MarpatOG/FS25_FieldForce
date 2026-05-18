using System.Text.Json;
using FieldForce.App.Models;
using FieldForce.App.Services;

namespace FieldForce.App.Tests;

public sealed class WheelProfileCatalogTests
{
    [Theory]
    [InlineData("Logitech MOMO Racing USB", WheelProfileCatalog.LogitechMomoRacingId)]
    [InlineData("Logitech Driving Force GT USB", "logitech-driving-force-gt")]
    [InlineData("Logitech Driving Force Pro", "logitech-driving-force-pro")]
    [InlineData("Logitech Driving Force EX", "logitech-driving-force-ex")]
    [InlineData("Logitech G25 Racing Wheel USB", "logitech-g25")]
    [InlineData("Logitech G27 Racing Wheel USB", "logitech-g27")]
    [InlineData("Logitech G29 Driving Force Racing Wheel", "logitech-g29")]
    [InlineData("Logitech G920 Driving Force Racing Wheel", "logitech-g920")]
    [InlineData("Logitech G923 TRUEFORCE Racing Wheel", "logitech-g923")]
    public void Catalog_matches_logitech_aliases(string directInputName, string expectedId)
    {
        var profile = WheelProfileCatalog.Resolve(directInputName);

        Assert.Equal(expectedId, profile.Id);
    }

    [Fact]
    public void Catalog_falls_back_to_generic_for_unknown_wheel()
    {
        var profile = WheelProfileCatalog.Resolve("Unknown USB Steering Device");

        Assert.Equal(WheelProfileCatalog.GenericId, profile.Id);
        Assert.Equal(DeviceHapticProfile.Generic, profile.Haptics);
    }

    [Fact]
    public void Effects_profile_path_is_stable_by_wheel_profile_id()
    {
        var store = new EffectsProfileStore(Path.Combine(Path.GetTempPath(), "FieldForce.Tests", Guid.NewGuid().ToString("N")));

        var path = store.GetProfilePath("logitech-g29");

        Assert.EndsWith(Path.Combine("logitech-g29.json"), path);
        Assert.DoesNotContain("Logitech G29", path);
    }

    [Fact]
    public void Legacy_device_haptic_profile_name_migrates_to_wheel_profile_id()
    {
        var path = Path.Combine(Path.GetTempPath(), "FieldForce.Tests", Guid.NewGuid().ToString("N"), "config.json");
        var oldConfig = new AppConfig
        {
            EffectsProfileVersion = 15,
            DeviceProfileName = "Logitech G29 Driving Force Racing Wheel"
        };
        oldConfig.GameplayFfb.DeviceHapticProfileName = "Logitech G29 Driving Force Racing Wheel";

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(oldConfig));

        var migrated = new ConfigStore(path).Load();

        Assert.Equal(AppConfig.CurrentEffectsProfileVersion, migrated.EffectsProfileVersion);
        Assert.Equal("logitech-g29", migrated.WheelProfileId);
        Assert.Equal("logitech-g29", migrated.GameplayFfb.WheelProfileId);
        Assert.Equal("Logitech G29", migrated.GameplayFfb.DeviceHapticProfileName);
    }

    [Fact]
    public void Legacy_display_name_effect_profile_is_copied_to_stable_id_path()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FieldForce.Tests", Guid.NewGuid().ToString("N"));
        var store = new EffectsProfileStore(directory);
        Directory.CreateDirectory(directory);
        var legacyPath = Path.Combine(directory, "Logitech G27 Racing Wheel.json");
        var legacy = new
        {
            EffectsProfileVersion = 15,
            DeviceProfileName = "Logitech G27 Racing Wheel",
            GameplayFfb = new GameplayFfbSettings
            {
                DeviceHapticProfileName = "Logitech G27 Racing Wheel",
                SpeedSpring = { StrengthPercent = 33 }
            }
        };
        File.WriteAllText(legacyPath, JsonSerializer.Serialize(legacy));

        var fallback = new GameplayFfbSettings
        {
            WheelProfileId = "logitech-g27",
            DeviceHapticProfileName = "Logitech G27 Racing Wheel"
        };
        var loaded = store.Load("logitech-g27", fallback);

        Assert.Equal("logitech-g27", loaded.WheelProfileId);
        Assert.Equal(33, loaded.SpeedSpring.StrengthPercent);
        Assert.True(File.Exists(Path.Combine(directory, "logitech-g27.json")));
    }

    [Fact]
    public void Legacy_effect_profile_directory_is_migrated_to_current_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "FieldForce.Tests", Guid.NewGuid().ToString("N"));
        var currentDirectory = Path.Combine(root, "FieldForce", "effect-profiles");
        var legacyDirectory = Path.Combine(root, "FS25FFBBridge", "effect-profiles");
        var store = new EffectsProfileStore(currentDirectory, legacyDirectory);
        Directory.CreateDirectory(legacyDirectory);
        var legacyPath = Path.Combine(legacyDirectory, "logitech-g27.json");
        var legacy = new
        {
            EffectsProfileVersion = AppConfig.CurrentEffectsProfileVersion,
            WheelProfileId = "logitech-g27",
            DeviceProfileName = "Logitech G27",
            GameplayFfb = new GameplayFfbSettings
            {
                WheelProfileId = "logitech-g27",
                DeviceHapticProfileName = "Logitech G27",
                SpeedDamper = { StrengthPercent = 44 }
            }
        };
        File.WriteAllText(legacyPath, JsonSerializer.Serialize(legacy));

        var loaded = store.Load("logitech-g27", new GameplayFfbSettings { WheelProfileId = "logitech-g27" });

        Assert.Equal(44, loaded.SpeedDamper.StrengthPercent);
        Assert.True(File.Exists(Path.Combine(currentDirectory, "logitech-g27.json")));
    }
}
