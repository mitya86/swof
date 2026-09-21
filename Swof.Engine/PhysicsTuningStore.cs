using System.Text.Json;
using System.Text.Json.Serialization;

namespace Swof.Engine;

public static class PhysicsTuningStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    static PhysicsTuningStore()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SWOF",
        "physics-tuning.json");

    public static PhysicsTuning LoadOrDefault(string? path = null)
    {
        var resolvedPath = path ?? DefaultPath;
        if (!File.Exists(resolvedPath))
        {
            return PhysicsTuning.Default;
        }

        try
        {
            var json = File.ReadAllText(resolvedPath);
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(
                    nameof(PhysicsTuning.ProfileVersion),
                    out var versionElement)
                || versionElement.ValueKind != JsonValueKind.Number
                || !versionElement.TryGetInt32(out var version)
                || version != PhysicsTuning.CurrentProfileVersion)
            {
                return PhysicsTuning.Default;
            }

            return JsonSerializer.Deserialize<PhysicsTuning>(json, JsonOptions) ?? PhysicsTuning.Default;
        }
        catch
        {
            return PhysicsTuning.Default;
        }
    }

    public static void Save(PhysicsTuning tuning, string? path = null)
    {
        var resolvedPath = path ?? DefaultPath;
        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var currentTuning = tuning with { ProfileVersion = PhysicsTuning.CurrentProfileVersion };
        var json = JsonSerializer.Serialize(currentTuning, JsonOptions);
        File.WriteAllText(resolvedPath, json);
    }
}
