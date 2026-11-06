using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>Two walls of flame in a V. Each arm grows along the ground as the server reports its length.</summary>
public sealed class FireWallNode
{
    private const float Spacing = 0.7f;

    private sealed record Flame(Node3D Node, float Distance, float Seed);

    private readonly List<Flame> _flames = new();
    private float _time;

    public Node3D Root { get; } = new();

    public FireWallNode(World world, Fx fx, Vector3 origin, float yaw)
    {
        var outer = fx.Stipple(new Color("e43b44"), 1f);
        var middle = fx.Stipple(new Color("f77622"), 1f);
        var inner = fx.Stipple(new Color("fee761"), 1f);
        var forward = new Vector3(-Mathf.Sin(yaw), 0, -Mathf.Cos(yaw));
        var count = (int)(FireWallHazard.MaxLength / Spacing);
        foreach (var side in new[] { -1f, 1f })
        {
            var arm = forward.Rotated(Vector3.Up, side * FireWallHazard.HalfAngle);
            for (var i = 1; i <= count; i++)
            {
                var distance = i * Spacing;
                var spot = origin + arm * distance;
                spot.Y = world.GroundHeight(spot.X, spot.Z);
                var flame = new Node3D { Position = spot, Visible = false };
                flame.AddChild(Tongue(0.46f, 1.9f, outer));
                flame.AddChild(Tongue(0.33f, 1.35f, middle));
                flame.AddChild(Tongue(0.2f, 0.8f, inner));
                Root.AddChild(flame);
                _flames.Add(new Flame(flame, distance, i * 1.7f + side));
            }
        }
    }

    public void Update(float length, float delta)
    {
        _time += delta;
        foreach (var flame in _flames)
        {
            var lit = flame.Distance <= length;
            flame.Node.Visible = lit;
            if (lit)
            {
                var flicker = 0.8f + Mathf.Sin(_time * 13f + flame.Seed) * 0.16f + Mathf.Sin(_time * 29f + flame.Seed * 2f) * 0.1f;
                flame.Node.Scale = new Vector3(1f, flicker, 1f);
                flame.Node.Rotation = new Vector3(0, _time * 2f + flame.Seed, 0);
            }
        }
    }

    private static MeshInstance3D Tongue(float radius, float height, Material material) => new()
    {
        Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = radius, Height = height, RadialSegments = 5, Rings = 1 },
        Position = new Vector3(0, height / 2f, 0),
        MaterialOverride = material,
    };
}
