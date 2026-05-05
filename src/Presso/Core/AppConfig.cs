using System;
using System.IO;
using System.Text.Json;

namespace Presso.Core;

public sealed class AppConfig
{
    public double TargetMB { get; set; } = 10.0;
    public bool UseHevc { get; set; } = true;
    public string OutputMode { get; set; } = "SourceDir"; // "SourceDir" | "FixedDir"
    public string FixedOutputDir { get; set; } = "";

    public static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Presso");

    public static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null) return cfg;
            }
        }
        catch { /* fall through to default */ }
        return new AppConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
