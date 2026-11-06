using System;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>A filled V of real-looking flame. The front of the fire follows the length from the server.</summary>
public sealed class FireWallNode
{
    private const float Spacing = 1.25f;
    private const float FlameSize = 1.9f;

    private readonly ShaderMaterial _flames;
    private readonly TelegraphView _burn;
    private readonly OmniLight3D _light;
    private readonly Vector3 _origin;
    private readonly Vector3 _forward;
    private readonly float _yaw;
    private float _time;

    public Node3D Root { get; } = new();

    public FireWallNode(World world, Fx fx, Vector3 origin, float yaw)
    {
        _origin = origin;
        _yaw = yaw;
        _forward = new Vector3(-Mathf.Sin(yaw), 0, -Mathf.Cos(yaw));
        _flames = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/flame.gdshader") };

        var multi = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = BuildQuad(),
        };
        var spots = new System.Collections.Generic.List<(Vector3 Position, float Distance)>();
        var random = new Random((int)(origin.X * 31f + origin.Z * 17f));
        for (var distance = 0.8f; distance <= FireWallHazard.MaxLength; distance += Spacing)
        {
            var half = distance * Mathf.Tan(FireWallHazard.HalfAngle);
            var side = new Vector3(-_forward.Z, 0, _forward.X);
            for (var across = -half; across <= half; across += Spacing)
            {
                var jitter = new Vector3((float)random.NextDouble() - 0.5f, 0, (float)random.NextDouble() - 0.5f) * 0.5f;
                var spot = origin + _forward * distance + side * across + jitter;
                spot.Y = world.GroundHeight(spot.X, spot.Z);
                spots.Add((spot, spot.DistanceTo(origin)));
            }
        }

        multi.InstanceCount = spots.Count;
        for (var i = 0; i < spots.Count; i++)
        {
            var scale = FlameSize * (0.75f + (float)random.NextDouble() * 0.6f);
            multi.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * scale), spots[i].Position));
            multi.SetInstanceCustomData(i, new Color(spots[i].Distance, (float)random.NextDouble(), 0f, 0f));
        }

        Root.AddChild(new MultiMeshInstance3D { Multimesh = multi, MaterialOverride = _flames, ExtraCullMargin = 30f });
        _burn = new TelegraphView(world, fx, Root, new Color("3e2731"));
        _light = new OmniLight3D { LightColor = new Color("f77622"), OmniRange = 18f, LightEnergy = 3f, Position = origin + Vector3.Up * 2f };
        Root.AddChild(_light);
    }

    public void Update(float length, float delta)
    {
        _time += delta;
        _flames.SetShaderParameter("reach", length);
        _burn.ShowWedge(_origin, _yaw, length, 0.85f);
        _light.Position = _origin + _forward * (length * 0.6f) + Vector3.Up * 2f;
        _light.LightEnergy = 2.6f + Mathf.Sin(_time * 17f) * 0.5f + Mathf.Sin(_time * 29f) * 0.3f;
    }

    /// <summary>A quad that stands on the ground, so a flame scales up from its base.</summary>
    private static QuadMesh BuildQuad() => new()
    {
        Size = Vector2.One,
        CenterOffset = new Vector3(0, 0.5f, 0),
    };
}
