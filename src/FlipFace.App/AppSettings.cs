using System.IO;
using System.Text.Json;
using FlipFace.Input;

namespace FlipFace.App;

/// <summary>App preferences, saved as JSON next to the profiles folder.</summary>
public sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlipFace", "settings.json");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public bool RemappingEnabled { get; set; } = true;

    /// <summary>The profile last used with each controller, keyed by <see cref="ControllerKey"/>.</summary>
    public Dictionary<string, string> ProfileByController { get; set; } = [];

    /// <summary>Used until a controller with a remembered profile connects.</summary>
    public string LastProfile { get; set; } = Core.Presets.MatchLabels.Name;

    /// <summary>Whether we've already told the user that closing the window keeps FlipFace in the tray.</summary>
    public bool TrayTipShown { get; set; }

    public static string ControllerKey(GamepadInfo controller) => $"{controller.VendorId:X4}:{controller.ProductId:X4}";

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json) ?? new();
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            // A damaged settings file just means starting from defaults.
        }
        return new();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
    }
}
