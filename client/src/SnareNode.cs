using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// A goblin trapper's snare: a loop of rough rope pegged out flat on the ground with a slip knot and a short
/// stake, plain enough to spot if the player looks where they step. It sways a little as if the noose were loose.
/// </summary>
public sealed class SnareNode : IGroundHazard
{
    private readonly Node3D _loop = new();
    private float _clock;

    public Node3D Root { get; } = new();

    public SnareNode(World world, Vector3 origin)
    {
        Root.Position = origin with { Y = world.GroundHeight(origin.X, origin.Z) + 0.03f };
        Root.AddChild(_loop);
        var rope = new StandardMaterial3D { AlbedoColor = new Color("b8924e"), Roughness = 1f };
        var wood = new StandardMaterial3D { AlbedoColor = new Color("4a3028"), Roughness = 1f };
        _loop.AddChild(new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = Snare.Reach * 0.62f, OuterRadius = Snare.Reach * 0.62f + 0.05f, Rings = 18, RingSegments = 4 },
            MaterialOverride = rope,
        });
        // The slip knot and the tail of rope running to the stake.
        _loop.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.07f, Height = 0.12f, RadialSegments = 6, Rings = 3 },
            Position = new Vector3(Snare.Reach * 0.62f, 0.03f, 0),
            MaterialOverride = rope,
        });
        _loop.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.55f, 0.03f, 0.03f) },
            Position = new Vector3(Snare.Reach * 0.62f + 0.3f, 0.02f, 0),
            MaterialOverride = rope,
        });
        Root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.035f, Height = 0.32f, RadialSegments = 5 },
            Position = new Vector3(Snare.Reach * 0.62f + 0.6f, 0.12f, 0),
            MaterialOverride = wood,
        });
        foreach (var angle in new[] { 1.3f, 3.0f, 4.6f })
        {
            Root.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.025f, Height = 0.16f, RadialSegments = 4 },
                Position = new Vector3(Mathf.Cos(angle) * Snare.Reach * 0.66f, 0.06f, Mathf.Sin(angle) * Snare.Reach * 0.66f),
                MaterialOverride = wood,
            });
        }
    }

    public void Update(float length, float delta)
    {
        _clock += delta;
        _loop.Scale = new Vector3(1f + Mathf.Sin(_clock * 1.7f) * 0.03f, 1f, 1f + Mathf.Cos(_clock * 1.3f) * 0.03f);
    }
}
