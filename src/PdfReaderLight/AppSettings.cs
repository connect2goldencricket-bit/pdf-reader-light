using System.Text.Json;

namespace PdfReaderLight;

internal sealed class AppSettings
{
    public List<string> Recent { get; set; } = new();
    public Dictionary<string, int> LastPage { get; set; } = new();
    public double ZoomPercent { get; set; } = 100;
    public bool ShowThumbnails { get; set; } = true;

    private static string SettingsFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PdfReaderLight", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile)) ?? new AppSettings();
        }
        catch
        {
            // corrupted settings file: fall back to defaults
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(this));
        }
        catch
        {
            // settings are best-effort
        }
    }

    public void AddRecent(string path)
    {
        Recent.Remove(path);
        Recent.Insert(0, path);
        if (Recent.Count > 10)
            Recent.RemoveRange(10, Recent.Count - 10);
    }
}
