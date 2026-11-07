using System.Collections.Generic;
using Godot;

namespace PixelCreate;

/// <summary>
/// Short-lived effects. Everything fades by stippling, because the pixel post pass reads only opaque
/// pixels and true transparency would never reach the screen.
/// </summary>
public sealed class Fx
{
    private sealed record Fading(MeshInstance3D Mesh, ShaderMaterial Material, float Life, float Strength)
    {
        public float Age { get; set; }
    }

    private readonly Node3D _parent;
    private readonly Shader _stipple = GD.Load<Shader>("res://shaders/stipple.gdshader");
    private readonly List<Fading> _fading = new();

    public Fx(Node3D parent) => _parent = parent;

    public ShaderMaterial Stipple(Color color, float strength)
    {
        var material = new ShaderMaterial { Shader = _stipple };
        material.SetShaderParameter("color", color);
        material.SetShaderParameter("strength", strength);
        return material;
    }

    /// <summary>A one-shot spray of small pieces.</summary>
    public void Burst(Vector3 position, Color color, int amount, float speed, float life, float size = 0.07f, float gravity = -14f, float spread = 70f)
    {
        var particles = new CpuParticles3D
        {
            OneShot = true,
            Explosiveness = 1f,
            Amount = amount,
            Lifetime = life,
            Direction = Vector3.Up,
            Spread = spread,
            InitialVelocityMin = speed * 0.4f,
            InitialVelocityMax = speed,
            Gravity = new Vector3(0, gravity, 0),
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1.3f,
            Mesh = new BoxMesh { Size = Vector3.One * size },
            MaterialOverride = Stipple(color, 1f),
            Position = position,
            Emitting = true,
        };
        _parent.AddChild(particles);
        particles.GetTree().CreateTimer(life + 0.2f).Timeout += particles.QueueFree;
    }

    /// <summary>A shape that dissolves over its life.</summary>
    public void Fade(Mesh mesh, Vector3 position, Vector3 rotation, Color color, float life, float strength = 0.7f)
    {
        var material = Stipple(color, strength);
        var instance = new MeshInstance3D { Mesh = mesh, Position = position, Rotation = rotation, MaterialOverride = material };
        _parent.AddChild(instance);
        _fading.Add(new Fading(instance, material, life, strength));
    }

    /// <summary>A flat splash of slime that shrinks into the ground.</summary>
    public void Puddle(Vector3 position, Color color, float radius, float life)
    {
        Fade(new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.02f, RadialSegments = 12 },
             position + Vector3.Up * 0.06f, Vector3.Zero, color, life, 0.95f);
    }

    /// <summary>A rising column of light and sparks.</summary>
    public void LevelUp(Vector3 position)
    {
        Burst(position + Vector3.Up * 0.3f, new Color("fee761"), 46, 9f, 1.1f, 0.09f, -4f, 28f);
        Burst(position + Vector3.Up * 1f, Colors.White, 22, 6f, 0.9f, 0.06f, -2f, 60f);
    }

    public void Update(float delta)
    {
        for (var i = _fading.Count - 1; i >= 0; i--)
        {
            var item = _fading[i];
            item.Age += delta;
            var left = 1f - item.Age / item.Life;
            item.Material.SetShaderParameter("strength", item.Strength * left);
            if (left <= 0f)
            {
                item.Mesh.QueueFree();
                _fading.RemoveAt(i);
            }
        }
    }
}
