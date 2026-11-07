using System.Numerics;
using System.Reflection;
using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>Ground heights on a regular grid centred on the origin.</summary>
public sealed class HeightField
{
    private readonly float[] _heights;

    public float Size { get; }
    public int Cells { get; }
    public float CellSize => Size / Cells;

    public HeightField(float size, int cells, Func<float, float, float> height)
    {
        Size = size;
        Cells = cells;
        _heights = new float[(cells + 1) * (cells + 1)];
        for (var row = 0; row <= cells; row++)
        {
            for (var column = 0; column <= cells; column++)
            {
                _heights[row * (cells + 1) + column] = height(GridX(column), GridX(row));
            }
        }
    }

    public float GridX(int index) => index * CellSize - Size / 2;

    public float Vertex(int column, int row) => _heights[row * (Cells + 1) + column];

    /// <summary>Height at a point, interpolated across the two triangles of its cell.</summary>
    public float At(float x, float z)
    {
        var gridX = Math.Clamp((x + Size / 2) / CellSize, 0f, Cells - 0.001f);
        var gridZ = Math.Clamp((z + Size / 2) / CellSize, 0f, Cells - 0.001f);
        var column = (int)gridX;
        var row = (int)gridZ;
        var u = gridX - column;
        var v = gridZ - row;
        var a = Vertex(column, row);
        var b = Vertex(column + 1, row);
        var c = Vertex(column, row + 1);
        var d = Vertex(column + 1, row + 1);
        return u + v <= 1f ? a + (b - a) * u + (c - a) * v : d + (c - d) * (1f - u) + (b - d) * (1f - v);
    }
}

public readonly record struct PlacedProp(string Model, Vector3 Position, float Scale, float Yaw);

/// <summary>
/// A part of the world, built from a hand-written file in world/zones. The layout is authored;
/// the scattered detail comes from the zone seed. Client and server build the same zone from it.
/// </summary>
public sealed class Zone
{
    private sealed record HillData(float X, float Z, float Radius, float Height);

    private sealed record PathData(float Width, float[][] Points);

    private sealed record PropData(string Model, float X, float Z, float Yaw = 0f);

    private sealed record ScatterData(string Model, int Count, float[] Area, float MinScale, float MaxScale, float Spacing);

    private sealed record EnemySpawnData(
        float X, float Z, float Radius, int Count, int MinLevel = 1, int MaxLevel = 3, int RespawnTicks = 900,
        bool Rare = false, string[]? Types = null);

    private sealed record RegionData(string Name, float X, float Z, float Radius);

    private sealed record RiverData(float Width, float Depth, float[][] Points);

    private sealed record RectData(float X0, float Z0, float X1, float Z1, float Height = 4f);

    private sealed record PortalData(float X, float Z, float ToX, float ToZ);

    private sealed record NpcData(string Id, string Name, string Role, float X, float Z, float Yaw = 0f);

    private sealed record ZoneData(
        string Name, float Size, int Cells, int Seed, float[] Spawn, HillData[] Hills, PathData[] Paths,
        PropData[] Props, ScatterData[] Scatter, EnemySpawnData[]? Enemies, RegionData[]? Regions,
        RiverData[]? Rivers = null, RectData[]? Platforms = null, RectData[]? Walls = null, PortalData[]? Portals = null,
        NpcData[]? Npcs = null, float[]? Trial = null, float[]? Waterfall = null);

    /// <summary>Half width, half depth and height of each model's collision at scale 1.</summary>
    private static readonly Dictionary<string, Vector3> Collision = new()
    {
        ["cottage"] = new(3.7f, 2.9f, 6f),
        ["cottage_b"] = new(4.2f, 2.8f, 6f),
        ["tavern"] = new(4.9f, 3.3f, 9f),
        ["guild_hall"] = new(8.4f, 6.2f, 12f),
        ["cliff"] = new(4.5f, 3.5f, 11f),
        ["ruin_arch"] = new(0f, 0f, 0f),
        ["ruin_wall"] = new(3f, 0.5f, 4f),
        ["jungle_tree"] = new(0.7f, 0.7f, 11f),
        ["fern"] = new(0f, 0f, 0f),
        ["bridge"] = new(0f, 0f, 0f),
        ["training_dummy"] = new(0.3f, 0.3f, 1.8f),
        ["guild_interior"] = new(0f, 0f, 0f),
        ["trial_arena"] = new(0f, 0f, 0f),
        ["well"] = new(1.05f, 1.05f, 3f),
        ["stall"] = new(1.7f, 1.2f, 2.6f),
        ["barrel"] = new(0.42f, 0.42f, 1.1f),
        ["crate"] = new(0.47f, 0.47f, 0.9f),
        ["sacks"] = new(0.5f, 0.5f, 0.9f),
        ["cart"] = new(1.5f, 0.9f, 1.6f),
        ["bench"] = new(0.95f, 0.28f, 0.6f),
        ["lantern_post"] = new(0.14f, 0.14f, 2.9f),
        ["signpost"] = new(0.14f, 0.14f, 2.3f),
        ["hay"] = new(0.85f, 0.85f, 1.4f),
        ["log_pile"] = new(1.1f, 0.85f, 1.1f),
        ["fence"] = new(1.6f, 0.1f, 1f),
        ["stone_wall"] = new(2.1f, 0.35f, 0.8f),
        ["rock_b"] = new(1f, 1f, 1.1f),
        ["rock_c"] = new(0.6f, 0.6f, 0.5f),
        ["flowers"] = new(0f, 0f, 0f),
        ["mushroom"] = new(0f, 0f, 0f),
        ["camp_set"] = new(0f, 0f, 0f),
        ["tree_a"] = new(0.5f, 0.5f, 6f),
        ["tree_b"] = new(0.5f, 0.5f, 6f),
        ["rock"] = new(0.55f, 0.55f, 0.7f),
        ["bush"] = new(0f, 0f, 0f),
        ["tent"] = new(2.1f, 2.1f, 3f),
        ["camp_fire"] = new(0.7f, 0.7f, 0.6f),
        ["palisade"] = new(4.2f, 0.4f, 3f),
        ["ruin"] = new(1.1f, 1.1f, 4f),
    };

    private readonly PathData[] _paths;
    private readonly RegionData[] _regions;

    public string Name { get; }
    public float Size { get; }
    public World World { get; }
    public IReadOnlyList<PlacedProp> Props { get; }

    /// <summary>The foot of the waterfall, when the zone has one.</summary>
    public Vector3? Waterfall { get; }

    private RiverData[] Rivers { get; }

    /// <summary>Points along each river, in order, with the width at that point.</summary>
    public IEnumerable<(Vector2[] Points, float Width)> RiverCourses =>
        Rivers.Select(r => (r.Points.Select(p => new Vector2(p[0], p[1])).ToArray(), r.Width));

    private Zone(ZoneData data)
    {
        Name = data.Name;
        Size = data.Size;
        _paths = data.Paths;
        var terrain = new HeightField(data.Size, data.Cells, (x, z) => TerrainHeight(data, x, z));
        var random = new Random(data.Seed);
        var props = new List<PlacedProp>();
        var platforms = (data.Platforms ?? Array.Empty<RectData>()).Select(p => new Platform(p.X0, p.Z0, p.X1, p.Z1, p.Height)).ToArray();
        var ground = new World(Array.Empty<Aabb>(), terrain) { Platforms = platforms };
        foreach (var prop in data.Props)
        {
            var position = new Vector3(prop.X, ground.GroundHeight(prop.X, prop.Z), prop.Z);
            props.Add(new PlacedProp(prop.Model, position, 1f, prop.Yaw));
        }

        foreach (var scatter in data.Scatter)
        {
            Scatter(scatter, terrain, random, props);
        }

        Props = props;
        var spawn = new Vector3(data.Spawn[0], terrain.At(data.Spawn[0], data.Spawn[1]), data.Spawn[1]);
        var enemySpawns = (data.Enemies ?? Array.Empty<EnemySpawnData>())
            .Select(e => new EnemySpawn(new Vector3(e.X, terrain.At(e.X, e.Z), e.Z), e.Radius, e.Count, e.MinLevel, e.MaxLevel, e.RespawnTicks, e.Rare, e.Types)).ToArray();
        _regions = data.Regions ?? Array.Empty<RegionData>();
        var boxes = BuildBoxes(props, data.Size);
        boxes.AddRange((data.Walls ?? Array.Empty<RectData>()).Select(w => new Aabb(new(w.X0, -2, w.Z0), new(w.X1, w.Height, w.Z1))));
        Vector3 Place(float x, float z) => new(x, ground.GroundHeight(x, z), z);
        var trial = data.Trial is { } t ? Place(t[0], t[1]) : spawn;
        World = new World(boxes, terrain, spawn, enemySpawns)
        {
            Platforms = platforms,
            TrialGround = trial,
            Portals = (data.Portals ?? Array.Empty<PortalData>()).Select(p => new Portal(Place(p.X, p.Z), Place(p.ToX, p.ToZ))).ToArray(),
            Npcs = (data.Npcs ?? Array.Empty<NpcData>()).Select(n => new Npc(n.Id, n.Name, n.Role, Place(n.X, n.Z), n.Yaw)).ToArray(),
        };
        Rivers = data.Rivers ?? Array.Empty<RiverData>();
        Waterfall = data.Waterfall is { } w ? new Vector3(w[0], terrain.At(w[0], w[1]), w[1]) : null;
    }

    public static Zone Load(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream($"zones.{name}")
            ?? throw new ArgumentException($"There is no zone called {name}.");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return new Zone(JsonSerializer.Deserialize<ZoneData>(stream, options)!);
    }

    /// <summary>True on water or on a platform, where no grass should grow.</summary>
    public bool IsBare(float x, float z)
    {
        foreach (var platform in World.Platforms)
        {
            if (x >= platform.MinX && x <= platform.MaxX && z >= platform.MinZ && z <= platform.MaxZ)
            {
                return true;
            }
        }

        return Rivers.Any(r => PolylineDistance(r.Points, x, z) < r.Width * 1.45f);
    }

    /// <summary>The name of the place at a point, or an empty string in the wilds between places.</summary>
    public string RegionAt(float x, float z)
    {
        foreach (var region in _regions)
        {
            if (new Vector2(x - region.X, z - region.Z).Length() < region.Radius)
            {
                return region.Name;
            }
        }

        return "";
    }

    /// <summary>Distance from a point to the edge of the nearest path; negative means on the path.</summary>
    public float PathDistance(float x, float z)
    {
        var point = new Vector2(x, z);
        var nearest = float.MaxValue;
        foreach (var path in _paths)
        {
            for (var i = 0; i + 1 < path.Points.Length; i++)
            {
                var a = new Vector2(path.Points[i][0], path.Points[i][1]);
                var b = new Vector2(path.Points[i + 1][0], path.Points[i + 1][1]);
                var along = Math.Clamp(Vector2.Dot(point - a, b - a) / (b - a).LengthSquared(), 0f, 1f);
                nearest = MathF.Min(nearest, Vector2.Distance(point, a + (b - a) * along) - path.Width / 2);
            }
        }

        return nearest;
    }

    private static float TerrainHeight(ZoneData data, float x, float z)
    {
        var height = 0f;
        foreach (var hill in data.Hills)
        {
            var distance = new Vector2(x - hill.X, z - hill.Z).Length() / hill.Radius;
            if (distance < 1f)
            {
                var falloff = 1f - distance * distance;
                height += hill.Height * falloff * falloff;
            }
        }

        foreach (var river in data.Rivers ?? Array.Empty<RiverData>())
        {
            var distance = PolylineDistance(river.Points, x, z);
            var bank = river.Width * 1.8f;
            if (distance < bank)
            {
                var t = 1f - distance / bank;
                height -= river.Depth * t * t * (3f - 2f * t);
            }
        }

        // Mountains close the zone, so there is no visible end of the world.
        var edge = MathF.Max(MathF.Abs(x), MathF.Abs(z)) / (data.Size / 2);
        if (edge > 0.84f)
        {
            var rise = (edge - 0.84f) / 0.16f;
            height += rise * rise * 55f + MathF.Sin(x * 0.11f) * MathF.Cos(z * 0.13f) * 5f * rise;
        }

        return height + Bumps(data.Seed, x, z);
    }

    private static float PolylineDistance(float[][] points, float x, float z)
    {
        var point = new Vector2(x, z);
        var nearest = float.MaxValue;
        for (var i = 0; i + 1 < points.Length; i++)
        {
            var a = new Vector2(points[i][0], points[i][1]);
            var b = new Vector2(points[i + 1][0], points[i + 1][1]);
            var along = Math.Clamp(Vector2.Dot(point - a, b - a) / (b - a).LengthSquared(), 0f, 1f);
            nearest = MathF.Min(nearest, Vector2.Distance(point, a + (b - a) * along));
        }

        return nearest;
    }

    /// <summary>Small uneven ground from a position hash, so it does not depend on visit order.</summary>
    private static float Bumps(int seed, float x, float z)
    {
        // Integers only: casting a negative float to uint gives different results on different runtimes.
        var cellX = (uint)(int)MathF.Round(x * 2f);
        var cellZ = (uint)(int)MathF.Round(z * 2f);
        var hash = unchecked((cellX * 73856093u) ^ (cellZ * 19349663u) ^ (uint)seed);
        hash = unchecked((hash ^ (hash >> 13)) * 1274126177u);
        return ((hash ^ (hash >> 16)) & 0xFFFF) / 65535f * 0.18f;
    }

    private void Scatter(ScatterData scatter, HeightField terrain, Random random, List<PlacedProp> props)
    {
        var placed = 0;
        for (var attempt = 0; attempt < scatter.Count * 20 && placed < scatter.Count; attempt++)
        {
            var x = scatter.Area[0] + (float)random.NextDouble() * (scatter.Area[2] - scatter.Area[0]);
            var z = scatter.Area[1] + (float)random.NextDouble() * (scatter.Area[3] - scatter.Area[1]);
            var scale = scatter.MinScale + (float)random.NextDouble() * (scatter.MaxScale - scatter.MinScale);
            var yaw = (float)random.NextDouble() * MathF.Tau;
            if (PathDistance(x, z) < 1.5f || terrain.At(x, z) < -0.3f || props.Any(p => Near(p, x, z, scatter.Spacing)))
            {
                continue;
            }

            props.Add(new PlacedProp(scatter.Model, new Vector3(x, terrain.At(x, z), z), scale, yaw));
            placed++;
        }
    }

    private static bool Near(PlacedProp prop, float x, float z, float spacing)
    {
        var reach = spacing + (prop.Model is "cottage" or "cottage_b" or "tavern" or "stall" or "tent" ? 5f : prop.Model == "guild_hall" ? 10f : 0f);
        return new Vector2(prop.Position.X - x, prop.Position.Z - z).LengthSquared() < reach * reach;
    }

    private static List<Aabb> BuildBoxes(List<PlacedProp> props, float size)
    {
        const float wallHeight = 80f;
        const float thick = 2f;
        var half = size / 2 - 1f;
        var boxes = new List<Aabb>
        {
            new(new(-half - thick, -10, -half - thick), new(half + thick, wallHeight, -half)),
            new(new(-half - thick, -10, half), new(half + thick, wallHeight, half + thick)),
            new(new(-half - thick, -10, -half), new(-half, wallHeight, half)),
            new(new(half, -10, -half), new(half + thick, wallHeight, half)),
        };
        foreach (var prop in props)
        {
            var shape = Collision[prop.Model] * prop.Scale;
            if (shape.X > 0)
            {
                // A turned prop needs a box that still covers it, so its footprint is rotated first.
                var cos = MathF.Abs(MathF.Cos(prop.Yaw));
                var sin = MathF.Abs(MathF.Sin(prop.Yaw));
                var halfX = shape.X * cos + shape.Y * sin;
                var halfZ = shape.X * sin + shape.Y * cos;
                var p = prop.Position;
                boxes.Add(new(new(p.X - halfX, p.Y - 1f, p.Z - halfZ), new(p.X + halfX, p.Y + shape.Z, p.Z + halfZ)));
            }
        }

        return boxes;
    }
}
