using System.Collections.Generic;
using Godot;

namespace PixelCreate;

/// <summary>
/// The player's settings, kept in user://settings.cfg between runs: how loud sound effects and music
/// are, and how fast the mouse turns the camera, with its own speed while aiming a bow.
/// </summary>
public static class Settings
{
    private const string Path = "user://settings.cfg";

    /// <summary>Volumes from 0 (silent) to 1 (full).</summary>
    public static float SfxVolume { get; set; } = 0.8f;
    public static float MusicVolume { get; set; } = 0.6f;

    /// <summary>Mouse speed as a share of the usual: 1 is the default, 0.2 to 3 the range offered.</summary>
    public static float Sensitivity { get; set; } = 1f;

    /// <summary>Mouse speed while a bow is drawn, as a share of the usual mouse speed.</summary>
    public static float AimSensitivity { get; set; } = 0.6f;

    /// <summary>The places this player has found; the minimap names only these.</summary>
    public static HashSet<string> Discovered { get; } = new();

    /// <summary>Marks a place as found, and keeps it for next time.</summary>
    public static void Discover(string place)
    {
        if (place.Length > 0 && Discovered.Add(place))
        {
            Save();
        }
    }

    public const float MinSensitivity = 0.2f;
    public const float MaxSensitivity = 3f;

    public static void Load()
    {
        var file = new ConfigFile();
        if (file.Load(Path) == Error.Ok)
        {
            SfxVolume = (float)file.GetValue("audio", "sfx", SfxVolume).AsDouble();
            MusicVolume = (float)file.GetValue("audio", "music", MusicVolume).AsDouble();
            Sensitivity = (float)file.GetValue("mouse", "sensitivity", Sensitivity).AsDouble();
            AimSensitivity = (float)file.GetValue("mouse", "aim", AimSensitivity).AsDouble();
            foreach (var place in file.GetValue("map", "discovered", "").AsString().Split('|', System.StringSplitOptions.RemoveEmptyEntries))
            {
                Discovered.Add(place);
            }
        }

        Apply();
    }

    public static void Save()
    {
        var file = new ConfigFile();
        file.SetValue("audio", "sfx", SfxVolume);
        file.SetValue("audio", "music", MusicVolume);
        file.SetValue("mouse", "sensitivity", Sensitivity);
        file.SetValue("mouse", "aim", AimSensitivity);
        file.SetValue("map", "discovered", string.Join("|", Discovered));
        file.Save(Path);
        Apply();
    }

    /// <summary>Sets the volume of the SFX and Music buses, making them first if they do not exist yet.</summary>
    public static void Apply()
    {
        SetBus("SFX", SfxVolume);
        SetBus("Music", MusicVolume);
    }

    private static void SetBus(string name, float volume)
    {
        var index = AudioServer.GetBusIndex(name);
        if (index < 0)
        {
            AudioServer.AddBus();
            index = AudioServer.BusCount - 1;
            AudioServer.SetBusName(index, name);
            AudioServer.SetBusSend(index, "Master");
        }

        AudioServer.SetBusMute(index, volume <= 0.001f);
        AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Mathf.Max(volume, 0.001f)));
    }
}
