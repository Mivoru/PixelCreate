using System.Collections.Generic;
using System.Linq;
using Godot;
using PixelCreate.Core;
using Num = System.Numerics;

namespace PixelCreate;

/// <summary>
/// Where the player is being led and by which way. The target is the player's own point from the
/// big map when there is one, else the place the tracked quest step points to (<see cref="Navigation"/>).
/// The way runs along the roads (<see cref="RoadMap.Route"/>) and is worked out again when the target
/// moves or the player strays from it. The first metres of it glow on the ground ahead.
/// The own point is the player's, kept per save in user://waypoints.cfg; reaching it clears it.
/// </summary>
public sealed class Navigator
{
    private const string FilePath = "user://waypoints.cfg";

    /// <summary>How far ahead the trail on the ground reaches, and the gap between its marks.</summary>
    private const float TrailMetres = 40f;
    private const float TrailGap = 2.2f;

    /// <summary>How far the player may stray from the way before it is worked out again.</summary>
    private const float StrayMetres = 6f;

    private readonly World _world;
    private readonly RoadMap _roads;
    private readonly MultiMeshInstance3D _trail;
    private readonly List<Num.Vector3> _route = new();
    private Num.Vector3? _routeGoal;
    private double _sinceRoute;
    private float _phase;

    /// <summary>The player's own point, in metres (x, z), or null.</summary>
    public Vector2? Waypoint { get; private set; }

    /// <summary>The place being led to now (the own point or the quest's), its name, and whether it is the own point.</summary>
    public (Vector2 At, string Label, bool Own, float Radius)? Target { get; private set; }

    /// <summary>The way from the player to the target, x and z in metres, the player first.</summary>
    public IReadOnlyList<Vector2> Route => _route.Select(p => new Vector2(p.X, p.Z)).ToList();

    /// <summary>Turns the trail on the ground off (PIXEL_OFF=trail), to measure what it costs.</summary>
    public bool TrailEnabled { get; set; } = true;

    public Navigator(World world, Node view)
    {
        _world = world;
        _roads = new RoadMap(world.Roads);
        var mesh = Chevron();
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = false,
        });
        _trail = new MultiMeshInstance3D
        {
            Multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                Mesh = mesh,
                InstanceCount = (int)(TrailMetres / TrailGap) + 1,
                VisibleInstanceCount = 0,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        view.AddChild(_trail);
        Load();
    }

    /// <summary>A flat chevron lying on the ground, its point towards +Z (the way on), about 0.7 m wide.</summary>
    private static ArrayMesh Chevron()
    {
        var tip = new Vector3(0, 0, 0.42f);
        var tipIn = new Vector3(0, 0, 0.14f);
        var left = new Vector3(-0.36f, 0, -0.06f);
        var leftIn = new Vector3(-0.36f, 0, -0.34f);
        var right = new Vector3(0.36f, 0, -0.06f);
        var rightIn = new Vector3(0.36f, 0, -0.34f);
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var point in new[] { tip, left, leftIn, tip, leftIn, tipIn, tip, tipIn, rightIn, tip, rightIn, right })
        {
            tool.SetNormal(Vector3.Up);
            tool.AddVertex(point);
        }

        return tool.Commit();
    }

    // Only a game started from the main menu keeps its mark; bots and test runs start without one.
    private static string Slot => Launch.Slot;

    private void Load()
    {
        if (!Launch.FromMenu)
        {
            return;
        }

        var file = new ConfigFile();
        if (file.Load(FilePath) == Error.Ok && file.GetValue("waypoints", Slot, "").AsString() is { Length: > 0 } text)
        {
            var parts = text.Split(',');
            if (parts.Length == 2 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z))
            {
                Waypoint = new Vector2(x, z);
            }
        }
    }

    private void Save()
    {
        if (!Launch.FromMenu)
        {
            return;
        }

        var file = new ConfigFile();
        file.Load(FilePath);
        var value = Waypoint is { } at ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{at.X:0.0},{at.Y:0.0}") : "";
        file.SetValue("waypoints", Slot, value);
        file.Save(FilePath);
    }

    /// <summary>Sets the player's own point (from the big map), or clears it with null.</summary>
    public void SetWaypoint(Vector2? at)
    {
        Waypoint = at;
        _routeGoal = null;
        Save();
    }

    /// <summary>Works out target and way for this frame and lays the trail ahead of the player.</summary>
    public void Update(double delta, Num.Vector3 player, IStoryContext story)
    {
        if (Waypoint is { } own && new Vector2(player.X - own.X, player.Z - own.Y).Length() < 4f)
        {
            // Arrived: the point has done its job.
            SetWaypoint(null);
        }

        Target = Waypoint is { } point ? (point, "Your mark", true, 0f)
            : Navigation.QuestTarget(story, _world) is { } quest ? (new Vector2(quest.At.X, quest.At.Z), quest.Label, false, quest.Radius)
            : null;

        if (Target is not { } target)
        {
            _route.Clear();
            _routeGoal = null;
            _trail.Multimesh.VisibleInstanceCount = 0;
            return;
        }

        if (target.Radius > 0f && new Vector2(player.X - target.At.X, player.Z - target.At.Y).Length() < target.Radius)
        {
            // In the area where the monsters live: the way has done its job; the area stays on the maps.
            _route.Clear();
            _routeGoal = null;
            _trail.Multimesh.VisibleInstanceCount = 0;
            return;
        }

        var goal = Navigation.Reachable(_world, player, new Num.Vector3(target.At.X, 0, target.At.Y));
        goal.Y = _world.GroundHeight(goal.X, goal.Z);
        _sinceRoute += delta;
        if (_routeGoal is not { } last || Num.Vector3.DistanceSquared(last, goal) > 1f || (_sinceRoute > 2.0 && Stray(player) > StrayMetres))
        {
            _route.Clear();
            _route.Add(player);
            _route.AddRange(_roads.Route(player, goal, _world));
            _routeGoal = goal;
            _sinceRoute = 0;
        }
        else if (_route.Count > 0)
        {
            // The way starts where the player stands now; points already passed fall away.
            _route[0] = player;
            while (_route.Count > 2 && Num.Vector3.Distance(player with { Y = 0 }, _route[1] with { Y = 0 }) < 2.5f)
            {
                _route.RemoveAt(1);
            }
        }

        LayTrail((float)delta);
    }

    /// <summary>How far the player stands from the nearest leg of the way.</summary>
    private float Stray(Num.Vector3 player)
    {
        var at = new Vector2(player.X, player.Z);
        var best = float.MaxValue;
        for (var i = 1; i < _route.Count; i++)
        {
            var a = new Vector2(_route[i - 1].X, _route[i - 1].Z);
            var b = new Vector2(_route[i].X, _route[i].Z);
            var along = b - a;
            var t = along.LengthSquared() < 0.001f ? 0f : Mathf.Clamp((at - a).Dot(along) / along.LengthSquared(), 0f, 1f);
            best = Mathf.Min(best, at.DistanceTo(a + along * t));
        }

        return _route.Count < 2 ? 0f : best;
    }

    /// <summary>Marks every gap along the first metres of the way, pointing along it, with a glow that runs forward.</summary>
    private void LayTrail(float delta)
    {
        var mesh = _trail.Multimesh;
        if (!TrailEnabled || _route.Count < 2)
        {
            mesh.VisibleInstanceCount = 0;
            return;
        }

        _phase = (_phase + delta * 1.6f) % 1f;
        var count = 0;
        var walked = 0f;
        var next = 1.2f;
        for (var i = 1; i < _route.Count && count < mesh.InstanceCount; i++)
        {
            var a = _route[i - 1];
            var b = _route[i];
            var leg = Num.Vector3.Distance(a with { Y = 0 }, b with { Y = 0 });
            while (next <= walked + leg && count < mesh.InstanceCount && next <= TrailMetres)
            {
                var t = (next - walked) / Mathf.Max(leg, 0.001f);
                var x = a.X + (b.X - a.X) * t;
                var z = a.Z + (b.Z - a.Z) * t;
                var y = _world.GroundHeight(x, z) + 0.06f;
                var yaw = Mathf.Atan2(b.X - a.X, b.Z - a.Z);
                var basis = new Basis(Vector3.Up, yaw);
                mesh.SetInstanceTransform(count, new Transform3D(basis, new Vector3(x, y, z)));

                // Brighter where the running glow passes, fading towards the far end.
                var wave = 0.5f + 0.5f * Mathf.Cos((next / TrailMetres - _phase) * Mathf.Tau * 3f);
                var fade = 1f - next / TrailMetres;
                var glow = (0.45f + 0.55f * wave) * (0.35f + 0.65f * fade);
                mesh.SetInstanceColor(count, new Color(1f, 0.9f, 0.45f).Lerp(new Color(0.85f, 0.55f, 0.15f), 1f - glow));
                count++;
                next += TrailGap;
            }

            walked += leg;
            if (walked > TrailMetres)
            {
                break;
            }
        }

        mesh.VisibleInstanceCount = count;
    }
}
