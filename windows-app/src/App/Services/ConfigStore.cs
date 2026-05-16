using System.Text.Json;
using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Services;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ConfigStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FS25FFBBridge",
            "config.json"))
    {
    }

    public ConfigStore(string configPath)
    {
        ConfigPath = configPath;
    }

    public string ConfigPath { get; }

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return new AppConfig();
            }

            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            var previousEffectsProfileVersion = config.EffectsProfileVersion;
            config = Normalize(config);
            if (previousEffectsProfileVersion < config.EffectsProfileVersion)
            {
                Save(config);
            }

            return config;
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions));
    }

    internal static AppConfig Normalize(AppConfig config)
    {
        config.TelemetryTransportMode = NormalizeTelemetryTransportMode(config.TelemetryTransportMode);
        config.TelemetryFfbUpdateRateHz = NormalizeTelemetryRate(config.TelemetryFfbUpdateRateHz);
        config.GameplayFfb ??= new GameplayFfbSettings();
        GameplayFfbEffectProfile.NormalizeEffectSettings(config.GameplayFfb);
        config.GameplayFfb.VehicleCategoryProfiles = NormalizeVehicleCategoryProfiles(config.GameplayFfb.VehicleCategoryProfiles);
        config.GameplayFfb.TireSurfaceTuning = TireSurfaceTuningSettings.CreateNormalized(config.GameplayFfb.TireSurfaceTuning);
        if (config.EffectsProfileVersion < 4)
        {
            config.GameplayFfb.SurfaceFeedback.MinSpeedKmh = Math.Min(config.GameplayFfb.SurfaceFeedback.MinSpeedKmh, 0.2);
            config.GameplayFfb.SlipFeedback.MinSpeedKmh = Math.Min(config.GameplayFfb.SlipFeedback.MinSpeedKmh, 0.5);
        }

        if (config.EffectsProfileVersion < 3)
        {
            config.EffectsProfileVersion = 3;
        }

        if (config.EffectsProfileVersion < 4)
        {
            config.EffectsProfileVersion = 4;
        }

        if (config.EffectsProfileVersion < 5)
        {
            config.EffectsProfileVersion = 5;
        }

        if (config.EffectsProfileVersion < 6)
        {
            config.GameplayFfb.VehicleCategoryEffectProfiles =
                GameplayFfbEffectProfile.CreateCategoryProfiles(
                    config.GameplayFfb,
                    config.GameplayFfb.VehicleCategoryProfiles,
                    applyLegacyMultipliers: true);
            config.EffectsProfileVersion = 6;
        }
        else
        {
            config.GameplayFfb.VehicleCategoryEffectProfiles =
                NormalizeVehicleCategoryEffectProfiles(config.GameplayFfb.VehicleCategoryEffectProfiles, config.GameplayFfb);
        }

        if (config.EffectsProfileVersion < 8)
        {
            GameplayFfbEffectProfile.ApplyCurrentSpeedSpringPreset(config.GameplayFfb);
            foreach (var profile in config.GameplayFfb.VehicleCategoryEffectProfiles.Values)
            {
                GameplayFfbEffectProfile.NormalizeEffectSettings(profile);
                GameplayFfbEffectProfile.ApplyCurrentSpeedSpringPreset(profile);
            }

            config.EffectsProfileVersion = 8;
        }

        if (config.EffectsProfileVersion < 9)
        {
            GameplayFfbEffectProfile.ApplyOverallOutputCap(
                config.GameplayFfb,
                config.GameplayFfb.SpeedSpring.MaxOutputPercent);
            foreach (var profile in config.GameplayFfb.VehicleCategoryEffectProfiles.Values)
            {
                GameplayFfbEffectProfile.NormalizeEffectSettings(profile);
                GameplayFfbEffectProfile.ApplyOverallOutputCap(profile, profile.SpeedSpring.MaxOutputPercent);
            }

            config.EffectsProfileVersion = 9;
        }

        if (config.EffectsProfileVersion < 10)
        {
            if (string.IsNullOrWhiteSpace(config.GameplayFfb.DeviceHapticProfileName))
            {
                config.GameplayFfb.DeviceHapticProfileName = config.DeviceProfileName;
            }

            config.EffectsProfileVersion = 10;
        }

        if (config.EffectsProfileVersion < 11)
        {
            GameplayFfbEffectProfile.NormalizeEffectSettings(config.GameplayFfb);
            foreach (var profile in config.GameplayFfb.VehicleCategoryEffectProfiles.Values)
            {
                GameplayFfbEffectProfile.NormalizeEffectSettings(profile);
            }

            config.EffectsProfileVersion = 11;
        }

        if (config.EffectsProfileVersion < 12)
        {
            GameplayFfbEffectProfile.NormalizeEffectSettings(config.GameplayFfb);
            GameplayFfbEffectProfile.ApplyCurrentSuspensionTerrainPreset(config.GameplayFfb);
            foreach (var profile in config.GameplayFfb.VehicleCategoryEffectProfiles.Values)
            {
                GameplayFfbEffectProfile.NormalizeEffectSettings(profile);
                GameplayFfbEffectProfile.ApplyCurrentSuspensionTerrainPreset(profile);
            }

            config.EffectsProfileVersion = 12;
        }

        if (config.EffectsProfileVersion < 13)
        {
            GameplayFfbEffectProfile.NormalizeEffectSettings(config.GameplayFfb);
            GameplayFfbEffectProfile.ApplyCurrentSuspensionTerrainPreset(config.GameplayFfb);
            foreach (var profile in config.GameplayFfb.VehicleCategoryEffectProfiles.Values)
            {
                GameplayFfbEffectProfile.NormalizeEffectSettings(profile);
                GameplayFfbEffectProfile.ApplyCurrentSuspensionTerrainPreset(profile);
            }

            config.EffectsProfileVersion = 13;
        }

        if (config.EffectsProfileVersion < 14)
        {
            config.GameplayFfb.TireSurfaceTuning = TireSurfaceTuningSettings.CreateNormalized(config.GameplayFfb.TireSurfaceTuning);
            config.EffectsProfileVersion = 14;
        }

        if (config.EffectsProfileVersion < 15)
        {
            GameplayFfbEffectProfile.NormalizeEffectSettings(config.GameplayFfb);
            foreach (var profile in config.GameplayFfb.VehicleCategoryEffectProfiles.Values)
            {
                GameplayFfbEffectProfile.NormalizeEffectSettings(profile);
            }

            config.EffectsProfileVersion = 15;
        }

        if (config.EffectsProfileVersion < 16)
        {
            var wheelProfile = WheelProfileCatalog.Resolve(config.GameplayFfb.DeviceHapticProfileName);
            if (wheelProfile.Id == WheelProfileCatalog.GenericId)
            {
                wheelProfile = WheelProfileCatalog.Resolve(config.DeviceProfileName);
            }

            if (wheelProfile.Id == WheelProfileCatalog.GenericId)
            {
                wheelProfile = WheelProfileCatalog.Resolve(config.GameplayFfb.WheelProfileId);
            }

            ApplyWheelProfile(config, wheelProfile);
            config.EffectsProfileVersion = 16;
        }

        if (config.EffectsProfileVersion < 17)
        {
            if (string.Equals(config.GameplayFfb.WheelProfileId, WheelProfileCatalog.LogitechMomoRacingId, StringComparison.OrdinalIgnoreCase))
            {
                GameplayFfbEffectProfile.ApplyLogitechMomoRacingPreset(config.GameplayFfb);
                config.GameplayFfb.VehicleCategoryEffectProfiles = GameplayFfbEffectProfile.CreateLogitechMomoRacingCategoryDefaults();
                config.GameplayFfb.TireSurfaceTuning = new TireSurfaceTuningSettings();
            }

            config.EffectsProfileVersion = 17;
        }

        return config;
    }

    private static void ApplyWheelProfile(AppConfig config, WheelProfile wheelProfile)
    {
        config.WheelProfileId = wheelProfile.Id;
        config.DeviceProfileName = wheelProfile.DisplayName;
        config.RotationDegrees = wheelProfile.RotationDegrees;
        config.RecommendedMode = wheelProfile.RecommendedMode;
        config.GameplayFfb.WheelProfileId = wheelProfile.Id;
        config.GameplayFfb.DeviceHapticProfileName = wheelProfile.DisplayName;
    }

    private static string NormalizeTelemetryTransportMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "udp" => "udp",
            "file+udp" => "file+udp",
            _ => "file"
        };
    }

    private static int NormalizeTelemetryRate(int value)
    {
        return value is 1 or 10 or 30 or 60 ? value : 60;
    }

    private static Dictionary<string, VehicleCategoryFfbProfile> NormalizeVehicleCategoryProfiles(
        Dictionary<string, VehicleCategoryFfbProfile>? profiles)
    {
        var normalized = VehicleCategoryFfbProfile.CreateDefaults();
        if (profiles is null)
        {
            return normalized;
        }

        foreach (var (key, profile) in profiles)
        {
            if (!string.IsNullOrWhiteSpace(key) && profile is not null)
            {
                normalized[key] = profile;
            }
        }

        return normalized;
    }

    private static Dictionary<string, GameplayFfbEffectProfile> NormalizeVehicleCategoryEffectProfiles(
        Dictionary<string, GameplayFfbEffectProfile>? profiles,
        GameplayFfbEffectProfile baseProfile)
    {
        var normalized = GameplayFfbEffectProfile.CreateCategoryProfiles(
            baseProfile,
            VehicleCategoryFfbProfile.CreateDefaults(),
            applyLegacyMultipliers: false);
        if (profiles is null)
        {
            return normalized;
        }

        foreach (var (key, profile) in profiles)
        {
            if (!string.IsNullOrWhiteSpace(key) && profile is not null)
            {
                GameplayFfbEffectProfile.NormalizeEffectSettings(profile);
                normalized[key] = profile;
            }
        }

        return normalized;
    }
}
