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

    public string GetProfilePath(string? wheelProfileId)
    {
        var profile = WheelProfileCatalog.Resolve(wheelProfileId);
        var fileName = SanitizeFileName(profile.Id);
        return Path.Combine(ProfilesDirectory, $"{fileName}.json");
    }

    public GameplayFfbSettings Load(string? wheelProfileId, GameplayFfbSettings fallback)
    {
        var wheelProfile = ResolveWheelProfile(wheelProfileId, fallback);
        var path = GetProfilePath(wheelProfile.Id);
        try
        {
            if (File.Exists(path))
            {
                var profile = JsonSerializer.Deserialize<WheelEffectsProfile>(File.ReadAllText(path), JsonOptions);
                if (profile?.GameplayFfb is not null)
                {
                    return Normalize(profile.GameplayFfb, wheelProfile, profile.EffectsProfileVersion);
                }
            }

            var legacyPath = GetLegacyProfilePath(fallback.DeviceHapticProfileName);
            if (legacyPath != path && File.Exists(legacyPath))
            {
                var profile = JsonSerializer.Deserialize<WheelEffectsProfile>(File.ReadAllText(legacyPath), JsonOptions);
                if (profile?.GameplayFfb is not null)
                {
                    var migrated = Normalize(profile.GameplayFfb, wheelProfile, profile.EffectsProfileVersion);
                    Save(wheelProfile.Id, migrated);
                    return migrated;
                }
            }
        }
        catch
        {
            // Fall back to the main config copy when a wheel profile is unreadable.
        }

        var cloned = Clone(fallback);
        var normalized = Normalize(cloned, wheelProfile, AppConfig.CurrentEffectsProfileVersion);
        Save(normalized.WheelProfileId, normalized);
        return normalized;
    }

    public void Save(string? wheelProfileId, GameplayFfbSettings settings)
    {
        Directory.CreateDirectory(ProfilesDirectory);
        var wheelProfile = ResolveWheelProfile(wheelProfileId, settings);
        settings.WheelProfileId = wheelProfile.Id;
        settings.DeviceHapticProfileName = wheelProfile.DisplayName;
        var profile = new WheelEffectsProfile
        {
            EffectsProfileVersion = AppConfig.CurrentEffectsProfileVersion,
            WheelProfileId = wheelProfile.Id,
            DeviceProfileName = wheelProfile.DisplayName,
            GameplayFfb = Clone(settings)
        };

        File.WriteAllText(GetProfilePath(profile.WheelProfileId), JsonSerializer.Serialize(profile, JsonOptions));
    }

    private static GameplayFfbSettings Normalize(GameplayFfbSettings settings, WheelProfile wheelProfile, int version)
    {
        var config = new AppConfig
        {
            EffectsProfileVersion = version,
            WheelProfileId = wheelProfile.Id,
            DeviceProfileName = wheelProfile.DisplayName,
            RotationDegrees = wheelProfile.RotationDegrees,
            RecommendedMode = wheelProfile.RecommendedMode,
            GameplayFfb = settings
        };
        config.GameplayFfb.WheelProfileId = wheelProfile.Id;
        config.GameplayFfb.DeviceHapticProfileName = wheelProfile.DisplayName;
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

    private string GetLegacyProfilePath(string? deviceProfileName)
    {
        var fileName = SanitizeFileName(string.IsNullOrWhiteSpace(deviceProfileName)
            ? "Unknown FFB Wheel"
            : deviceProfileName);
        return Path.Combine(ProfilesDirectory, $"{fileName}.json");
    }

    private static WheelProfile ResolveWheelProfile(string? wheelProfileId, GameplayFfbSettings fallback)
    {
        var profile = WheelProfileCatalog.ResolveById(wheelProfileId);
        if (profile.Id != WheelProfileCatalog.GenericId || string.Equals(wheelProfileId, WheelProfileCatalog.GenericId, StringComparison.OrdinalIgnoreCase))
        {
            return profile;
        }

        profile = WheelProfileCatalog.Resolve(fallback.WheelProfileId);
        if (profile.Id != WheelProfileCatalog.GenericId)
        {
            return profile;
        }

        return WheelProfileCatalog.Resolve(fallback.DeviceHapticProfileName);
    }

    private sealed class WheelEffectsProfile
    {
        public int EffectsProfileVersion { get; set; } = AppConfig.CurrentEffectsProfileVersion;
        public string WheelProfileId { get; set; } = WheelProfileCatalog.LogitechMomoRacingId;
        public string DeviceProfileName { get; set; } = "Logitech MOMO Racing Wheel";
        public GameplayFfbSettings GameplayFfb { get; set; } = new();
    }
}
