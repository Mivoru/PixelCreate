using System;
using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>Builds what the player sees from the same zone data the server uses for collision.</summary>
public static class ZoneView
{
    private const int GrassBlades = 220000;

    /// <summary>Material name from Blender: detail texture, world scale and leaf sway.</summary>
    private static readonly Dictionary<string, (string Texture, float Scale, float Sway)> Surfaces = new()
    {
        ["wall"] = ("plaster", 0.5f, 0f),
        ["wall_b"] = ("plaster", 0.5f, 0f),
        ["roof"] = ("shingles", 0.6f, 0f),
        ["thatch"] = ("leaves", 0.7f, 0f),
        ["hay"] = ("leaves", 0.9f, 0f),
        ["sack"] = ("plaster", 0.9f, 0f),
        ["shutter"] = ("planks", 0.9f, 0f),
        ["cloth_red"] = ("plaster", 0.8f, 0f),
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
        BuildWater(zone, root);
        BuildInteriorLights(root);

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

    private static void BuildWater(Zone zone, Node3D root)
    {
        var water = GD.Load<Shader>("res://shaders/water.gdshader");
        foreach (var (points, width) in zone.RiverCourses)
        {
            var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            var travelled = 0f;
            Vector3? lastLeft = null;
            Vector3? lastRight = null;
            var lastTravelled = 0f;
            var level = float.MaxValue;
            for (var i = 0; i + 1 < points.Length; i++)
            {
                var a = new Vector3(points[i].X, 0, points[i].Y);
                var b = new Vector3(points[i + 1].X, 0, points[i + 1].Y);
                var length = a.DistanceTo(b);
                var side = new Vector3(-(b - a).Normalized().Z, 0, (b - a).Normalized().X) * width * 1.25f;
                for (var along = 0f; along < length; along += 2f)
                {
                    var middle = a.Lerp(b, along / length);
                    // A stream only ever runs downhill, so its surface never climbs a bump in the bed.
                    level = Mathf.Min(level, zone.World.GroundHeight(middle.X, middle.Z) + 1.1f);
                    var left = middle - side + Vector3.Up * level;
                    var right = middle + side + Vector3.Up * level;
                    travelled += 2f;
                    if (lastLeft is { } previousLeft && lastRight is { } previousRight)
                    {
                        Vertex(surface, previousLeft, lastTravelled, 0f);
                        Vertex(surface, left, travelled, 0f);
                        Vertex(surface, right, travelled, 1f);
                        Vertex(surface, previousLeft, lastTravelled, 0f);
                        Vertex(surface, right, travelled, 1f);
                        Vertex(surface, previousRight, lastTravelled, 1f);
                    }

                    lastLeft = left;
                    lastRight = right;
                    lastTravelled = travelled;
                }
            }

            root.AddChild(new MeshInstance3D { Mesh = surface.Commit(), MaterialOverride = new ShaderMaterial { Shader = water } });
        }

        if (zone.Waterfall is { } source)
        {
            var foot = new Vector3(source.X, source.Y, source.Z);
            var material = new ShaderMaterial { Shader = water };
            material.SetShaderParameter("flow", 1.1f);
            material.SetShaderParameter("foam", 0.9f);
            var fall = new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(16f, 9f), Orientation = PlaneMesh.OrientationEnum.Z },
                MaterialOverride = material,
                Position = new Vector3(foot.X, foot.Y + 8f, foot.Z + 1.5f),
                Rotation = new Vector3(0, 0, Mathf.Pi / 2f),
            };
            root.AddChild(fall);
            root.AddChild(new CpuParticles3D
            {
                Amount = 60,
                Lifetime = 1.1f,
                Position = foot + Vector3.Up * 1.4f,
                Direction = Vector3.Up,
                Spread = 70f,
                InitialVelocityMin = 1.5f,
                InitialVelocityMax = 4f,
                Gravity = new Vector3(0, -4f, 0),
                EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
                EmissionBoxExtents = new Vector3(4f, 0.2f, 1f),
                Mesh = new SphereMesh { Radius = 0.2f, Height = 0.4f, RadialSegments = 5, Rings = 3 },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("e8f6f8"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            });
        }
    }

    private static void Vertex(SurfaceTool surface, Vector3 position, float u, float v)
    {
        surface.SetUV(new Vector2(u * 0.14f, v));
        surface.AddVertex(position);
    }

    /// <summary>The guild hall and the trial ground lie outside the map, under their own lamps.</summary>
    private static void BuildInteriorLights(Node3D root)
    {
        foreach (var (x, z) in new[] { (416f, 2f), (424f, 2f), (416f, -4f), (424f, -4f) })
        {
            root.AddChild(new OmniLight3D { Position = new Vector3(x, 5f, z), LightColor = new Color("ffcf8a"), LightEnergy = 2.4f, OmniRange = 13f });
        }
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

    /// <summary>
    /// Marks each metre of the zone where no grass grows. Asking the paths and rivers once per cell
    /// instead of once per blade keeps loading fast.
    /// </summary>
    private static bool[,] BuildGrassMask(Zone zone)
    {
        var cells = (int)zone.Size;
        var mask = new bool[cells, cells];
        for (var row = 0; row < cells; row++)
        {
            for (var column = 0; column < cells; column++)
            {
                var x = column - zone.Size / 2f + 0.5f;
                var z = row - zone.Size / 2f + 0.5f;
                mask[row, column] = zone.PathDistance(x, z) < 0.6f || zone.IsBare(x, z);
            }
        }

        return mask;
    }

    private static bool IsClearOfGrass(bool[,] mask, float size, float x, float z)
    {
        var column = Mathf.Clamp((int)(x + size / 2f), 0, mask.GetLength(1) - 1);
        var row = Mathf.Clamp((int)(z + size / 2f), 0, mask.GetLength(0) - 1);
        return mask[row, column];
    }

    /// <summary>Thousands of small blades in one draw call, kept off paths and steep ground.</summary>
    private static MultiMeshInstance3D BuildGrass(Zone zone)
    {
        var blade = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new[]
        {
            new Vector3(-0.05f, 0, 0), new Vector3(0.05f, 0, 0), new Vector3(0, 0.34f, 0),
            new Vector3(0, 0, -0.05f), new Vector3(0, 0, 0.05f), new Vector3(0.05f, 0.28f, 0.02f),
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

        var blocked = BuildGrassMask(zone);
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
            if (IsClearOfGrass(blocked, zone.Size, x, z))
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
