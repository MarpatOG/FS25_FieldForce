using System.Text.Json;
using FieldForce.App.Models;

namespace FieldForce.App.Services;

public sealed class EffectsProfileStore
{
    private const string CurrentAppDataFolder = "FieldForce";
    private const string LegacyAppDataFolder = "FS25FFBBridge";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public EffectsProfileStore()
        : this(GetDefaultProfilesDirectory(CurrentAppDataFolder), GetDefaultProfilesDirectory(LegacyAppDataFolder))
    {
    }

    public EffectsProfileStore(string profilesDirectory)
        : this(profilesDirectory, null)
    {
    }

    public EffectsProfileStore(string profilesDirectory, string? legacyProfilesDirectory)
    {
        ProfilesDirectory = profilesDirectory;
        LegacyProfilesDirectory = legacyProfilesDirectory;
    }

    public string ProfilesDirectory { get; }
    public string? LegacyProfilesDirectory { get; }

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
            if (TryLoadProfile(path, wheelProfile, out var loaded))
            {
                return loaded;
            }

            var legacyPath = GetLegacyProfilePath(fallback.DeviceHapticProfileName);
            if (legacyPath != path && TryLoadProfile(legacyPath, wheelProfile, out loaded))
            {
                Save(wheelProfile.Id, loaded);
                return loaded;
            }

            if (!string.IsNullOrWhiteSpace(LegacyProfilesDirectory))
            {
                var legacyDirectoryProfilePath = Path.Combine(LegacyProfilesDirectory, Path.GetFileName(path));
                if (TryLoadProfile(legacyDirectoryProfilePath, wheelProfile, out loaded))
                {
                    Save(wheelProfile.Id, loaded);
                    return loaded;
                }

                legacyPath = GetLegacyProfilePath(LegacyProfilesDirectory, fallback.DeviceHapticProfileName);
                if (TryLoadProfile(legacyPath, wheelProfile, out loaded))
                {
                    Save(wheelProfile.Id, loaded);
                    return loaded;
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
        return GetLegacyProfilePath(ProfilesDirectory, deviceProfileName);
    }

    private static string GetLegacyProfilePath(string profilesDirectory, string? deviceProfileName)
    {
        var fileName = SanitizeFileName(string.IsNullOrWhiteSpace(deviceProfileName)
            ? "Unknown FFB Wheel"
            : deviceProfileName);
        return Path.Combine(profilesDirectory, $"{fileName}.json");
    }

    private static bool TryLoadProfile(string path, WheelProfile wheelProfile, out GameplayFfbSettings settings)
    {
        settings = new GameplayFfbSettings();
        if (!File.Exists(path))
        {
            return false;
        }

        var profile = JsonSerializer.Deserialize<WheelEffectsProfile>(File.ReadAllText(path), JsonOptions);
        if (profile?.GameplayFfb is null)
        {
            return false;
        }

        settings = Normalize(profile.GameplayFfb, wheelProfile, profile.EffectsProfileVersion);
        return true;
    }

    private static string GetDefaultProfilesDirectory(string appDataFolder)
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            appDataFolder,
            "effect-profiles");
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
        public string DeviceProfileName { get; set; } = "Logitech Momo Racing";
        public GameplayFfbSettings GameplayFfb { get; set; } = new();
    }
}
