using System.Collections.Generic;
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

    public bool Goblin => Kind is "skirmisher" or "brute" or "archer" or "sharpshooter" or "shaman";

    /// <summary>Colours that replace the hero's, by Blender material name.</summary>
    public System.Collections.Generic.Dictionary<string, Color> Palette { get; init; } = new();

    /// <summary>The hair model: hero, mira, rhea, bram or tess.</summary>
    public string Hair { get; init; } = "hero";

    public bool Feminine { get; init; }

    public bool Skirt { get; init; }

    public bool Cloak { get; init; } = true;

    /// <summary>Extra Blender parts: "vest", "bow", "armor", "apron", "robe", "beard".</summary>
    public string[] Extras { get; init; } = System.Array.Empty<string>();

    public Color Eyes { get; init; } = new("2f8f62");

    public Color Brows { get; init; } = new("2b3044");

    /// <summary>How the arms rest when the hands are free: relaxed, clasped or behind.</summary>
    public string Rest { get; init; } = "relaxed";

    /// <summary>A permanent stoop, for the old.</summary>
    public float Stoop { get; init; }
}

/// <summary>
/// A jointed figure animated from code. Each state gives a target pose and the rig blends towards it,
/// so every change of state becomes a smooth transition without an authored clip for each pair.
/// </summary>
public sealed class PlayerRig
{
    private const int JointCount = 16;
    private const int Lean = 0, Twist = 1, Bob = 2, Head = 3;
    private const int ThighL = 4, ThighR = 5, ShinL = 6, ShinR = 7;
    private const int ArmL = 8, ArmR = 9, ArmRSide = 10, ForearmL = 11, ForearmR = 12, SwordTilt = 13, SwordYaw = 14, ArmLSide = 15;

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
    private readonly System.Collections.Generic.List<ClothChain> _cloth = new();
    private readonly Node3D _hairRoot = new();
    private readonly Node3D _cape = new();
    private readonly System.Collections.Generic.List<Node3D> _eyes = new();
    private float _blinkClock = 3f;
    private float _clasp;
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
        _hipHeight = look.Goblin ? 0.6f : 0.98f;
        Root.AddChild(_hips);
        _hips.Position = new Vector3(0, _hipHeight, 0);
        _hips.AddChild(_torso);
        _torso.Position = new Vector3(0, 0.1f, 0);
        _torso.AddChild(_head);
        _head.Position = new Vector3(0, look.Goblin ? 0.5f : 0.3f, look.Goblin ? -0.06f : 0f);
        if (look.Goblin)
        {
            BuildGoblin(tunic, skin);
        }
        else
        {
            BuildHuman();
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

    /// <summary>An original anime-style hero: slim, long-legged, with a big head and a dark cloak.</summary>
    private void BuildHuman()
    {
        _hips.AddChild(Model("anime_pelvis"));
        if (_look.Skirt)
        {
            _hips.AddChild(Model("anime_skirt"));
        }

        _torso.AddChild(Model(_look.Feminine ? "anime_torso_f" : "anime_torso"));
        foreach (var extra in _look.Extras)
        {
            var holder = extra is "beard" or "headband" ? _hairRoot : extra is "robe" or "apron" ? _hips : _torso;
            var part = Model($"anime_{extra}");
            if (extra == "headband")
            {
                part.Position = new Vector3(0, -0.3f, 0);
            }

            holder.AddChild(part);
        }

        var head = Model("anime_head");
        head.Position = new Vector3(0, 0.3f, 0);
        _head.AddChild(head);
        _head.AddChild(_hairRoot);
        _hairRoot.Position = head.Position;
        BuildHair();
        BuildEyes(head);

        if (_look.Cloak)
        {
            _cape.Position = new Vector3(0, 0.44f, 0.02f);
            var cloak = new ClothChain(new ClothShape(0.86f, 0.22f, 0.1f, 0.21f, 3.2f, 9.8f, 0.004f, 0.2f, 0.13f), _cape, Root);
            _cape.AddChild(Cloth("anime_cloak", cloak, 0.02f, 0.86f));
            _torso.AddChild(_cape);
            _cloth.Add(cloak);
        }

        foreach (var (thigh, shin, x) in new[] { (_thighL, _shinL, -0.085f), (_thighR, _shinR, 0.085f) })
        {
            _hips.AddChild(thigh);
            thigh.Position = new Vector3(x, -0.06f, 0);
            thigh.AddChild(Model("anime_thigh"));
            thigh.AddChild(shin);
            shin.Position = new Vector3(0, -0.47f, 0);
            shin.AddChild(Model("anime_shin"));
        }

        foreach (var (arm, forearm, x) in new[] { (_armL, _forearmL, -0.19f), (_armR, _forearmR, 0.19f) })
        {
            _torso.AddChild(arm);
            arm.Position = new Vector3(x, 0.385f, 0);
            arm.AddChild(Model("anime_upper_arm"));
            arm.AddChild(forearm);
            forearm.Position = new Vector3(0, -0.3f, 0);
            forearm.AddChild(Model("anime_forearm"));
        }
    }

    /// <summary>Each hair style: a cap that moves a little, and for some a long part that swings freely.</summary>
    private void BuildHair()
    {
        var cap = new ClothChain(new ClothShape(0.3f, 0.15f, 0.13f, 0.17f, 2.6f, 6f, 0.03f, 0.13f, 0.13f, 1f), _hairRoot, Root);
        _hairRoot.AddChild(Cloth($"anime_hair_{_look.Hair}", cap, 0.12f, 0.3f));
        _cloth.Add(cap);
        var free = _look.Hair switch
        {
            "mira" => ("anime_hair_long", new ClothShape(0.58f, 0.14f, 0.13f, 0.17f, 2.4f, 9.8f, 0.006f, 0.15f, 0.11f), 0.08f),
            "rhea" => ("anime_ponytail", new ClothShape(0.5f, 0.05f, 0.17f, 0.2f, 2.6f, 9.8f, 0.004f, 0.13f, 0.13f), 0.13f),
            "tess" => ("anime_braids", new ClothShape(0.44f, 0.16f, 0.02f, 0.04f, 2f, 9.8f, 0.01f, 0.13f, 0.09f), -0.02f),
            "sora" => ("anime_hair_wavy", new ClothShape(0.62f, 0.15f, 0.13f, 0.17f, 2.3f, 9.8f, 0.006f, 0.15f, 0.11f), 0.08f),
            "elda" => ("anime_bun", new ClothShape(0.12f, 0.05f, 0.12f, 0.13f, 3f, 9.8f, 0.08f, 0.12f, 0.12f), 0.2f),
            _ => default,
        };
        if (free.Item1 is not null)
        {
            var chain = new ClothChain(free.Item2, _hairRoot, Root);
            _hairRoot.AddChild(Cloth(free.Item1, chain, free.Item3, free.Item2.Length));
            _cloth.Add(chain);
        }
    }

    private Node3D Cloth(string name, ClothChain chain, float top, float length)
    {
        return Model(name, chain.Materials, top, length);
    }

    /// <summary>Large eyes with a dark lash, a coloured iris and a highlight, and a small mouth and nose.</summary>
    private void BuildEyes(Node3D head)
    {
        var skinShade = new Color("e0a890");
        foreach (var side in new[] { -1f, 1f })
        {
            var eye = new Node3D { Position = new Vector3(side * 0.057f, -0.018f, -0.118f) };
            eye.Rotation = new Vector3(0, side * -0.32f, 0);
            eye.AddChild(Flat(new Vector3(0.03f, 0.031f, 0.006f), new Color("f8f4ee"), Vector3.Zero));
            eye.AddChild(Flat(new Vector3(0.024f, 0.03f, 0.006f), _look.Eyes, new Vector3(side * -0.002f, -0.002f, -0.005f)));
            eye.AddChild(Flat(new Vector3(0.024f, 0.012f, 0.0055f), _look.Eyes.Darkened(0.4f), new Vector3(side * -0.002f, 0.014f, -0.0065f)));
            eye.AddChild(Flat(new Vector3(0.012f, 0.017f, 0.005f), new Color("15202c"), new Vector3(side * -0.002f, 0.001f, -0.009f)));
            eye.AddChild(Flat(new Vector3(0.0085f, 0.0085f, 0.004f), Colors.White, new Vector3(side * 0.008f, 0.012f, -0.0125f)));
            eye.AddChild(Flat(new Vector3(0.0045f, 0.0045f, 0.003f), Colors.White, new Vector3(side * -0.009f, -0.014f, -0.0125f)));

            // The lash lifts at the outer end, which reads as awake and friendly.
            var lash = Flat(new Vector3(0.037f, 0.0075f, 0.008f), new Color("1c1a24"), new Vector3(side * 0.002f, 0.031f, -0.006f));
            lash.Rotation = new Vector3(0, 0, side * 0.07f);
            eye.AddChild(lash);
            if (_look.Feminine)
            {
                var wing = Flat(new Vector3(0.012f, 0.004f, 0.006f), new Color("1c1a24"), new Vector3(side * 0.037f, 0.031f, -0.003f));
                wing.Rotation = new Vector3(0, 0, side * 0.6f);
                eye.AddChild(wing);
                head.AddChild(Flat(new Vector3(0.026f, 0.012f, 0.004f), new Color("f4b0a8"), new Vector3(side * 0.07f, -0.062f, -0.106f)));
            }

            head.AddChild(eye);
            _eyes.Add(eye);
            var brow = Flat(new Vector3(0.03f, _look.Feminine ? 0.0035f : 0.0055f, 0.004f), _look.Brows, new Vector3(side * 0.06f, 0.056f, -0.122f));
            brow.Rotation = new Vector3(0, side * -0.3f, side * -0.08f);
            head.AddChild(brow);
        }

        head.AddChild(Flat(new Vector3(0.0065f, 0.004f, 0.005f), skinShade, new Vector3(0, -0.058f, -0.14f)));
        var mouth = Flat(new Vector3(0.017f, 0.0035f, 0.004f), new Color("b05a58"), new Vector3(0, -0.098f, -0.116f));
        head.AddChild(mouth);
        if (_look.Feminine)
        {
            head.AddChild(Flat(new Vector3(0.008f, 0.003f, 0.004f), new Color("d88078"), new Vector3(0, -0.103f, -0.115f)));
        }
    }

    private static MeshInstance3D Flat(Vector3 radius, Color color, Vector3 position)
    {
        var part = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 10, Rings = 6 },
            Position = position,
            Scale = radius,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
        };
        return part;
    }

    /// <summary>Loads a Blender part and gives it the cel shader.</summary>
    private Node3D Model(string name, System.Collections.Generic.List<ShaderMaterial>? cloth = null, float top = 0f, float length = 1f)
    {
        var model = GD.Load<PackedScene>($"res://assets/{name}.glb").Instantiate<Node3D>();
        Toonify(model, cloth, top, length);
        return model;
    }

    private void Toonify(Node node, System.Collections.Generic.List<ShaderMaterial>? cloth, float top, float length)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh is not null)
        {
            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.Mesh.SurfaceGetMaterial(surface) is StandardMaterial3D original)
                {
                    var color = _look.Palette.GetValueOrDefault(original.ResourceName, original.AlbedoColor);
                    var skin = original.ResourceName == "anime_skin";
                    var material = Toon(skin ? _look.Skin : color);
                    if (skin)
                    {
                        // Faces stay almost fully lit, with a warm shadow, as drawn anime faces do.
                        material.SetShaderParameter("shade", _look.Skin * new Color(0.95f, 0.78f, 0.76f));
                        material.SetShaderParameter("threshold", -0.45f);
                        material.SetShaderParameter("rim_strength", 0.08f);
                    }

                    if (original.ResourceName == "anime_hair")
                    {
                        material.SetShaderParameter("shine", 1f);
                    }

                    material.SetShaderParameter("sway_top", top);
                    material.SetShaderParameter("sway_length", length);
                    mesh.SetSurfaceOverrideMaterial(surface, material);
                    cloth?.Add(material);
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            Toonify(child, cloth, top, length);
        }
    }

    /// <summary>The cel material, for other views that build their own figures.</summary>
    public static ShaderMaterial ToonMaterial(Color color) => Toon(color);

    /// <summary>A cel material. The shadow leans towards cool purple, as hand-coloured shading does.</summary>
    private static ShaderMaterial Toon(Color color)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/toon.gdshader") };
        material.SetShaderParameter("base", color);
        material.SetShaderParameter("shade", color.Lerp(new Color("4a3e78"), 0.3f));
        return material;
    }

    /// <summary>Lets hair and cloth hang still, for a character that was just placed.</summary>
    public void ResetCloth()
    {
        foreach (var cloth in _cloth)
        {
            cloth.Reset();
        }
    }

    /// <summary>Moves hair and cloth for this frame and blinks now and then.</summary>
    private void AnimateCloth(in MotionState state, float delta)
    {
        var velocity = Root.GlobalBasis.Orthonormalized() * new Vector3(0, 0, -state.Speed);
        velocity.Y = state.Grounded ? 0f : state.VerticalSpeed;
        foreach (var cloth in _cloth)
        {
            cloth.Step(velocity, delta);
        }

        _blinkClock -= delta;
        if (_blinkClock < -0.12f)
        {
            _blinkClock = (float)GD.RandRange(2.0, 5.0);
        }

        foreach (var eye in _eyes)
        {
            eye.Scale = new Vector3(1f, _blinkClock < 0f ? 0.1f : 1f, 1f);
        }
    }

    private static MeshInstance3D Rounded(Vector3 radius, Color color, Vector3 position)
    {
        var part = Part(new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 14, Rings = 8 }, color, position);
        part.Scale = radius;
        return part;
    }

    /// <summary>Small and hunched, with a huge head, ears and nose, ragged clothes and clawed feet.</summary>
    private void BuildGoblin(Color cloth, Color skin)
    {
        var leather = new Color("733e39");
        var dark = new Color("3e2731");
        var bone = new Color("ead4aa");
        var brute = _look.Kind == "brute";

        _hips.AddChild(Part(new BoxMesh { Size = new Vector3(0.42f, 0.18f, 0.3f) }, dark, Vector3.Zero));
        var width = brute ? 0.36f : 0.27f;
        _torso.AddChild(Model(brute ? "gob_torso_brute" : "gob_torso_lean"));
        if (_look.Kind is "archer" or "sharpshooter")
        {
            var quiver = Part(new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.07f, Height = 0.56f, RadialSegments = 7 }, leather, new Vector3(0.14f, 0.34f, 0.26f));
            quiver.Rotation = new Vector3(0, 0, -0.35f);
            _torso.AddChild(quiver);
            foreach (var x in new[] { 0.2f, 0.28f })
            {
                _torso.AddChild(Part(new BoxMesh { Size = new Vector3(0.03f, 0.22f, 0.03f) }, new Color("c0cbdc"), new Vector3(x, 0.68f, 0.26f)));
            }
        }

        BuildGoblinHead(skin, cloth, dark, bone);
        var legs = brute ? skin : dark;
        BuildLeg(_thighL, _shinL, -0.13f, legs, skin, 0.62f, true);
        BuildLeg(_thighR, _shinR, 0.13f, legs, skin, 0.62f, true);
        BuildArm(_armL, _forearmL, -width - 0.05f, skin, skin, leather);
        BuildArm(_armR, _forearmR, width + 0.05f, skin, skin, leather);
    }

    /// <summary>A goblin head from Blender: big nose, scowling brow, grin and long ears, with the gear of its kind.</summary>
    private void BuildGoblinHead(Color skin, Color cloth, Color dark, Color bone)
    {
        _head.AddChild(Model($"gob_head_{_look.Kind}"));
    }

    private void BuildWeapon(string weapon)
    {
        var wood = new Color("733e39");
        var dark = new Color("3e2731");
        switch (weapon)
        {
            case "none":
                break;
            case "dagger":
                _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.06f, 0.06f, 0.16f) }, dark, Vector3.Zero));
                _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.16f, 0.04f, 0.05f) }, new Color("8b9bb4"), new Vector3(0, 0, -0.1f)));
                _sword.AddChild(Part(new BoxMesh { Size = new Vector3(0.1f, 0.03f, 0.3f) }, new Color("c0cbdc"), new Vector3(0, 0, -0.26f)));
                var tip = Part(new BoxMesh { Size = new Vector3(0.08f, 0.03f, 0.22f) }, new Color("ffffff"), new Vector3(0.03f, 0, -0.48f));
                tip.Rotation = new Vector3(0, 0.45f, 0);
                _sword.AddChild(tip);
                break;
            case "club":
                var shaft = Part(new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.06f, Height = 1.35f, RadialSegments = 7 }, wood, new Vector3(0, 0, -0.62f));
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
        if (_look.Weapon == "none")
        {
            RestArms();
        }
        else if (_look.Weapon != "sword")
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
        if (!_look.Goblin)
        {
            AnimateCloth(state, delta);
        }
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

    /// <summary>Where free hands go: down by the sides, folded in front, or behind the back.</summary>
    private void RestArms()
    {
        _target[Lean] += _look.Stoop;
        _target[Head] -= _look.Stoop * 0.8f;
        switch (_look.Rest)
        {
            case "clasped":
                _target[ArmL] = 0.14f;
                _target[ArmR] = 0.14f;
                _target[ArmLSide] = -0.1f;
                _target[ArmRSide] = -0.1f;
                _target[ForearmL] = -0.8f;
                _target[ForearmR] = -0.8f;
                _clasp = 0.95f;
                break;
            case "behind":
                _target[ArmL] = -0.42f;
                _target[ArmR] = -0.42f;
                _target[ArmLSide] = -0.12f;
                _target[ArmRSide] = -0.12f;
                _target[ForearmL] = -0.75f;
                _target[ForearmR] = -0.75f;
                break;
            default:
                _target[ArmL] = 0.04f;
                _target[ArmR] = 0.04f;
                _target[ArmRSide] = 0f;
                _target[ForearmL] = -0.22f;
                _target[ForearmR] = -0.22f;
                break;
        }
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
        _armL.Rotation = new Vector3(_pose[ArmL], 0, -0.08f - _pose[ArmLSide]);
        _armR.Rotation = new Vector3(_pose[ArmR], 0, 0.08f + _pose[ArmRSide]);
        _forearmL.Rotation = new Vector3(-_pose[ForearmL], -_clasp, 0);
        _forearmR.Rotation = new Vector3(-_pose[ForearmR], _clasp, 0);
        _sword.Rotation = new Vector3(_pose[SwordTilt], _pose[SwordYaw], 0);
    }

    private void BuildLeg(Node3D thigh, Node3D shin, float x, Color cloth, Color boot, float length, bool claws)
    {
        _hips.AddChild(thigh);
        thigh.Position = new Vector3(x, -0.08f, 0);
        var hip = claws ? 0.11f : 0.09f;
        var knee = claws ? 0.085f : 0.06f;
        thigh.AddChild(Part(new CylinderMesh { TopRadius = hip, BottomRadius = knee, Height = 0.42f * length, RadialSegments = 8 }, cloth, new Vector3(0, -0.21f * length, 0)));
        thigh.AddChild(shin);
        shin.Position = new Vector3(0, -0.42f * length, 0);
        if (claws)
        {
            thigh.AddChild(Rounded(Vector3.One * hip * 1.05f, cloth, Vector3.Zero));
            shin.AddChild(Rounded(Vector3.One * knee * 1.1f, cloth, Vector3.Zero));
        }

        if (!claws)
        {
            shin.AddChild(Rounded(Vector3.One * 0.062f, cloth, Vector3.Zero));
            shin.AddChild(Rounded(new Vector3(0.066f, 0.13f, 0.07f), cloth, new Vector3(0, -0.1f, 0.012f)));
        }

        shin.AddChild(Part(new CylinderMesh { TopRadius = knee, BottomRadius = claws ? 0.07f : 0.045f, Height = 0.32f * length, RadialSegments = 8 }, cloth, new Vector3(0, -0.16f * length, 0)));
        var foot = -0.32f * length - 0.03f;
        if (!claws)
        {
            shin.AddChild(Part(new CylinderMesh { TopRadius = 0.058f, BottomRadius = 0.05f, Height = 0.17f, RadialSegments = 8 }, boot, new Vector3(0, -0.25f, 0)));
            shin.AddChild(Rounded(new Vector3(0.062f, 0.05f, 0.135f), boot, new Vector3(0, foot + 0.005f, -0.055f)));
            return;
        }

        var clawed = Model("gob_foot");
        clawed.Position = new Vector3(0, foot + 0.045f, 0);
        shin.AddChild(clawed);
    }

    private void BuildArm(Node3D arm, Node3D forearm, float x, Color cloth, Color skin, Color glove)
    {
        var goblin = _look.Goblin;
        _torso.AddChild(arm);
        arm.Position = new Vector3(x, goblin ? 0.36f : 0.45f, 0);
        var top = goblin ? 0.09f : 0.058f;
        arm.AddChild(Part(new CylinderMesh { TopRadius = top, BottomRadius = top * 0.78f, Height = 0.32f, RadialSegments = 12 }, cloth, new Vector3(0, -0.16f, 0)));
        arm.AddChild(forearm);
        if (!goblin)
        {
            arm.AddChild(Rounded(Vector3.One * 0.062f, cloth, Vector3.Zero));
            arm.AddChild(Rounded(Vector3.One * 0.047f, cloth, new Vector3(0, -0.32f, 0)));
        }

        if (goblin)
        {
            // Round shoulders and elbows, so the limbs read as flesh and not as pipes.
            arm.AddChild(Rounded(Vector3.One * top * 1.12f, skin, Vector3.Zero));
            arm.AddChild(Rounded(Vector3.One * top * 0.82f, skin, new Vector3(0, -0.32f, 0)));
        }

        forearm.Position = new Vector3(0, -0.32f, 0);
        var wrist = goblin ? 0.055f : 0.036f;
        forearm.AddChild(Part(new CylinderMesh { TopRadius = top * 0.78f, BottomRadius = wrist, Height = 0.26f, RadialSegments = 12 }, goblin ? skin : cloth, new Vector3(0, -0.13f, 0)));
        forearm.AddChild(Part(new CylinderMesh { TopRadius = wrist + 0.018f, BottomRadius = wrist + 0.012f, Height = 0.1f, RadialSegments = 8 }, glove, new Vector3(0, -0.21f, 0)));
        if (goblin)
        {
            var hand = Model("gob_hand");
            hand.Position = new Vector3(0, -0.22f, 0);
            forearm.AddChild(hand);
            return;
        }

        // A hand: palm, four fingers curled together and a thumb.
        forearm.AddChild(Rounded(new Vector3(0.042f, 0.052f, 0.022f), skin, new Vector3(0, -0.3f, 0)));
        forearm.AddChild(Rounded(new Vector3(0.04f, 0.038f, 0.019f), skin, new Vector3(0, -0.355f, -0.008f)));
        var thumb = Rounded(new Vector3(0.014f, 0.034f, 0.014f), skin, new Vector3(Mathf.Sign(x) * -0.042f, -0.31f, -0.012f));
        thumb.Rotation = new Vector3(0, 0, Mathf.Sign(x) * -0.5f);
        forearm.AddChild(thumb);
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
        MaterialOverride = Toon(color),
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
