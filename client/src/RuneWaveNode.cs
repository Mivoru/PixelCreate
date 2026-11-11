using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// One lane of an Archive Warden's rune wave: a strip of azure light runs along the ground, rune tiles
/// spring up out of it as the front passes and sink again, and a cold light rides the front. The front
/// follows the length from the server; the lane fades once it has run its course.
/// </summary>
public sealed class RuneWaveNode : IGroundHazard
{
    private const float TileStep = 0.9f;
    private static readonly Color Azure = new("2ce8f5");

    private readonly Fx _fx;
    private readonly World _world;
    private readonly TelegraphView _glow;
    private readonly OmniLight3D _light;
    private readonly List<(MeshInstance3D Tile, float Distance)> _tiles = new();
    private readonly Vector3 _origin;
    private readonly Vector3 _forward;
    private float _lastLength;
    private float _age;

    public Node3D Root { get; } = new();

    public RuneWaveNode(World world, Fx fx, Vector3 origin, float yaw)
    {
        _fx = fx;
        _world = world;
        _origin = origin;
        _forward = new Vector3(-Mathf.Sin(yaw), 0, -Mathf.Cos(yaw));
        _glow = new TelegraphView(world, fx, Root, Azure);
        _light = new OmniLight3D { LightColor = Azure, OmniRange = 9f, LightEnergy = 2.5f };
        Root.AddChild(_light);

        // Rune tiles: small glowing slabs, turned a little each, waiting under the ground for the front.
        var material = new StandardMaterial3D { AlbedoColor = Azure, EmissionEnabled = true, Emission = Azure, EmissionEnergyMultiplier = 2.2f };
        var random = new RandomNumberGenerator { Seed = (ulong)(Mathf.Abs(origin.X * 13f + origin.Z * 7f) + 1) };
        for (var distance = 0.6f; distance <= RuneWave.Length; distance += TileStep)
        {
            var tile = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(RuneWave.HalfWidth * 1.3f, 0.12f, 0.5f) },
                MaterialOverride = material,
                Visible = false,
            };
            tile.Rotation = new Vector3(random.RandfRange(-0.25f, 0.25f), Mathf.Atan2(-_forward.X, -_forward.Z) + random.RandfRange(-0.4f, 0.4f), 0);
            Root.AddChild(tile);
            _tiles.Add((tile, distance));
        }
    }

    public void Update(float length, float delta)
    {
        _age += delta;
        _glow.ShowLine(_origin, _origin + _forward * Mathf.Max(length, 0.2f), RuneWave.HalfWidth * 2f, 0.9f);
        var front = _origin + _forward * length;
        _light.Position = front with { Y = _world.GroundHeight(front.X, front.Z) + 1.2f };
        if (length > _lastLength + 0.01f)
        {
            _fx.Burst(_light.Position - Vector3.Up * 0.9f, Azure, 3, 3f, 0.35f, 0.06f, -6f, 40f);
        }

        // A tile rises just behind the front, stands a moment, and sinks back as the front moves on.
        foreach (var (tile, distance) in _tiles)
        {
            var behind = length - distance;
            var shown = behind >= 0f && length > _lastLength - 0.01f && behind < 3.5f;
            tile.Visible = shown;
            if (shown)
            {
                var rise = behind < 0.6f ? behind / 0.6f : 1f - (behind - 0.6f) / 2.9f;
                var spot = _origin + _forward * distance;
                tile.Position = spot with { Y = _world.GroundHeight(spot.X, spot.Z) - 0.1f + rise * 0.45f };
            }
        }

        _light.LightEnergy = length >= _lastLength - 0.01f && length < RuneWave.Length - 0.05f ? 2.5f : Mathf.Max(0f, _light.LightEnergy - delta * 6f);
        _lastLength = length;
    }
}
