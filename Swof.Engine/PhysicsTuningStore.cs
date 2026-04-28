using System.Text.Json;

namespace Swof.Engine;

public static class PhysicsTuningStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

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

        var json = JsonSerializer.Serialize(tuning, JsonOptions);
        File.WriteAllText(resolvedPath, json);
    }
}
