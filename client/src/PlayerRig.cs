using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>A move of a weapon other than the player's sword.</summary>
public enum WeaponMove
{
    None,
    BowDraw,
    BowRelease,
    StabWindup,
    Stab,
    ClubRaise,
    ClubSmash,
    ClubSpin,
    StaffRaise,
    StaffThrust,
    StaffPlant,
}

/// <summary>What a character is doing this frame. The rig turns it into a pose.</summary>
public readonly record struct MotionState(
    float Speed, bool Grounded, float VerticalSpeed, bool Dashing, AttackKind Attack, int AttackTick,
    WeaponMove Move = WeaponMove.None);

/// <summary>How a rig looks. Kind picks the body: player, skirmisher, brute, archer, sharpshooter or shaman.</summary>
public sealed record RigLook(string Kind, Color Skin, float Scale, string Weapon)
{
    public static readonly RigLook Player = new("player", new Color("e8b796"), 1f, "sword");

    public bool Goblin => Kind != "player";
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
    private RigLook _look = RigLook.Player;
    private float _hipHeight = 0.92f;
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
        _look = look;
        _hipHeight = look.Goblin ? 0.6f : 0.92f;
        Root.AddChild(_hips);
        _hips.Position = new Vector3(0, _hipHeight, 0);
        _hips.AddChild(_torso);
        _torso.Position = new Vector3(0, 0.1f, 0);
        _torso.AddChild(_head);
        _head.Position = new Vector3(0, look.Goblin ? 0.5f : 0.58f, look.Goblin ? -0.06f : 0f);
        if (look.Goblin)
        {
            BuildGoblin(tunic, skin);
        }
        else
        {
            BuildHuman(tunic, skin);
        }

        // A bow is held in the left hand, every other weapon in the right.
        (look.Weapon == "bow" ? _forearmL : _forearmR).AddChild(_sword);
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

    private void BuildHuman(Color tunic, Color skin)
    {
        var trousers = new Color("5a6988");
        var leather = new Color("733e39");
        var dark = new Color("3e2731");
        var straw = new Color("e4a672");
        _hips.AddChild(Part(new BoxMesh { Size = new Vector3(0.44f, 0.2f, 0.3f) }, trousers, Vector3.Zero));

        // A tunic that widens into a skirt, a belt with a buckle and pouch, a scarf and a pack with a bedroll.
        _torso.AddChild(Part(new CylinderMesh { TopRadius = 0.26f, BottomRadius = 0.21f, Height = 0.5f, RadialSegments = 8 }, tunic, new Vector3(0, 0.27f, 0)));
        _torso.AddChild(Part(new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.3f, Height = 0.26f, RadialSegments = 8 }, tunic.Darkened(0.15f), new Vector3(0, -0.1f, 0)));
        _torso.AddChild(Part(new BoxMesh { Size = new Vector3(0.48f, 0.08f, 0.36f) }, leather, new Vector3(0, 0.05f, 0)));
        _torso.AddChild(Part(new BoxMesh { Size = new Vector3(0.1f, 0.09f, 0.04f) }, new Color("feae34"), new Vector3(0, 0.05f, -0.19f)));
        _torso.AddChild(Part(new BoxMesh { Size = new Vector3(0.13f, 0.16f, 0.1f) }, dark, new Vector3(-0.26f, -0.04f, -0.05f)));
        _torso.AddChild(Part(new CylinderMesh { TopRadius = 0.19f, BottomRadius = 0.25f, Height = 0.13f, RadialSegments = 8 }, new Color("a22633"), new Vector3(0, 0.54f, 0)));
        var strap = Part(new BoxMesh { Size = new Vector3(0.07f, 0.62f, 0.44f) }, leather, new Vector3(0, 0.28f, 0));
        strap.Rotation = new Vector3(0, 0, 0.5f);
        _torso.AddChild(strap);
        _torso.AddChild(Part(new BoxMesh { Size = new Vector3(0.42f, 0.46f, 0.2f) }, leather, new Vector3(0, 0.3f, 0.26f)));
        var bedroll = Part(new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 0.5f, RadialSegments = 7 }, new Color("c28569"), new Vector3(0, 0.58f, 0.27f));
        bedroll.Rotation = new Vector3(0, 0, Mathf.Pi / 2f);
        _torso.AddChild(bedroll);

        _head.AddChild(Part(new SphereMesh { Radius = 0.21f, Height = 0.42f, RadialSegments = 10, Rings = 5 }, skin, new Vector3(0, 0.14f, 0)));
        var hair = Part(new SphereMesh { Radius = 0.22f, Height = 0.44f, RadialSegments = 8, Rings = 4 }, new Color("733e39"), new Vector3(0, 0.16f, 0.05f));
        _head.AddChild(hair);
        _head.AddChild(Part(new BoxMesh { Size = new Vector3(0.06f, 0.09f, 0.08f) }, skin.Darkened(0.1f), new Vector3(0, 0.12f, -0.21f)));
        foreach (var x in new[] { -0.09f, 0.09f })
        {
            _head.AddChild(Part(new BoxMesh { Size = new Vector3(0.05f, 0.07f, 0.03f) }, new Color("181425"), new Vector3(x, 0.17f, -0.2f)));
            _head.AddChild(Part(new BoxMesh { Size = new Vector3(0.09f, 0.025f, 0.03f) }, dark, new Vector3(x, 0.22f, -0.2f)));
        }

        _head.AddChild(Part(new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.47f, Height = 0.05f, RadialSegments = 12 }, straw, new Vector3(0, 0.27f, 0)));
        _head.AddChild(Part(new CylinderMesh { TopRadius = 0.13f, BottomRadius = 0.23f, Height = 0.24f, RadialSegments = 10 }, straw, new Vector3(0, 0.4f, 0)));
        _head.AddChild(Part(new CylinderMesh { TopRadius = 0.235f, BottomRadius = 0.235f, Height = 0.06f, RadialSegments = 10 }, leather, new Vector3(0, 0.31f, 0)));

        BuildLeg(_thighL, _shinL, -0.12f, trousers, leather, 1f, false);
        BuildLeg(_thighR, _shinR, 0.12f, trousers, leather, 1f, false);
        BuildArm(_armL, _forearmL, -0.32f, tunic, skin, leather);
        BuildArm(_armR, _forearmR, 0.32f, tunic, skin, leather);
        foreach (var x in new[] { -0.32f, 0.32f })
        {
            _torso.AddChild(Part(new SphereMesh { Radius = 0.13f, Height = 0.2f, RadialSegments = 8, Rings = 4 }, leather, new Vector3(x, 0.5f, 0)));
        }
    }

    /// <summary>Small and hunched, with a huge head, ears and nose, ragged clothes and clawed feet.</summary>
    private void BuildGoblin(Color cloth, Color skin)
    {
        var leather = new Color("733e39");
        var dark = new Color("3e2731");
        var bone = new Color("ead4aa");
        var shaman = _look.Kind == "shaman";
        var brute = _look.Kind == "brute";

        _hips.AddChild(Part(new BoxMesh { Size = new Vector3(0.42f, 0.18f, 0.3f) }, dark, Vector3.Zero));

        // A round belly, ragged cloth hanging from the belt and crossed straps.
        var width = brute ? 0.36f : 0.27f;
        var belly = Part(new SphereMesh { Radius = width, Height = width * 2f, RadialSegments = 10, Rings = 5 }, brute ? skin : cloth, new Vector3(0, 0.22f, 0));
        belly.Scale = new Vector3(1f, 1.05f, 0.9f);
        _torso.AddChild(belly);
        _torso.AddChild(Part(new BoxMesh { Size = new Vector3(width * 2.05f, 0.08f, width * 1.8f) }, leather, new Vector3(0, 0.03f, 0)));
        _torso.AddChild(Part(new BoxMesh { Size = new Vector3(0.1f, 0.09f, 0.04f) }, new Color("8b9bb4"), new Vector3(0, 0.03f, -width * 0.93f)));
        var hem = shaman ? 0.42f : 0.26f;
        for (var i = 0; i < 8; i++)
        {
            var angle = i / 8f * Mathf.Tau;
            var rag = Part(new PrismMesh { Size = new Vector3(0.17f, hem + (i % 3) * 0.07f, 0.05f) }, cloth.Darkened(0.12f * (i % 2)),
                new Vector3(Mathf.Sin(angle) * width * 0.92f, -hem / 2f, Mathf.Cos(angle) * width * 0.86f));
            rag.Rotation = new Vector3(Mathf.Pi, angle, 0);
            _torso.AddChild(rag);
        }

        var strap = Part(new BoxMesh { Size = new Vector3(0.06f, 0.58f, width * 2.05f) }, leather, new Vector3(0, 0.25f, 0));
        strap.Rotation = new Vector3(0, 0, 0.55f);
        _torso.AddChild(strap);

        if (brute)
        {
            foreach (var x in new[] { -0.4f, 0.4f })
            {
                _torso.AddChild(Part(new SphereMesh { Radius = 0.2f, Height = 0.36f, RadialSegments = 8, Rings = 4 }, new Color("5a4a3a"), new Vector3(x, 0.44f, 0)));
            }
        }
        else if (_look.Kind is "archer" or "sharpshooter")
        {
            var quiver = Part(new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.07f, Height = 0.56f, RadialSegments = 7 }, leather, new Vector3(0.14f, 0.34f, 0.26f));
            quiver.Rotation = new Vector3(0, 0, -0.35f);
            _torso.AddChild(quiver);
            foreach (var x in new[] { 0.2f, 0.28f })
            {
                _torso.AddChild(Part(new BoxMesh { Size = new Vector3(0.03f, 0.22f, 0.03f) }, new Color("c0cbdc"), new Vector3(x, 0.68f, 0.26f)));
            }
        }

        if (shaman)
        {
            // A rope belt and a necklace of little skulls.
            for (var i = 0; i < 3; i++)
            {
                _torso.AddChild(Part(new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 6, Rings = 3 }, bone, new Vector3((i - 1) * 0.12f, 0.36f - (i == 1 ? 0.04f : 0f), -0.26f)));
            }
        }

        BuildGoblinHead(skin, cloth, dark, bone);
        var legs = brute ? skin : dark;
        BuildLeg(_thighL, _shinL, -0.13f, legs, skin, 0.62f, true);
        BuildLeg(_thighR, _shinR, 0.13f, legs, skin, 0.62f, true);
        BuildArm(_armL, _forearmL, -width - 0.05f, skin, skin, leather);
        BuildArm(_armR, _forearmR, width + 0.05f, skin, skin, leather);
    }

    private void BuildGoblinHead(Color skin, Color cloth, Color dark, Color bone)
    {
        var head = new Node3D { Position = new Vector3(0, 0.14f, 0), Scale = Vector3.One * 1.5f };
        _head.AddChild(head);
        head.AddChild(Part(new SphereMesh { Radius = 0.2f, Height = 0.38f, RadialSegments = 10, Rings = 5 }, skin, Vector3.Zero));
        head.AddChild(Part(new SphereMesh { Radius = 0.14f, Height = 0.2f, RadialSegments = 8, Rings = 4 }, skin.Darkened(0.08f), new Vector3(0, -0.12f, -0.07f)));

        // A long hooked nose, a heavy brow, yellow eyes and two teeth.
        var nose = Part(new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0f, Height = 0.2f, RadialSegments = 6 }, skin.Darkened(0.14f), new Vector3(0, -0.01f, -0.24f));
        nose.Rotation = new Vector3(1.95f, 0, 0);
        head.AddChild(nose);
        head.AddChild(Part(new BoxMesh { Size = new Vector3(0.3f, 0.045f, 0.06f) }, skin.Darkened(0.28f), new Vector3(0, 0.075f, -0.17f)));
        foreach (var side in new[] { -1f, 1f })
        {
            head.AddChild(Part(new BoxMesh { Size = new Vector3(0.07f, 0.045f, 0.03f) }, new Color("fee761"), new Vector3(side * 0.085f, 0.035f, -0.185f)));
            head.AddChild(Part(new BoxMesh { Size = new Vector3(0.03f, 0.04f, 0.02f) }, new Color("181425"), new Vector3(side * 0.08f, 0.035f, -0.2f)));
            head.AddChild(Part(new BoxMesh { Size = new Vector3(0.035f, 0.05f, 0.03f) }, bone, new Vector3(side * 0.05f, -0.15f, -0.17f)));

            // Big ears that sweep out and slightly back.
            var ear = Part(new PrismMesh { Size = new Vector3(0.2f, 0.5f, 0.05f) }, skin, new Vector3(side * 0.34f, 0.05f, 0.03f));
            ear.Rotation = new Vector3(0, side * 0.3f, -side * 1.32f);
            head.AddChild(ear);
            var inner = Part(new PrismMesh { Size = new Vector3(0.1f, 0.34f, 0.02f) }, new Color("e8b796"), new Vector3(side * 0.33f, 0.05f, 0f));
            inner.Rotation = ear.Rotation;
            head.AddChild(inner);
        }

        switch (_look.Kind)
        {
            case "skirmisher":
            case "archer":
                var hood = Part(new SphereMesh { Radius = 0.23f, Height = 0.42f, RadialSegments = 10, Rings = 5 }, cloth, new Vector3(0, 0.03f, 0.05f));
                head.AddChild(hood);
                var peak = Part(new CylinderMesh { TopRadius = 0f, BottomRadius = 0.18f, Height = 0.2f, RadialSegments = 7 }, cloth, new Vector3(0, 0.22f, 0.07f));
                head.AddChild(peak);
                head.AddChild(Part(new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.3f, Height = 0.12f, RadialSegments = 8 }, cloth.Darkened(0.15f), new Vector3(0, -0.22f, 0.02f)));
                break;
            case "sharpshooter":
                head.AddChild(Part(new CylinderMesh { TopRadius = 0.14f, BottomRadius = 0.23f, Height = 0.13f, RadialSegments = 9 }, dark, new Vector3(0, 0.17f, 0.02f)));
                var plume = Part(new PrismMesh { Size = new Vector3(0.06f, 0.4f, 0.04f) }, new Color("e43b44"), new Vector3(0.14f, 0.32f, 0.08f));
                plume.Rotation = new Vector3(0.3f, 0, -0.3f);
                head.AddChild(plume);
                break;
            case "brute":
                head.AddChild(Part(new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.09f, Height = 0.16f, RadialSegments = 6 }, dark, new Vector3(0, 0.25f, 0.03f)));
                foreach (var side in new[] { -1f, 1f })
                {
                    head.AddChild(Part(new BoxMesh { Size = new Vector3(0.05f, 0.09f, 0.04f) }, bone, new Vector3(side * 0.08f, -0.11f, -0.18f)));
                }

                break;
            case "shaman":
                // An animal skull worn as a mask, with feathers rising behind it.
                var skull = Part(new SphereMesh { Radius = 0.22f, Height = 0.32f, RadialSegments = 10, Rings = 5 }, bone, new Vector3(0, 0.13f, -0.02f));
                head.AddChild(skull);
                foreach (var side in new[] { -1f, 1f })
                {
                    head.AddChild(Part(new BoxMesh { Size = new Vector3(0.07f, 0.07f, 0.03f) }, new Color("181425"), new Vector3(side * 0.09f, 0.15f, -0.21f)));
                }

                var horn = Part(new CylinderMesh { TopRadius = 0f, BottomRadius = 0.04f, Height = 0.16f, RadialSegments = 5 }, bone, new Vector3(0, 0.33f, -0.06f));
                head.AddChild(horn);
                for (var i = 0; i < 5; i++)
                {
                    var tilt = (i - 2) * 0.32f;
                    var feather = Part(new PrismMesh { Size = new Vector3(0.09f, 0.44f, 0.03f) }, new Color(i % 2 == 0 ? "8b9bb4" : "c28569"),
                        new Vector3(tilt * 0.34f, 0.42f, 0.08f));
                    feather.Rotation = new Vector3(0.2f, 0, -tilt);
                    head.AddChild(feather);
                }

                break;
        }
    }

    private void BuildWeapon(string weapon)
    {
        var wood = new Color("733e39");
        var dark = new Color("3e2731");
        switch (weapon)
        {
            case "dagger":
                _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.06f, 0.06f, 0.16f) }, dark, Vector3.Zero));
                _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.16f, 0.04f, 0.05f) }, new Color("8b9bb4"), new Vector3(0, 0, -0.1f)));
                _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.1f, 0.03f, 0.3f) }, new Color("c0cbdc"), new Vector3(0, 0, -0.26f)));
                var tip = Part(new BoxMesh { Size = new Vector3(0.08f, 0.03f, 0.22f) }, new Color("ffffff"), new Vector3(0.03f, 0, -0.48f));
                tip.Rotation = new Vector3(0, 0.45f, 0);
                _sword.AddChild(tip);
                break;
            case "club":
                var shaft = Part(new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.21f, Height = 1.35f, RadialSegments = 7 }, wood, new Vector3(0, 0, -0.62f));
                shaft.Rotation = new Vector3(-Mathf.Pi / 2f, 0, 0);
                _sword.AddChild(shaft);
                foreach (var z in new[] { -0.75f, -1f, -1.2f })
                {
                    _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.44f, 0.06f, 0.06f) }, new Color("c0cbdc"), new Vector3(0, 0, z)));
                    _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.06f, 0.44f, 0.06f) }, new Color("c0cbdc"), new Vector3(0, 0, z - 0.1f)));
                }

                break;
            case "bow":
                // Limbs bend back from the grip, with the string between the tips. The bow stands upright.
                _sword.Position = new Vector3(0, -0.32f, 0);
                _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.06f, 0.08f, 0.2f) }, dark, Vector3.Zero));
                foreach (var side in new[] { -1f, 1f })
                {
                    for (var i = 0; i < 4; i++)
                    {
                        var z = side * (0.15f + i * 0.14f);
                        var limb = Part(new BoxMesh { Size = new Vector3(0.05f, 0.055f, 0.17f) }, wood, new Vector3(0, 0.02f + i * i * 0.016f, z));
                        limb.Rotation = new Vector3(side * (-0.1f - i * 0.16f), 0, 0);
                        _sword.AddChild(limb);
                    }
                }

                _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.015f, 0.015f, 1.14f) }, new Color("ead4aa"), new Vector3(0, 0.17f, 0)));
                break;
            case "staff":
                var pole = Part(new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.055f, Height = 1.9f, RadialSegments = 6 }, wood, new Vector3(0, 0, -0.45f));
                pole.Rotation = new Vector3(-Mathf.Pi / 2f, 0, 0);
                _sword.AddChild(pole);
                var bone = new Color("ead4aa");
                _sword.AddChild(Part(new SphereMesh { Radius = 0.17f, Height = 0.3f, RadialSegments = 8, Rings = 4 }, bone, new Vector3(0, 0, -1.48f)));
                _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.14f, 0.1f, 0.05f) }, bone, new Vector3(0, -0.14f, -1.38f)));
                foreach (var x in new[] { -0.06f, 0.06f })
                {
                    _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.05f, 0.06f, 0.05f) }, new Color("f77622"), new Vector3(x, -0.15f, -1.5f)));
                }

                _sword.AddChild(Part(new SphereMesh { Radius = 0.07f, Height = 0.13f, RadialSegments = 6, Rings = 3 }, bone, new Vector3(0.08f, 0, -1.2f)));
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
        if (_look.Weapon != "sword")
        {
            CarryWeapon();
        }

        if (state.Move != WeaponMove.None)
        {
            WeaponPose(state.Move);
            blend = 26f;
        }

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

    /// <summary>How each weapon rests in the hand while walking.</summary>
    private void CarryWeapon()
    {
        switch (_look.Weapon)
        {
            case "bow":
                _target[ArmL] = 0.5f;
                _target[ForearmL] = -0.2f;
                _target[SwordTilt] = 0f;
                _target[SwordYaw] = 0f;
                _target[ArmR] = 0.1f;
                _target[ArmRSide] = 0.2f;
                _target[ForearmR] = -0.4f;
                break;
            case "club":
                _target[ArmR] = 0.9f;
                _target[ArmRSide] = 0.3f;
                _target[ForearmR] = -1.7f;
                _target[SwordTilt] = 0.1f;
                _target[SwordYaw] = -0.2f;
                break;
            case "staff":
                _target[ArmR] = 0.6f;
                _target[ArmRSide] = 0.2f;
                _target[ForearmR] = -1f;
                _target[SwordTilt] = 0f;
                _target[SwordYaw] = 0f;
                break;
            default:
                _target[ArmR] = 0.2f;
                _target[ArmRSide] = 0.4f;
                _target[ForearmR] = -0.6f;
                _target[SwordTilt] = -0.5f;
                _target[SwordYaw] = -0.3f;
                break;
        }
    }

    private void WeaponPose(WeaponMove move)
    {
        const float alongArm = -1.5708f;
        switch (move)
        {
            case WeaponMove.BowDraw:
                _target[ArmL] = 1.55f;
                _target[ForearmL] = 0f;
                _target[ArmR] = 1.5f;
                _target[ArmRSide] = 0f;
                _target[ForearmR] = -2.1f;
                _target[Twist] = 0.75f;
                _target[Head] = 0f;
                _target[Lean] = 0.02f;
                _target[ThighL] = -0.35f;
                _target[ThighR] = 0.3f;
                break;
            case WeaponMove.BowRelease:
                _target[ArmL] = 1.5f;
                _target[ForearmL] = 0f;
                _target[ArmR] = 0.9f;
                _target[ArmRSide] = 0.5f;
                _target[ForearmR] = -0.6f;
                _target[Twist] = 0.75f;
                _target[Lean] = -0.04f;
                break;
            case WeaponMove.StabWindup:
                _target[ArmR] = -0.25f;
                _target[ArmRSide] = 0.2f;
                _target[ForearmR] = -1.9f;
                _target[SwordTilt] = alongArm;
                _target[SwordYaw] = 0f;
                _target[Twist] = 0.5f;
                _target[Lean] = 0.1f;
                _target[ArmL] = 0.7f;
                _target[ForearmL] = -0.7f;
                break;
            case WeaponMove.Stab:
                _target[ArmR] = 1.5f;
                _target[ArmRSide] = 0f;
                _target[ForearmR] = -0.05f;
                _target[SwordTilt] = alongArm;
                _target[SwordYaw] = 0f;
                _target[Twist] = -0.55f;
                _target[Lean] = 0.4f;
                _target[ArmL] = -0.5f;
                _target[ThighL] = -0.6f;
                _target[ThighR] = 0.5f;
                _target[ShinR] = 0.4f;
                break;
            case WeaponMove.ClubRaise:
                _target[ArmR] = 3f;
                _target[ArmL] = 2.8f;
                _target[ArmRSide] = 0f;
                _target[ForearmR] = -0.7f;
                _target[ForearmL] = -0.9f;
                _target[SwordTilt] = 0.3f;
                _target[SwordYaw] = 0f;
                _target[Lean] = -0.28f;
                break;
            case WeaponMove.ClubSmash:
                _target[ArmR] = 1.15f;
                _target[ArmL] = 1.1f;
                _target[ArmRSide] = 0f;
                _target[ForearmR] = -0.1f;
                _target[ForearmL] = -0.15f;
                _target[SwordTilt] = -0.6f;
                _target[SwordYaw] = 0f;
                _target[Lean] = 0.7f;
                _target[Bob] = -0.12f;
                _target[ThighL] = -0.6f;
                _target[ThighR] = 0.5f;
                break;
            case WeaponMove.ClubSpin:
                _target[ArmR] = 1.5f;
                _target[ArmRSide] = 1.35f;
                _target[ArmL] = 0.9f;
                _target[ForearmR] = -0.05f;
                _target[SwordTilt] = alongArm;
                _target[SwordYaw] = 0f;
                _target[Lean] = 0.1f;
                break;
            case WeaponMove.StaffRaise:
                _target[ArmR] = 2.5f;
                _target[ArmRSide] = 0.1f;
                _target[ForearmR] = -0.3f;
                _target[SwordTilt] = 0.3f;
                _target[SwordYaw] = 0f;
                _target[ArmL] = 1f;
                _target[ForearmL] = -1f;
                _target[Lean] = -0.15f;
                break;
            case WeaponMove.StaffThrust:
                _target[ArmR] = 1.7f;
                _target[ArmRSide] = 0f;
                _target[ForearmR] = -0.1f;
                _target[SwordTilt] = 0f;
                _target[SwordYaw] = 0f;
                _target[ArmL] = 1.5f;
                _target[ForearmL] = -0.1f;
                _target[Lean] = 0.3f;
                break;
            case WeaponMove.StaffPlant:
                // Both hands drive the staff straight down into the ground.
                _target[ArmR] = 1.1f;
                _target[ArmL] = 1.1f;
                _target[ArmRSide] = -0.15f;
                _target[ForearmR] = -0.6f;
                _target[ForearmL] = -0.6f;
                _target[SwordTilt] = -3.27f;
                _target[SwordYaw] = 0f;
                _target[Lean] = 0.5f;
                _target[Bob] = -0.1f;
                _target[ThighL] = -0.5f;
                _target[ThighR] = 0.4f;
                _target[ShinL] = 0.7f;
                _target[ShinR] = 0.5f;
                break;
        }
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
        _hips.Position = new Vector3(shake, _hipHeight + _pose[Bob], 0);

        // Goblins stoop, with the head craned forward.
        var hunch = _look.Goblin ? 0.32f : 0f;
        _torso.Rotation = new Vector3(-_pose[Lean] - hunch, _pose[Twist], 0);
        _head.Rotation = new Vector3(-_pose[Head] + hunch * 0.8f, -_pose[Twist] * 0.6f, 0);
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

    private void BuildLeg(Node3D thigh, Node3D shin, float x, Color cloth, Color boot, float length, bool claws)
    {
        _hips.AddChild(thigh);
        thigh.Position = new Vector3(x, -0.08f, 0);
        thigh.AddChild(Part(new CylinderMesh { TopRadius = 0.11f, BottomRadius = 0.085f, Height = 0.42f * length, RadialSegments = 6 }, cloth, new Vector3(0, -0.21f * length, 0)));
        thigh.AddChild(shin);
        shin.Position = new Vector3(0, -0.42f * length, 0);
        shin.AddChild(Part(new CylinderMesh { TopRadius = 0.085f, BottomRadius = 0.07f, Height = 0.32f * length, RadialSegments = 6 }, cloth, new Vector3(0, -0.16f * length, 0)));
        var foot = -0.32f * length - 0.03f;
        if (!claws)
        {
            shin.AddChild(Part(new CylinderMesh { TopRadius = 0.11f, BottomRadius = 0.09f, Height = 0.1f, RadialSegments = 7 }, boot, new Vector3(0, -0.24f, 0)));
            shin.AddChild(Part(new BoxMesh { Size = new Vector3(0.15f, 0.11f, 0.28f) }, boot, new Vector3(0, foot, -0.06f)));
            return;
        }

        shin.AddChild(Part(new BoxMesh { Size = new Vector3(0.22f, 0.08f, 0.34f) }, boot, new Vector3(0, foot, -0.09f)));
        foreach (var toe in new[] { -0.08f, 0f, 0.08f })
        {
            shin.AddChild(Part(new BoxMesh { Size = new Vector3(0.05f, 0.05f, 0.1f) }, new Color("3e2731"), new Vector3(toe, foot - 0.01f, -0.29f)));
        }
    }

    private void BuildArm(Node3D arm, Node3D forearm, float x, Color cloth, Color skin, Color glove)
    {
        _torso.AddChild(arm);
        arm.Position = new Vector3(x, _look.Goblin ? 0.36f : 0.47f, 0);
        arm.AddChild(Part(new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.07f, Height = 0.32f, RadialSegments = 6 }, cloth, new Vector3(0, -0.16f, 0)));
        arm.AddChild(forearm);
        forearm.Position = new Vector3(0, -0.32f, 0);
        forearm.AddChild(Part(new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.055f, Height = 0.26f, RadialSegments = 6 }, skin, new Vector3(0, -0.13f, 0)));
        forearm.AddChild(Part(new CylinderMesh { TopRadius = 0.085f, BottomRadius = 0.075f, Height = 0.12f, RadialSegments = 6 }, glove, new Vector3(0, -0.18f, 0)));
        forearm.AddChild(Part(new SphereMesh { Radius = _look.Goblin ? 0.095f : 0.075f, Height = _look.Goblin ? 0.19f : 0.15f, RadialSegments = 6, Rings = 3 }, _look.Goblin ? skin : glove, new Vector3(0, -0.29f, 0)));
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
