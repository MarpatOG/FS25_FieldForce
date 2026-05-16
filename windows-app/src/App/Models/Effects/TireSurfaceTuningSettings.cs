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

        SetScaleRow(matrix, "asphalt", street: 3, agricultural: 3, mud: 2, offRoad: 4, tracked: 2, mixed: 3, unknown: 5);
        SetScaleRow(matrix, "dirt", street: 5, agricultural: 6, mud: 5, offRoad: 7, tracked: 5, mixed: 6, unknown: 5);
        SetScaleRow(matrix, "gravel", street: 6, agricultural: 6, mud: 5, offRoad: 7, tracked: 5, mixed: 6, unknown: 5);
        SetScaleRow(matrix, "mud", street: 8, agricultural: 8, mud: 7, offRoad: 9, tracked: 6, mixed: 8, unknown: 5);
        SetScaleRow(matrix, "grass", street: 4, agricultural: 5, mud: 4, offRoad: 6, tracked: 4, mixed: 5, unknown: 5);
        SetScaleRow(matrix, "snow", street: 6, agricultural: 6, mud: 5, offRoad: 7, tracked: 5, mixed: 6, unknown: 5);
        SetScaleRow(matrix, "shallowWater", street: 7, agricultural: 7, mud: 6, offRoad: 8, tracked: 5, mixed: 7, unknown: 5);
        SetScaleRow(matrix, "field", street: 6, agricultural: 7, mud: 6, offRoad: 8, tracked: 5, mixed: 7, unknown: 5);
        SetScaleRow(matrix, "plowedField", street: 8, agricultural: 8, mud: 7, offRoad: 9, tracked: 6, mixed: 8, unknown: 5);
        SetScaleRow(matrix, "cultivatedField", street: 7, agricultural: 7, mud: 6, offRoad: 8, tracked: 5, mixed: 7, unknown: 5);
        SetScaleRow(matrix, "wetField", street: 9, agricultural: 9, mud: 8, offRoad: 10, tracked: 7, mixed: 9, unknown: 5);
        SetScaleRow(matrix, "unknownMixed", street: 5, agricultural: 5, mud: 5, offRoad: 5, tracked: 5, mixed: 5, unknown: 5);
        return matrix;
    }

    private static void SetScaleRow(
        Dictionary<string, Dictionary<string, int>> matrix,
        string surface,
        int street,
        int agricultural,
        int mud,
        int offRoad,
        int tracked,
        int mixed,
        int unknown)
    {
        matrix["street"][surface] = ScaleToPercent(street);
        matrix["agricultural"][surface] = ScaleToPercent(agricultural);
        matrix["mud"][surface] = ScaleToPercent(mud);
        matrix["offRoad"][surface] = ScaleToPercent(offRoad);
        matrix["tracked"][surface] = ScaleToPercent(tracked);
        matrix["mixed"][surface] = ScaleToPercent(mixed);
        matrix["unknown"][surface] = ScaleToPercent(unknown);
    }

    private static int ScaleToPercent(int scale)
    {
        return Math.Clamp(scale, 1, 10) * 20;
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
