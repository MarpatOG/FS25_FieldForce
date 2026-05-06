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

    private static AppConfig Normalize(AppConfig config)
    {
        config.GameplayFfb ??= new GameplayFfbSettings();
        config.GameplayFfb.SpeedSpring ??= new SpeedConditionSettings();
        config.GameplayFfb.SpeedDamper ??= new SpeedConditionSettings();
        config.GameplayFfb.MechanicalFriction ??= new MechanicalFrictionSettings();
        config.GameplayFfb.LoadResistance ??= new LoadResistanceSettings();
        config.GameplayFfb.EngineVibration ??= new EngineVibrationSettings();
        config.GameplayFfb.SurfaceFeedback ??= new SurfaceFeedbackSettings();
        config.GameplayFfb.SlipFeedback ??= new SlipFeedbackSettings();
        config.GameplayFfb.WetnessFeedback ??= new WetnessFeedbackSettings();
        config.GameplayFfb.MotionFeedback ??= new MotionFeedbackSettings();
        config.GameplayFfb.BumpFeedback ??= new BumpFeedbackSettings();
        config.GameplayFfb.VehicleCategoryProfiles = NormalizeVehicleCategoryProfiles(config.GameplayFfb.VehicleCategoryProfiles);
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

        return config;
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
}
