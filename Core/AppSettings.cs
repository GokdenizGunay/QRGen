using System.IO;
using System.Text.Json;

namespace QRGen.Core;

/// <summary>Taşınabilir ayarlar: önce exe yanındaki QRGen.settings.json, yazılamıyorsa %APPDATA%\QRGen.</summary>
public sealed class AppSettings
{
    public string Theme { get; set; } = "System";
    public int ExportSize { get; set; } = 1024;
    public int Dpi { get; set; } = 300;
    public string? LastFolder { get; set; }
    public StylePreset? LastStyle { get; set; }

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    static string LocalPath => Path.Combine(AppContext.BaseDirectory, "QRGen.settings.json");
    static string RoamingPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QRGen", "settings.json");

    public static AppSettings Load()
    {
        foreach (var p in new[] { LocalPath, RoamingPath })
        {
            try
            {
                if (File.Exists(p)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(p)) ?? new();
            }
            catch { }
        }
        return new();
    }

    public void Save()
    {
        var text = JsonSerializer.Serialize(this, Json);
        try { File.WriteAllText(LocalPath, text); return; } catch { }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RoamingPath)!);
            File.WriteAllText(RoamingPath, text);
        }
        catch { }
    }
}
