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

    private sealed record ZoneData(
        string Name, float Size, int Cells, int Seed, float[] Spawn, HillData[] Hills, PathData[] Paths,
        PropData[] Props, ScatterData[] Scatter, EnemySpawnData[]? Enemies, RegionData[]? Regions);

    /// <summary>Half width, half depth and height of each model's collision at scale 1.</summary>
    private static readonly Dictionary<string, Vector3> Collision = new()
    {
        ["cottage"] = new(3.7f, 2.9f, 6f),
        ["cottage_b"] = new(4.2f, 2.8f, 6f),
        ["tavern"] = new(4.9f, 3.3f, 9f),
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

    private Zone(ZoneData data)
    {
        Name = data.Name;
        Size = data.Size;
        _paths = data.Paths;
        var terrain = new HeightField(data.Size, data.Cells, (x, z) => TerrainHeight(data, x, z));
        var random = new Random(data.Seed);
        var props = new List<PlacedProp>();
        foreach (var prop in data.Props)
        {
            var position = new Vector3(prop.X, terrain.At(prop.X, prop.Z), prop.Z);
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
        World = new World(BuildBoxes(props, data.Size), terrain, spawn, enemySpawns);
    }

    public static Zone Load(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream($"zones.{name}")
            ?? throw new ArgumentException($"There is no zone called {name}.");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return new Zone(JsonSerializer.Deserialize<ZoneData>(stream, options)!);
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

        // Mountains close the zone, so there is no visible end of the world.
        var edge = MathF.Max(MathF.Abs(x), MathF.Abs(z)) / (data.Size / 2);
        if (edge > 0.84f)
        {
            var rise = (edge - 0.84f) / 0.16f;
            height += rise * rise * 55f + MathF.Sin(x * 0.11f) * MathF.Cos(z * 0.13f) * 5f * rise;
        }

        return height + Bumps(data.Seed, x, z);
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
            if (PathDistance(x, z) < 1.5f || props.Any(p => Near(p, x, z, scatter.Spacing)))
            {
                continue;
            }

            props.Add(new PlacedProp(scatter.Model, new Vector3(x, terrain.At(x, z), z), scale, yaw));
            placed++;
        }
    }

    private static bool Near(PlacedProp prop, float x, float z, float spacing)
    {
        var reach = spacing + (prop.Model is "cottage" or "cottage_b" or "tavern" or "stall" or "tent" ? 5f : 0f);
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
                var p = prop.Position;
                boxes.Add(new(new(p.X - shape.X, p.Y - 1f, p.Z - shape.Y), new(p.X + shape.X, p.Y + shape.Z, p.Z + shape.Y)));
            }
        }

        return boxes;
    }
}
