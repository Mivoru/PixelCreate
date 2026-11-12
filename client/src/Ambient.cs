using System;
using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// Small life that only exists for the eye and never touches the game: flocks of birds, butterflies
/// and pollen around the camera, drifting leaves, and smoke from chimneys. Each client makes its own.
/// </summary>
public partial class Ambient : Node3D
{
    private const int Flocks = 4;
    private const int BirdsPerFlock = 8;

    /// <summary>Where the top of each chimney is, in model space.</summary>
    private static readonly Dictionary<string, Vector3> Chimneys = new()
    {
        ["cottage"] = new(1.6f, 6.9f, -0.48f),
        ["cottage_b"] = new(1.4f, 6.3f, -0.46f),
        ["tavern"] = new(2.3f, 10.5f, -0.6f),
        ["smithy"] = new(-1.4f, 4.6f, -1.4f),
    };

    private sealed class Bird
    {
        public required Node3D Body;
        public required Node3D LeftWing;
        public required Node3D RightWing;
        public required Vector3 Offset;
        public required float Phase;
    }

    private sealed record Flock(Vector3 Centre, float Radius, float Speed, float Height, Bird[] Birds);

    private readonly List<Flock> _flocks = new();
    private readonly Zone _zone;
    private CpuParticles3D _butterflies = null!;
    private CpuParticles3D _pollen = null!;
    private CpuParticles3D _leaves = null!;
    private CpuParticles3D _fireflies = null!;
    private float _time;

    /// <summary>How dark it is, from 0 by day to 1 at night; the game sets it from the clock every frame.</summary>
    public static float Night { get; set; }

    public Ambient(Zone zone)
    {
        _zone = zone;
    }

    public override void _Ready()
    {
        var random = new Random(5);
        var centres = new[] { new Vector3(0, 0, 0), new Vector3(-96, 0, 46), new Vector3(70, 0, 100), new Vector3(30, 0, -60) };
        for (var i = 0; i < Flocks; i++)
        {
            var birds = new Bird[BirdsPerFlock];
            for (var b = 0; b < BirdsPerFlock; b++)
            {
                birds[b] = BuildBird(random);
            }

            _flocks.Add(new Flock(centres[i], 26f + i * 7f, 0.16f + i * 0.03f, 20f + i * 4f, birds));
        }

        _butterflies = Swarm(40, 7f, new Vector3(22, 2.2f, 22), new BoxMesh { Size = new Vector3(0.14f, 0.02f, 0.1f) }, new Color("fee761"), 0.5f, 0.4f);
        _butterflies.ColorRamp = null;
        _pollen = Swarm(120, 9f, new Vector3(24, 6, 24), new BoxMesh { Size = Vector3.One * 0.035f }, new Color("fff6d0"), 0.25f, 0.05f);
        _leaves = Swarm(36, 8f, new Vector3(26, 9, 26), new BoxMesh { Size = new Vector3(0.11f, 0.015f, 0.08f) }, new Color("c8b048"), 0.9f, -0.75f);
        // Fireflies: small glowing motes low over the grass after dark, drifting slowly.
        _fireflies = Swarm(90, 6f, new Vector3(20, 1.4f, 20), new BoxMesh { Size = Vector3.One * 0.085f }, new Color("d8f86a"), 0.35f, 0.02f);
        _fireflies.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color("d8f86a"), EmissionEnabled = true, Emission = new Color("c8f040"), EmissionEnergyMultiplier = 3f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        _fireflies.Emitting = false;
        foreach (var prop in _zone.Props)
        {
            if (Chimneys.TryGetValue(prop.Model, out var top))
            {
                var at = new Vector3(prop.Position.X, prop.Position.Y, prop.Position.Z) + top.Rotated(Vector3.Up, prop.Yaw);
                AddChild(Smoke(at));
            }
        }
    }

    private Bird BuildBird(Random random)
    {
        var material = Plain(new Color("2a2f3a"));
        var body = new Node3D();
        AddChild(body);
        body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.1f, 0.08f, 0.42f) }, MaterialOverride = material });
        var left = Wing(material, -1f);
        var right = Wing(material, 1f);
        body.AddChild(left);
        body.AddChild(right);
        var spread = new Vector3((float)random.NextDouble() - 0.5f, ((float)random.NextDouble() - 0.5f) * 0.4f, (float)random.NextDouble() - 0.5f) * 7f;
        return new Bird { Body = body, LeftWing = left, RightWing = right, Offset = spread, Phase = (float)random.NextDouble() * Mathf.Tau };
    }

    private static Node3D Wing(Material material, float side)
    {
        var joint = new Node3D();
        joint.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.55f, 0.02f, 0.22f) },
            MaterialOverride = material,
            Position = new Vector3(side * 0.3f, 0, 0),
        });
        return joint;
    }

    private static StandardMaterial3D Plain(Color color) => new() { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };

    /// <summary>A cloud of little things that stays around the camera, in world space so it does not swim.</summary>
    private CpuParticles3D Swarm(int amount, float life, Vector3 extents, Mesh mesh, Color color, float speed, float gravity)
    {
        var swarm = new CpuParticles3D
        {
            Amount = amount,
            Lifetime = life,
            Preprocess = life,
            LocalCoords = false,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = extents,
            Direction = Vector3.Up,
            Spread = 180f,
            InitialVelocityMin = speed * 0.4f,
            InitialVelocityMax = speed,
            Gravity = new Vector3(0.12f, -gravity, 0.05f),
            AngularVelocityMin = -120f,
            AngularVelocityMax = 120f,
            Mesh = mesh,
            MaterialOverride = Plain(color),
        };
        AddChild(swarm);
        return swarm;
    }

    private static CpuParticles3D Smoke(Vector3 at) => new()
    {
        Position = at,
        Amount = 10,
        Lifetime = 5f,
        Preprocess = 5f,
        Direction = Vector3.Up,
        Spread = 6f,
        InitialVelocityMin = 0.7f,
        InitialVelocityMax = 1.1f,
        Gravity = new Vector3(0.28f, 0.1f, 0.1f),
        ScaleAmountMin = 0.4f,
        ScaleAmountMax = 0.9f,
        ScaleAmountCurve = SmokeCurve(),
        Mesh = new SphereMesh { Radius = 0.45f, Height = 0.9f, RadialSegments = 7, Rings = 4 },
        MaterialOverride = Plain(new Color("dfe4ea")),
    };

    private static Curve SmokeCurve()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 0.4f));
        curve.AddPoint(new Vector2(0.5f, 1f));
        curve.AddPoint(new Vector2(1f, 0f));
        return curve;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        foreach (var flock in _flocks)
        {
            // Birds roost at night.
            foreach (var bird in flock.Birds)
            {
                bird.Body.Visible = Night < 0.5f;
            }

            var angle = _time * flock.Speed;
            var lead = flock.Centre + new Vector3(Mathf.Cos(angle) * flock.Radius, flock.Height + Mathf.Sin(angle * 2.3f) * 3f, Mathf.Sin(angle) * flock.Radius);
            var heading = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
            foreach (var bird in flock.Birds)
            {
                bird.Body.Position = lead + bird.Offset + Vector3.Up * Mathf.Sin(_time * 1.3f + bird.Phase) * 0.4f;
                bird.Body.LookAt(bird.Body.Position + heading, Vector3.Up);
                var glide = Mathf.Sin(_time * 0.7f + bird.Phase) > 0.4f;
                var flap = glide ? 0.12f : Mathf.Sin(_time * 11f + bird.Phase) * 0.7f;
                bird.LeftWing.Rotation = new Vector3(0, 0, -flap);
                bird.RightWing.Rotation = new Vector3(0, 0, flap);
            }
        }

        if (GetViewport().GetCamera3D() is { } camera)
        {
            var around = camera.GlobalPosition;
            _butterflies.GlobalPosition = around + new Vector3(0, -1.5f, 0);
            _pollen.GlobalPosition = around;
            _leaves.GlobalPosition = around + Vector3.Up * 5f;

            // Inside the Guild and the arena there is no meadow to fly over.
            var outside = around.X < _zone.Size / 2f;
            var night = Night > 0.5f;
            _butterflies.Emitting = outside && !night;
            _pollen.Emitting = outside && !night;
            _leaves.Emitting = outside;
            _fireflies.GlobalPosition = around + new Vector3(0, -1.6f, 0);
            _fireflies.Emitting = outside && night;
        }
    }
}
