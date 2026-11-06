using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>What a character is doing this frame. The rig turns it into a pose.</summary>
public readonly record struct MotionState(
    float Speed, bool Grounded, float VerticalSpeed, bool Dashing, AttackKind Attack, int AttackTick);

/// <summary>
/// A jointed figure animated from code. Each state gives a target pose and the rig blends towards it,
/// so every change of state becomes a smooth transition without an authored clip for each pair.
/// </summary>
public sealed class PlayerRig
{
    private const int JointCount = 14;
    private const int Lean = 0, Twist = 1, Bob = 2, Head = 3;
    private const int ThighL = 4, ThighR = 5, ShinL = 6, ShinR = 7;
    private const int ArmL = 8, ArmR = 9, ArmRSide = 10, ForearmL = 11, ForearmR = 12, SwordTilt = 13;

    private readonly float[] _pose = new float[JointCount];
    private readonly float[] _target = new float[JointCount];
    private readonly Node3D _hips = new();
    private readonly Node3D _torso = new();
    private readonly Node3D _head = new();
    private readonly Node3D _thighL = new(), _thighR = new(), _shinL = new(), _shinR = new();
    private readonly Node3D _armL = new(), _armR = new(), _forearmL = new(), _forearmR = new();
    private readonly Node3D _sword = new();
    private readonly MeshInstance3D _slash;
    private float _phase;

    public Node3D Root { get; } = new();

    public bool SwordVisible
    {
        get => _sword.Visible;
        set => _sword.Visible = value;
    }

    public PlayerRig(Color tunic)
    {
        var skin = new Color("e8b796");
        var trousers = new Color("5a6988");
        var leather = new Color("733e39");

        Root.AddChild(_hips);
        _hips.Position = new Vector3(0, 0.92f, 0);
        _hips.AddChild(Part(new BoxMesh { Size = new Vector3(0.44f, 0.2f, 0.3f) }, trousers, Vector3.Zero));

        _hips.AddChild(_torso);
        _torso.Position = new Vector3(0, 0.1f, 0);
        _torso.AddChild(Part(new CylinderMesh { TopRadius = 0.27f, BottomRadius = 0.22f, Height = 0.52f, RadialSegments = 8 }, tunic, new Vector3(0, 0.26f, 0)));
        _torso.AddChild(Part(new BoxMesh { Size = new Vector3(0.46f, 0.07f, 0.34f) }, leather, new Vector3(0, 0.03f, 0)));
        _torso.AddChild(Part(new BoxMesh { Size = new Vector3(0.42f, 0.46f, 0.2f) }, leather, new Vector3(0, 0.3f, 0.26f)));

        _torso.AddChild(_head);
        _head.Position = new Vector3(0, 0.58f, 0);
        _head.AddChild(Part(new SphereMesh { Radius = 0.21f, Height = 0.42f, RadialSegments = 10, Rings = 5 }, skin, new Vector3(0, 0.14f, 0)));
        _head.AddChild(Part(new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.46f, Height = 0.05f, RadialSegments = 10 }, new Color("e4a672"), new Vector3(0, 0.26f, 0)));
        _head.AddChild(Part(new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.23f, Height = 0.24f, RadialSegments = 8 }, new Color("e4a672"), new Vector3(0, 0.39f, 0)));
        _head.AddChild(Part(new BoxMesh { Size = new Vector3(0.05f, 0.07f, 0.04f) }, new Color("181425"), new Vector3(-0.08f, 0.15f, -0.2f)));
        _head.AddChild(Part(new BoxMesh { Size = new Vector3(0.05f, 0.07f, 0.04f) }, new Color("181425"), new Vector3(0.08f, 0.15f, -0.2f)));

        BuildLeg(_thighL, _shinL, -0.12f, trousers, leather);
        BuildLeg(_thighR, _shinR, 0.12f, trousers, leather);
        BuildArm(_armL, _forearmL, -0.32f, tunic, skin);
        BuildArm(_armR, _forearmR, 0.32f, tunic, skin);

        _forearmR.AddChild(_sword);
        _sword.Position = new Vector3(0, -0.3f, -0.04f);
        _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.06f, 0.05f, 0.2f) }, new Color("733e39"), Vector3.Zero));
        _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.24f, 0.05f, 0.07f) }, new Color("3e2731"), new Vector3(0, 0, -0.12f)));
        _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.1f, 0.04f, 0.9f) }, new Color("c28569"), new Vector3(0, 0, -0.58f)));
        _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.05f, 0.05f, 0.9f) }, new Color("e4a672"), new Vector3(-0.025f, 0.005f, -0.58f)));

        _slash = BuildSlash();
        Root.AddChild(_slash);
    }

    /// <summary>Moves the rig towards the pose for this state. Call it once a frame.</summary>
    public void Animate(in MotionState state, float delta)
    {
        System.Array.Clear(_target);
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

        var weight = 1f - Mathf.Exp(-blend * delta);
        for (var i = 0; i < JointCount; i++)
        {
            _pose[i] = Mathf.Lerp(_pose[i], _target[i], weight);
        }

        Apply();
    }

    private void IdlePose()
    {
        var breath = Mathf.Sin(_phase);
        _target[Bob] = breath * 0.012f;
        _target[ArmL] = 0.05f;
        _target[ArmR] = 0.25f;
        _target[ForearmR] = -0.55f;
        _target[ForearmL] = -0.15f;
        _target[SwordTilt] = 0.5f;
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
        _target[ArmR] = swing * 0.25f + 0.25f;
        _target[ForearmL] = -0.3f;
        _target[ForearmR] = -0.6f;
        _target[SwordTilt] = 0.5f;
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
        _target[ArmR] = swing * 0.4f + 0.9f;
        _target[ForearmL] = -1.2f;
        _target[ForearmR] = -0.4f;
        _target[SwordTilt] = 1.4f;
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
        _target[ArmL] = 1.2f;
        _target[ArmR] = 1.3f;
        _target[ForearmR] = -0.2f;
        _target[SwordTilt] = 1.5f;
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
        _target[ArmR] = rising ? 0.7f : 0.4f;
        _target[ArmRSide] = 0.35f;
        _target[ForearmL] = -0.5f;
        _target[ForearmR] = -0.6f;
        _target[SwordTilt] = 0.5f;
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
        _target[ArmL] = swung ? -0.6f : 0.45f;
        _target[ForearmL] = -0.6f;
        _target[ThighL] = heavy ? -0.6f : -0.3f;
        _target[ThighR] = heavy ? 0.5f : 0.25f;
        _target[ShinL] = 0.3f;
        _target[ShinR] = 0.5f;
        _target[Bob] = heavy && swung ? -0.14f : -0.05f;

        var showSlash = tick >= attack.HitAt - 3 && tick <= attack.HitAt + 4;
        _slash.Visible = showSlash;
        if (showSlash)
        {
            var progress = (tick - attack.HitAt + 3) / 7f;
            _slash.Rotation = new Vector3(heavy ? 0.5f : 0f, 1.1f - progress * 2.2f, heavy ? 0.9f : 0.15f);
            _slash.Scale = Vector3.One * (heavy ? 1.3f : 1f);
        }
    }

    private void Apply()
    {
        _hips.Position = new Vector3(0, 0.92f + _pose[Bob], 0);
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
        _sword.Rotation = new Vector3(_pose[SwordTilt], 0, 0);
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

    private static MeshInstance3D Part(Mesh mesh, Color color, Vector3 position) => new()
    {
        Mesh = mesh,
        Position = position,
        MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 1 },
    };

    /// <summary>A flat crescent that shows the path of the blade for a few frames.</summary>
    private static MeshInstance3D BuildSlash()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        const int steps = 10;
        for (var i = 0; i < steps; i++)
        {
            var a = Mathf.Lerp(-1.1f, 1.1f, i / (float)steps);
            var b = Mathf.Lerp(-1.1f, 1.1f, (i + 1) / (float)steps);
            var inner = 1.1f + Mathf.Sin(i / (float)steps * Mathf.Pi) * 0.5f;
            var innerNext = 1.1f + Mathf.Sin((i + 1) / (float)steps * Mathf.Pi) * 0.5f;
            var p0 = new Vector3(Mathf.Sin(a) * 2.2f, 0, -Mathf.Cos(a) * 2.2f);
            var p1 = new Vector3(Mathf.Sin(b) * 2.2f, 0, -Mathf.Cos(b) * 2.2f);
            var q0 = new Vector3(Mathf.Sin(a) * inner, 0, -Mathf.Cos(a) * inner);
            var q1 = new Vector3(Mathf.Sin(b) * innerNext, 0, -Mathf.Cos(b) * innerNext);
            foreach (var vertex in new[] { p0, p1, q0, q0, p1, q1 })
            {
                surface.AddVertex(vertex);
            }
        }

        return new MeshInstance3D
        {
            Mesh = surface.Commit(),
            Position = new Vector3(0, 1.15f, 0),
            Visible = false,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color("fee761"),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
        };
    }
}
