namespace FS25FfbBridge.App.Models;

public sealed class TireSurfaceTuningSettings
{
    public const int DefaultMultiplierPercent = 100;
    public const int UnknownFallbackPercent = 50;

    public static IReadOnlyList<string> TireProfiles { get; } =
    [
        "street",
        "agricultural",
        "mud",
        "offRoad",
        "tracked",
        "mixed",
        "unknown"
    ];

    public static IReadOnlyList<string> SurfaceTypes { get; } =
    [
        "asphalt",
        "dirt",
        "gravel",
        "mud",
        "grass",
        "snow",
        "shallowWater",
        "field",
        "plowedField",
        "cultivatedField",
        "wetField",
        "unknownMixed"
    ];

    public Dictionary<string, string> SurfaceAliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, Dictionary<string, int>> Matrix { get; set; } = CreateDefaultMatrix();

    public static Dictionary<string, Dictionary<string, int>> CreateDefaultMatrix()
    {
        var matrix = TireProfiles.ToDictionary(
            profile => profile,
            _ => SurfaceTypes.ToDictionary(surface => surface, _ => DefaultMultiplierPercent, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        foreach (var surface in SurfaceTypes)
        {
            matrix["unknown"][surface] = UnknownFallbackPercent;
            matrix["mixed"][surface] = 60;
        }

        matrix["street"]["asphalt"] = 20;
        foreach (var surface in new[] { "dirt", "gravel", "field", "plowedField" })
        {
            matrix["street"][surface] = 85;
        }

        foreach (var profile in new[] { "agricultural", "mud", "offRoad" })
        {
            foreach (var surface in new[] { "field", "dirt", "plowedField" })
            {
                matrix[profile][surface] = 25;
            }

            matrix[profile]["asphalt"] = 90;
        }

        foreach (var surface in new[] { "field", "dirt", "plowedField" })
        {
            matrix["tracked"][surface] = 20;
        }

        matrix["tracked"]["asphalt"] = 80;
        return matrix;
    }

    public static void Normalize(TireSurfaceTuningSettings? settings)
    {
        if (settings is null)
        {
            return;
        }

        settings.SurfaceAliases = NormalizeAliases(settings.SurfaceAliases);
        settings.Matrix = NormalizeMatrix(settings.Matrix);
    }

    public static TireSurfaceTuningSettings CreateNormalized(TireSurfaceTuningSettings? settings)
    {
        settings ??= new TireSurfaceTuningSettings();
        Normalize(settings);
        return settings;
    }

    public int GetMultiplierPercent(string? tireProfile, string? surfaceType)
    {
        var normalizedProfile = NormalizeTireProfile(tireProfile);
        var normalizedSurface = NormalizeSurfaceType(surfaceType);
        if (Matrix.TryGetValue(normalizedProfile, out var row) &&
            row.TryGetValue(normalizedSurface, out var value))
        {
            return Math.Clamp(value, 0, 200);
        }

        return UnknownFallbackPercent;
    }

    public string ResolveSurfaceAlias(string? rawSurface)
    {
        var raw = rawSurface?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "unknownMixed";
        }

        if (SurfaceAliases.TryGetValue(raw, out var mapped))
        {
            return NormalizeSurfaceType(mapped);
        }

        return NormalizeSurfaceType(raw);
    }

    public static string NormalizeTireProfile(string? value)
    {
        return value?.Trim() switch
        {
            "street" => "street",
            "agricultural" => "agricultural",
            "mud" => "mud",
            "offRoad" => "offRoad",
            "crawler" or "tracked" => "tracked",
            "mixed" => "mixed",
            _ => "unknown"
        };
    }

    public static string NormalizeSurfaceType(string? value)
    {
        return value?.Trim() switch
        {
            "asphalt" or "road" or "concrete" or "pavement" or "tarmac" => "asphalt",
            "dirt" => "dirt",
            "gravel" => "gravel",
            "mud" => "mud",
            "grass" => "grass",
            "snow" => "snow",
            "shallowWater" or "shallowwater" => "shallowWater",
            "field" => "field",
            "plowedField" or "plowedfield" => "plowedField",
            "cultivatedField" or "cultivatedfield" => "cultivatedField",
            "wetField" or "wetfield" => "wetField",
            _ => "unknownMixed"
        };
    }

    private static Dictionary<string, string> NormalizeAliases(Dictionary<string, string>? aliases)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (aliases is null)
        {
            return result;
        }

        foreach (var (raw, mapped) in aliases)
        {
            if (!string.IsNullOrWhiteSpace(raw))
            {
                result[raw.Trim()] = NormalizeSurfaceType(mapped);
            }
        }

        return result;
    }

    private static Dictionary<string, Dictionary<string, int>> NormalizeMatrix(Dictionary<string, Dictionary<string, int>>? matrix)
    {
        var normalized = CreateDefaultMatrix();
        if (matrix is null)
        {
            return normalized;
        }

        foreach (var (profile, row) in matrix)
        {
            var normalizedProfile = NormalizeTireProfile(profile);
            if (!normalized.TryGetValue(normalizedProfile, out var target) || row is null)
            {
                continue;
            }

            foreach (var (surface, value) in row)
            {
                target[NormalizeSurfaceType(surface)] = Math.Clamp(value, 0, 200);
            }
        }

        return normalized;
    }
}
