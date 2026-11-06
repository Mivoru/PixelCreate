using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>What a character is doing this frame. The rig turns it into a pose.</summary>
public readonly record struct MotionState(
    float Speed, bool Grounded, float VerticalSpeed, bool Dashing, AttackKind Attack, int AttackTick);

/// <summary>How a rig looks. The player uses the default; goblins set their own.</summary>
public sealed record RigLook(Color Skin, float Scale, bool Hat, bool Pack, bool Ears, string Weapon)
{
    public static readonly RigLook Player = new(new Color("e8b796"), 1f, true, true, false, "sword");
}

/// <summary>
/// A jointed figure animated from code. Each state gives a target pose and the rig blends towards it,
/// so every change of state becomes a smooth transition without an authored clip for each pair.
/// </summary>
public sealed class PlayerRig
{
    private const int JointCount = 15;
    private const int Lean = 0, Twist = 1, Bob = 2, Head = 3;
    private const int ThighL = 4, ThighR = 5, ShinL = 6, ShinR = 7;
    private const int ArmL = 8, ArmR = 9, ArmRSide = 10, ForearmL = 11, ForearmR = 12, SwordTilt = 13, SwordYaw = 14;

    private readonly float[] _pose = new float[JointCount];
    private readonly float[] _target = new float[JointCount];
    private readonly Node3D _hips = new();
    private readonly Node3D _torso = new();
    private readonly Node3D _head = new();
    private readonly Node3D _thighL = new(), _thighR = new(), _shinL = new(), _shinR = new();
    private readonly Node3D _armL = new(), _armR = new(), _forearmL = new(), _forearmR = new();
    private readonly Node3D _sword = new();
    private readonly MeshInstance3D _slash;
    private readonly ShaderMaterial _slashMaterial;
    private readonly CpuParticles3D _sparks;
    private readonly CpuParticles3D _wind;
    private float _phase;
    private float _lastStride;
    private bool _footDown;
    private int _lastAttackTick = -1;

    public Node3D Root { get; } = new();

    /// <summary>True once for each step that lands. Reading it clears it.</summary>
    public bool TakeFootDown()
    {
        var down = _footDown;
        _footDown = false;
        return down;
    }

    public bool SwordVisible
    {
        get => _sword.Visible;
        set => _sword.Visible = value;
    }

    /// <summary>Set while the figure is hit. It shakes for as long as this is on.</summary>
    public bool Flash { get; set; }

    public PlayerRig(Color tunic, RigLook? look = null)
    {
        look ??= RigLook.Player;
        var skin = look.Skin;
        Root.Scale = Vector3.One * look.Scale;
        var trousers = new Color("5a6988");
        var leather = new Color("733e39");

        Root.AddChild(_hips);
        _hips.Position = new Vector3(0, 0.92f, 0);
        _hips.AddChild(Part(new BoxMesh { Size = new Vector3(0.44f, 0.2f, 0.3f) }, trousers, Vector3.Zero));

        _hips.AddChild(_torso);
        _torso.Position = new Vector3(0, 0.1f, 0);
        _torso.AddChild(Part(new CylinderMesh { TopRadius = 0.27f, BottomRadius = 0.22f, Height = 0.52f, RadialSegments = 8 }, tunic, new Vector3(0, 0.26f, 0)));
        _torso.AddChild(Part(new BoxMesh { Size = new Vector3(0.46f, 0.07f, 0.34f) }, leather, new Vector3(0, 0.03f, 0)));
        if (look.Pack)
        {
            _torso.AddChild(Part(new BoxMesh { Size = new Vector3(0.42f, 0.46f, 0.2f) }, leather, new Vector3(0, 0.3f, 0.26f)));
        }

        _torso.AddChild(_head);
        _head.Position = new Vector3(0, 0.58f, 0);
        _head.AddChild(Part(new SphereMesh { Radius = 0.21f, Height = 0.42f, RadialSegments = 10, Rings = 5 }, skin, new Vector3(0, 0.14f, 0)));
        if (look.Hat)
        {
            _head.AddChild(Part(new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.46f, Height = 0.05f, RadialSegments = 10 }, new Color("e4a672"), new Vector3(0, 0.26f, 0)));
            _head.AddChild(Part(new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.23f, Height = 0.24f, RadialSegments = 8 }, new Color("e4a672"), new Vector3(0, 0.39f, 0)));
        }

        if (look.Ears)
        {
            foreach (var side in new[] { -1f, 1f })
            {
                var ear = Part(new PrismMesh { Size = new Vector3(0.14f, 0.34f, 0.08f) }, skin, new Vector3(side * 0.28f, 0.2f, 0));
                ear.Rotation = new Vector3(0, 0, -side * 1.25f);
                _head.AddChild(ear);
            }
        }
        _head.AddChild(Part(new BoxMesh { Size = new Vector3(0.05f, 0.07f, 0.04f) }, new Color("181425"), new Vector3(-0.08f, 0.15f, -0.2f)));
        _head.AddChild(Part(new BoxMesh { Size = new Vector3(0.05f, 0.07f, 0.04f) }, new Color("181425"), new Vector3(0.08f, 0.15f, -0.2f)));

        BuildLeg(_thighL, _shinL, -0.12f, trousers, leather);
        BuildLeg(_thighR, _shinR, 0.12f, trousers, leather);
        BuildArm(_armL, _forearmL, -0.32f, tunic, skin);
        BuildArm(_armR, _forearmR, 0.32f, tunic, skin);

        _forearmR.AddChild(_sword);
        _sword.Position = new Vector3(0, -0.3f, -0.04f);
        BuildWeapon(look.Weapon);

        _slashMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/slash.gdshader") };
        _slash = new MeshInstance3D
        {
            Mesh = BuildSlashMesh(),
            Position = new Vector3(0, 1.15f, 0),
            Visible = false,
            MaterialOverride = _slashMaterial,
        };
        Root.AddChild(_slash);

        _sparks = new CpuParticles3D
        {
            Emitting = false,
            OneShot = true,
            Explosiveness = 0.85f,
            Amount = 26,
            Lifetime = 0.42f,
            Direction = Vector3.Forward,
            Spread = 55f,
            InitialVelocityMin = 3f,
            InitialVelocityMax = 9f,
            Gravity = new Vector3(0, -7f, 0),
            ScaleAmountMin = 0.4f,
            ScaleAmountMax = 1.2f,
            Mesh = new BoxMesh { Size = Vector3.One * 0.06f },
            MaterialOverride = Flat(new Color("fee761")),
            Position = new Vector3(0, 1.15f, -1.7f),
        };
        Root.AddChild(_sparks);

        _wind = new CpuParticles3D
        {
            Emitting = false,
            Amount = 34,
            Lifetime = 0.3f,
            Direction = Vector3.Back,
            Spread = 16f,
            InitialVelocityMin = 5f,
            InitialVelocityMax = 11f,
            Gravity = Vector3.Zero,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(0.45f, 0.85f, 0.2f),
            Mesh = new BoxMesh { Size = new Vector3(0.03f, 0.03f, 0.55f) },
            MaterialOverride = Flat(new Color("dff6f5")),
            Position = new Vector3(0, 0.95f, 0.2f),
            LocalCoords = false,
        };
        Root.AddChild(_wind);
    }

    private void BuildWeapon(string weapon)
    {
        var wood = new Color("733e39");
        var dark = new Color("3e2731");
        switch (weapon)
        {
            case "dagger":
                _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.06f, 0.05f, 0.14f) }, dark, Vector3.Zero));
                _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.08f, 0.03f, 0.42f) }, new Color("c0cbdc"), new Vector3(0, 0, -0.28f)));
                break;
            case "club":
                _sword.AddChild(Part(new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.2f, Height = 1.3f, RadialSegments = 7 }, wood, new Vector3(0, 0, -0.6f)));
                _sword.GetChild<Node3D>(0).Rotation = new Vector3(-Mathf.Pi / 2f, 0, 0);
                break;
            case "bow":
                var arc = Part(new TorusMesh { InnerRadius = 0.5f, OuterRadius = 0.56f, Rings = 12, RingSegments = 5 }, wood, new Vector3(0, 0, -0.1f));
                arc.Rotation = new Vector3(0, 0, Mathf.Pi / 2f);
                arc.Scale = new Vector3(1f, 1f, 0.45f);
                _sword.AddChild(arc);
                break;
            case "staff":
                var pole = Part(new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.05f, Height = 1.7f, RadialSegments = 6 }, wood, new Vector3(0, 0, -0.5f));
                pole.Rotation = new Vector3(-Mathf.Pi / 2f, 0, 0);
                _sword.AddChild(pole);
                _sword.AddChild(Part(new SphereMesh { Radius = 0.16f, Height = 0.32f, RadialSegments = 8, Rings = 4 }, new Color("f77622"), new Vector3(0, 0, -1.38f)));
                break;
            default:
                _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.06f, 0.05f, 0.2f) }, wood, Vector3.Zero));
                _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.24f, 0.05f, 0.07f) }, dark, new Vector3(0, 0, -0.12f)));
                _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.1f, 0.04f, 0.9f) }, new Color("c28569"), new Vector3(0, 0, -0.58f)));
                _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.05f, 0.05f, 0.9f) }, new Color("e4a672"), new Vector3(-0.025f, 0.005f, -0.58f)));
                break;
        }
    }

    /// <summary>Moves the rig towards the pose for this state. Call it once a frame.</summary>
    public void Animate(in MotionState state, float delta)
    {
        System.Array.Clear(_target);
        CarrySword(0f);
        _wind.Emitting = state.Dashing;
        var blend = 12f;
        if (state.Dashing)
        {
            DashPose();
            blend = 30f;
        }
        else if (!state.Grounded)
        {
            AirPose(state.VerticalSpeed);
            blend = 14f;
        }
        else if (state.Speed > 0.4f)
        {
            // Slowed or attacking players walk; at full speed they run.
            var running = state.Speed > Tuning.MoveSpeed * 0.7f;
            _phase += delta * (running ? 13.5f : 9f) * Mathf.Clamp(state.Speed / Tuning.MoveSpeed, 0.5f, 1.2f);
            if (running) RunPose(); else WalkPose();
            var stride = Mathf.Floor(_phase / Mathf.Pi);
            _footDown |= stride != _lastStride && state.Grounded;
            _lastStride = stride;
        }
        else
        {
            _phase += delta * 2f;
            IdlePose();
        }

        _slash.Visible = false;
        if (state.Attack != AttackKind.None)
        {
            AttackPose(state.Attack, state.AttackTick);
            blend = 34f;
        }

        _lastAttackTick = state.Attack == AttackKind.None ? -1 : state.AttackTick;

        var weight = 1f - Mathf.Exp(-blend * delta);
        for (var i = 0; i < JointCount; i++)
        {
            _pose[i] = Mathf.Lerp(_pose[i], _target[i], weight);
        }

        Apply();
    }

    /// <summary>The ready grip: the hand out from the hip and the blade angled forward and away from the body.</summary>
    private void CarrySword(float swing)
    {
        _target[ArmR] = 0.2f + swing;
        _target[ArmRSide] = 0.42f;
        _target[ForearmR] = -0.75f;
        _target[SwordTilt] = -0.55f;
        _target[SwordYaw] = -0.55f;
    }

    private void IdlePose()
    {
        var breath = Mathf.Sin(_phase);
        _target[Bob] = breath * 0.012f;
        _target[ArmL] = 0.05f;
        _target[ForearmL] = -0.15f;
        _target[Lean] = 0.03f + breath * 0.01f;
    }

    private void WalkPose()
    {
        var swing = Mathf.Sin(_phase);
        var lift = Mathf.Cos(_phase);
        _target[ThighL] = swing * 0.62f;
        _target[ThighR] = -swing * 0.62f;
        _target[ShinL] = Mathf.Max(0, lift) * 0.8f + 0.1f;
        _target[ShinR] = Mathf.Max(0, -lift) * 0.8f + 0.1f;
        _target[ArmL] = -swing * 0.5f;
        CarrySword(swing * 0.12f);
        _target[ForearmL] = -0.3f;
        _target[Lean] = 0.08f;
        _target[Twist] = swing * 0.1f;
        _target[Bob] = Mathf.Abs(lift) * 0.05f - 0.03f;
    }

    private void RunPose()
    {
        var swing = Mathf.Sin(_phase);
        var lift = Mathf.Cos(_phase);
        _target[ThighL] = swing * 1.05f - 0.15f;
        _target[ThighR] = -swing * 1.05f - 0.15f;
        _target[ShinL] = Mathf.Max(0, lift) * 1.5f + 0.25f;
        _target[ShinR] = Mathf.Max(0, -lift) * 1.5f + 0.25f;
        _target[ArmL] = -swing * 0.95f;
        _target[ForearmL] = -1.2f;
        // A running sword arm stays low and trails the blade behind, clear of the legs.
        _target[ArmR] = -0.35f + swing * 0.14f;
        _target[ArmRSide] = 0.5f;
        _target[ForearmR] = -0.5f;
        _target[SwordTilt] = 2.5f;
        _target[SwordYaw] = 0.45f;
        _target[Lean] = 0.36f;
        _target[Twist] = swing * 0.18f;
        _target[Bob] = Mathf.Abs(lift) * 0.09f - 0.08f;
        _target[Head] = -0.24f;
    }

    private void DashPose()
    {
        _target[Lean] = 0.75f;
        _target[ThighL] = -1.1f;
        _target[ThighR] = 0.9f;
        _target[ShinL] = 0.5f;
        _target[ShinR] = 1.1f;
        _target[ArmL] = -0.9f;
        _target[ArmR] = -0.9f;
        _target[ArmRSide] = 0.6f;
        _target[ForearmR] = -0.2f;
        _target[SwordTilt] = 2.7f;
        _target[SwordYaw] = 0.3f;
        _target[Bob] = -0.2f;
        _target[Head] = -0.6f;
    }

    private void AirPose(float verticalSpeed)
    {
        var rising = verticalSpeed > 0;
        _target[ThighL] = rising ? -0.95f : -0.3f;
        _target[ThighR] = rising ? 0.2f : 0.4f;
        _target[ShinL] = rising ? 1.1f : 0.4f;
        _target[ShinR] = rising ? 0.5f : 0.7f;
        _target[ArmL] = rising ? -0.5f : 0.1f;
        _target[ArmR] = rising ? 0.5f : 0.2f;
        _target[ArmRSide] = 0.6f;
        _target[ForearmL] = -0.5f;
        _target[Lean] = rising ? 0.12f : -0.05f;
    }

    private void AttackPose(AttackKind kind, int tick)
    {
        var attack = Tuning.Attack(kind);
        var heavy = kind == AttackKind.Heavy;
        var windup = Mathf.Clamp(tick / (float)attack.HitAt, 0f, 1f);
        var recover = Mathf.Clamp((tick - attack.HitAt) / (float)(attack.Total - attack.HitAt), 0f, 1f);
        var raise = tick < attack.HitAt ? windup : 1f - recover;
        var swung = tick >= attack.HitAt - 3;

        _target[Lean] = heavy ? (swung ? 0.55f - recover * 0.4f : -0.3f) : (swung ? 0.25f : -0.05f);
        _target[Twist] = heavy ? (swung ? -0.85f : 0.8f) : (swung ? -0.6f : 0.55f);
        _target[ArmR] = swung ? 0.7f + recover * 0.2f : (heavy ? 2.9f : 1.9f) * raise;
        _target[ArmRSide] = swung ? -0.7f : 0.55f;
        _target[ForearmR] = swung ? -0.15f : -1.1f;
        _target[SwordTilt] = swung ? 0.0f : 1.25f;
        _target[SwordYaw] = 0f;
        _target[ArmL] = swung ? -0.6f : 0.45f;
        _target[ForearmL] = -0.6f;
        _target[ThighL] = heavy ? -0.6f : -0.3f;
        _target[ThighR] = heavy ? 0.5f : 0.25f;
        _target[ShinL] = 0.3f;
        _target[ShinR] = 0.5f;
        _target[Bob] = heavy && swung ? -0.14f : -0.05f;

        // The head of the slash races along the arc, then the tail catches up and it dissolves.
        var first = attack.HitAt - 4;
        var length = heavy ? 13f : 10f;
        var progress = (tick - first) / length;
        _slash.Visible = progress is >= 0f and <= 1.6f;
        if (_slash.Visible)
        {
            _slashMaterial.SetShaderParameter("head", Mathf.Min(progress * 1.5f, 1.8f));
            _slashMaterial.SetShaderParameter("strength", progress < 1f ? 1f : 2.6f - progress * 1.6f);
            _slashMaterial.SetShaderParameter("inner_color", new Color(heavy ? "f77622" : "feae34"));
            _slashMaterial.SetShaderParameter("tip_color", new Color(heavy ? "fee761" : "ffffff"));
            _slash.Rotation = new Vector3(heavy ? 0.35f : 0.05f, 0f, heavy ? 0.85f : 0.2f);
            _slash.Scale = Vector3.One * (heavy ? 1.25f : 1f);
        }

        if (tick >= attack.HitAt && _lastAttackTick < attack.HitAt)
        {
            _sparks.Amount = heavy ? 42 : 24;
            _sparks.Restart();
            _sparks.Emitting = true;
        }
    }

    private void Apply()
    {
        var shake = Flash ? (float)GD.RandRange(-0.05, 0.05) : 0f;
        _hips.Position = new Vector3(shake, 0.92f + _pose[Bob], 0);
        _torso.Rotation = new Vector3(-_pose[Lean], _pose[Twist], 0);
        _head.Rotation = new Vector3(-_pose[Head], -_pose[Twist] * 0.6f, 0);
        _thighL.Rotation = new Vector3(_pose[ThighL], 0, 0);
        _thighR.Rotation = new Vector3(_pose[ThighR], 0, 0);
        _shinL.Rotation = new Vector3(-_pose[ShinL], 0, 0);
        _shinR.Rotation = new Vector3(-_pose[ShinR], 0, 0);
        _armL.Rotation = new Vector3(_pose[ArmL], 0, 0.08f);
        _armR.Rotation = new Vector3(_pose[ArmR], 0, -0.08f - _pose[ArmRSide]);
        _forearmL.Rotation = new Vector3(-_pose[ForearmL], 0, 0);
        _forearmR.Rotation = new Vector3(-_pose[ForearmR], 0, 0);
        _sword.Rotation = new Vector3(_pose[SwordTilt], _pose[SwordYaw], 0);
    }

    private void BuildLeg(Node3D thigh, Node3D shin, float x, Color cloth, Color boot)
    {
        _hips.AddChild(thigh);
        thigh.Position = new Vector3(x, -0.08f, 0);
        thigh.AddChild(Part(new CylinderMesh { TopRadius = 0.11f, BottomRadius = 0.085f, Height = 0.42f, RadialSegments = 6 }, cloth, new Vector3(0, -0.21f, 0)));
        thigh.AddChild(shin);
        shin.Position = new Vector3(0, -0.42f, 0);
        shin.AddChild(Part(new CylinderMesh { TopRadius = 0.085f, BottomRadius = 0.07f, Height = 0.32f, RadialSegments = 6 }, cloth, new Vector3(0, -0.16f, 0)));
        shin.AddChild(Part(new BoxMesh { Size = new Vector3(0.15f, 0.11f, 0.28f) }, boot, new Vector3(0, -0.35f, -0.06f)));
    }

    private void BuildArm(Node3D arm, Node3D forearm, float x, Color cloth, Color skin)
    {
        _torso.AddChild(arm);
        arm.Position = new Vector3(x, 0.47f, 0);
        arm.AddChild(Part(new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.07f, Height = 0.32f, RadialSegments = 6 }, cloth, new Vector3(0, -0.16f, 0)));
        arm.AddChild(forearm);
        forearm.Position = new Vector3(0, -0.32f, 0);
        forearm.AddChild(Part(new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.055f, Height = 0.26f, RadialSegments = 6 }, skin, new Vector3(0, -0.13f, 0)));
        forearm.AddChild(Part(new SphereMesh { Radius = 0.075f, Height = 0.15f, RadialSegments = 6, Rings = 3 }, skin, new Vector3(0, -0.29f, 0)));
    }

    /// <summary>A solid colour without the outline the post pass would draw around small pieces.</summary>
    private static ShaderMaterial Flat(Color color)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/stipple.gdshader") };
        material.SetShaderParameter("color", color);
        material.SetShaderParameter("strength", 1f);
        return material;
    }

    private static MeshInstance3D Part(Mesh mesh, Color color, Vector3 position) => new()
    {
        Mesh = mesh,
        Position = position,
        MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 1 },
    };

    /// <summary>The swing as a flat band: U runs along the swing, V from the player to the blade tip.</summary>
    private static ArrayMesh BuildSlashMesh()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        const int steps = 16;
        const float reach = 2.5f;
        for (var i = 0; i < steps; i++)
        {
            var u0 = i / (float)steps;
            var u1 = (i + 1) / (float)steps;
            var a = Mathf.Lerp(-1.25f, 1.25f, u0);
            var b = Mathf.Lerp(-1.25f, 1.25f, u1);
            var p0 = new Vector3(Mathf.Sin(a) * reach, 0, -Mathf.Cos(a) * reach);
            var p1 = new Vector3(Mathf.Sin(b) * reach, 0, -Mathf.Cos(b) * reach);
            var q0 = p0 * 0.28f;
            var q1 = p1 * 0.28f;
            AddSlashVertex(surface, p0, u0, 1f);
            AddSlashVertex(surface, p1, u1, 1f);
            AddSlashVertex(surface, q0, u0, 0f);
            AddSlashVertex(surface, q0, u0, 0f);
            AddSlashVertex(surface, p1, u1, 1f);
            AddSlashVertex(surface, q1, u1, 0f);
        }

        return surface.Commit();
    }

    private static void AddSlashVertex(SurfaceTool surface, Vector3 position, float u, float v)
    {
        surface.SetUV(new Vector2(u, v));
        surface.AddVertex(position);
    }
}
