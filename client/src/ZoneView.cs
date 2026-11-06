using System;
using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>Builds what the player sees from the same zone data the server uses for collision.</summary>
public static class ZoneView
{
    private const int GrassBlades = 90000;

    /// <summary>Material name from Blender: detail texture, world scale and leaf sway.</summary>
    private static readonly Dictionary<string, (string Texture, float Scale, float Sway)> Surfaces = new()
    {
        ["wall"] = ("plaster", 0.5f, 0f),
        ["roof"] = ("shingles", 0.6f, 0f),
        ["wood"] = ("planks", 0.7f, 0f),
        ["wood_dark"] = ("planks", 0.7f, 0f),
        ["stone"] = ("stone", 0.6f, 0f),
        ["stone_dark"] = ("stone", 0.8f, 0f),
        ["grass_dark"] = ("leaves", 0.9f, 1f),
        ["grass"] = ("leaves", 0.9f, 1f),
        ["leaf"] = ("leaves", 0.9f, 1f),
    };

    public static Node3D Build(Zone zone)
    {
        var root = new Node3D { Name = zone.Name };
        root.AddChild(BuildTerrain(zone));
        root.AddChild(BuildGrass(zone));

        var models = new Dictionary<string, PackedScene>();
        var shader = GD.Load<Shader>("res://shaders/prop.gdshader");
        var materials = new Dictionary<Material, ShaderMaterial>();
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
            Retexture(instance, shader, materials);
            root.AddChild(instance);
        }

        return root;
    }

    /// <summary>Swaps the flat Blender materials for the textured prop shader.</summary>
    private static void Retexture(Node node, Shader shader, Dictionary<Material, ShaderMaterial> materials)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh is not null)
        {
            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.Mesh.SurfaceGetMaterial(surface) is StandardMaterial3D original)
                {
                    if (!materials.TryGetValue(original, out var replacement))
                    {
                        replacement = BuildMaterial(original, shader);
                        materials.Add(original, replacement);
                    }

                    mesh.SetSurfaceOverrideMaterial(surface, replacement);
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            Retexture(child, shader, materials);
        }
    }

    private static ShaderMaterial BuildMaterial(StandardMaterial3D original, Shader shader)
    {
        var look = Surfaces.GetValueOrDefault(original.ResourceName, ("stone", 0.6f, 0f));
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("tint", original.AlbedoColor);
        material.SetShaderParameter("detail", GD.Load<Texture2D>($"res://assets/textures/{look.Item1}.png"));
        material.SetShaderParameter("detail_scale", look.Item2);
        material.SetShaderParameter("sway", look.Item3);
        return material;
    }

    private static MeshInstance3D BuildTerrain(Zone zone)
    {
        var terrain = zone.World.Terrain!;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (var row = 0; row < terrain.Cells; row++)
        {
            for (var column = 0; column < terrain.Cells; column++)
            {
                AddVertex(surface, zone, terrain, column, row);
                AddVertex(surface, zone, terrain, column + 1, row);
                AddVertex(surface, zone, terrain, column, row + 1);
                AddVertex(surface, zone, terrain, column + 1, row + 1);
                AddVertex(surface, zone, terrain, column, row + 1);
                AddVertex(surface, zone, terrain, column + 1, row);
            }
        }

        surface.Index();
        surface.GenerateNormals();
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/terrain.gdshader") };
        foreach (var (uniform, file) in new[] { ("grass_tex", "grass"), ("dry_tex", "grass_dry"), ("dirt_tex", "dirt"), ("rock_tex", "rock") })
        {
            material.SetShaderParameter(uniform, GD.Load<Texture2D>($"res://assets/textures/{file}.png"));
        }

        return new MeshInstance3D { Mesh = surface.Commit(), MaterialOverride = material };
    }

    private static void AddVertex(SurfaceTool surface, Zone zone, HeightField terrain, int column, int row)
    {
        var x = terrain.GridX(column);
        var z = terrain.GridX(row);
        var left = terrain.Vertex(Math.Max(column - 1, 0), row);
        var right = terrain.Vertex(Math.Min(column + 1, terrain.Cells), row);
        var near = terrain.Vertex(column, Math.Max(row - 1, 0));
        var far = terrain.Vertex(column, Math.Min(row + 1, terrain.Cells));
        var slope = new Vector2(right - left, far - near).Length() / (terrain.CellSize * 2f);

        // Red fades out across the edge of the path, so the shader can cut a ragged line through it.
        var path = Mathf.Clamp(0.5f - zone.PathDistance(x, z) / 1.6f, 0f, 1f);
        var rock = Mathf.Clamp((slope - 0.55f) * 3f, 0f, 1f);
        surface.SetColor(new Color(path, rock, 0f));
        surface.AddVertex(new Vector3(x, terrain.Vertex(column, row), z));
    }

    /// <summary>Thousands of small blades in one draw call, kept off paths and steep ground.</summary>
    private static MultiMeshInstance3D BuildGrass(Zone zone)
    {
        var blade = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new[]
        {
            new Vector3(-0.11f, 0, 0), new Vector3(0.11f, 0, 0), new Vector3(0, 0.3f, 0),
            new Vector3(0, 0, -0.11f), new Vector3(0, 0, 0.11f), new Vector3(0.05f, 0.25f, 0.02f),
        };
        arrays[(int)Mesh.ArrayType.Normal] = new[] { Vector3.Up, Vector3.Up, Vector3.Up, Vector3.Up, Vector3.Up, Vector3.Up };
        blade.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        var multi = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = blade,
            InstanceCount = GrassBlades,
        };

        var random = new Random(11);
        var colors = new[] { new Color("3e8948"), new Color("4f9a4e"), new Color("5ab552"), new Color("63c74d") };
        var half = zone.Size / 2 - 4f;
        var placed = 0;
        for (var attempt = 0; attempt < GrassBlades * 3 && placed < GrassBlades; attempt++)
        {
            // Half of the blades stay near the middle of the zone, where players spend their time.
            var spread = attempt % 2 == 0 ? 0.5f : 1f;
            var x = ((float)random.NextDouble() * 2 - 1) * half * spread;
            var z = ((float)random.NextDouble() * 2 - 1) * half * spread;
            if (zone.PathDistance(x, z) < 0.6f)
            {
                continue;
            }

            var y = zone.World.GroundHeight(x, z);
            var basis = Basis.Identity.Rotated(Vector3.Up, (float)random.NextDouble() * Mathf.Tau)
                .Scaled(Vector3.One * (0.7f + (float)random.NextDouble() * 0.9f));
            multi.SetInstanceTransform(placed, new Transform3D(basis, new Vector3(x, y, z)));
            multi.SetInstanceColor(placed, colors[random.Next(colors.Length)].SrgbToLinear());
            placed++;
        }

        multi.VisibleInstanceCount = placed;
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/grass.gdshader") };
        return new MultiMeshInstance3D { Multimesh = multi, MaterialOverride = material };
    }
}
