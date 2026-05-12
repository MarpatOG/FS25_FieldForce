using System.Text.Json;
using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Services;

public sealed class EffectsProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public EffectsProfileStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FS25FFBBridge",
            "effect-profiles"))
    {
    }

    public EffectsProfileStore(string profilesDirectory)
    {
        ProfilesDirectory = profilesDirectory;
    }

    public string ProfilesDirectory { get; }

    public string GetProfilePath(string? deviceProfileName)
    {
        var fileName = SanitizeFileName(string.IsNullOrWhiteSpace(deviceProfileName)
            ? "Unknown FFB Wheel"
            : deviceProfileName);
        return Path.Combine(ProfilesDirectory, $"{fileName}.json");
    }

    public GameplayFfbSettings Load(string? deviceProfileName, GameplayFfbSettings fallback)
    {
        var path = GetProfilePath(deviceProfileName);
        try
        {
            if (File.Exists(path))
            {
                var profile = JsonSerializer.Deserialize<WheelEffectsProfile>(File.ReadAllText(path), JsonOptions);
                if (profile?.GameplayFfb is not null)
                {
                    return Normalize(profile.GameplayFfb, deviceProfileName, profile.EffectsProfileVersion);
                }
            }
        }
        catch
        {
            // Fall back to the main config copy when a wheel profile is unreadable.
        }

        var cloned = Clone(fallback);
        cloned.DeviceHapticProfileName = string.IsNullOrWhiteSpace(deviceProfileName)
            ? cloned.DeviceHapticProfileName
            : deviceProfileName!;
        var normalized = Normalize(cloned, cloned.DeviceHapticProfileName, AppConfig.CurrentEffectsProfileVersion);
        Save(normalized.DeviceHapticProfileName, normalized);
        return normalized;
    }

    public void Save(string? deviceProfileName, GameplayFfbSettings settings)
    {
        Directory.CreateDirectory(ProfilesDirectory);
        var profile = new WheelEffectsProfile
        {
            EffectsProfileVersion = AppConfig.CurrentEffectsProfileVersion,
            DeviceProfileName = string.IsNullOrWhiteSpace(deviceProfileName)
                ? settings.DeviceHapticProfileName
                : deviceProfileName!,
            GameplayFfb = Clone(settings)
        };

        File.WriteAllText(GetProfilePath(profile.DeviceProfileName), JsonSerializer.Serialize(profile, JsonOptions));
    }

    private static GameplayFfbSettings Normalize(GameplayFfbSettings settings, string? deviceProfileName, int version)
    {
        var config = new AppConfig
        {
            EffectsProfileVersion = version,
            GameplayFfb = settings
        };
        config.GameplayFfb.DeviceHapticProfileName = string.IsNullOrWhiteSpace(deviceProfileName)
            ? config.GameplayFfb.DeviceHapticProfileName
            : deviceProfileName!;
        return ConfigStore.Normalize(config).GameplayFfb;
    }

    private static GameplayFfbSettings Clone(GameplayFfbSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        return JsonSerializer.Deserialize<GameplayFfbSettings>(json, JsonOptions) ?? new GameplayFfbSettings();
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown FFB Wheel" : sanitized;
    }

    private sealed class WheelEffectsProfile
    {
        public int EffectsProfileVersion { get; set; } = AppConfig.CurrentEffectsProfileVersion;
        public string DeviceProfileName { get; set; } = "Logitech MOMO Racing Wheel";
        public GameplayFfbSettings GameplayFfb { get; set; } = new();
    }
}
