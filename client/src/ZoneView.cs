using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>Builds what the player sees from the same zone data the server uses for collision.</summary>
public static class ZoneView
{
    /// <summary>Props shown or hidden by story flags, which the game toggles every frame.</summary>
    public static readonly System.Collections.Generic.List<(Node3D Node, string? Flag, string? Unless)> Conditional = new();

    private const int BatchMinimum = 8;
    private const float BatchCell = 60f;
    private const float SmallRange = 70f;
    private const float GrassPatch = 24f;
    private const int GrassPerPatch = 4200;
    private const float GrassRange = 95f;

    /// <summary>
    /// A prop's turn and size. A fence on a slope is sheared rather than tilted, so its length follows
    /// the ground while its posts still stand upright.
    /// </summary>
    private static Basis PropBasis(PlacedProp prop)
    {
        var sin = Mathf.Sin(prop.Yaw);
        var cos = Mathf.Cos(prop.Yaw);
        var along = new Vector3(cos, prop.Slope, -sin);
        return new Basis(along, Vector3.Up, new Vector3(sin, 0, cos)).Scaled(Vector3.One * prop.Scale);
    }

    /// <summary>Material name from Blender: detail texture, world scale and leaf sway.</summary>
    private static readonly Dictionary<string, (string Texture, float Scale, float Sway)> Surfaces = new()
    {
        ["wall"] = ("plaster", 0.5f, 0f),
        ["wall_b"] = ("plaster", 0.5f, 0f),
        ["roof"] = ("shingles", 0.6f, 0f),
        ["thatch"] = ("thatch", 0.8f, 0f),
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
        ["hide"] = ("plaster", 0.9f, 0f),
        ["foliage_core"] = ("leaves", 0.9f, 1f),
        ["bark"] = ("bark", 1.2f, 0f),
        ["bark_dark"] = ("bark", 1.2f, 0f),
        ["bark_birch"] = ("plaster", 1.4f, 0f),
        ["bark_grey"] = ("bark", 1.2f, 0f),
        ["reed"] = ("leaves", 2f, 1f),
        ["grass_tall"] = ("leaves", 2f, 1f),
        ["hide_dark"] = ("plaster", 0.9f, 0f),
        ["sand"] = ("plaster", 0.6f, 0f),
        ["crop"] = ("leaves", 1.2f, 0.4f),
        ["paper"] = ("plaster", 2f, 0f),
        ["window"] = ("plaster", 3f, 0f),
        ["pane"] = ("glass", 1.1f, 0f),
        ["glass"] = ("glass", 1.1f, 0f),
        ["moss"] = ("leaves", 1.2f, 0f),
        ["rock"] = ("rockface", 0.5f, 0f),
        ["rock_dark"] = ("rockface", 0.6f, 0f),
        ["iron"] = ("plaster", 2f, 0f),
    };

    public static Node3D Build(Zone zone)
    {
        var root = new Node3D { Name = zone.Name };
        root.AddChild(BuildTerrain(zone));
        if (!PixelStage.Off("grass"))
        {
            root.AddChild(BuildGrass(zone));
        }
        BuildWater(zone, root);
        BuildInteriorLights(root);
        root.AddChild(new Ambient(zone));

        var models = new Dictionary<string, PackedScene>();
        var shader = GD.Load<Shader>("res://shaders/prop.gdshader");
        var materials = new Dictionary<Material, ShaderMaterial>();
        var batched = BuildBatches(zone, root, shader, materials);
        Conditional.Clear();
        foreach (var prop in zone.Props)
        {
            if (batched.Contains(prop.Model) && !prop.Conditional)
            {
                continue;
            }

            if (!models.TryGetValue(prop.Model, out var model))
            {
                model = GD.Load<PackedScene>($"res://assets/{prop.Model}.glb");
                models.Add(prop.Model, model);
            }

            var instance = model.Instantiate<Node3D>();
            instance.Transform = new Transform3D(PropBasis(prop), new Vector3(prop.Position.X, prop.Position.Y, prop.Position.Z));
            if (prop.Conditional)
            {
                Conditional.Add((instance, prop.Flag, prop.Unless));
            }
            Retexture(instance, shader, materials);
            root.AddChild(instance);
            // The mill's sails hang under their own node, so they can turn.
            if (instance.FindChild("sails", true, false) is Node3D sails)
            {
                sails.AddChild(new Spinner());
            }

            if (prop.Model == "camp_fire")
            {
                root.AddChild(BuildCampFlames(new Vector3(prop.Position.X, prop.Position.Y, prop.Position.Z)));
            }
        }

        SplitLayers(root);
        return root;
    }

    /// <summary>Render layer of the static world outdoors; the game camera leaves it out while the player is inside.</summary>
    public const uint OutdoorLayer = 1u << 2;

    /// <summary>Render layer of the interiors and dungeons on their platforms off the map; left out while the player is outdoors.</summary>
    public const uint InteriorLayer = 1u << 1;

    /// <summary>Interiors start past this x; the map itself ends at 300.</summary>
    public const float InteriorX = 380f;

    /// <summary>
    /// Puts every static piece of the zone on the outdoor or the interior layer by where it stands. Without this the
    /// guild hall and the dungeon platforms hung in the sky beyond the eastern mountains.
    /// </summary>
    private static void SplitLayers(Node3D root)
    {
        foreach (var child in root.GetChildren())
        {
            var x = child switch
            {
                GeometryInstance3D geometry when geometry.GetAabb().Size != Vector3.Zero => (geometry.Transform * geometry.GetAabb()).GetCenter().X,
                Node3D node => node.Position.X,
                _ => 0f,
            };
            SetLayer(child, x > InteriorX ? InteriorLayer : OutdoorLayer);
        }
    }

    private static void SetLayer(Node node, uint layer)
    {
        if (node is VisualInstance3D visual)
        {
            visual.Layers = layer;
        }

        foreach (var child in node.GetChildren())
        {
            SetLayer(child, layer);
        }
    }

    private static void BuildWater(Zone zone, Node3D root)
    {
        var water = GD.Load<Shader>("res://shaders/water.gdshader");
        var lakes = zone.LakeSurfaces.Select(l => (Centre: new Vector3(l.Centre.X, l.Centre.Y, l.Centre.Z), l.Radius)).ToList();
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

                    // A stream joining a lake runs on under the lake's surface for a few metres, so the
                    // two waters meet without a gap or a square end.
                    var lake = lakes.FirstOrDefault(l => new Vector2(middle.X - l.Centre.X, middle.Z - l.Centre.Z).Length() < l.Radius * 1.6f);
                    var fromLake = lake.Radius > 0f ? new Vector2(middle.X - lake.Centre.X, middle.Z - lake.Centre.Z).Length() : float.MaxValue;
                    if (fromLake < lake.Radius * 0.7f)
                    {
                        continue;
                    }

                    // A stream only ever runs downhill, so its surface never climbs a bump in the bed.
                    level = Mathf.Min(level, zone.World.GroundHeight(middle.X, middle.Z) + 1.1f);
                    if (lake.Radius > 0f)
                    {
                        // Just under the lake where they overlap; level with it at the mouth.
                        level = Mathf.Min(level, lake.Centre.Y - (fromLake < lake.Radius * 1.12f ? 0.03f : 0.01f));
                    }
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

        var courses = zone.RiverCourses.ToList();
        foreach (var (centre, radius) in zone.LakeSurfaces)
        {
            root.AddChild(BuildLake(new Vector3(centre.X, centre.Y, centre.Z), radius, water, courses));
        }

        var cliff = zone.Props.FirstOrDefault(p => p.Model == "falls_cliff");
        if (cliff.Model is not null)
        {
            BuildWaterfall(root, new Vector3(cliff.Position.X, cliff.Position.Y, cliff.Position.Z), water);
        }
    }

    /// <summary>How far in front of the cliff's middle its chute is, at height fraction t (see falls_cliff in build_meshes.py).</summary>
    private static float ChuteFront(float t) => 9.8f * (1f - 0.34f * Mathf.Pow(t, 1.6f));

    /// <summary>
    /// The waterfall: a stream across the cliff top, a sheet that pours over the lip and then hugs the
    /// chute all the way down, spray where it lands, and mist that drifts off the pool.
    /// </summary>
    private static void BuildWaterfall(Node3D root, Vector3 cliff, Shader water)
    {
        const float height = 16f;
        var fall = new SurfaceTool();
        fall.Begin(Mesh.PrimitiveType.Triangles);
        var rows = new List<(Vector3 Left, Vector3 Right, float Along)>();
        var along = 0f;
        Vector3? last = null;

        // On top: from the spring to the lip.
        for (var back = 6f; back > 0f; back -= 1f)
        {
            var z = cliff.Z - (ChuteFront(1f) - back);
            AddRow(new Vector3(cliff.X, cliff.Y + height + 0.25f, z), 2.6f + (6f - back) * 0.12f);
        }

        // Over the lip: the water first leaves the rock in a short arc.
        for (var i = 0; i <= 4; i++)
        {
            var t = i / 4f;
            var z = cliff.Z - (ChuteFront(1f) + 0.4f + Mathf.Sin(t * Mathf.Pi * 0.5f) * 0.9f);
            AddRow(new Vector3(cliff.X, cliff.Y + height + 0.25f - t * t * 1.6f, z), 3.4f + t * 0.4f);
        }

        // Down the chute, half a metre in front of the rock.
        for (var i = 1; i <= 24; i++)
        {
            var t = 1f - i / 24f;
            var y = cliff.Y + (height - 1.4f) * t;
            var z = cliff.Z - (ChuteFront(Mathf.Clamp(y - cliff.Y, 0f, height) / height) + 0.55f + t * 0.5f);
            AddRow(new Vector3(cliff.X, y, z), 3.8f + (1f - t) * 1.2f);
        }

        for (var i = 1; i < rows.Count; i++)
        {
            var (a, b, u0) = rows[i - 1];
            var (c, d, u1) = rows[i];
            Vertex(fall, a, u0, 0f);
            Vertex(fall, c, u1, 0f);
            Vertex(fall, d, u1, 1f);
            Vertex(fall, a, u0, 0f);
            Vertex(fall, d, u1, 1f);
            Vertex(fall, b, u0, 1f);
        }

        var material = new ShaderMaterial { Shader = water };
        material.SetShaderParameter("flow", 2.2f);
        material.SetShaderParameter("foam", 0.75f);
        root.AddChild(new MeshInstance3D { Mesh = fall.Commit(), MaterialOverride = material });

        var foot = new Vector3(cliff.X, cliff.Y + 0.6f, cliff.Z - ChuteFront(0f) - 0.9f);
        root.AddChild(Spray(foot, new Vector3(2.8f, 0.2f, 0.8f), 70, 1.1f, 4f, 0.22f));
        root.AddChild(Spray(foot + new Vector3(0, 0.8f, -1.5f), new Vector3(4f, 0.6f, 2f), 24, 3f, 0.8f, 0.6f));

        void AddRow(Vector3 middle, float halfWidth)
        {
            if (last is { } previous)
            {
                along += previous.DistanceTo(middle);
            }

            last = middle;
            rows.Add((middle - Vector3.Right * halfWidth, middle + Vector3.Right * halfWidth, along));
        }
    }

    private static CpuParticles3D Spray(Vector3 at, Vector3 extents, int amount, float life, float speed, float size) => new()
    {
        Amount = amount,
        Lifetime = life,
        Position = at,
        Direction = Vector3.Up,
        Spread = 70f,
        InitialVelocityMin = speed * 0.4f,
        InitialVelocityMax = speed,
        Gravity = new Vector3(0.3f, -3f, -0.4f),
        EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
        EmissionBoxExtents = extents,
        Mesh = new SphereMesh { Radius = size, Height = size * 2f, RadialSegments = 6, Rings = 3 },
        MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("eaf7fa"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
    };

    /// <summary>A round sheet of water: the shader's foam runs along the rim, where V reaches 1.</summary>
    private static MeshInstance3D BuildLake(Vector3 centre, float radius, Shader water, List<(System.Numerics.Vector2[] Points, float Width)> rivers)
    {
        // The shore foam stops where a stream flows in or out: open water has no bank there.
        bool AtMouth(Vector3 point) => rivers.Any(r => Enumerable.Range(0, r.Points.Length - 1).Any(i =>
        {
            var a = new Vector2(r.Points[i].X, r.Points[i].Y);
            var b = new Vector2(r.Points[i + 1].X, r.Points[i + 1].Y);
            var p = new Vector2(point.X, point.Z);
            var t = Mathf.Clamp((p - a).Dot(b - a) / (b - a).LengthSquared(), 0f, 1f);
            return p.DistanceTo(a + (b - a) * t) < r.Width * 1.7f;
        }));

        const int sides = 40;
        const int rings = 6;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Point(int ring, int side)
        {
            var angle = side / (float)sides * Mathf.Tau;
            return centre + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (radius * 1.12f * ring / rings);
        }

        void Add(int ring, int side)
        {
            var point = Point(ring, side);
            var shore = ring >= rings - 1 && AtMouth(point) ? 0.4f : Mathf.Pow(ring / (float)rings, 5f);
            Vertex(surface, point, point.X * 0.5f, 0.5f + 0.5f * shore);
        }

        for (var ring = 0; ring < rings; ring++)
        {
            for (var side = 0; side < sides; side++)
            {
                Add(ring, side);
                Add(ring + 1, side);
                Add(ring + 1, side + 1);
                Add(ring, side);
                Add(ring + 1, side + 1);
                Add(ring, side + 1);
            }
        }

        return new MeshInstance3D { Mesh = surface.Commit(), MaterialOverride = new ShaderMaterial { Shader = water } };
    }

    private static void Vertex(SurfaceTool surface, Vector3 position, float u, float v)
    {
        surface.SetUV(new Vector2(u * 0.14f, v));
        surface.AddVertex(position);
    }

    /// <summary>
    /// Scattered models, which appear many times, are drawn as one MultiMesh per model and area
    /// instead of one node each. Small things stop drawing at a shorter distance.
    /// </summary>
    private static HashSet<string> BuildBatches(Zone zone, Node3D root, Shader shader, Dictionary<Material, ShaderMaterial> materials)
    {
        var groups = new Dictionary<(string Model, int X, int Z), List<PlacedProp>>();
        var counts = new Dictionary<string, int>();
        foreach (var prop in zone.Props)
        {
            counts[prop.Model] = counts.GetValueOrDefault(prop.Model) + 1;
        }

        var batched = new HashSet<string>(counts.Where(c => c.Value >= BatchMinimum && c.Key != "camp_fire").Select(c => c.Key));
        foreach (var prop in zone.Props.Where(p => batched.Contains(p.Model) && !p.Conditional))
        {
            var key = (prop.Model, (int)Mathf.Floor(prop.Position.X / BatchCell), (int)Mathf.Floor(prop.Position.Z / BatchCell));
            if (!groups.TryGetValue(key, out var list))
            {
                groups[key] = list = new List<PlacedProp>();
            }

            list.Add(prop);
        }

        // A model's mesh hangs under the glb root with its own offset (Blender keeps the first part's origin):
        // instances must carry that offset, or batched props sink or slide off their collision.
        var meshes = new Dictionary<string, (Mesh Mesh, Transform3D Local)>();
        foreach (var ((model, _, _), props) in groups)
        {
            if (PixelStage.Off("batches"))
            {
                continue;
            }

            if (!meshes.TryGetValue(model, out var entry))
            {
                var source = GD.Load<PackedScene>($"res://assets/{model}.glb").Instantiate<Node3D>();
                var part = FindMesh(source)!;
                var copy = (Mesh)part.Mesh.Duplicate();
                for (var surface = 0; surface < copy.GetSurfaceCount(); surface++)
                {
                    if (copy.SurfaceGetMaterial(surface) is StandardMaterial3D original)
                    {
                        if (!materials.TryGetValue(original, out var replacement))
                        {
                            replacement = BuildMaterial(original, shader);
                            materials.Add(original, replacement);
                        }

                        copy.SurfaceSetMaterial(surface, replacement);
                    }
                }

                var local = Transform3D.Identity;
                for (Node node = part; node != source && node is Node3D spatial; node = node.GetParent())
                {
                    local = spatial.Transform * local;
                }

                meshes[model] = entry = (copy, local);
                source.Free();
            }

            var mesh = entry.Mesh;
            var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = props.Count };
            for (var i = 0; i < props.Count; i++)
            {
                var p = props[i];
                multi.SetInstanceTransform(i, new Transform3D(PropBasis(p), new Vector3(p.Position.X, p.Position.Y, p.Position.Z)) * entry.Local);
            }

            var small = model is "flowers" or "twigs" or "grass_tuft" or "mushroom" or "rock_c" or "fern" or "reeds" or "stump";
            root.AddChild(new MultiMeshInstance3D
            {
                Multimesh = multi,
                VisibilityRangeEnd = small ? SmallRange : 0f,
                CastShadow = small ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On,
            });
        }

        return batched;
    }

    private static MeshInstance3D? FindMesh(Node node)
    {
        if (node is MeshInstance3D mesh)
        {
            return mesh;
        }

        foreach (var child in node.GetChildren())
        {
            if (FindMesh(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>A few flames and a warm light over a camp fire.</summary>
    private static Node3D BuildCampFlames(Vector3 position)
    {
        var multi = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new QuadMesh { Size = Vector2.One, CenterOffset = new Vector3(0, 0.5f, 0) },
            InstanceCount = 5,
        };
        for (var i = 0; i < 5; i++)
        {
            var angle = i * 1.26f;
            var spot = new Vector3(Mathf.Cos(angle) * 0.2f, 0.12f, Mathf.Sin(angle) * 0.2f);
            multi.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * (1f + (i % 2) * 0.35f)), spot));
            multi.SetInstanceCustomData(i, new Color(0f, i * 0.19f, 0f, 0f));
        }

        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/flame.gdshader") };
        material.SetShaderParameter("reach", 99f);
        var root = new Node3D { Position = position };
        root.AddChild(new MultiMeshInstance3D { Multimesh = multi, MaterialOverride = material });
        root.AddChild(new OmniLight3D { Position = Vector3.Up * 1.2f, LightColor = new Color("ffa050"), LightEnergy = 1.6f, OmniRange = 8f });
        return root;
    }

    /// <summary>Gives a model the same materials it has in the world.</summary>
    public static void Dress(Node model, Shader shader) => Retexture(model, shader, new Dictionary<Material, ShaderMaterial>());

    /// <summary>The guild hall and the trial ground lie outside the map, under their own lamps.</summary>
    private static void BuildInteriorLights(Node3D root)
    {
        foreach (var (x, z) in new[] { (416f, 2f), (424f, 2f), (416f, -4f), (424f, -4f) })
        {
            root.AddChild(new OmniLight3D { Position = new Vector3(x, 5f, z), LightColor = new Color("ffcf8a"), LightEnergy = 2.4f, OmniRange = 13f });
        }

        // A real fire burns in the guild hearth.
        root.AddChild(BuildCampFlames(new Vector3(430.55f, 0.15f, 3f)));

        // The Sunken Archive glows only where its runes still burn.
        foreach (var (x, z) in new[] { (543f, -6f), (551f, 6f), (569f, -6f), (577f, 6f), (560f, -15f), (580f, -7f) })
        {
            root.AddChild(new OmniLight3D { Position = new Vector3(x, 3.2f, z), LightColor = new Color("2ce8f5"), LightEnergy = 1.6f, OmniRange = 11f });
        }

        // The Goblin Warren's torches, besides its two fires.
        foreach (var (x, z) in new[] { (625f, -14.6f), (649f, -14.6f), (617.4f, 7f), (662.6f, -7f), (631f, 14.6f),
                     (705f, -14.6f), (729f, -14.6f), (697.4f, 7f), (742.6f, -7f), (711f, 14.6f) })
        {
            root.AddChild(new OmniLight3D { Position = new Vector3(x, 2.8f, z), LightColor = new Color("ff9a48"), LightEnergy = 1.8f, OmniRange = 10f });
        }

        // Greystone Mine: miners' lanterns hang from the beams down the drift.
        foreach (var (x, z) in new[] { (702f, 0f), (714f, -6f), (714f, 8f), (726f, 2f), (738f, -2f), (738f, 10f) })
        {
            root.AddChild(new OmniLight3D { Position = new Vector3(x, 3.6f, z), LightColor = new Color("ffc070"), LightEnergy = 2.2f, OmniRange = 12f });
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
        if (original.ResourceName.StartsWith("foliage_") && original.ResourceName != "foliage_core")
        {
            var foliage = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/foliage.gdshader") };
            foliage.SetShaderParameter("tint", original.AlbedoColor);
            foliage.SetShaderParameter("leaves", GD.Load<Texture2D>("res://assets/textures/foliage.png"));
            foliage.SetShaderParameter("deep", original.AlbedoColor.Lerp(new Color("123a38"), 0.72f));
            return foliage;
        }

        // Anything without its own surface (gold, flowers, cloth trim) gets a faint neutral grain,
        // not the masonry courses of built stone.
        var look = Surfaces.GetValueOrDefault(original.ResourceName, ("plaster", 1.2f, 0f));
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("tint", original.AlbedoColor);
        material.SetShaderParameter("detail", GD.Load<Texture2D>($"res://assets/textures/{look.Item1}.png"));
        material.SetShaderParameter("detail_scale", look.Item2);
        material.SetShaderParameter("sway", look.Item3);
        return material;
    }

    /// <summary>Cells along each side of one terrain chunk. Chunks off screen are not drawn.</summary>
    private const int TerrainChunk = 60;

    /// <summary>
    /// The ground as square chunks that share one material, so the camera culls what it cannot see.
    /// Each vertex is worked out once: its height, its normal from the slope around it (the same on
    /// both sides of a chunk seam, so the light does not jump), and its colour, which marks worn
    /// paths in red and steep rock in green for the shader.
    /// </summary>
    private static Node3D BuildTerrain(Zone zone)
    {
        var terrain = zone.World.Terrain!;
        var size = terrain.Cells + 1;
        var positions = new Vector3[size * size];
        var normals = new Vector3[size * size];
        var colors = new Color[size * size];
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                var x = terrain.GridX(column);
                var z = terrain.GridX(row);
                var left = terrain.Vertex(Math.Max(column - 1, 0), row);
                var right = terrain.Vertex(Math.Min(column + 1, terrain.Cells), row);
                var near = terrain.Vertex(column, Math.Max(row - 1, 0));
                var far = terrain.Vertex(column, Math.Min(row + 1, terrain.Cells));
                var span = terrain.CellSize * 2f;
                var slope = new Vector2(right - left, far - near).Length() / span;

                // Red fades out across the edge of the path, so the shader can cut a ragged line through it.
                // Trampled ground counts as path; blue marks forest floor and alpha falls with blight.
                var (forest, blight, trampled) = zone.GroundAt(x, z);
                var path = Mathf.Max(Mathf.Clamp(0.5f - zone.WornPathDistance(x, z) / 1.6f, 0f, 1f), trampled * 0.62f);
                var rock = Mathf.Clamp((slope - 0.55f) * 3f, 0f, 1f);
                var i = row * size + column;
                positions[i] = new Vector3(x, terrain.Vertex(column, row), z);
                normals[i] = new Vector3((left - right) / span, 1f, (near - far) / span).Normalized();
                colors[i] = new Color(path, rock, forest, 1f - blight);
            }
        }

        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/terrain.gdshader") };
        foreach (var (uniform, file) in new[] { ("grass_tex", "grass"), ("dry_tex", "grass_dry"), ("dirt_tex", "dirt"), ("rock_tex", "rock") })
        {
            material.SetShaderParameter(uniform, GD.Load<Texture2D>($"res://assets/textures/{file}.png"));
        }

        var root = new Node3D { Name = "Terrain" };
        for (var top = 0; top < terrain.Cells; top += TerrainChunk)
        {
            for (var left = 0; left < terrain.Cells; left += TerrainChunk)
            {
                var surface = new SurfaceTool();
                surface.Begin(Mesh.PrimitiveType.Triangles);
                var rows = Math.Min(TerrainChunk, terrain.Cells - top);
                var columns = Math.Min(TerrainChunk, terrain.Cells - left);
                for (var row = 0; row <= rows; row++)
                {
                    for (var column = 0; column <= columns; column++)
                    {
                        var i = (top + row) * size + left + column;
                        surface.SetNormal(normals[i]);
                        surface.SetColor(colors[i]);
                        surface.AddVertex(positions[i]);
                    }
                }

                var stride = columns + 1;
                for (var row = 0; row < rows; row++)
                {
                    for (var column = 0; column < columns; column++)
                    {
                        var a = row * stride + column;
                        surface.AddIndex(a);
                        surface.AddIndex(a + 1);
                        surface.AddIndex(a + stride);
                        surface.AddIndex(a + stride + 1);
                        surface.AddIndex(a + stride);
                        surface.AddIndex(a + 1);
                    }
                }

                root.AddChild(new MeshInstance3D { Mesh = surface.Commit(), MaterialOverride = material });
            }
        }

        return root;
    }

    /// <summary>
    /// Marks each metre of the zone where no grass grows. Asking the paths and rivers once per cell
    /// instead of once per blade keeps loading fast.
    /// </summary>
    /// <summary>Cells of the grass mask per metre: fine enough that grass stops at the foot of a rock.</summary>
    private const int MaskPerMetre = 2;

    private static bool[,] BuildGrassMask(Zone zone)
    {
        var cells = (int)zone.Size * MaskPerMetre;
        var mask = new bool[cells, cells];
        for (var row = 0; row < cells; row++)
        {
            for (var column = 0; column < cells; column++)
            {
                var x = (column + 0.5f) / MaskPerMetre - zone.Size / 2f;
                var z = (row + 0.5f) / MaskPerMetre - zone.Size / 2f;
                mask[row, column] = zone.PathDistance(x, z) < 0.6f || zone.IsBare(x, z) || zone.IsCovered(x, z);
            }
        }

        return mask;
    }

    private static bool IsClearOfGrass(bool[,] mask, float size, float x, float z)
    {
        var column = Mathf.Clamp((int)((x + size / 2f) * MaskPerMetre), 0, mask.GetLength(1) - 1);
        var row = Mathf.Clamp((int)((z + size / 2f) * MaskPerMetre), 0, mask.GetLength(0) - 1);
        return mask[row, column];
    }

    /// <summary>
    /// Dense grass in square patches. Each patch is one draw call and stops drawing beyond a set
    /// distance, so the meadow is thick around the player and costs nothing far away.
    /// </summary>
    private static Node3D BuildGrass(Zone zone)
    {
        var blade = BuildBlade();
        var blocked = BuildGrassMask(zone);
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/grass.gdshader") };
        var random = new Random(11);
        var colors = new[] { new Color("4f9a4e"), new Color("5ab552"), new Color("63c74d"), new Color("7ac85a"), new Color("8fce5a") };
        var root = new Node3D { Name = "Grass" };
        var patches = (int)(zone.Size / GrassPatch);
        for (var row = 0; row < patches; row++)
        {
            for (var column = 0; column < patches; column++)
            {
                var left = column * GrassPatch - zone.Size / 2f;
                var top = row * GrassPatch - zone.Size / 2f;
                var multi = new MultiMesh
                {
                    TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                    UseColors = true,
                    Mesh = blade,
                    InstanceCount = GrassPerPatch,
                };
                var placed = 0;
                for (var i = 0; i < GrassPerPatch; i++)
                {
                    var x = left + (float)random.NextDouble() * GrassPatch;
                    var z = top + (float)random.NextDouble() * GrassPatch;
                    if (IsClearOfGrass(blocked, zone.Size, x, z) || MathF.Max(MathF.Abs(x), MathF.Abs(z)) > zone.Size * 0.47f)
                    {
                        continue;
                    }

                    // Under the trees the grass thins out; on trampled earth none grows.
                    var (forest, blight, trampled) = zone.GroundAt(x, z);
                    if (random.NextDouble() < forest * 0.72f || trampled > 0.45f)
                    {
                        continue;
                    }

                    var basis = Basis.Identity.Rotated(Vector3.Up, (float)random.NextDouble() * Mathf.Tau)
                        .Scaled(Vector3.One * (0.7f + (float)random.NextDouble() * 0.8f));
                    multi.SetInstanceTransform(placed, new Transform3D(basis, new Vector3(x, zone.World.GroundHeight(x, z), z)));

                    // Patches of paler and deeper grass, so a meadow is never one flat colour.
                    var patch = Mathf.Sin(x * 0.09f + 1f) * Mathf.Cos(z * 0.07f) * 0.5f + 0.5f;
                    var pick = Mathf.Clamp((int)(patch * colors.Length + random.Next(-1, 2)), 0, colors.Length - 1);
                    var shade = colors[pick].Lerp(new Color("4a5a3a"), forest * 0.45f).Lerp(new Color("8a8468"), blight * 0.7f);
                    multi.SetInstanceColor(placed, shade.SrgbToLinear());
                    placed++;
                }

                multi.VisibleInstanceCount = placed;
                if (placed > 0)
                {
                    root.AddChild(new MultiMeshInstance3D { Multimesh = multi, MaterialOverride = material, VisibilityRangeEnd = GrassRange, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                }
            }
        }

        return root;
    }

    /// <summary>One narrow curved blade.</summary>
    private static ArrayMesh BuildBlade()
    {
        var blade = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new[]
        {
            new Vector3(-0.035f, 0, 0), new Vector3(0.035f, 0, 0),
            new Vector3(-0.028f, 0.22f, 0.02f), new Vector3(0.028f, 0.22f, 0.02f),
            new Vector3(0.01f, 0.46f, 0.08f),
        };
        arrays[(int)Mesh.ArrayType.Normal] = new[] { Vector3.Up, Vector3.Up, Vector3.Up, Vector3.Up, Vector3.Up };
        arrays[(int)Mesh.ArrayType.Index] = new[] { 0, 1, 2, 2, 1, 3, 2, 3, 4 };
        blade.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return blade;
    }
}
