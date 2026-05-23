using System.IO;
using System.Text.Json;
using Godot;

namespace Skock.Meta;

public sealed class UserSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public float MasterVolume { get; set; } = 1.0f;
    public float MusicVolume { get; set; } = 1.0f;
    public float SfxVolume { get; set; } = 1.0f;
    public bool Fullscreen { get; set; } = false;
    public bool ScreenShake { get; set; } = true;

    public void Apply()
    {
        SetBusVolume("Master", MasterVolume);
        SetBusVolume("Music", MusicVolume);
        SetBusVolume("SFX", SfxVolume);
        if (!OS.HasFeature("editor"))
            DisplayServer.WindowSetMode(
                Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed
            );
    }

    public void Save(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    public static UserSettings Load(string path)
    {
        if (!File.Exists(path))
            return new UserSettings();
        try
        {
            return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path), JsonOptions)
                ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    private static void SetBusVolume(string busName, float linear)
    {
        var idx = AudioServer.GetBusIndex(busName);
        if (idx >= 0)
            AudioServer.SetBusVolumeDb(idx, Mathf.LinearToDb(linear));
    }
}
