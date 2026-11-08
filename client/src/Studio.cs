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
        var jobs = (args.ElementAtOrDefault(1) ?? "people,animals,monsters,motion,models,world,hitboxes,map").Split(',');
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
                    "animals" => Animals(),
                    "monsters" => Monsters(),
                    "motion" => Motion(),
                    "attacks" => Attacks(),
                    "weapons" => Weapons(),
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
        var prefix = job switch { "animals" => "animal_", "monsters" => "monster_", "people" => "person_", "motion" => "motion_", "attacks" => "attack_", "weapons" => "weapon_", "models" => "model_", "world" => "world_", "hitboxes" => "box_", _ => "map_" };
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

    /// <summary>
    /// Every slime and goblin: a portrait from the front, one at three quarters, the moment before an
    /// attack, and a lineup of all of them side by side to compare sizes and silhouettes.
    /// </summary>
    private async Task Monsters()
    {
        Floor();
        var sfx = new Sfx(this) { Muted = true };
        var monsters = EnemyDef.All.Select((def, index) => (def, index)).Where(p => p.def.Kind is "slime" or "goblin" or "orc" or "werewolf").ToList();
        foreach (var (def, index) in monsters)
        {
            IEnemyView view = def.Kind == "slime" ? new SlimeView() : new GoblinView(def);
            _set.AddChild(view.Root);
            var height = def.Size * 1.1f;
            var poses = new List<(string Pose, float Yaw, float Distance, EnemyAction Action, byte Ability)>
            {
                ("front", 0f, 3.6f, EnemyAction.Idle, 0), ("three", 0.7f, 4.2f, EnemyAction.Idle, 0), ("windup", 0.5f, 4.2f, EnemyAction.Windup, 0),
                ("side", 1.57f, 4.2f, EnemyAction.Idle, 0),
            };

            // Orcs also show each ability: the wind-up and the blow, from three quarters.
            for (var a = 0; def.Kind is "orc" or "werewolf" && a < (def.Abilities?.Length ?? 0); a++)
            {
                var effect = def.Abilities![a].Effect;
                poses.Add(($"{effect}_cast", 1.0f, 4.6f, EnemyAction.Cast, (byte)a));
                if (effect is "pounce" or "superJump")
                {
                    poses.Add(($"{effect}_leap", 1.57f, 5.4f, EnemyAction.Leap, (byte)a));
                }

                poses.Add(($"{effect}_strike", 1.0f, 4.6f, EnemyAction.Strike, (byte)a));
                if (effect == "superJump")
                {
                    poses.Add(($"{effect}_roll", 1.57f, 5.4f, EnemyAction.Roll, (byte)a));
                }
            }

            foreach (var (pose, yaw, distance, action, ability) in poses)
            {
                for (var frame = 0; frame < 50; frame++)
                {
                    var state = new EnemyView(1, (byte)index, 3, default, yaw, 10, 10, action, 0, new System.Numerics.Vector3(0, 0, -2), true, ability);
                    view.Update(state, Vector3.Zero, Step, sfx);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }

                // Goblins carry staves and clubs that reach well above their heads.
                _stage.Camera.Fov = def.Kind == "slime" ? 30 : 40;
                _stage.Camera.LookAtFromPosition(new Vector3(0, height * 0.9f + 0.4f, -distance * def.Size), new Vector3(0, height * (def.Kind == "slime" ? 0.7f : 0.85f), 0));
                await Wait(2);
                Save($"monster_{def.Id}_{pose}");
            }

            view.Root.QueueFree();
        }

        // All of them in a row, with the hero for scale.
        var x = -monsters.Count * 0.9f;
        var lineup = new List<IEnemyView>();
        foreach (var (def, index) in monsters)
        {
            IEnemyView view = def.Kind == "slime" ? new SlimeView() : new GoblinView(def);
            _set.AddChild(view.Root);
            lineup.Add(view);
            view.Update(new EnemyView(1, (byte)index, 3, default, 0f, 10, 10, EnemyAction.Idle, 0, default, false), new Vector3(x, 0, 0), Step, sfx);
            x += 1.9f;
        }

        Rig(RigLook.Player, new Vector3(x, 0, 0), 0f);
        _stage.Camera.Fov = 40;
        _stage.Camera.LookAtFromPosition(new Vector3(0, 2.4f, -13f), new Vector3(0, 1f, 0));
        await Wait(20);
        Save("monster_lineup");
    }

    /// <summary>Every animal standing, grazing and running, from the side and at three quarters.</summary>
    private async Task Animals()
    {
        Floor();
        _stage.Camera.Fov = 22;
        foreach (var def in EnemyDef.All.Where(d => d.Kind is "animal" or "wolf"))
        {
            var view = new AnimalView(def, 2);
            _set.AddChild(view.Root);
            var poses = new[]
            {
                ("stand", 0f, 1.4f, EnemyAction.Idle), ("graze", 0f, 0.7f, EnemyAction.Graze),
                ("alert", 0f, 2.2f, EnemyAction.Alert), ("run", 6f, 1.57f, EnemyAction.Walk),
            };
            if (def.Kind == "wolf")
            {
                // A hunter's own moves: the snarl before a strike, and the lunge.
                poses = poses.Append(("snarl", 0f, 1.2f, EnemyAction.Windup)).Append(("lunge", 9f, 1.57f, EnemyAction.Strike))
                    .Append(("pounce", 7f, 1.57f, EnemyAction.Leap)).ToArray();
            }

            if (def.Abilities?.Any(a => a.Effect == "shadowRun") == true)
            {
                poses = poses.Append(("shadow", 6f, 1.2f, EnemyAction.Shadow)).ToArray();
            }

            foreach (var (pose, speed, yaw, action) in poses)
            {
                var position = Vector3.Zero;
                for (var frame = 0; frame < (speed > 0 ? 40 : 120); frame++)
                {
                    position += new Vector3(-Mathf.Sin(yaw) * speed * Step, 0, -Mathf.Cos(yaw) * speed * Step);
                    view.Update(new EnemyView(1, 0, 1, default, yaw, 10, 10, action, 0, default, def.Kind == "wolf"), position, Step, null!);
                    _stage.Camera.LookAtFromPosition(position + new Vector3(Mathf.Sin(yaw + 1.57f) * 5.5f, 1.4f, Mathf.Cos(yaw + 1.57f) * 5.5f), position + Vector3.Up * 0.6f);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }

                Save($"animal_{def.Id}_{pose}");
            }

            view.Root.QueueFree();
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

    /// <summary>
    /// Moonleaper's great leap played as the game plays it: the crouch, the flight along the real arc
    /// and the landing, once striking the player and once rolling out of a miss, a frame every few ticks.
    /// </summary>
    private async Task GreatLeaps()
    {
        var index = EnemyDef.All.ToList().FindIndex(d => d.Id == "werewolf_moonleaper");
        var def = EnemyDef.All[index];
        var ability = (byte)def.Abilities!.ToList().FindIndex(a => a.Effect == "superJump");
        var sfx = new Sfx(this) { Muted = true };
        foreach (var ending in new[] { EnemyAction.Strike, EnemyAction.Roll })
        {
            var view = new GoblinView(def);
            _set.AddChild(view.Root);
            const float distance = 14f;
            var air = Enemy.SuperJumpAirTicks;
            var script = new List<(EnemyAction Action, int Ticks)> { (EnemyAction.Idle, 20), (EnemyAction.Cast, def.Abilities![ability].WindupTicks), (EnemyAction.Leap, air), (ending, ending == EnemyAction.Roll ? Enemy.RollTicks : 40), (EnemyAction.Idle, 12) };
            _stage.Camera.Fov = 40;
            _stage.Camera.LookAtFromPosition(new Vector3(30f, 3f, -distance / 2f), new Vector3(0, 1.8f, -distance / 2f));
            _stage.Camera.Fov = 24;
            var frame = 0;
            var tick = 0;
            var landed = Vector3.Zero;
            foreach (var (action, ticks) in script)
            {
                for (var t = 0; t < ticks; t++, tick++)
                {
                    var position = action switch
                    {
                        EnemyAction.Leap => new Vector3(0, Tuning.Gravity * (t * Tuning.TickSeconds) * ((air - t) * Tuning.TickSeconds) / 2f, -distance * t / air),
                        EnemyAction.Roll => landed + new Vector3(0, 0, -(t < Enemy.RollTicks / 2 ? 5f * t : 5f * Enemy.RollTicks / 2f + 1.5f * (t - Enemy.RollTicks / 2)) * Tuning.TickSeconds),
                        _ when tick > 20 + def.Abilities![ability].WindupTicks => landed,
                        _ => Vector3.Zero,
                    };
                    if (action == EnemyAction.Leap)
                    {
                        landed = new Vector3(0, 0, -distance * (t + 1) / air);
                    }

                    var state = new EnemyView(1, (byte)index, 15, new System.Numerics.Vector3(position.X, position.Y, position.Z), 0f, 10, 10, action, 0,
                        new System.Numerics.Vector3(0, 0, -distance), true, ability);
                    view.Update(state, position, Step, sfx);

                    // The camera keeps alongside the wolf, so its body reads at every moment of the leap.
                    _stage.Camera.Fov = 34;
                    _stage.Camera.LookAtFromPosition(position + new Vector3(9f, 1.4f, 0f), position + new Vector3(0, 1.4f, 0));
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (tick % 6 == 0)
                    {
                        Save($"attack_moonleaper_{(ending == EnemyAction.Roll ? "miss" : "hit")}_{frame++:00}");
                    }
                }
            }

            view.Root.QueueFree();
        }
    }

    /// <summary>Each weapon's attacks from the side, a frame every few ticks, for checking the poses.</summary>
    private async Task Attacks()
    {
        Floor();
        foreach (var (weapon, kinds) in new[]
        {
            ("sword", new[] { AttackKind.Light, AttackKind.Heavy, AttackKind.RisingSlash, AttackKind.Whirlwind }),
            ("dagger", new[] { AttackKind.DaggerFlurry, AttackKind.DaggerLeap, AttackKind.BladeDance, AttackKind.ShadowStep }),
            ("club", new[] { AttackKind.ClubSwing, AttackKind.ClubSpin, AttackKind.GroundPound }),
            ("staff", new[] { AttackKind.StaffBolt, AttackKind.StaffWall }),
        })
        {
            foreach (var kind in kinds)
            {
                var rig = Rig(RigLook.Player, Vector3.Zero, Mathf.Pi / 2f);
                rig.SetWeapon(weapon);
                var leap = kind == AttackKind.DaggerLeap;
                _stage.Camera.Fov = leap ? 36 : 24;
                _stage.Camera.LookAtFromPosition(new Vector3(0, leap ? 1.8f : 1.4f, 7f), new Vector3(0, leap ? 1.6f : 1.15f, 0));
                await Wait(20);
                var attack = Tuning.Attack(kind);
                for (var tick = 0; tick < attack.Total; tick++)
                {
                    if (kind == AttackKind.GroundPound && tick is > 16 and < Tuning.GroundPoundLandTick)
                    {
                        // The plunge lasts as long as the fall; the film skips to the landing.
                        tick = Tuning.GroundPoundLandTick;
                    }

                    // The leap rises and falls as it would in the game.
                    var airborne = kind == AttackKind.DaggerLeap && tick is > 2 and < Tuning.LeapHitAt;
                    var t = (tick - 2) / (float)(Tuning.LeapHitAt - 2);
                    rig.Root.Position = new Vector3(0, airborne ? 4f * 1.1f * t * (1f - t) : 0f, 0);
                    await Wait(1, new MotionState(0f, !airborne, 0f, false, kind, tick));
                    if (tick == attack.HitAt)
                    {
                        await Reach(weapon, kind, rig, attack);
                    }
                    if (tick % 4 == 0)
                    {
                        Save($"attack_{weapon}_{kind}_{tick:00}");
                    }
                }

                Dismiss(rig);
            }
        }

        // How each weapon is carried: standing, walking, running, in the air and dashing, from the side.
        foreach (var weapon in new[] { "sword", "dagger", "club", "bow", "staff" })
        {
            foreach (var (name, state) in new[]
            {
                ("idle", new MotionState(0f, true, 0f, false, AttackKind.None, 0)),
                ("walk", new MotionState(4f, true, 0f, false, AttackKind.None, 0)),
                ("run", new MotionState(9f, true, 0f, false, AttackKind.None, 0)),
                ("air", new MotionState(3f, false, 2f, false, AttackKind.None, 0)),
                ("dash", new MotionState(20f, true, 0f, true, AttackKind.None, 0)),
            })
            {
                var rig = Rig(RigLook.Player, Vector3.Zero, Mathf.Pi / 2f);
                rig.SetWeapon(weapon);
                _stage.Camera.Fov = 24;
                _stage.Camera.LookAtFromPosition(new Vector3(0, 1.4f, 7f), new Vector3(0, 1.15f, 0));
                await Wait(40, state);
                Save($"attack_carry_all_{weapon}_{name}");
                Dismiss(rig);
            }
        }

        await GreatLeaps();

        // The slide, from the side.
        {
            var rig = Rig(RigLook.Player, Vector3.Zero, Mathf.Pi / 2f);
            rig.SetWeapon("sword");
            _stage.Camera.Fov = 28;
            _stage.Camera.LookAtFromPosition(new Vector3(0, 1.2f, 4.5f), new Vector3(0, 0.8f, 0));
            await Wait(40, new MotionState(6f, true, 0f, false, AttackKind.None, 0, WeaponMove.Slide));
            Save("attack_slide_side");
            Dismiss(rig);
        }

        // The guard, from the side and from the front, and an orc reeling from a parry.
        foreach (var (view, eye) in new[] { ("side", new Vector3(4.2f, 1.4f, 0f)), ("front", new Vector3(0f, 1.5f, -4.2f)) })
        {
            var rig = Rig(RigLook.Player, Vector3.Zero, 0f);
            rig.SetWeapon("sword");
            _stage.Camera.Fov = 28;
            _stage.Camera.LookAtFromPosition(eye, new Vector3(0, 1.2f, 0));
            await Wait(40, new MotionState(0f, true, 0f, false, AttackKind.None, 0, WeaponMove.Guard));
            Save($"attack_guard_{view}");
            Dismiss(rig);
        }

        // The bow: drawing to a full draw, loosing and nocking the next arrow, from the side and over the shoulder.
        foreach (var (view, eye, look, pitch) in new[]
        {
            ("side", new Vector3(0, 1.5f, 4.2f), new Vector3(-0.2f, 1.35f, 0), 0f),
            ("shoulder", new Vector3(0.62f, 1.68f, 2.3f), new Vector3(0.3f, 1.5f, -6f), 0f),
            ("top", new Vector3(0.01f, 5.5f, 0f), new Vector3(0, 1f, 0), 0f),
            ("up", new Vector3(0, 1.5f, 4.2f), new Vector3(-0.2f, 1.5f, 0), 0.6f),
        })
        {
            var rig = Rig(RigLook.Player, Vector3.Zero, view is "side" or "up" ? Mathf.Pi / 2f : 0f);
            rig.SetWeapon("bow");
            _stage.Camera.Fov = view == "shoulder" ? 50 : view == "top" ? 30 : 28;
            _stage.Camera.LookAtFromPosition(eye, look);
            await Wait(20);
            for (var tick = 0; tick <= Tuning.FullDraw; tick++)
            {
                await Wait(1, new MotionState(0f, true, 0f, false, AttackKind.None, 0, WeaponMove.BowDraw, Tuning.Drawn((byte)tick), pitch));
                if (tick % 18 == 0)
                {
                    Save($"attack_bow_{view}_draw_{tick:00}");
                }
            }

            var shot = Tuning.Attack(AttackKind.BowShot);
            for (var tick = 0; tick < shot.Total && view != "up"; tick++)
            {
                await Wait(1, new MotionState(0f, true, 0f, false, AttackKind.BowShot, tick, WeaponMove.BowRelease, 0f, pitch));
                if (tick % 6 == 2)
                {
                    Save($"attack_bow_{view}_loose_{tick:00}");
                }
            }

            Dismiss(rig);
        }
    }

    /// <summary>Every weapon model up close: its flat, its edge, and from three quarters, as the toon shading shows it.</summary>
    private async Task Weapons()
    {
        Floor().Visible = false;
        var names = DirAccess.GetFilesAt("res://assets").Where(f => f.StartsWith("weapon_") && f.EndsWith(".glb"))
            .Select(f => f[7..^4]).OrderBy(n => n).ToArray();
        foreach (var name in names)
        {
            var model = PlayerRig.WeaponModel(name)!;
            _set.AddChild(model);
            var bounds = Bounds(model);
            var middle = bounds.GetCenter();
            var distance = bounds.Size.Length() * 1.9f;
            _stage.Camera.Fov = 30;
            foreach (var (view, eye, up) in new[]
            {
                ("flat", new Vector3(0, 1, 0.001f), Vector3.Forward),
                ("edge", new Vector3(1, 0, 0), Vector3.Up),
                ("three", new Vector3(0.7f, 0.55f, 0.45f), Vector3.Up),
            })
            {
                _stage.Camera.LookAtFromPosition(middle + eye.Normalized() * distance, middle, up);
                await Wait(3);
                Save($"weapon_{name}_{view}");
            }

            model.QueueFree();
            await Wait(1);
        }
    }

    /// <summary>Every model from two sides and from below, with its collision box drawn over it.</summary>
    private async Task Models()
    {
        var floor = Floor();
        var shader = GD.Load<Shader>("res://shaders/prop.gdshader");
        var names = DirAccess.GetFilesAt("res://assets").Where(f => f.EndsWith(".glb")).Select(f => f[..^4])
            .Where(n => !n.StartsWith("anime_")).OrderBy(n => n).ToArray();
        // PIXEL_MODELS=cottage,bridge photographs only the models whose names start with those words.
        var only = (System.Environment.GetEnvironmentVariable("PIXEL_MODELS") ?? "").Split(',', System.StringSplitOptions.RemoveEmptyEntries);
        if (only.Length > 0)
        {
            names = names.Where(n => only.Any(prefix => n.StartsWith(prefix, System.StringComparison.Ordinal))).ToArray();
        }

        foreach (var name in names)
        {
            var model = GD.Load<PackedScene>($"res://assets/{name}.glb").Instantiate<Node3D>();
            ZoneView.Dress(model, shader);
            _set.AddChild(model);
            var bounds = Bounds(model);
            var size = bounds.Size.Length();
            var middle = bounds.GetCenter();
            MeshInstance3D? wire = null;
            // The measured collision boxes, exactly as the game places them.
            var shape = Zone.ShapeOf(name);
            if (shape.Count > 0)
            {
                wire = Wire(shape.Select(b => new CoreBox(new(b.X0, 0, b.Z0), new(b.X1, b.Top, b.Z1))).ToArray(), new Color("ff2fd0"));
                _set.AddChild(wire);
            }

            _stage.PostQuad.Visible = wire is null;
            foreach (var (view, eye) in new[]
            {
                ("a", new Vector3(0.75f, 0.55f, 1f)), ("b", new Vector3(-0.8f, 0.4f, -1f)), ("under", new Vector3(0.7f, -0.45f, 0.9f)),
                ("front", new Vector3(0f, 0.15f, 1f)), ("side", new Vector3(1f, 0.15f, -0.1f)),
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
        // PIXEL_SHOTS=guild,arena films only the shots whose names start with one of those words.
        var only = (System.Environment.GetEnvironmentVariable("PIXEL_SHOTS") ?? "").Split(',', System.StringSplitOptions.RemoveEmptyEntries);
        foreach (var shot in JsonSerializer.Deserialize<ShotData[]>(text, options)!)
        {
            if (only.Length > 0 && !only.Any(prefix => shot.Name.StartsWith(prefix, System.StringComparison.Ordinal)))
            {
                continue;
            }

            var eye = new Vector3(shot.Eye[0], zone.World.GroundHeight(shot.Eye[0], shot.Eye[2]) + shot.Eye[1], shot.Eye[2]);
            var at = new Vector3(shot.At[0], zone.World.GroundHeight(shot.At[0], shot.At[2]) + shot.At[1], shot.At[2]);
            _stage.SetGloom(zone.GroundAt(shot.Eye[0], shot.Eye[2]).Blight);
            _stage.Camera.LookAtFromPosition(eye, at);
            _stage.PlaceMoon();
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

        // The same picture is the game's minimap, north (-Z) up.
        _stage.View.GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://assets/ui/minimap.png"));
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

    /// <summary>
    /// The blow's frame from straight above, with the area it hits laid on the ground: red is the
    /// attack's reach and arc, the yellow ring the reach to a goblin-sized enemy's middle.
    /// </summary>
    private async Task Reach(string weapon, AttackKind kind, PlayerRig rig, AttackDef attack)
    {
        var half = attack.MinDot <= -1f ? Mathf.Pi : Mathf.Acos(Mathf.Clamp(attack.MinDot, -1f, 1f));
        var stipple = GD.Load<Shader>("res://shaders/stipple.gdshader");
        ShaderMaterial Paint(Color color)
        {
            var material = new ShaderMaterial { Shader = stipple };
            material.SetShaderParameter("color", color);
            material.SetShaderParameter("strength", 0.75f);
            return material;
        }

        const int steps = 48;
        Vector3 At(float angle, float distance) => new Vector3(-Mathf.Sin(angle), 0, -Mathf.Cos(angle)) * distance + Vector3.Up * 0.03f;
        var area = new ArrayMesh();
        var fill = new SurfaceTool();
        fill.Begin(Mesh.PrimitiveType.Triangles);
        var ring = new SurfaceTool();
        ring.Begin(Mesh.PrimitiveType.Triangles);
        for (var i = 0; i < steps; i++)
        {
            var a0 = -half + 2f * half * i / steps;
            var a1 = -half + 2f * half * (i + 1) / steps;
            fill.AddVertex(Vector3.Up * 0.03f);
            fill.AddVertex(At(a0, attack.Range));
            fill.AddVertex(At(a1, attack.Range));
            var outer = attack.Range + 0.5f;
            ring.AddVertex(At(a0, outer - 0.06f));
            ring.AddVertex(At(a0, outer + 0.06f));
            ring.AddVertex(At(a1, outer + 0.06f));
            ring.AddVertex(At(a0, outer - 0.06f));
            ring.AddVertex(At(a1, outer + 0.06f));
            ring.AddVertex(At(a1, outer - 0.06f));
        }

        fill.Commit(area);
        ring.Commit(area);
        area.SurfaceSetMaterial(0, Paint(new Color("e43b44")));
        area.SurfaceSetMaterial(1, Paint(new Color("fee761")));
        var wedge = new MeshInstance3D { Mesh = area, Position = rig.Root.Position with { Y = 0 }, Rotation = new Vector3(0, rig.Root.Rotation.Y, 0) };
        _set.AddChild(wedge);
        var fov = _stage.Camera.Fov;
        var place = _stage.Camera.GlobalTransform;
        _stage.Camera.Fov = 60;
        _stage.Camera.LookAtFromPosition(new Vector3(0, 9f, 0.4f), Vector3.Zero);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Save($"attack_reach_all_{weapon}_{kind}");
        wedge.QueueFree();
        _stage.Camera.Fov = fov;
        _stage.Camera.GlobalTransform = place;
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
