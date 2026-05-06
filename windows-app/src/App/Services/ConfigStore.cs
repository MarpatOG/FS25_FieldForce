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

    public string ConfigPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FS25FFBBridge",
        "config.json");

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
        config.GameplayFfb.SpeedSpring ??= new FfbEffectSettings();
        config.GameplayFfb.SpeedDamper ??= new FfbEffectSettings();
        config.GameplayFfb.LoadResistance ??= new LoadResistanceSettings();
        config.GameplayFfb.EngineVibration ??= new EngineVibrationSettings();
        config.GameplayFfb.SurfaceFeedback ??= new SurfaceFeedbackSettings();
        if (config.EffectsProfileVersion < 2)
        {
            ApplyMvpMomoDefaults(config);
        }

        return config;
    }

    private static void ApplyMvpMomoDefaults(AppConfig config)
    {
        config.EffectsProfileVersion = 2;
        config.GlobalForceLimitPercent = Math.Max(config.GlobalForceLimitPercent, 45);
        config.DeviceForceLimitPercent = Math.Max(config.DeviceForceLimitPercent, 45);
        config.GameplayFfb.SpeedSpring.StrengthPercent = 85;
        config.GameplayFfb.SpeedSpring.MaxOutputPercent = 90;
        config.GameplayFfb.SpeedDamper.StrengthPercent = 90;
        config.GameplayFfb.SpeedDamper.MaxOutputPercent = 95;
        config.GameplayFfb.LoadResistance.StrengthPercent = 65;
        config.GameplayFfb.LoadResistance.MaxOutputPercent = 65;
        config.GameplayFfb.EngineVibration.StrengthPercent = 55;
        config.GameplayFfb.EngineVibration.MaxOutputPercent = 55;
        config.GameplayFfb.SurfaceFeedback.StrengthPercent = 60;
        config.GameplayFfb.SurfaceFeedback.MaxOutputPercent = 60;
    }
}
