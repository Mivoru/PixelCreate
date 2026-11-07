using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using PixelCreate.Core;
using CoreBox = PixelCreate.Core.Aabb;

namespace PixelCreate;

/// <summary>
/// A photo studio for checking the game without playing it. It needs no server, renders through the
/// same pixel stage as the game and saves named pictures. Run it with scripts/studio.ps1.
/// Arguments after "--": [folder] [jobs]. The jobs are people, motion, models, world, hitboxes and map.
/// </summary>
public partial class Studio : Node
{
    private const float Step = 1f / 60f;

    /// <summary>A scripted stretch of movement for the motion job.</summary>
    private readonly record struct Beat(string Name, float Seconds, float Speed, bool Grounded, float Vertical, bool Dashing, float Turn);

    private sealed record ShotData(string Name, float[] Eye, float[] At);

    private static readonly Beat[] Script =
    {
        new("idle", 0.6f, 0f, true, 0f, false, 0f),
        new("run", 1.4f, Tuning.MoveSpeed, true, 0f, false, 0f),
        new("stop", 1.2f, 0f, true, 0f, false, 0f),
        new("spin", 0.6f, 0f, true, 0f, false, 5f),
        new("settle", 0.6f, 0f, true, 0f, false, 0f),
        new("dash", 0.3f, Tuning.MoveSpeed * 2.6f, true, 0f, true, 0f),
        new("rise", 0.4f, Tuning.MoveSpeed, false, 6f, false, 0f),
        new("fall", 0.4f, Tuning.MoveSpeed, false, -7f, false, 0f),
        new("land", 0.8f, 0f, true, 0f, false, 0f),
    };

    private readonly List<PlayerRig> _rigs = new();
    private PixelStage _stage = null!;
    private string _folder = "";
    private Node3D _set = null!;

    public override async void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        _folder = args.ElementAtOrDefault(0) ?? "studio";
        var jobs = (args.ElementAtOrDefault(1) ?? "people,motion,models,world,hitboxes,map").Split(',');
        System.IO.Directory.CreateDirectory(_folder);
        _stage = new PixelStage(this);
        _set = new Node3D();
        _stage.View.AddChild(_set);
        try
        {
            foreach (var job in jobs)
            {
                GD.Print($"STUDIO {job}");
                Forget(job);
                await (job switch
                {
                    "people" => People(),
                    "motion" => Motion(),
                    "models" => Models(),
                    "world" => World(false),
                    "hitboxes" => World(true),
                    "map" => Map(),
                    _ => throw new ArgumentException($"There is no job called {job}."),
                });
                Clear();
            }
        }
        catch (Exception problem)
        {
            GD.PrintErr(problem.ToString());
        }

        GetTree().Quit();
    }

    /// <summary>Removes the old pictures of a job, so a renamed shot cannot linger and mislead.</summary>
    private void Forget(string job)
    {
        var prefix = job switch { "people" => "person_", "motion" => "motion_", "models" => "model_", "world" => "world_", "hitboxes" => "box_", _ => "map_" };
        foreach (var file in System.IO.Directory.GetFiles(_folder, prefix + "*.png"))
        {
            System.IO.File.Delete(file);
        }
    }

    private void Clear()
    {
        foreach (var child in _set.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var rig in _rigs)
        {
            rig.Root.QueueFree();
        }

        _rigs.Clear();
        _stage.Camera.Projection = Camera3D.ProjectionType.Perspective;
        _stage.Camera.Fov = 58;
        _stage.Camera.Far = 260;
        _stage.PostQuad.Visible = true;
    }

    /// <summary>Lets frames pass, with every rig standing idle or doing what the state says.</summary>
    private async Task Wait(int frames, MotionState? state = null)
    {
        var motion = state ?? new MotionState(0, true, 0, false, AttackKind.None, 0);
        for (var i = 0; i < frames; i++)
        {
            foreach (var rig in _rigs)
            {
                rig.Animate(motion, Step);
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void Save(string name) => _stage.View.GetTexture().GetImage().SavePng($"{_folder}/{name}.png");

    private MeshInstance3D Floor()
    {
        var floor = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(120, 0.1f, 120) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("8a9a84") },
            Position = new Vector3(0, -0.05f, 0),
        };
        _set.AddChild(floor);
        return floor;
    }

    private PlayerRig Rig(RigLook look, Vector3 position, float yaw)
    {
        var rig = new PlayerRig(new Color("0099db"), look);
        rig.Root.Position = position;
        rig.Root.Rotation = new Vector3(0, yaw, 0);
        _stage.View.AddChild(rig.Root);
        _rigs.Add(rig);
        return rig;
    }

    private void Dismiss(PlayerRig rig)
    {
        rig.Root.QueueFree();
        _rigs.Remove(rig);
    }

    /// <summary>Every human from four sides, then a close look at each face.</summary>
    private async Task People()
    {
        Floor();
        var goblin = new Color("7fb04a");
        var looks = new[] { ("hero", RigLook.Player) }.Concat(NpcLooks.All).Concat(new[]
        {
            ("skirmisher", new RigLook("skirmisher", goblin, 0.85f, "dagger")),
            ("archer", new RigLook("archer", goblin, 0.85f, "bow")),
            ("brute", new RigLook("brute", goblin, 1.3f, "club")),
            ("shaman", new RigLook("shaman", goblin, 1f, "staff")),
        }).ToArray();
        foreach (var (name, look) in looks)
        {
            var rig = Rig(look, Vector3.Zero, 0f);
            await Wait(20);
            _stage.Camera.Fov = 24;
            foreach (var (side, yaw) in new[] { ("front", 0f), ("three", 0.7f), ("side", Mathf.Pi / 2f), ("back", Mathf.Pi) })
            {
                rig.Root.Rotation = new Vector3(0, yaw, 0);
                rig.ResetCloth();
                _stage.Camera.LookAtFromPosition(new Vector3(0, 1.25f, -7.5f), new Vector3(0, 0.95f, 0));
                await Wait(8);
                Save($"person_{name}_{side}");
            }

            rig.Root.Rotation = new Vector3(0, 0.35f, 0);
            rig.ResetCloth();
            _stage.Camera.Fov = 10;
            var head = look.Scale * 1.68f;
            _stage.Camera.LookAtFromPosition(new Vector3(0, head + 0.05f, -4.2f), new Vector3(0, head, 0));
            await Wait(8);
            Save($"person_{name}_face");
            Dismiss(rig);
        }
    }

    /// <summary>Plays a fixed script of movement and films it, to judge how hair and cloth react.</summary>
    private async Task Motion()
    {
        Floor();
        foreach (var (name, look) in new[] { ("hero", RigLook.Player), ("mira", NpcLooks.For("receptionist")!) })
        {
            foreach (var (view, eye) in new[] { ("side", new Vector3(7f, 1.3f, 0f)), ("back", new Vector3(0, 1.8f, 7f)) })
            {
                var rig = Rig(look, Vector3.Zero, 0f);
                _stage.Camera.Fov = 24;
                _stage.Camera.LookAtFromPosition(eye, new Vector3(0, 1f, 0));
                await Wait(30);
                var frame = 0;
                foreach (var beat in Script)
                {
                    var state = new MotionState(beat.Speed, beat.Grounded, beat.Vertical, beat.Dashing, AttackKind.None, 0);
                    var ticks = (int)(beat.Seconds * 60f);
                    for (var tick = 0; tick < ticks; tick++)
                    {
                        rig.Root.Rotation += new Vector3(0, beat.Turn * Step, 0);
                        await Wait(1, state);
                        if (tick % 9 == 0)
                        {
                            Save($"motion_{name}_{view}_{frame++:00}_{beat.Name}");
                        }
                    }
                }

                Dismiss(rig);
            }
        }
    }

    /// <summary>Every model from two sides and from below, with its collision box drawn over it.</summary>
    private async Task Models()
    {
        var floor = Floor();
        var shader = GD.Load<Shader>("res://shaders/prop.gdshader");
        var names = DirAccess.GetFilesAt("res://assets").Where(f => f.EndsWith(".glb")).Select(f => f[..^4])
            .Where(n => !n.StartsWith("anime_")).OrderBy(n => n).ToArray();
        foreach (var name in names)
        {
            var model = GD.Load<PackedScene>($"res://assets/{name}.glb").Instantiate<Node3D>();
            ZoneView.Dress(model, shader);
            _set.AddChild(model);
            var bounds = Bounds(model);
            var size = bounds.Size.Length();
            var middle = bounds.GetCenter();
            MeshInstance3D? wire = null;
            if (Zone.CollisionOf(name) is { X: > 0 } shape)
            {
                wire = Wire(new[] { new CoreBox(new(-shape.X, 0, -shape.Y), new(shape.X, shape.Z, shape.Y)) }, new Color("ff2fd0"));
                _set.AddChild(wire);
            }

            _stage.PostQuad.Visible = wire is null;
            foreach (var (view, eye) in new[]
            {
                ("a", new Vector3(0.75f, 0.55f, 1f)), ("b", new Vector3(-0.8f, 0.4f, -1f)), ("under", new Vector3(0.7f, -0.45f, 0.9f)),
            })
            {
                floor.Visible = view != "under";
                _stage.Camera.LookAtFromPosition(middle + eye.Normalized() * Mathf.Max(size * 1.15f, 2f), middle);
                await Wait(3);
                Save($"model_{name}_{view}");
            }

            floor.Visible = true;
            model.QueueFree();
            wire?.QueueFree();
            await Wait(1);
        }
    }

    private static Godot.Aabb Bounds(Node node)
    {
        var bounds = new Godot.Aabb();
        var first = true;
        foreach (var mesh in Meshes(node))
        {
            var box = mesh.GlobalTransform * mesh.GetAabb();
            bounds = first ? box : bounds.Merge(box);
            first = false;
        }

        return bounds;
    }

    private static IEnumerable<MeshInstance3D> Meshes(Node node)
    {
        if (node is MeshInstance3D mesh)
        {
            yield return mesh;
        }

        foreach (var child in node.GetChildren())
        {
            foreach (var found in Meshes(child))
            {
                yield return found;
            }
        }
    }

    /// <summary>The shots listed in studio/shots.json, with people in place and optional collision lines.</summary>
    private async Task World(bool boxes)
    {
        var zone = Zone.Load("meadow-valley");
        _set.AddChild(ZoneView.Build(zone));
        foreach (var npc in zone.World.Npcs)
        {
            if (NpcLooks.For(npc.Role) is { } look)
            {
                Rig(look, new Vector3(npc.Position.X, npc.Position.Y, npc.Position.Z), npc.Yaw);
            }
        }

        if (boxes)
        {
            _set.AddChild(Wire(zone.World.Boxes, new Color("ff2fd0")));
            _stage.PostQuad.Visible = false;
        }

        var text = FileAccess.GetFileAsString("res://studio/shots.json");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        foreach (var shot in JsonSerializer.Deserialize<ShotData[]>(text, options)!)
        {
            var eye = new Vector3(shot.Eye[0], zone.World.GroundHeight(shot.Eye[0], shot.Eye[2]) + shot.Eye[1], shot.Eye[2]);
            var at = new Vector3(shot.At[0], zone.World.GroundHeight(shot.At[0], shot.At[2]) + shot.At[1], shot.At[2]);
            _stage.Camera.LookAtFromPosition(eye, at);
            await Wait(4);
            Save($"{(boxes ? "box" : "world")}_{shot.Name}");
        }
    }

    /// <summary>The zone from straight above, with collision, enemy camps, doors and people marked.</summary>
    private async Task Map()
    {
        var zone = Zone.Load("meadow-valley");
        _set.AddChild(ZoneView.Build(zone));
        var camera = _stage.Camera;
        camera.Projection = Camera3D.ProjectionType.Orthogonal;
        camera.Size = zone.Size;
        _stage.View.GetChildren().OfType<WorldEnvironment>().First().Environment.FogEnabled = false;
        _stage.View.Size = new Vector2I(1100, 1100);
        camera.Far = 500;
        camera.LookAtFromPosition(new Vector3(0, 200, 0.01f), Vector3.Zero, Vector3.Forward);
        await Wait(4);
        Save("map_plain");
        _set.AddChild(Wire(zone.World.Boxes, new Color("ff2fd0")));
        foreach (var camp in zone.World.EnemySpawns)
        {
            _set.AddChild(Ring(camp.Center, camp.Radius, new Color(camp.Rare ? "fee761" : "ff3030")));
        }

        foreach (var portal in zone.World.Portals)
        {
            _set.AddChild(Ring(portal.From, 3f, new Color("30e0ff")));
        }

        foreach (var npc in zone.World.Npcs)
        {
            _set.AddChild(Ring(npc.Position, 2f, Colors.White));
        }

        _stage.PostQuad.Visible = false;
        await Wait(4);
        Save("map_marked");
        _stage.View.GetChildren().OfType<WorldEnvironment>().First().Environment.FogEnabled = true;
        _stage.View.Size = new Vector2I(960, 540);
    }

    private static StandardMaterial3D LineMaterial(Color color) => new()
    {
        AlbedoColor = color,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        NoDepthTest = true,
    };

    private static MeshInstance3D Wire(IEnumerable<CoreBox> boxes, Color color)
    {
        var lines = new ImmediateMesh();
        lines.SurfaceBegin(Mesh.PrimitiveType.Lines, LineMaterial(color));
        foreach (var box in boxes)
        {
            var a = new Vector3(box.Min.X, box.Min.Y, box.Min.Z);
            var b = new Vector3(box.Max.X, box.Max.Y, box.Max.Z);
            var corners = new[]
            {
                new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, a.Y, b.Z), new Vector3(a.X, a.Y, b.Z),
                new Vector3(a.X, b.Y, a.Z), new Vector3(b.X, b.Y, a.Z), new Vector3(b.X, b.Y, b.Z), new Vector3(a.X, b.Y, b.Z),
            };
            foreach (var (from, to) in new[] { (0, 1), (1, 2), (2, 3), (3, 0), (4, 5), (5, 6), (6, 7), (7, 4), (0, 4), (1, 5), (2, 6), (3, 7) })
            {
                lines.SurfaceAddVertex(corners[from]);
                lines.SurfaceAddVertex(corners[to]);
            }
        }

        lines.SurfaceEnd();
        return new MeshInstance3D { Mesh = lines };
    }

    private static MeshInstance3D Ring(System.Numerics.Vector3 centre, float radius, Color color)
    {
        var lines = new ImmediateMesh();
        lines.SurfaceBegin(Mesh.PrimitiveType.LineStrip, LineMaterial(color));
        for (var i = 0; i <= 32; i++)
        {
            var angle = i / 32f * Mathf.Tau;
            lines.SurfaceAddVertex(new Vector3(centre.X + Mathf.Cos(angle) * radius, centre.Y + 1f, centre.Z + Mathf.Sin(angle) * radius));
        }

        lines.SurfaceEnd();
        return new MeshInstance3D { Mesh = lines };
    }
}
