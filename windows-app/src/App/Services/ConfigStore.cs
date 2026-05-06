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
            return Normalize(JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig());
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
        return config;
    }
}
