using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>Builds what the player sees from the same zone data the server uses for collision.</summary>
public static class ZoneView
{
    private static readonly Color Grass = new("3e8948");
    private static readonly Color DarkGrass = new("265c42");
    private static readonly Color LightGrass = new("63c74d");
    private static readonly Color Path = new("d77643");
    private static readonly Color Rock = new("5a6988");

    public static Node3D Build(Zone zone)
    {
        var root = new Node3D { Name = zone.Name };
        root.AddChild(BuildTerrain(zone));

        var models = new Dictionary<string, PackedScene>();
        foreach (var prop in zone.Props)
        {
            if (!models.TryGetValue(prop.Model, out var model))
            {
                model = GD.Load<PackedScene>($"res://assets/{prop.Model}.glb");
                models.Add(prop.Model, model);
            }

            var instance = model.Instantiate<Node3D>();
            instance.Position = new Vector3(prop.Position.X, prop.Position.Y, prop.Position.Z);
            instance.Rotation = new Vector3(0, prop.Yaw, 0);
            instance.Scale = Vector3.One * prop.Scale;
            root.AddChild(instance);
        }

        return root;
    }

    private static MeshInstance3D BuildTerrain(Zone zone)
    {
        var terrain = zone.World.Terrain!;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetSmoothGroup(uint.MaxValue);
        for (var row = 0; row < terrain.Cells; row++)
        {
            for (var column = 0; column < terrain.Cells; column++)
            {
                var a = Corner(terrain, column, row);
                var b = Corner(terrain, column + 1, row);
                var c = Corner(terrain, column, row + 1);
                var d = Corner(terrain, column + 1, row + 1);
                AddTriangle(surface, zone, a, b, c);
                AddTriangle(surface, zone, d, c, b);
            }
        }

        surface.GenerateNormals();
        return new MeshInstance3D
        {
            Mesh = surface.Commit(),
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 1 },
        };
    }

    private static Vector3 Corner(HeightField terrain, int column, int row) =>
        new(terrain.GridX(column), terrain.Vertex(column, row), terrain.GridX(row));

    /// <summary>Godot treats clockwise triangles as the front, so the order here faces up.</summary>
    private static void AddTriangle(SurfaceTool surface, Zone zone, Vector3 a, Vector3 b, Vector3 c)
    {
        var color = SurfaceColor(zone, a, b, c);
        foreach (var vertex in new[] { a, b, c })
        {
            surface.SetColor(color);
            surface.AddVertex(vertex);
        }
    }

    private static Color SurfaceColor(Zone zone, Vector3 a, Vector3 b, Vector3 c)
    {
        var middle = (a + b + c) / 3f;
        if (zone.PathDistance(middle.X, middle.Z) < 0)
        {
            return Path;
        }

        var steepness = (b - a).Cross(c - a).Normalized().Y;
        if (Mathf.Abs(steepness) < 0.72f)
        {
            return Rock;
        }

        var patch = Mathf.Sin(middle.X * 0.21f) + Mathf.Sin(middle.Z * 0.17f + middle.X * 0.05f);
        return patch > 1.1f ? LightGrass : patch < -1.0f ? DarkGrass : Grass;
    }
}
