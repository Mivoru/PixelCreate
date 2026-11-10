using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Godot;

namespace PixelCreate;

/// <summary>
/// The spoken lines of the people of the world. asset-pipeline/generate_voices.py records each line
/// to assets/voices/&lt;npc&gt;/&lt;hash of the text&gt;.mp3; the recordings are not in git, so a line without
/// a file is simply read in silence, as before. A line in a dialogue plays as it is; a line said in
/// passing comes from where its speaker stands.
/// </summary>
public sealed class Voices
{
    private readonly AudioStreamPlayer _talk = new() { Bus = "SFX" };
    private readonly AudioStreamPlayer3D _passing = new() { Bus = "SFX", MaxDistance = 26f, UnitSize = 5f, AttenuationFilterCutoffHz = 20500f };
    private readonly Dictionary<string, AudioStream?> _lines = new();
    private readonly string _folder = ProjectSettings.GlobalizePath("res://assets/voices");

    /// <summary>Lines spoken aloud so far. Tests read it, because a test cannot listen.</summary>
    public int Spoken { get; private set; }

    /// <summary>True while a line of a dialogue is being said: the speaker's mouth moves.</summary>
    public bool Talking => _talk.Playing;

    public Voices(Node parent)
    {
        parent.AddChild(_talk);
        parent.AddChild(_passing);
    }

    /// <summary>The file name of a line, the same as generate_voices.py gives it: the first 16 hex digits of the text's SHA-1.</summary>
    public static string Key(string text) => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();

    /// <summary>Says a line of a dialogue, cutting off the one before. False when it has no recording.</summary>
    public bool Say(string npc, string text)
    {
        _talk.Stop();
        if (Find(npc, text) is not { } line)
        {
            return false;
        }

        _talk.Stream = line;
        _talk.Play();
        Heard(npc, text);
        return true;
    }

    /// <summary>Says a line in passing from a place in the world, unless someone is still saying one.</summary>
    public bool SayAt(string npc, string text, Vector3 position)
    {
        if (_passing.Playing || Find(npc, text) is not { } line)
        {
            return false;
        }

        _passing.Stream = line;
        _passing.Position = position;
        _passing.Play();
        Heard(npc, text);
        return true;
    }

    /// <summary>Stops the line of a dialogue: the talk is over, or the next thing shown has no voice.</summary>
    public void Hush() => _talk.Stop();

    private void Heard(string npc, string text)
    {
        Spoken++;
        GD.Print($"VOICE {npc} {Key(text)}");
    }

    private AudioStream? Find(string npc, string text)
    {
        if (npc.Length == 0 || text.Length == 0)
        {
            return null;
        }

        var name = $"{npc}/{Key(text)}.mp3";
        if (!_lines.TryGetValue(name, out var line))
        {
            var path = System.IO.Path.Combine(_folder, name);
            line = System.IO.File.Exists(path) ? new AudioStreamMP3 { Data = System.IO.File.ReadAllBytes(path) } : null;
            _lines.Add(name, line);
        }

        return line;
    }
}
