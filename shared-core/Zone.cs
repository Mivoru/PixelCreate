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

/// <param name="Slope">How much a long piece such as a fence rises per metre along its own x axis, so it follows the ground.</param>
/// <param name="Flag">Shown only once this story flag is set; <paramref name="Unless"/> hides it once that flag is set.</param>
public readonly record struct PlacedProp(string Model, Vector3 Position, float Scale, float Yaw, float Slope = 0f, string? Flag = null, string? Unless = null)
{
    public bool Conditional => Flag is not null || Unless is not null;
}

/// <summary>
/// A part of the world, built from a hand-written file in world/zones. The layout is authored;
/// the scattered detail comes from the zone seed. Client and server build the same zone from it.
/// </summary>
public sealed class Zone
{
    /// <summary>
    /// A hill. Round by default; with <c>Stretch</c> above 1 it is a long ridge turned by <c>Yaw</c>
    /// (degrees, 0 along x) with a wandering, lobed outline, and <c>Terrace</c> (metres) steps its slopes
    /// into shelves.
    /// </summary>
    private sealed record HillData(float X, float Z, float Radius, float Height, float Stretch = 1f, float Yaw = 0f, float Terrace = 0f);

    /// <summary>
    /// A kind of ground over a round area: forest (a dark floor of needles and moss, little grass),
    /// blight (grey, dying grass) or trampled (bare earth where many feet have gone).
    /// </summary>
    private sealed record GroundData(string Kind, float X, float Z, float Radius);

    /// <summary>A mountain chain along a line: its height on the crest, and how far its slopes reach to each side.</summary>
    private sealed record RidgeData(float Width, float Height, float[][] Points);

    private const float BedStep = 2f;
    private const float BridgeRamp = 11f;
    private const float BridgeRampWidth = 6f;
    private const float BridgeOverlap = 3f;

    /// <summary>The top of the pier deck above the ground at its middle, as build_meshes.py makes it.</summary>
    private const float PierDeck = 0.63f;

    private sealed record PathData(float Width, float[][] Points);

    /// <param name="Flag">Shown only once this story flag is set.</param>
    /// <param name="Unless">Hidden once this story flag is set.</param>
    private sealed record PropData(string Model, float X, float Z, float Yaw = 0f, string? Flag = null, string? Unless = null);

    /// <summary>
    /// Blob shapes the area like a wood instead of a rectangle; Clusters above 0 gathers things into groups.
    /// Biome rules pick the ground inside the area: MinHeight and MaxHeight above the valley floor, Steep
    /// for slopes only (rocks on a hillside), Dry for a least distance from water (a meadow oak), Edge for
    /// the rim of a forest ground (bushes and young trees where a wood meets the meadow).
    /// </summary>
    private sealed record ScatterData(
        string Model, int Count, float[] Area, float MinScale, float MaxScale, float Spacing, bool Blob = false, float Clusters = 0f,
        float Waterside = 0f, float MinHeight = -100f, bool InWater = false, float MaxHeight = 1000f, bool Steep = false, bool Bank = false,
        float Dry = 0f, bool Edge = false);

    private sealed record EnemySpawnData(
        float X, float Z, float Radius, int Count, int MinLevel = 1, int MaxLevel = 3, int RespawnTicks = 900,
        bool Rare = false, string[]? Types = null, float[][]? Route = null, bool Hunts = false, float? Yaw = null, float[]? Drum = null,
        bool Night = false, float[][]? Wards = null);

    private sealed record RegionData(string Name, float X, float Z, float Radius);

    /// <summary><c>Count</c> spots of a gatherable item, scattered by the seed in a circle on dry, open ground.</summary>
    private sealed record GatherData(string Item, float X, float Z, float Radius, int Count);

    private sealed record RiverData(float Width, float Depth, float[][] Points);

    private sealed record RectData(float X0, float Z0, float X1, float Z1, float Height = 4f, float Bottom = -2f);

    private sealed record LakeData(float X, float Z, float Radius, float Depth);

    private sealed record PortalData(float X, float Z, float ToX, float ToZ);

    private sealed record NpcData(string Id, string Name, string Role, float X, float Z, float Yaw = 0f);

    private sealed record ZoneData(
        string Name, float Size, int Cells, int Seed, float[] Spawn, HillData[] Hills, PathData[] Paths,
        PropData[] Props, ScatterData[] Scatter, EnemySpawnData[]? Enemies, RegionData[]? Regions,
        RiverData[]? Rivers = null, RectData[]? Platforms = null, RectData[]? Walls = null, PortalData[]? Portals = null,
        NpcData[]? Npcs = null, float[]? Trial = null, float[]? Waterfall = null, float[][]? Clearings = null, LakeData[]? Lakes = null,
        float[][]? Yards = null, RidgeData[]? Ridges = null, GroundData[]? Grounds = null, PassData[]? Passes = null,
        GatherData[]? Gathers = null);

    /// <summary>
    /// A pass through the mountains round the zone, towards a neighbouring region: the wall opens along the
    /// line from the middle of the zone through this point, <c>Width</c> metres across, and the ground climbs
    /// only <c>Floor</c> metres up to the border instead of the full wall. <c>To</c> names the region beyond.
    /// </summary>
    private sealed record PassData(float X, float Z, float Width = 44f, float Floor = 9f, string? To = null);

    /// <summary>
    /// Models a walker passes through or over: plants, flat things, and the bridges, piers and
    /// interiors whose floors are platforms and whose walls the zone file lists.
    /// </summary>
    private static readonly HashSet<string> NonSolid = new()
    {
        "twigs", "berry_bush", "tall_bush", "reeds", "grass_tuft", "field", "pier", "fern", "bridge", "bridge_stone",
        "guild_interior", "trial_arena", "flowers", "mushroom", "bush", "heather", "archive_floor", "archive_ceiling", "warren_floor", "warren_ceiling", "inn_shell",
    };

    /// <summary>One collision box of a model, around its origin in its own axes: x, z and the top.</summary>
    public readonly record struct ModelBox(float X0, float Z0, float X1, float Z1, float Top);

    /// <summary>
    /// Each model's collision boxes, measured from the model itself when it is built
    /// (asset-pipeline/collision.py writes world/collision.json).
    /// </summary>
    private static readonly Dictionary<string, ModelBox[]> Shapes = LoadShapes();

    private static Dictionary<string, ModelBox[]> LoadShapes()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("collision")
            ?? throw new InvalidOperationException("world/collision.json is missing; build the models.");
        var raw = JsonSerializer.Deserialize<Dictionary<string, float[][]>>(stream)!;
        return raw.ToDictionary(e => e.Key, e => e.Value.Select(b => new ModelBox(b[0], b[1], b[2], b[3], b[4])).ToArray());
    }

    /// <summary>The collision boxes of a solid model, or none for something a walker passes through.</summary>
    public static IReadOnlyList<ModelBox> ShapeOf(string model) =>
        !NonSolid.Contains(model) && Shapes.TryGetValue(model, out var boxes) ? boxes : Array.Empty<ModelBox>();

    /// <summary>True for a model that blocks walkers but has no measured shape: its model needs building.</summary>
    public static bool MissingShape(string model) => !NonSolid.Contains(model) && !Shapes.ContainsKey(model);

    /// <summary>Half width, half depth and height of what a model covers on the ground, or null when it covers nothing.</summary>
    public static Vector3? CollisionOf(string model)
    {
        var boxes = ShapeOf(model);
        if (boxes.Count == 0)
        {
            return null;
        }

        return new Vector3(boxes.Max(b => MathF.Max(-b.X0, b.X1)), boxes.Max(b => MathF.Max(-b.Z0, b.Z1)), boxes.Max(b => b.Top));
    }

    private readonly PathData[] _paths;
    private readonly float[][] _yards;

    /// <summary>The bridges of the open map: authored platforms, without piers or interiors.</summary>
    public IReadOnlyList<Platform> Bridges { get; }
    private readonly RegionData[] _regions;

    public string Name { get; }
    public float Size { get; }
    public World World { get; }
    public IReadOnlyList<PlacedProp> Props { get; }

    /// <summary>The foot of the waterfall, when the zone has one.</summary>
    public Vector3? Waterfall { get; }

    private RiverData[] Rivers { get; }

    private LakeData[] Lakes { get; }

    /// <summary>Each lake: its centre on the water surface, and its radius.</summary>
    public IEnumerable<(Vector3 Centre, float Radius)> LakeSurfaces => Lakes.Select(l =>
        (new Vector3(l.X, World.Terrain!.At(l.X, l.Z) + 1.1f, l.Z), l.Radius));

    /// <summary>Points along each river, in order, with the width at that point.</summary>
    public IEnumerable<(Vector2[] Points, float Width)> RiverCourses =>
        Rivers.Select(r => (r.Points.Select(p => new Vector2(p[0], p[1])).ToArray(), r.Width));

    private Zone(ZoneData data)
    {
        Name = data.Name;
        _grounds = data.Grounds ?? Array.Empty<GroundData>();
        Size = data.Size;
        _paths = data.Paths;
        _yards = data.Yards ?? Array.Empty<float[]>();
        Rivers = data.Rivers ?? Array.Empty<RiverData>();
        Lakes = data.Lakes ?? Array.Empty<LakeData>();
        var beds = (data.Rivers ?? Array.Empty<RiverData>()).Select(r => RiverBed(data, r)).ToArray();
        var terrain = new HeightField(data.Size, data.Cells, (x, z) => TerrainHeight(data, beds, x, z));
        var random = new Random(data.Seed);
        var props = new List<PlacedProp>();
        var platforms = (data.Platforms ?? Array.Empty<RectData>()).Select(p => new Platform(p.X0, p.Z0, p.X1, p.Z1, p.Height)).ToArray();
        var ground = new World(Array.Empty<Aabb>(), terrain) { Platforms = platforms };
        foreach (var prop in data.Props)
        {
            var position = new Vector3(prop.X, Settle(ground, prop.Model, prop.X, prop.Z, prop.Yaw), prop.Z);
            var (height, slope) = IsRun(prop.Model) ? Lean(ground, prop.Model, prop.X, prop.Z, prop.Yaw) : (position.Y, 0f);
            props.Add(new PlacedProp(prop.Model, position with { Y = height }, 1f, prop.Yaw, slope, prop.Flag, prop.Unless));
        }

        Bridges = platforms.Where(p => p.MaxX < data.Size / 2).ToArray();

        // A pier is something to stand on: its deck becomes a platform. Piers lie along an axis.
        platforms = platforms.Concat(props.Where(p => p.Model == "pier").Select(p =>
        {
            var alongX = MathF.Abs(MathF.Cos(p.Yaw)) > 0.5f;
            var (halfX, halfZ) = alongX ? (5f, 1.2f) : (1.2f, 5f);
            return new Platform(p.Position.X - halfX, p.Position.Z - halfZ, p.Position.X + halfX, p.Position.Z + halfZ, p.Position.Y + PierDeck);
        })).ToArray();
        ground = new World(Array.Empty<Aabb>(), terrain) { Platforms = platforms };

        foreach (var scatter in data.Scatter)
        {
            Scatter(scatter, data, terrain, random, props);
        }

        Props = props;
        _fields = props.Where(p => p.Model == "field").ToArray();
        var spawn = new Vector3(data.Spawn[0], terrain.At(data.Spawn[0], data.Spawn[1]), data.Spawn[1]);
        var enemySpawns = (data.Enemies ?? Array.Empty<EnemySpawnData>())
            .Select(e => new EnemySpawn(new Vector3(e.X, terrain.At(e.X, e.Z), e.Z), e.Radius, e.Count, e.MinLevel, e.MaxLevel, e.RespawnTicks, e.Rare, e.Types,
                e.Route?.Select(p => new Vector3(p[0], terrain.At(p[0], p[1]), p[1])).ToArray(), e.Hunts, e.Yaw,
                e.Drum is { } drum ? new Vector3(drum[0], terrain.At(drum[0], drum[1]), drum[1]) : null, e.Night,
                e.Wards?.Select(p => new Vector3(p[0], terrain.At(p[0], p[1]), p[1])).ToArray())).ToArray();
        _regions = data.Regions ?? Array.Empty<RegionData>();
        var boxes = BuildBoxes(props, data.Size);
        boxes.AddRange((data.Walls ?? Array.Empty<RectData>()).Select(w => new Aabb(new(w.X0, w.Bottom, w.Z0), new(w.X1, w.Height, w.Z1))));
        Vector3 Place(float x, float z) => new(x, ground.GroundHeight(x, z), z);
        var trial = data.Trial is { } t ? Place(t[0], t[1]) : spawn;
        World = new World(boxes, terrain, spawn, enemySpawns)
        {
            Platforms = platforms,
            TrialGround = trial,
            Portals = (data.Portals ?? Array.Empty<PortalData>()).Select(p => new Portal(Place(p.X, p.Z), Place(p.ToX, p.ToZ))).ToArray(),
            Roads = RoadCourses.ToArray(),
            Npcs = (data.Npcs ?? Array.Empty<NpcData>()).Select(n => new Npc(n.Id, n.Name, n.Role, Place(n.X, n.Z), n.Yaw)).ToArray(),
        };
        Waterfall = data.Waterfall is { } w ? new Vector3(w[0], terrain.At(w[0], w[1]), w[1]) : null;
        World.Gathers = PlaceGathers(data);
    }

    /// <summary>
    /// The spots of every gatherable item: random points of each circle (from the seed, so server and client agree)
    /// on dry ground that is not too steep, off roads and clear of anything solid, at least two metres apart.
    /// </summary>
    private GatherSpot[] PlaceGathers(ZoneData data)
    {
        var spots = new List<GatherSpot>();
        var random = new Random(data.Seed ^ 0x6a7e);
        foreach (var gather in data.Gathers ?? Array.Empty<GatherData>())
        {
            var item = ItemDef.Index(gather.Item);
            var placed = 0;
            for (var attempt = 0; attempt < gather.Count * 40 && placed < gather.Count; attempt++)
            {
                var angle = random.NextDouble() * Math.Tau;
                var reach = Math.Sqrt(random.NextDouble()) * gather.Radius;
                var x = gather.X + (float)(Math.Cos(angle) * reach);
                var z = gather.Z + (float)(Math.Sin(angle) * reach);
                var at = new Vector3(x, World.GroundHeight(x, z), z);
                if (IsWater(x, z) || NearWater(x, z) || PathDistance(x, z) < 1.5f || TooSteep(World.Terrain!, x, z) || World.OverlapsPlayer(at + Vector3.UnitY * 0.05f) ||
                    spots.Any(s => Vector3.DistanceSquared(s.Position, at) < 4f))
                {
                    continue;
                }

                spots.Add(new GatherSpot(spots.Count, item, at));
                placed++;
            }
        }

        return spots.ToArray();
    }

    /// <summary>
    /// The named places of a zone, read without building it: the main menu names where each save
    /// stands. The first place whose circle holds a point is the place there, as in <see cref="RegionAt"/>.
    /// </summary>
    public static (string Name, float X, float Z, float Radius)[] RegionsOf(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"zones.{name}")
            ?? throw new ArgumentException($"There is no zone called {name}.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.TryGetProperty("regions", out var regions)
            ? regions.EnumerateArray().Select(r => (r.GetProperty("name").GetString()!, r.GetProperty("x").GetSingle(),
                r.GetProperty("z").GetSingle(), r.GetProperty("radius").GetSingle())).ToArray()
            : Array.Empty<(string, float, float, float)>();
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

        return IsWater(x, z) || _yards.Any(y => new Vector2(x - y[0], z - y[1]).Length() < y[2]);
    }

    /// <summary>
    /// True where something stands on the ground: inside a solid model's collision, or on a field's
    /// soil. Grass does not grow there.
    /// </summary>
    public bool IsCovered(float x, float z)
    {
        var ground = World.GroundHeight(x, z);
        foreach (var box in World.BoxesNear(x, z, x, z))
        {
            if (x > box.Min.X && x < box.Max.X && z > box.Min.Z && z < box.Max.Z && box.Max.Y > ground + 0.1f &&
                box.Max.X - box.Min.X < 60f && box.Max.Z - box.Min.Z < 60f)
            {
                return true;
            }
        }

        foreach (var field in _fields)
        {
            var (sin, cos) = MathF.SinCos(field.Yaw);
            var dx = x - field.Position.X;
            var dz = z - field.Position.Z;
            if (MathF.Abs(dx * cos - dz * sin) < FieldHalf.X && MathF.Abs(dx * sin + dz * cos) < FieldHalf.Y)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Half the length and width of a field's soil (build_meshes.py: field).</summary>
    private static readonly Vector2 FieldHalf = new(6f, 4f);

    private PlacedProp[] _fields = Array.Empty<PlacedProp>();

    /// <summary>True in a river or a lake.</summary>
    public bool IsWater(float x, float z) => Rivers.Any(r => PolylineDistance(r.Points, x, z) < r.Width * 1.45f) || InLake(x, z, 1f);

    /// <summary>Every named place: its name, middle and radius, in metres.</summary>
    public IEnumerable<(string Name, float X, float Z, float Radius)> Places => _regions.Select(r => (r.Name, r.X, r.Z, r.Radius));

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

    /// <summary>The middle line of each road.</summary>
    public IEnumerable<Vector2[]> RoadCourses => _paths.Select(p => p.Points.Select(q => new Vector2(q[0], q[1])).ToArray());

    /// <summary>The two ends of each road.</summary>
    public IEnumerable<Vector2> RoadEnds => RoadCourses.SelectMany(r => new[] { r[0], r[^1] });

    /// <summary>Distance from a road end to the nearest other road, ignoring the road it belongs to.</summary>
    public float PathDistanceExcept(Vector2 end)
    {
        var nearest = float.MaxValue;
        foreach (var path in _paths)
        {
            var own = new Vector2(path.Points[0][0], path.Points[0][1]) == end || new Vector2(path.Points[^1][0], path.Points[^1][1]) == end;
            if (!own)
            {
                nearest = MathF.Min(nearest, PolylineDistance(path.Points, end.X, end.Y));
            }
        }

        return nearest;
    }

    private GroundData[] _grounds = Array.Empty<GroundData>();

    /// <summary>
    /// How much of each kind of ground lies at a point, from 0 to 1: forest floor, blight and
    /// trampled earth. Each area is full inside and fades out over its outer third, with a ragged edge.
    /// </summary>
    public (float Forest, float Blight, float Trampled) GroundAt(float x, float z)
    {
        float forest = 0f, blight = 0f, trampled = 0f;
        foreach (var ground in _grounds)
        {
            var distance = new Vector2(x - ground.X, z - ground.Z).Length() / ground.Radius;
            var ragged = 0.08f * MathF.Sin(x * 0.21f + z * 0.13f) + 0.06f * MathF.Sin(x * 0.07f - z * 0.19f);
            var weight = Math.Clamp((1f - distance + ragged) / 0.33f, 0f, 1f);
            switch (ground.Kind)
            {
                case "forest": forest = MathF.Max(forest, weight); break;
                case "blight": blight = MathF.Max(blight, weight); break;
                case "trampled": trampled = MathF.Max(trampled, weight); break;
            }
        }

        return (forest, blight, trampled);
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

    /// <summary>
    /// Distance to the edge of the worn road surface. A road stops at a river bank and at a bridge
    /// deck, so no trodden earth runs down the bank and under the bridge into the water.
    /// </summary>
    public float WornPathDistance(float x, float z)
    {
        foreach (var bridge in Bridges)
        {
            if (x >= bridge.MinX - 0.5f && x <= bridge.MaxX + 0.5f && z >= bridge.MinZ - 1f && z <= bridge.MaxZ + 1f)
            {
                return float.MaxValue;
            }
        }

        foreach (var river in Rivers)
        {
            if (PolylineDistance(river.Points, x, z) < river.Width * 1.6f)
            {
                return float.MaxValue;
            }
        }

        // A yard (a farmyard, a camp) is trodden bare all over.
        var worn = PathDistance(x, z);
        foreach (var yard in _yards)
        {
            worn = MathF.Min(worn, new Vector2(x - yard[0], z - yard[1]).Length() - yard[2]);
        }

        return worn;
    }

    /// <summary>The height of the hills alone, before rivers and mountains shape them.</summary>
    private static float HillHeight(ZoneData data, float x, float z)
    {
        var height = 0f;
        foreach (var hill in data.Hills)
        {
            var distance = HillDistance(hill, x - hill.X, z - hill.Z);
            if (distance < 1f)
            {
                var falloff = 1f - distance * distance;
                var rise = hill.Height * falloff * falloff;
                if (hill.Terrace > 0f)
                {
                    // Shelves: flat treads joined by short, still walkable risers.
                    var steps = rise / hill.Terrace;
                    var tread = MathF.Floor(steps);
                    var riser = Math.Clamp((steps - tread - 0.6f) / 0.4f, 0f, 1f);
                    rise = (tread + riser * riser * (3f - 2f * riser)) * hill.Terrace;
                }

                height += rise;
            }
        }

        foreach (var ridge in data.Ridges ?? Array.Empty<RidgeData>())
        {
            var (near, travelled) = NearestOnPolyline(ridge.Points, x, z);
            var distance = near / ridge.Width;
            if (distance < 1f)
            {
                // A rugged crest: the height wanders along the chain, and shoulders break the slopes.
                // Both ends sink gently to the ground, so a gap between two chains is a walkable pass.
                var falloff = 1f - distance * distance;
                var crags = 0.72f + 0.16f * MathF.Sin(x * 0.09f + z * 0.05f) + 0.12f * MathF.Sin(x * 0.23f - z * 0.31f);
                var end = Math.Clamp(MathF.Min(travelled, PolylineLength(ridge.Points) - travelled) / RidgeTaper, 0f, 1f);
                height += ridge.Height * falloff * falloff * crags * end * end * (3f - 2f * end);
            }
        }

        return height;
    }

    /// <summary>How far a point lies from a hill's middle, as a share of its outline (1 on the outline).</summary>
    private static float HillDistance(HillData hill, float dx, float dz)
    {
        if (hill.Stretch <= 1f)
        {
            return new Vector2(dx, dz).Length() / hill.Radius;
        }

        var yaw = hill.Yaw * MathF.PI / 180f;
        var along = dx * MathF.Cos(yaw) + dz * MathF.Sin(yaw);
        var across = -dx * MathF.Sin(yaw) + dz * MathF.Cos(yaw);
        var angle = MathF.Atan2(across, along);
        var seed = hill.X * 0.37f + hill.Z * 0.21f;
        var lobes = 1f + 0.14f * MathF.Sin(2f * angle + seed) + 0.09f * MathF.Sin(3f * angle - seed * 1.7f) + 0.05f * MathF.Sin(5f * angle + seed * 0.6f);
        return new Vector2(along / (hill.Radius * hill.Stretch), across / hill.Radius).Length() / lobes;
    }

    /// <summary>The bed height every few metres along a river. It only ever falls, so water can flow.</summary>
    private static float[] RiverBed(ZoneData data, RiverData river)
    {
        var bed = new List<float>();
        var lowest = float.MaxValue;
        for (var i = 0; i + 1 < river.Points.Length; i++)
        {
            var a = new Vector2(river.Points[i][0], river.Points[i][1]);
            var b = new Vector2(river.Points[i + 1][0], river.Points[i + 1][1]);
            var length = Vector2.Distance(a, b);
            for (var along = bed.Count * BedStep - Travelled(river.Points, i); along < length; along += BedStep)
            {
                var point = a + (b - a) * (along / length);
                lowest = MathF.Min(lowest, HillHeight(data, point.X, point.Y) - river.Depth);
                bed.Add(lowest);
            }
        }

        return bed.ToArray();
    }

    private static float Travelled(float[][] points, int segment)
    {
        var travelled = 0f;
        for (var i = 0; i < segment; i++)
        {
            travelled += Vector2.Distance(new Vector2(points[i][0], points[i][1]), new Vector2(points[i + 1][0], points[i + 1][1]));
        }

        return travelled;
    }

    private static float TerrainHeight(ZoneData data, float[][] beds, float x, float z)
    {
        var height = HillHeight(data, x, z);
        var rivers = data.Rivers ?? Array.Empty<RiverData>();
        for (var r = 0; r < rivers.Length; r++)
        {
            var (distance, travelled) = NearestOnPolyline(rivers[r].Points, x, z);
            var bank = rivers[r].Width * 1.8f;
            if (distance < bank)
            {
                var t = 1f - distance / bank;
                var index = Math.Min((int)(travelled / BedStep), beds[r].Length - 1);
                var cut = MathF.Min(height, beds[r][index]);
                height += (cut - height) * t * t * (3f - 2f * t);
            }
        }

        foreach (var lake in data.Lakes ?? Array.Empty<LakeData>())
        {
            var distance = new Vector2(x - lake.X, z - lake.Z).Length();
            var shore = lake.Radius * 1.5f;
            if (distance < shore)
            {
                var t = 1f - distance / shore;
                var bed = HillHeight(data, lake.X, lake.Z) - lake.Depth;
                height += (MathF.Min(height, bed) - height) * MathF.Min(t * 2.2f, 1f);
            }
        }

        // The ground rises to meet each end of a bridge, so nobody has to jump onto it.
        foreach (var bridge in (data.Platforms ?? Array.Empty<RectData>()).Where(p => p.X1 < data.Size / 2))
        {
            // A bridge runs along the longer side of its deck, east-west or north-south.
            var alongX = bridge.X1 - bridge.X0 >= bridge.Z1 - bridge.Z0;
            var along = alongX ? MathF.Max(bridge.X0 - x, x - bridge.X1) : MathF.Max(bridge.Z0 - z, z - bridge.Z1);
            var across = alongX ? MathF.Max(MathF.Max(bridge.Z0 - z, z - bridge.Z1), 0f) : MathF.Max(MathF.Max(bridge.X0 - x, x - bridge.X1), 0f);
            if (along > -BridgeOverlap && along < BridgeRamp && across < BridgeRampWidth)
            {
                // The rise starts under the deck, because the ground mesh is coarser than the join.
                var t = 1f - MathF.Max(along, 0f) / BridgeRamp;
                height += (bridge.Height - height) * t * t * (3f - 2f * t) * (1f - across / BridgeRampWidth);
            }
        }

        return height + Rim(data, x, z) + Bumps(data.Seed, x, z);
    }

    /// <summary>Where the mountain wall starts, as a share of the way from the middle to the border.</summary>
    private const float RimStart = 0.84f;

    /// <summary>Where the foothills before the wall start, and how high they swell.</summary>
    private const float RimFoot = 0.74f;

    private const float FoothillHeight = 3.5f;

    /// <summary>
    /// The mountains that close the zone, so there is no visible end of the world. Their foot wanders in
    /// and out and rounds the corners instead of drawing a square, low foothills lead up to it, and at
    /// each pass the wall opens into a saddle a road can climb.
    /// </summary>
    private static float Rim(ZoneData data, float x, float z)
    {
        var half = data.Size / 2;
        var ax = MathF.Abs(x) / half;
        var az = MathF.Abs(z) / half;
        // Rounded corners: a superellipse lies inside the square everywhere but on the axes.
        var round = MathF.Pow(MathF.Pow(ax, 10f) + MathF.Pow(az, 10f), 0.1f);
        var angle = MathF.Atan2(z, x);
        // The foot of the wall only ever moves outwards from the old square, so nothing built stands in it.
        var bays = 0.5f + 0.27f * MathF.Sin(3f * angle + 0.7f) + 0.15f * MathF.Sin(7f * angle - 1.3f) + 0.08f * MathF.Sin(13f * angle + 2.1f);
        var edge = MathF.Min(round, 1f) - 0.035f * Math.Clamp(bays, 0f, 1f) * (1f - MathF.Min(round, 1f)) / (1f - RimStart);
        edge = MathF.Max(edge, MathF.Max(ax, az) - 0.035f);
        if (edge <= RimFoot)
        {
            return 0f;
        }

        // Foothills: a gentle swell before the wall, so the valley floor does not meet it in a crease.
        var foot = Math.Clamp((edge - RimFoot) / (RimStart - RimFoot), 0f, 1f);
        var swell = 0.55f + 0.45f * MathF.Sin(x * 0.047f + z * 0.019f) * MathF.Cos(z * 0.041f - x * 0.023f);
        var height = FoothillHeight * foot * foot * (3f - 2f * foot) * swell;
        if (edge <= RimStart)
        {
            return height;
        }

        var rise = (edge - RimStart) / (1f - RimStart);
        var ridges = MathF.Sin(x * 0.11f) * MathF.Cos(z * 0.13f) + MathF.Sin(x * 0.043f + z * 0.031f) * 1.6f + MathF.Sin(z * 0.27f - x * 0.19f) * 0.35f;
        // Sharp crests where a wave turns over, at three scales running different ways: a skyline of
        // peaks and notches instead of a row of soft domes.
        var crest = Crest(x * 0.031f + z * 0.017f + 0.4f) * 0.5f + Crest(z * 0.043f - x * 0.029f + 1.1f) * 0.33f + Crest(x * 0.091f + z * 0.077f) * 0.17f;
        var wall = rise * rise * 46f + rise * (crest * crest * 28f + ridges * 3f);
        // Cliff bands: the wall gathers into ledges with steep steps between them, so its sides read as
        // rock faces. The ledges are where the snow settles.
        // The bands tilt and wander along the wall, so they never line up into rings like a stadium.
        var warp = MathF.Sin(x * 0.047f - z * 0.029f) * 3.5f + MathF.Sin(z * 0.081f + x * 0.063f) * 1.8f;
        var band = (wall + warp) / CliffBand;
        var level = MathF.Floor(band);
        var t = band - level;
        var terraced = (level + t * t * t * (t * (t * 6f - 15f) + 10f)) * CliffBand - warp;
        wall += (terraced - wall) * 0.6f * Math.Clamp(rise * 3f, 0f, 1f);
        var open = 0f;
        var floor = 0f;
        foreach (var pass in data.Passes ?? Array.Empty<PassData>())
        {
            var length = MathF.Sqrt(pass.X * pass.X + pass.Z * pass.Z);
            var along = (x * pass.X + z * pass.Z) / length;
            if (along <= 0f)
            {
                continue;
            }

            var across = MathF.Abs(x * pass.Z - z * pass.X) / length;
            // Level across the middle half of the pass, then shoulders climbing to the wall.
            var gate = 1f - Math.Clamp((across - pass.Width * 0.25f) / (pass.Width * 0.5f), 0f, 1f);
            gate = gate * gate * (3f - 2f * gate);
            if (gate > open)
            {
                open = gate;
                floor = pass.Floor;
            }
        }

        return height + wall * (1f - open) + floor * rise * open;
    }

    /// <summary>The height of one cliff band on the mountain wall, in metres.</summary>
    private const float CliffBand = 7f;

    /// <summary>A ridged wave between 0 and 1, sharp at its crests.</summary>
    private static float Crest(float v) => 1f - MathF.Abs(MathF.Sin(v));

    private static float PolylineDistance(float[][] points, float x, float z) => NearestOnPolyline(points, x, z).Distance;

    /// <summary>How far the ends of a ridge take to sink to the ground, in metres.</summary>
    private const float RidgeTaper = 34f;

    private static float PolylineLength(float[][] points)
    {
        var length = 0f;
        for (var i = 0; i + 1 < points.Length; i++)
        {
            length += Vector2.Distance(new Vector2(points[i][0], points[i][1]), new Vector2(points[i + 1][0], points[i + 1][1]));
        }

        return length;
    }

    /// <summary>How far a point lies from a polyline, and how far along the line that nearest spot is.</summary>
    private static (float Distance, float Travelled) NearestOnPolyline(float[][] points, float x, float z)
    {
        var point = new Vector2(x, z);
        var nearest = float.MaxValue;
        var nearestTravelled = 0f;
        var travelled = 0f;
        for (var i = 0; i + 1 < points.Length; i++)
        {
            var a = new Vector2(points[i][0], points[i][1]);
            var b = new Vector2(points[i + 1][0], points[i + 1][1]);
            var length = Vector2.Distance(a, b);
            var along = Math.Clamp(Vector2.Dot(point - a, b - a) / (b - a).LengthSquared(), 0f, 1f);
            var distance = Vector2.Distance(point, a + (b - a) * along);
            if (distance < nearest)
            {
                nearest = distance;
                nearestTravelled = travelled + along * length;
            }

            travelled += length;
        }

        return (nearest, nearestTravelled);
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

    /// <summary>Wavy noise from a few sines, between about -1 and 1.</summary>
    private static float Waves(float x, float z) =>
        (MathF.Sin(x * 0.071f + 1.3f) * MathF.Cos(z * 0.083f) + MathF.Sin(x * 0.033f - z * 0.047f) + MathF.Sin(z * 0.15f + x * 0.11f) * 0.4f) / 1.7f;

    private static bool OutsideShape(ScatterData scatter, float x, float z)
    {
        if (scatter.Clusters > 0f && Waves(x * 1.4f, z * 1.4f) < 1f - scatter.Clusters * 2f)
        {
            return true;
        }

        if (!scatter.Blob)
        {
            return false;
        }

        var a = scatter.Area;
        var u = (x - (a[0] + a[2]) / 2) / ((a[2] - a[0]) / 2);
        var v = (z - (a[1] + a[3]) / 2) / ((a[3] - a[1]) / 2);
        return MathF.Sqrt(u * u + v * v) + Waves(x * 2f, z * 2f) * 0.28f > 1f;
    }

    /// <summary>Waterside plants want the bank of a river or lake, others a minimum height.</summary>
    private bool MatchesGround(ScatterData scatter, HeightField terrain, float x, float z)
    {
        var height = terrain.At(x, z);
        if (height < scatter.MinHeight || height > scatter.MaxHeight)
        {
            return false;
        }

        if (scatter.Steep && Slope(terrain, x, z) < 0.9f)
        {
            return false;
        }

        if (scatter.Dry > 0f && (Rivers.Any(r => PolylineDistance(r.Points, x, z) < r.Width + scatter.Dry) || InLake(x, z, 1f + scatter.Dry / 15f)))
        {
            return false;
        }

        if (scatter.Edge && !_grounds.Any(g => g.Kind == "forest" && MathF.Abs(new Vector2(x - g.X, z - g.Z).Length() - g.Radius - 3f) < 9f))
        {
            return false;
        }

        if (scatter.Waterside <= 0f)
        {
            return true;
        }

        var river = Rivers.Any(r => PolylineDistance(r.Points, x, z) < r.Width * 1.75f + scatter.Waterside);
        return river || InLake(x, z, 1.25f + scatter.Waterside / 15f);
    }

    /// <summary>The rise over three metres across and along, summed: 2.4 is too steep to plant on.</summary>
    private static float Slope(HeightField terrain, float x, float z) =>
        MathF.Abs(terrain.At(x + 1.5f, z) - terrain.At(x - 1.5f, z)) + MathF.Abs(terrain.At(x, z + 1.5f) - terrain.At(x, z - 1.5f));

    private static bool TooSteep(HeightField terrain, float x, float z) => Slope(terrain, x, z) > 2.4f;

    private static bool InClearing(ZoneData data, float x, float z) =>
        (data.Clearings ?? Array.Empty<float[]>()).Any(c => new Vector2(x - c[0], z - c[1]).Length() < c[2]);

    private bool NearWater(float x, float z) => Rivers.Any(r => PolylineDistance(r.Points, x, z) < r.Width * 1.75f) || InLake(x, z, 1.2f);

    private bool InLake(float x, float z, float margin) =>
        Lakes.Any(l => new Vector2(x - l.X, z - l.Z).Length() < l.Radius * margin);

    private void Scatter(ScatterData scatter, ZoneData data, HeightField terrain, Random random, List<PlacedProp> props)
    {
        var placed = 0;
        for (var attempt = 0; attempt < scatter.Count * 20 && placed < scatter.Count; attempt++)
        {
            var x = scatter.Area[0] + (float)random.NextDouble() * (scatter.Area[2] - scatter.Area[0]);
            var z = scatter.Area[1] + (float)random.NextDouble() * (scatter.Area[3] - scatter.Area[1]);
            var scale = scatter.MinScale + (float)random.NextDouble() * (scatter.MaxScale - scatter.MinScale);
            var yaw = (float)random.NextDouble() * MathF.Tau;
            // A tree crown spreads far wider than its trunk, so trees keep more distance from what is built.
            var tree = scatter.Model.Contains("tree") || scatter.Model is "pine" or "pine_b" or "birch" or "willow" or "blossom" or "apple";
            // Cheap tests first; the distance to every placed prop is the slow one.
            if (OutsideShape(scatter, x, z) || InClearing(data, x, z) || (terrain.At(x, z) < -0.3f && !scatter.InWater) ||
                !MatchesGround(scatter, terrain, x, z) || (TooSteep(terrain, x, z) && !scatter.Steep) || (NearWater(x, z) && !scatter.InWater && !(scatter.Bank && terrain.At(x, z) > 0.2f)) ||
                PathDistance(x, z) < (tree ? 3.2f : 1.5f) ||
                props.Any(p => Near(p, x, z, scatter.Spacing, tree && Footprint(p.Model) > 1.2f ? 3f : 0f)))
            {
                continue;
            }

            props.Add(new PlacedProp(scatter.Model, new Vector3(x, terrain.At(x, z), z), scale, yaw));
            placed++;
        }
    }

    /// <summary>
    /// The height a model stands at: the lowest ground under its corners, so on a slope it sinks into
    /// the uphill side instead of floating over the downhill side. Bridges and piers keep their height.
    /// </summary>
    private static float Settle(World ground, string model, float x, float z, float yaw)
    {
        var centre = ground.GroundHeight(x, z);
        if (model is "bridge" or "bridge_stone" or "bridge_broken_end" or "pier" or "field" || CollisionOf(model) is not { } shape)
        {
            return centre;
        }

        var lowest = centre;
        var (sin, cos) = MathF.SinCos(yaw);
        foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f), (0f, 1f), (0f, -1f), (1f, 0f), (-1f, 0f) })
        {
            var dx = sx * shape.X;
            var dz = sz * shape.Y;
            lowest = MathF.Min(lowest, ground.GroundHeight(x + dx * cos + dz * sin, z - dx * sin + dz * cos));
        }

        return MathF.Max(lowest, centre - 2.5f);
    }

    /// <summary>
    /// A fence or wall on a slope follows the ground from end to end instead of burying its uphill
    /// end: the height of its middle and how much it rises per metre along its length.
    /// </summary>
    private static (float Height, float Slope) Lean(World ground, string model, float x, float z, float yaw)
    {
        var half = CollisionOf(model)?.X ?? 1f;
        var (sin, cos) = MathF.SinCos(yaw);
        var start = ground.GroundHeight(x - half * cos, z + half * sin);
        var end = ground.GroundHeight(x + half * cos, z - half * sin);
        var centre = ground.GroundHeight(x, z);
        // Sit a little low, so the posts never float over a dip in the middle.
        return (MathF.Min((start + end) / 2, centre) - 0.05f, (end - start) / (2 * half));
    }

    /// <summary>Fences and walls: long, thin pieces laid end to end.</summary>
    private static bool IsRun(string model) => model is "fence" or "stone_wall" or "palisade" or "palisade_half" or "palisade_broken" or "spike_wall" or "ruin_wall";

    /// <summary>How far from its centre a model reaches on the ground, by what it is made of.</summary>
    private static float Footprint(string model) => model switch
    {
        "field" => 8f,
        "pier" => 6f,
        "bridge" or "bridge_stone" => 11f,
        "camp_set" => 3f,
        "bone_pile" => 0.8f,
        "ruin_arch" => 4f,
        _ => CollisionOf(model) is { } shape ? MathF.Max(shape.X, shape.Y) : 0f,
    };

    private static bool Near(PlacedProp prop, float x, float z, float spacing, float extra)
    {
        var reach = spacing + (Footprint(prop.Model) + extra) * prop.Scale;
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
            AddShape(boxes, prop);
        }

        return boxes;
    }

    /// <summary>The most a piece of a turned model may measure along x or z before it is cut smaller.</summary>
    private const float TurnedPiece = 0.5f;

    /// <summary>
    /// Places a model's measured boxes in the world. A box of a model turned off the grid is cut into
    /// small pieces first, so the upright boxes that cover the pieces hug the turned shape instead
    /// of one big box covering a long turned wall from corner to corner.
    /// </summary>
    private static void AddShape(List<Aabb> boxes, PlacedProp prop)
    {
        var shape = ShapeOf(prop.Model);
        if (shape.Count == 0)
        {
            return;
        }

        var (sin, cos) = MathF.SinCos(prop.Yaw);
        var quarter = MathF.Abs(prop.Yaw / (MathF.PI / 2) - MathF.Round(prop.Yaw / (MathF.PI / 2))) < 0.01f;
        var p = prop.Position;
        foreach (var box in shape)
        {
            var cutsX = quarter ? 1 : Math.Max(1, (int)MathF.Ceiling((box.X1 - box.X0) * prop.Scale / TurnedPiece));
            var cutsZ = quarter ? 1 : Math.Max(1, (int)MathF.Ceiling((box.Z1 - box.Z0) * prop.Scale / TurnedPiece));
            for (var i = 0; i < cutsX; i++)
            {
                for (var j = 0; j < cutsZ; j++)
                {
                    var x0 = box.X0 + (box.X1 - box.X0) * i / cutsX;
                    var x1 = box.X0 + (box.X1 - box.X0) * (i + 1) / cutsX;
                    var z0 = box.Z0 + (box.Z1 - box.Z0) * j / cutsZ;
                    var z1 = box.Z0 + (box.Z1 - box.Z0) * (j + 1) / cutsZ;
                    var min = new Vector2(float.MaxValue);
                    var max = new Vector2(float.MinValue);
                    foreach (var (lx, lz) in new[] { (x0, z0), (x1, z0), (x0, z1), (x1, z1) })
                    {
                        var world = new Vector2(p.X + (lx * cos + lz * sin) * prop.Scale, p.Z + (-lx * sin + lz * cos) * prop.Scale);
                        min = Vector2.Min(min, world);
                        max = Vector2.Max(max, world);
                    }

                    // A sheared fence rises along its length; its boxes rise with it.
                    var lift = prop.Slope * (x0 + x1) / 2 * prop.Scale;
                    boxes.Add(new Aabb(new Vector3(min.X, p.Y + lift - 1f, min.Y), new Vector3(max.X, p.Y + lift + box.Top * prop.Scale, max.Y))
                    {
                        Flag = prop.Flag,
                        Unless = prop.Unless,
                    });
                }
            }
        }
    }
}
