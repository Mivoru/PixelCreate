using System.Collections.Generic;
using Godot;

namespace PixelCreate;

/// <summary>Plays positional sound effects from a small pool of players.</summary>
public sealed class Sfx
{
    private const int PoolSize = 24;
    private const float MaxDistance = 42f;

    private readonly List<AudioStreamPlayer3D> _pool = new();
    private readonly Dictionary<string, AudioStream> _streams = new();
    private readonly RandomNumberGenerator _random = new();
    private int _next;

    /// <summary>Every sound played so far. Tests read it, because a test cannot listen.</summary>
    public int Played { get; private set; }

    public bool Muted { get; set; }

    public Sfx(Node parent)
    {
        for (var i = 0; i < PoolSize; i++)
        {
            var player = new AudioStreamPlayer3D { MaxDistance = MaxDistance, UnitSize = 7f, AttenuationFilterCutoffHz = 20500f };
            parent.AddChild(player);
            _pool.Add(player);
        }
    }

    public void Play(string name, Vector3 position, float volumeDb = 0f)
    {
        if (Muted)
        {
            return;
        }

        if (!_streams.TryGetValue(name, out var stream))
        {
            stream = GD.Load<AudioStream>($"res://assets/sounds/{name}.wav");
            _streams.Add(name, stream);
        }

        var player = _pool[_next];
        _next = (_next + 1) % PoolSize;
        player.Stream = stream;
        player.Position = position;
        player.VolumeDb = volumeDb;
        player.PitchScale = _random.RandfRange(0.94f, 1.06f);
        player.Play();
        Played++;
    }
}
