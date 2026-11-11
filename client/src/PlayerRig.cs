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
    ClawRaise,
    ClawRake,

    /// <summary>A heavy blade drawn back over the right shoulder, before a flat swing.</summary>
    SwingBack,

    /// <summary>The blade swept flat across the front from right to left.</summary>
    SwingAcross,

    /// <summary>Both hands drive an axe down into the ground in front of the feet.</summary>
    GroundChop,

    /// <summary>A spear driven straight out in front at chest height.</summary>
    SpearThrust,

    /// <summary>A slide: low and leaning back, the front leg out straight and the back one folded, a hand trailing.</summary>
    Slide,

    /// <summary>On guard: the blade held level across the face, the free hand braced behind it.</summary>
    Guard,

    /// <summary>Knocked off balance by a parry: leaning back, arms flung wide, head rocked.</summary>
    Reel,

    /// <summary>Squatting deep to spring, arms swept back.</summary>
    Crouch,

    /// <summary>Stretched out like an arrow in flight: arms reaching forward, legs straight back.</summary>
    Dive,

    /// <summary>Landed on the prey: bent over it with both claws driven down into it.</summary>
    Impale,

    /// <summary>Curled up tight for a forward roll.</summary>
    Tuck,

    /// <summary>A spear raised over the shoulder, point forward, the body leaning back to throw.</summary>
    ThrowBack,

    /// <summary>The throwing arm whipped forward and down; the spear has left the hand.</summary>
    Throw,
}

/// <summary>What a character is doing this frame. The rig turns it into a pose.</summary>
/// <param name="Draw">How far a bow is drawn, from 0 to 1; below 0 when unknown, which shows a full draw.</param>
/// <param name="AimPitch">How far up (positive) or down the bow aims, in radians.</param>
public readonly record struct MotionState(
    float Speed, bool Grounded, float VerticalSpeed, bool Dashing, AttackKind Attack, int AttackTick,
    WeaponMove Move = WeaponMove.None, float Draw = -1f, float AimPitch = 0f);

/// <summary>How a rig looks. Kind picks the body: player, skirmisher, brute, archer, sharpshooter or shaman.</summary>
public sealed record RigLook(string Kind, Color Skin, float Scale, string Weapon)
{
    public static readonly RigLook Player = new("player", new Color("e8b796"), 1f, "sword");

    /// <summary>Goblins and orcs share the brute rig: thick limbs, clawed hands, heads and torsos from Blender.</summary>
    public bool Goblin => Kind is "skirmisher" or "brute" or "archer" or "sharpshooter" or "shaman" || Orc || Werewolf;

    /// <summary>A werewolf: a hunched, shaggy giant with a wolf's head, clawed hands, hind paws and a tail.</summary>
    public bool Werewolf => Kind.StartsWith("werewolf");

    /// <summary>An orc: a goblin's rig at a man's height. Kind is orc_ and the head: warrior, berserker, spearwoman or huntress.</summary>
    public bool Orc => Kind.StartsWith("orc_") || Sentinel;

    /// <summary>An Auren sentinel: a carved guardian of marble on an orc's frame. Kind is sentinel or sentinel_warden.</summary>
    public bool Sentinel => Kind.StartsWith("sentinel");

    public bool Female => Feminine || Kind is "orc_spearwoman" or "orc_huntress";

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

    /// <summary>How wide the head is: below 1 narrow, above 1 broad.</summary>
    public float FaceWidth { get; init; } = 1f;

    /// <summary>The expression the face rests in, by name in world/faces.json.</summary>
    public string Mood { get; init; } = "neutral";

    /// <summary>The face's details: nose, eye shape, brows, scar and stubble.</summary>
    public FaceStyle FaceStyle { get; init; } = FaceStyle.Plain;

    /// <summary>The item whose model the hand holds, when not the kind's default (Hanne's own short sword).</summary>
    public string? Item { get; init; }
}

/// <summary>
/// A jointed figure animated from code. Each state gives a target pose and the rig blends towards it,
/// so every change of state becomes a smooth transition without an authored clip for each pair.
/// </summary>
public sealed class PlayerRig
{
    private const int JointCount = 18;
    private const int Lean = 0, Twist = 1, Bob = 2, Head = 3;
    private const int ThighL = 4, ThighR = 5, ShinL = 6, ShinR = 7;
    private const int ArmL = 8, ArmR = 9, ArmRSide = 10, ForearmL = 11, ForearmR = 12, SwordTilt = 13, SwordYaw = 14, ArmLSide = 15;

    /// <summary>The blade in the left hand, when the rig fights with two daggers.</summary>
    private const int OffTilt = 16, OffYaw = 17;

    private readonly float[] _pose = new float[JointCount];
    private readonly float[] _target = new float[JointCount];
    private readonly Node3D _hips = new();
    private readonly Node3D _torso = new();
    private readonly Node3D _head = new();
    private readonly Node3D _thighL = new(), _thighR = new(), _shinL = new(), _shinR = new();
    private readonly Node3D _armL = new(), _armR = new(), _forearmL = new(), _forearmR = new();
    private readonly Node3D _sword = new();
    private readonly Node3D _offhand = new();

    /// <summary>The bow in hand and the quiver on the back, while the weapon is a bow.</summary>
    private BowView? _bow;

    /// <summary>The weapon item in hand, or null for the kind's usual one.</summary>
    private string? _weaponItem;
    private Node3D? _quiver;
    private MotionState _motion;

    /// <summary>How far the arms follow the dagger keys instead of the blended pose, from 0 to 1.</summary>
    private float _daggerReach;

    /// <summary>How far the arms follow the archer's reach instead of the blended pose, from 0 to 1.</summary>
    private float _archer;
    private Vector3? _drawHand;
    private readonly MeshInstance3D _slash;
    private readonly ShaderMaterial _slashMaterial;
    private readonly CpuParticles3D _sparks;
    private readonly CpuParticles3D _wind;
    private RigLook _look = RigLook.Player;
    private readonly System.Collections.Generic.List<ClothChain> _cloth = new();
    private readonly Node3D _hairRoot = new();
    private readonly Node3D _cape = new();

    /// <summary>The face of a human figure: expressions, blinking and talking. Goblins, orcs and werewolves have none.</summary>
    public FaceRig? Face { get; private set; }

    /// <summary>The head model the face sits on: the portrait camera looks at it.</summary>
    public Node3D? FaceAnchor { get; private set; }
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

    /// <summary>
    /// Changes the weapon in hand: a bow in the left hand, two daggers one in each, anything else in
    /// the right. A player picks up a new weapon whenever the gear they wear changes.
    /// </summary>
    /// <param name="weapon">The kind of weapon: sword, dagger, bow, club, staff or none.</param>
    /// <param name="item">The item in hand, which picks its model; without it the kind's usual one.</param>
    public void SetWeapon(string weapon, string? item = null)
    {
        if (_look.Weapon == weapon && _weaponItem == item)
        {
            return;
        }

        _look = _look with { Weapon = weapon };
        _weaponItem = item;
        foreach (var child in _sword.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var child in _offhand.GetChildren())
        {
            child.QueueFree();
        }

        _sword.GetParent()?.RemoveChild(_sword);
        (weapon == "bow" ? _forearmL : _forearmR).AddChild(_sword);
        _sword.Position = new Vector3(0, -0.3f, -0.04f);
        BuildWeapon(weapon);
    }

    /// <summary>How much thicker than usual the thighs are, for a werewolf gathering itself to spring.</summary>
    public float LegBulk
    {
        set
        {
            var scale = new Vector3(value, 1f, value);
            _thighL.Scale = scale;
            _thighR.Scale = scale;
        }
    }

    public bool SwordVisible
    {
        get => _sword.Visible;
        set => _sword.Visible = _offhand.Visible = value;
    }

    /// <summary>Set while the figure is hit. It shakes for as long as this is on.</summary>
    public bool Flash { get; set; }

    public PlayerRig(Color tunic, RigLook? look = null)
    {
        look ??= RigLook.Player;
        var skin = look.Skin;
        Root.Scale = Vector3.One * look.Scale;
        _look = look;
        _hipHeight = look.Orc || look.Werewolf ? 0.98f : look.Goblin ? 0.6f : 0.98f;
        Root.AddChild(_hips);
        _hips.Position = new Vector3(0, _hipHeight, 0);
        _hips.AddChild(_torso);
        _torso.Position = new Vector3(0, 0.1f, 0);
        _torso.AddChild(_head);
        _head.Position = look.Werewolf ? new Vector3(0, 0.55f, 0.06f) : look.Orc ? new Vector3(0, 0.47f, 0.01f) : new Vector3(0, look.Goblin ? 0.5f : 0.3f, look.Goblin ? -0.06f : 0f);
        if (look.Werewolf)
        {
            BuildWerewolf(skin);
        }
        else if (look.Sentinel)
        {
            BuildSentinel(skin);
        }
        else if (look.Orc)
        {
            BuildOrc(skin);
        }
        else if (look.Goblin)
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
        _forearmL.AddChild(_offhand);
        _offhand.Position = new Vector3(0, -0.3f, -0.04f);

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
            var holder = extra is "beard" or "beard_short" or "goatee" or "mustache" or "headband" ? _hairRoot : extra is "robe" or "apron" ? _hips : _torso;
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
        head.Scale = new Vector3(_look.FaceWidth, 1f, 1f);
        Face = new FaceRig(head, _look.Skin, _look.Eyes, _look.Brows, _look.Feminine, _look.FaceStyle);
        FaceAnchor = head;
        Face.Resting = _look.Mood;
        Face.Expression = _look.Mood;

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
            "mira" => ("anime_hair_long", new ClothShape(0.58f, 0.14f, 0.13f, 0.17f, 1.6f, 9.8f, 0.03f, 0.15f, 0.11f), 0.08f),
            "rhea" => ("anime_ponytail", new ClothShape(0.5f, 0.05f, 0.17f, 0.2f, 1.8f, 9.8f, 0.025f, 0.13f, 0.13f), 0.13f),
            "tess" => ("anime_braids", new ClothShape(0.44f, 0.16f, 0.02f, 0.04f, 1.6f, 9.8f, 0.03f, 0.13f, 0.09f), -0.02f),
            "sora" => ("anime_hair_wavy", new ClothShape(0.62f, 0.15f, 0.13f, 0.17f, 1.6f, 9.8f, 0.03f, 0.15f, 0.11f), 0.08f),
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
    /// <summary>
    /// The model of a weapon item, weapon_&lt;item id&gt; from Blender, in toon colours; null when the item
    /// has none. Its origin is the middle of the grip and the blade runs along -Z.
    /// </summary>
    public static Node3D? WeaponModel(string item)
    {
        var path = $"res://assets/weapon_{item}.glb";
        if (!ResourceLoader.Exists(path))
        {
            return null;
        }

        var model = GD.Load<PackedScene>(path).Instantiate<Node3D>();
        ToonColours(model);
        return model;
    }

    private static void ToonColours(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh is not null)
        {
            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.Mesh.SurfaceGetMaterial(surface) is StandardMaterial3D original)
                {
                    var material = Toon(original.AlbedoColor);
                    if (original.ResourceName is "wpn_edge" or "wpn_gem" or "wpn_ember")
                    {
                        // Edges and embers catch the light from every side.
                        material.SetShaderParameter("shade", original.AlbedoColor * new Color(0.85f, 0.85f, 0.9f));
                    }

                    mesh.SetSurfaceOverrideMaterial(surface, material);
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            ToonColours(child);
        }
    }

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
                    // The still cap of the hair takes the hair's colour, but never sways with the strands.
                    var cap = original.ResourceName == "anime_hair_cap";
                    var color = _look.Palette.GetValueOrDefault(cap ? "anime_hair" : original.ResourceName, original.AlbedoColor);
                    var skin = original.ResourceName == "anime_skin";
                    var material = Toon(skin ? _look.Skin : color);
                    if (skin)
                    {
                        // Faces stay almost fully lit, with a warm shadow, as drawn anime faces do.
                        material.SetShaderParameter("shade", _look.Skin * new Color(0.95f, 0.78f, 0.76f));
                        material.SetShaderParameter("threshold", -0.45f);
                        material.SetShaderParameter("rim_strength", 0.08f);
                    }

                    if (original.ResourceName is "were_eye")
                    {
                        // Eyes that burn in the dark: lit from every side.
                        material.SetShaderParameter("shade", color * 0.95f);
                        material.SetShaderParameter("rim_strength", 0f);
                    }

                    if (original.ResourceName == "anime_hair" || cap)
                    {
                        material.SetShaderParameter("shine", 1f);
                    }

                    material.SetShaderParameter("sway_top", top);
                    material.SetShaderParameter("sway_length", length);
                    mesh.SetSurfaceOverrideMaterial(surface, material);
                    if (!cap)
                    {
                        cloth?.Add(material);
                    }
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

        Face?.Step(delta);
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

    /// <summary>
    /// An orc: a man's height and broader, a muscled torso under fur and leather (or a corset for a
    /// woman), heavy boots, the head of its kind, and the goblin's clawed hands at the end of thick arms.
    /// </summary>
    private void BuildOrc(Color skin)
    {
        var leather = new Color("5a3a2c");
        var dark = new Color("3a2420");
        var female = _look.Female;
        _hips.AddChild(Part(new BoxMesh { Size = new Vector3(female ? 0.36f : 0.42f, 0.18f, 0.28f) }, dark, Vector3.Zero));
        _torso.AddChild(Model(female ? "orc_torso_f" : "orc_torso_m"));
        _head.AddChild(Model($"orc_head_{_look.Kind[4..]}"));
        var width = female ? 0.2f : 0.25f;
        // Legs long enough to reach the ground from a man's hip height.
        BuildLeg(_thighL, _shinL, -0.12f, dark, leather, 1.2f, true);
        BuildLeg(_thighR, _shinR, 0.12f, dark, leather, 1.2f, true);
        BuildArm(_armL, _forearmL, -width - 0.06f, skin, skin, leather);
        BuildArm(_armR, _forearmR, width + 0.06f, skin, skin, leather);
    }

    /// <summary>
    /// An Auren sentinel: a carved chest and faceless helm from Blender, thick marble limbs on an orc's
    /// frame, stone feet and gauntlets, bronze at the wrists. The warden wears a crest and a rune mantle.
    /// </summary>
    private void BuildSentinel(Color stone)
    {
        var warden = _look.Kind == "sentinel_warden";
        var dark = stone.Darkened(0.3f);
        var bronze = new Color("b8963c");
        _hips.AddChild(Part(new BoxMesh { Size = new Vector3(0.34f, 0.14f, 0.24f) }, stone.Darkened(0.12f), Vector3.Zero));
        _torso.AddChild(Model(warden ? "sentinel_torso_warden" : "sentinel_torso"));
        _head.AddChild(Model(warden ? "sentinel_head_warden" : "sentinel_head"));
        BuildLeg(_thighL, _shinL, -0.13f, stone, dark, 1.2f, true);
        BuildLeg(_thighR, _shinR, 0.13f, stone, dark, 1.2f, true);
        BuildArm(_armL, _forearmL, -0.33f, stone, stone, bronze);
        BuildArm(_armR, _forearmR, 0.33f, stone, stone, bronze);
    }

    /// <summary>
    /// A werewolf: a deep, hunched chest under shaggy fur, a wolf's head with glowing eyes, long thick
    /// arms ending in clawed hands, hind legs on paws, and a tail hanging from the hips.
    /// </summary>
    private void BuildWerewolf(Color fur)
    {
        _hips.AddChild(Part(new BoxMesh { Size = new Vector3(0.4f, 0.2f, 0.3f) }, fur, Vector3.Zero));
        _torso.AddChild(Model("were_torso"));
        _head.AddChild(Model("were_head"));
        var tail = Model("were_tail");
        tail.Position = new Vector3(0, 0.02f, 0.12f);
        _hips.AddChild(tail);
        BuildLeg(_thighL, _shinL, -0.13f, fur, fur, 1.2f, true);
        BuildLeg(_thighR, _shinR, 0.13f, fur, fur, 1.2f, true);
        BuildArm(_armL, _forearmL, -0.4f, fur, fur, fur.Darkened(0.3f));
        BuildArm(_armR, _forearmR, 0.4f, fur, fur, fur.Darkened(0.3f));
        // Shaggy fur hangs from the thighs and the upper arms, so the limbs are not smooth pillars.
        foreach (var (limb, model) in new[] { (_thighL, "were_thigh_fur"), (_thighR, "were_thigh_fur"), (_armL, "were_arm_fur"), (_armR, "were_arm_fur") })
        {
            limb.AddChild(Model(model));
        }
    }

    /// <summary>A goblin head from Blender: big nose, scowling brow, grin and long ears, with the gear of its kind.</summary>
    private void BuildGoblinHead(Color skin, Color cloth, Color dark, Color bone)
    {
        _head.AddChild(Model($"gob_head_{_look.Kind}"));
    }

    /// <summary>The model a kind of weapon shows when no item is named: a goblin's is cruder.</summary>
    private string DefaultItem(string weapon) => weapon switch
    {
        "dagger" => _look.Goblin ? "rusty_dagger" : "twin_daggers",
        "bow" => _look.Goblin ? "goblin_shortbow" : "hunters_longbow",
        "club" => "spiked_club",
        "staff" => "bone_staff",
        "longsword" => "orc_longsword",
        "spear" => "orc_spear",
        "axe" => "orc_battle_axe",
        _ => "wooden_sword",
    };

    /// <summary>
    /// Puts the weapon's model in the hand: a Blender model for blades, clubs and staves, and a bow
    /// that bends for bows. Daggers come in pairs for players, one in each hand.
    /// </summary>
    private void BuildWeapon(string weapon)
    {
        _bow = null;
        _quiver?.QueueFree();
        _quiver = null;
        foreach (var child in _offhand.GetChildren())
        {
            child.QueueFree();
        }

        if (weapon is "none" or "claws")
        {
            return;
        }

        var item = _weaponItem ?? _look.Item ?? DefaultItem(weapon);
        if (weapon == "bow")
        {
            // The bow stands upright in the fist, its string towards the archer; it bends as it is drawn.
            _sword.Position = new Vector3(0, -0.32f, 0);
            _bow = new BowView(BowLook.For(item, _look.Goblin));
            _sword.AddChild(_bow);
            if (!_look.Goblin)
            {
                _quiver = BuildQuiver();
                _torso.AddChild(_quiver);
            }

            return;
        }

        var model = WeaponModel(item) ?? WeaponModel(DefaultItem(weapon));
        if (model is not null)
        {
            _sword.AddChild(model);
        }

        if (weapon == "dagger" && !_look.Goblin && WeaponModel(item) is { } second)
        {
            _offhand.AddChild(second);
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
        else if (Sitting)
        {
            _phase += delta * 2f;
            SitPose();
        }
        else
        {
            _phase += delta * 2f;
            IdlePose();
        }

        _slash.Visible = false;
        _motion = state;
        if (_look.Weapon == "none")
        {
            RestArms();
        }
        else if (_look.Weapon != "sword")
        {
            CarryWeapon();
        }

        if (_look.Werewolf)
        {
            // A werewolf never stands straight: it crouches on bent knees, ready to spring.
            _target[ThighL] += 0.5f;
            _target[ThighR] += 0.42f;
            _target[ShinL] += 0.85f;
            _target[ShinR] += 0.8f;
            _target[Bob] -= 0.14f;
        }

        if (state.Attack is AttackKind.DaggerFlurry or AttackKind.DaggerLeap)
        {
            DaggerPose(state.Attack, state.AttackTick);
            blend = state.Attack == AttackKind.DaggerFlurry ? 40f : 26f;
        }
        else if (state.Attack == AttackKind.BladeDance)
        {
            // Blade Dance: the flurry's stabs, hand after hand, one for every blow of the dance.
            var dance = Tuning.Attack(AttackKind.BladeDance);
            var blow = Mathf.Max(0, (state.AttackTick - dance.HitAt + dance.HitEvery / 2) / dance.HitEvery);
            var offset = state.AttackTick - (dance.HitAt + blow * dance.HitEvery);
            var flurry = Tuning.Attack(AttackKind.DaggerFlurry);
            DaggerPose(AttackKind.DaggerFlurry, Mathf.Max(0, (blow % 2 == 0 ? flurry.HitAt : flurry.SecondHitAt) + offset));
            blend = 40f;
        }
        else if (state.Attack == AttackKind.ShadowStep)
        {
            // Shadow Step: the leap's raised daggers, driven down at the blow.
            var hit = Tuning.Attack(AttackKind.ShadowStep).HitAt;
            DaggerPose(AttackKind.DaggerLeap, state.AttackTick < hit ? Mathf.Min(4 + state.AttackTick, Tuning.LeapHitAt - 4) : Tuning.LeapHitAt + state.AttackTick - hit);
            blend = 30f;
        }
        else if (state.Attack == AttackKind.GroundPound)
        {
            WeaponPose(state.AttackTick < Tuning.GroundPoundLandTick ? WeaponMove.ClubRaise : WeaponMove.ClubSmash);
            blend = 30f;
        }
        else if (state.Attack == AttackKind.Whirlwind)
        {
            // Whirlwind: the blade held straight out while the body turns under it.
            ClubPose(AttackKind.ClubSpin, 20);
            blend = 30f;
        }
        else if (state.Attack == AttackKind.RisingSlash)
        {
            RisingPose(state.AttackTick);
            blend = 34f;
        }
        else if (state.Attack is AttackKind.ClubSwing or AttackKind.ClubSpin)
        {
            ClubPose(state.Attack, state.AttackTick);
            blend = 30f;
        }
        else if (state.Attack is AttackKind.StaffBolt or AttackKind.StaffWall)
        {
            StaffPose(state.Attack, state.AttackTick);
            blend = 24f;
        }
        else if (state.Attack is not (AttackKind.None or AttackKind.BowShot))
        {
            AttackPose(state.Attack, state.AttackTick);
            blend = 34f;
        }

        _lastAttackTick = state.Attack == AttackKind.None ? -1 : state.AttackTick;

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
        UpdateSpin(state);
        UpdateBow(state, delta);
        UpdateDaggers(state, delta);
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
                // The bow hangs upright at the side, not pointing ahead like a spear.
                _target[ArmL] = 0.5f;
                _target[ForearmL] = -0.2f;
                _target[SwordTilt] = 1.5708f;
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
            case "claws":
                // Long arms hanging forward, hands open and ready to tear.
                _target[ArmR] = 0.45f;
                _target[ArmRSide] = 0.3f;
                _target[ForearmR] = -0.55f;
                _target[ArmL] = 0.45f;
                _target[ArmLSide] = 0.3f;
                _target[ForearmL] = -0.55f;
                break;
            case "spear":
                // Upright in the right hand, butt by the foot, point well over the head.
                _target[ArmR] = 0.25f;
                _target[ArmRSide] = 0.25f;
                _target[ForearmR] = -1.35f;
                _target[SwordTilt] = 0.1f;
                _target[SwordYaw] = 0f;
                break;
            case "axe":
                // Over the shoulder, the head behind it.
                _target[ArmR] = 0.75f;
                _target[ArmRSide] = 0.35f;
                _target[ForearmR] = -2.1f;
                _target[SwordTilt] = 0f;
                _target[SwordYaw] = -0.2f;
                break;
            case "longsword":
                // Held low and forward, point down and out, ready to come up.
                _target[ArmR] = 0.35f;
                _target[ArmRSide] = 0.3f;
                _target[ForearmR] = -0.7f;
                _target[SwordTilt] = -1.6f;
                _target[SwordYaw] = -0.25f;
                break;
            case "dagger" when !_look.Goblin:
                // Two blades held low and forward, point first, ready to strike with either.
                _target[ArmR] = 0.35f;
                _target[ArmRSide] = 0.35f;
                _target[ForearmR] = -1.1f;
                _target[SwordTilt] = -1.1f;
                _target[SwordYaw] = -0.2f;
                _target[ArmL] = 0.35f;
                _target[ArmLSide] = 0.35f;
                _target[ForearmL] = -1.1f;
                _target[OffTilt] = -1.1f;
                _target[OffYaw] = 0.2f;
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

    /// <summary>
    /// The archer's stance: side on, the bow arm straight out along the aim, and the drawing hand
    /// travelling from the string in front of the bow back to the cheek as the draw fills. Aiming up
    /// or down raises or lowers both arms together and tips the head with them.
    /// </summary>
    private void BowDrawPose(float draw, float pitch)
    {
        var eased = 1f - (1f - draw) * (1f - draw);
        _target[ArmL] = 1.55f + pitch;
        _target[ArmLSide] = 0f;
        _target[ForearmL] = 0f;
        _target[ArmR] = Mathf.Lerp(1.55f, 1.45f, eased) + pitch;
        _target[ArmRSide] = Mathf.Lerp(-0.05f, 0.05f, eased);
        _target[ForearmR] = Mathf.Lerp(-0.25f, -2.15f, eased);
        _target[SwordTilt] = 0f;
        _target[SwordYaw] = 0f;
        _target[Twist] = _look.Goblin ? 0.75f : -0.8f;
        _target[Head] = -pitch * 0.6f;
        _target[Lean] = 0.02f - pitch * 0.15f;
        _target[ThighL] = -0.35f;
        _target[ThighR] = 0.3f;
    }

    /// <summary>
    /// After the arrow leaves: the drawing hand flies back past the ear, then reaches over the
    /// shoulder into the quiver and brings the next arrow round to the string.
    /// </summary>
    private void BowReleasePose(int tick, float pitch)
    {
        BowDrawPose(1f, pitch);
        if (tick < 7)
        {
            _target[ArmR] = 1.3f + pitch;
            _target[ArmRSide] = 0.45f;
            _target[ForearmR] = -1.4f;
            _target[Lean] -= 0.04f;
        }
        else if (tick < 16)
        {
            _target[ArmR] = 2.75f;
            _target[ArmRSide] = 0.3f;
            _target[ForearmR] = -2.2f;
            _target[ArmL] = 1.2f + pitch * 0.5f;
        }
        else
        {
            _target[ArmR] = 1.5f + pitch;
            _target[ArmRSide] = -0.05f;
            _target[ForearmR] = -0.4f;
        }
    }

    /// <summary>
    /// Bends the bow with the draw and keeps its string in the drawing hand: from the moment the
    /// next arrow is brought to the string until it is loosed.
    ///
    /// A player's arms reach for exact points instead of following joint angles. The arrow lies on a
    /// line from the cheek along the aim: the bow hand grips that line at arm's length, and the
    /// drawing hand holds the string on it, from the bow at the start of the draw to the cheek at a
    /// full draw. After the shot the hand flies back, fetches an arrow from the quiver and brings it
    /// to the string.
    /// </summary>
    private void UpdateBow(in MotionState state, float delta)
    {
        if (_bow is null)
        {
            return;
        }

        var drawing = state.Move == WeaponMove.BowDraw;
        var shotTick = state.Attack == AttackKind.BowShot ? state.AttackTick : -1;
        var draw = drawing ? (state.Draw < 0f ? 1f : state.Draw) : 0f;
        _bow.Draw = drawing ? draw : Mathf.Lerp(_bow.Draw, 0f, 1f - Mathf.Exp(-40f * delta));
        _bow.Nocked = drawing || shotTick >= 18;
        if (_look.Goblin)
        {
            _bow.Hand = drawing ? _bow.ToLocal(_forearmR.ToGlobal(new Vector3(0, -0.3f, -0.04f))) : null;
            _bow.Update();
            return;
        }

        var shooting = drawing || shotTick >= 0;
        _archer = Mathf.MoveToward(_archer, shooting ? 1f : 0f, delta * (shooting ? 7f : 4f));
        if (_archer <= 0f)
        {
            _hips.Rotation = Vector3.Zero;
            _drawHand = null;
            _bow.Hand = null;
            _bow.Update();
            return;
        }

        var w = _archer * _archer * (3f - 2f * _archer);
        var forward = -Root.GlobalBasis.Z.Normalized();
        var right = forward.Cross(Vector3.Up).Normalized();
        var aim = (forward * Mathf.Cos(state.AimPitch) + Vector3.Up * Mathf.Sin(state.AimPitch)).Normalized();

        // Side on to the target: the hips turn a little, the chest the rest, the head looks along the aim.
        _hips.Rotation = new Vector3(0, -0.6f * w, 0);
        _head.Rotation = new Vector3(_head.Rotation.X, Mathf.Lerp(_head.Rotation.Y, 0.8f + 0.6f, w), 0);

        var scale = Root.Scale.X;
        var reach = 0.3f * scale;
        var cheek = _head.GlobalPosition + Vector3.Up * 0.13f * scale + right * 0.05f * scale;
        var shoulderL = _armL.GlobalPosition;
        var along = ReachAlong(cheek - shoulderL, aim, 2f * reach * 0.95f);
        var eased = 1f - (1f - draw) * (1f - draw);
        var grip = cheek + aim * along * Mathf.Lerp(0.78f, 1f, drawing ? eased : 1f);
        var brace = 0.168f * scale;

        // Where the drawing hand goes.
        Vector3 hand;
        if (drawing)
        {
            var pull = Mathf.Max(0f, (grip - cheek).Dot(aim) - brace) * eased;
            hand = grip - aim * (brace + pull);
        }
        else if (shotTick is >= 0 and < 7)
        {
            hand = cheek - aim * 0.16f * scale + right * 0.1f * scale;
        }
        else if (shotTick is >= 7 and < 16)
        {
            hand = _torso.ToGlobal(new Vector3(0.2f, 0.66f, 0.2f));
        }
        else
        {
            hand = grip - aim * brace;
        }

        // The hand moves smoothly between those points, but holds the string exactly while drawing.
        _drawHand = drawing || _drawHand is null ? hand : _drawHand.Value.Lerp(hand, 1f - Mathf.Exp(-22f * delta));
        Reach(_armL, _forearmL, grip, -Vector3.Up - right * 0.3f, reach, w);
        Reach(_armR, _forearmR, _drawHand.Value, -aim + Vector3.Up * 0.35f + right * 0.4f, reach, w);

        // The bow stands upright across the line of the arrow, its string towards the archer.
        var up = (Vector3.Up - aim * aim.Dot(Vector3.Up)).Normalized();
        var y = -aim;
        var bowBasis = new Basis(y.Cross(up), y, up).Scaled(Vector3.One * scale);
        var held = _sword.GlobalTransform;
        _sword.GlobalTransform = new Transform3D(Blend(held.Basis, bowBasis, w).Scaled(Vector3.One * scale),
            held.Origin.Lerp(grip, w));
        _bow.Hand = drawing ? _bow.ToLocal(_drawHand.Value) : null;
        _bow.Update();
    }

    /// <summary>How far along the aim from the cheek a hand can reach from the shoulder, at most the arm's length.</summary>
    private static float ReachAlong(Vector3 cheekFromShoulder, Vector3 aim, float length)
    {
        var b = cheekFromShoulder.Dot(aim);
        var c = cheekFromShoulder.LengthSquared() - length * length;
        return -b + Mathf.Sqrt(Mathf.Max(0f, b * b - c));
    }

    /// <summary>
    /// Points an arm so its hand reaches a point: two bones of equal length, the elbow bending
    /// towards the pole. It blends from the pose the joints already hold by the weight.
    /// </summary>
    private static void Reach(Node3D arm, Node3D forearm, Vector3 target, Vector3 pole, float bone, float weight)
    {
        var shoulder = arm.GlobalPosition;
        var scale = arm.GlobalBasis.Scale;
        var toTarget = target - shoulder;
        var distance = Mathf.Clamp(toTarget.Length(), 0.05f, bone * 2f - 0.001f);
        var direction = toTarget.Normalized();
        var bend = (pole - direction * pole.Dot(direction)).Normalized();
        var elbow = shoulder + direction * distance / 2f + bend * Mathf.Sqrt(bone * bone - distance * distance / 4f);

        // The forearm folds towards the arm's -Z, so the arm's +Z points away from the bend.
        var upper = Bone(shoulder, elbow, -(target - elbow));
        arm.GlobalTransform = new Transform3D(Blend(arm.GlobalBasis, upper, weight).Scaled(scale), shoulder);
        var start = forearm.GlobalPosition;
        var lower = Bone(start, target, arm.GlobalBasis.Z);
        forearm.GlobalTransform = new Transform3D(Blend(forearm.GlobalBasis, lower, weight).Scaled(scale), start);
    }

    /// <summary>Turns part of the way from one rotation to another; scale in either is ignored.</summary>
    private static Basis Blend(Basis from, Basis to, float weight)
    {
        var a = new Quaternion(from.Orthonormalized()).Normalized();
        var b = new Quaternion(to.Orthonormalized()).Normalized();
        return new Basis(a.Slerp(b, weight).Normalized());
    }

    /// <summary>A bone's frame: -Y runs from one end to the other, +Z as near the hint as it can.</summary>
    private static Basis Bone(Vector3 from, Vector3 to, Vector3 zHint)
    {
        var y = (from - to).Normalized();
        var z = zHint - y * y.Dot(zHint);
        z = z.LengthSquared() < 1e-6f ? y.Cross(Vector3.Right) : z;
        z = z.Normalized();
        return new Basis(y.Cross(z), y, z);
    }

    /// <summary>A leather quiver slung on the back, with the fletchings of a few arrows showing.</summary>
    private static Node3D BuildQuiver()
    {
        var quiver = new Node3D { Position = new Vector3(0.12f, 0.15f, 0.2f), Rotation = new Vector3(0.25f, 0f, -0.45f) };
        quiver.AddChild(Part(new CylinderMesh { TopRadius = 0.075f, BottomRadius = 0.06f, Height = 0.5f, RadialSegments = 8 }, new Color("733e39"), Vector3.Zero));
        quiver.AddChild(Part(new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.08f, Height = 0.05f, RadialSegments = 8 }, new Color("3e2731"), new Vector3(0, 0.23f, 0)));
        quiver.AddChild(Part(new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.065f, Height = 0.04f, RadialSegments = 8 }, new Color("3e2731"), new Vector3(0, -0.1f, 0)));
        foreach (var (x, z, colour) in new[] { (-0.03f, 0.02f, "e43b44"), (0.03f, -0.01f, "ead4aa"), (0f, -0.035f, "e43b44"), (0.035f, 0.035f, "ead4aa") })
        {
            quiver.AddChild(Part(new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.008f, Height = 0.16f, RadialSegments = 4 }, new Color("b86f50"), new Vector3(x, 0.3f, z)));
            quiver.AddChild(Part(new BoxMesh { Size = new Vector3(0.035f, 0.09f, 0.035f) }, new Color(colour), new Vector3(x, 0.36f, z)));
        }

        return quiver;
    }

    private void WeaponPose(WeaponMove move)
    {
        const float alongArm = -1.5708f;
        switch (move)
        {
            case WeaponMove.BowDraw:
                BowDrawPose(_motion.Draw < 0f ? 1f : _motion.Draw, _motion.AimPitch);
                break;
            case WeaponMove.BowRelease:
                BowReleasePose(_motion.Attack == AttackKind.BowShot ? _motion.AttackTick : 0, _motion.AimPitch);
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
            case WeaponMove.ClawRaise:
                // Both arms flung up and wide, the body rearing back before it tears.
                _target[ArmR] = 2.5f;
                _target[ArmL] = 2.5f;
                _target[ArmRSide] = 0.7f;
                _target[ArmLSide] = 0.7f;
                _target[ForearmR] = -1.1f;
                _target[ForearmL] = -1.1f;
                _target[Lean] = -0.25f;
                _target[Head] = 0.25f;
                break;
            case WeaponMove.ClawRake:
                // Claws raked down across each other, the body thrown forward behind them.
                _target[ArmR] = 0.85f;
                _target[ArmL] = 0.85f;
                _target[ArmRSide] = -0.35f;
                _target[ArmLSide] = -0.35f;
                _target[ForearmR] = -0.25f;
                _target[ForearmL] = -0.25f;
                _target[Lean] = 0.6f;
                _target[Bob] = -0.12f;
                _target[ThighL] = -0.7f;
                _target[ThighR] = 0.5f;
                _target[ShinR] = 0.5f;
                break;
            case WeaponMove.SwingBack:
                _target[Twist] = 0.85f;
                _target[Lean] = -0.1f;
                _target[ArmR] = 1.9f;
                _target[ArmRSide] = 1.0f;
                _target[ForearmR] = -1.6f;
                _target[SwordTilt] = 0.4f;
                _target[SwordYaw] = -0.3f;
                _target[ArmL] = 0.9f;
                _target[ArmLSide] = -0.2f;
                _target[ForearmL] = -0.9f;
                _target[ThighL] = -0.2f;
                _target[ThighR] = 0.25f;
                _target[ShinR] = 0.2f;
                break;
            case WeaponMove.SwingAcross:
                _target[Twist] = -0.95f;
                _target[Lean] = 0.3f;
                _target[ArmR] = 1.45f;
                _target[ArmRSide] = -0.55f;
                _target[ForearmR] = -0.15f;
                _target[SwordTilt] = alongArm + 0.25f;
                _target[SwordYaw] = 0.5f;
                _target[ArmL] = 0.3f;
                _target[ArmLSide] = 0.4f;
                _target[ForearmL] = -0.9f;
                _target[ThighL] = -0.55f;
                _target[ThighR] = 0.45f;
                _target[ShinR] = 0.35f;
                _target[Bob] = -0.08f;
                break;
            case WeaponMove.GroundChop:
                // Bent over a wide stance, both hands low, the axe head struck into the ground ahead.
                _target[ArmR] = 1.0f;
                _target[ArmL] = 1.0f;
                _target[ArmRSide] = 0f;
                _target[ForearmR] = -0.1f;
                _target[ForearmL] = -0.15f;
                _target[SwordTilt] = -0.5f;
                _target[SwordYaw] = 0f;
                _target[Lean] = 0.7f;
                _target[Bob] = -0.18f;
                _target[ThighL] = -0.6f;
                _target[ThighR] = 0.5f;
                _target[ShinL] = 0.5f;
                _target[ShinR] = 0.4f;
                break;
            case WeaponMove.SpearThrust:
                _target[ArmR] = 1.8f;
                _target[ArmRSide] = 0f;
                _target[ForearmR] = -0.05f;
                _target[SwordTilt] = alongArm;
                _target[SwordYaw] = 0f;
                _target[ArmL] = 1.2f;
                _target[ForearmL] = -0.6f;
                _target[Twist] = -0.4f;
                _target[Lean] = 0.25f;
                _target[ThighL] = -0.6f;
                _target[ThighR] = 0.5f;
                _target[ShinR] = 0.4f;
                break;
            case WeaponMove.Slide:
                _target[Lean] = -0.55f;
                _target[Bob] = -0.5f;
                _target[ThighL] = 1.35f;
                _target[ShinL] = 0.1f;
                _target[ThighR] = 0.35f;
                _target[ShinR] = 1.8f;
                _target[ArmL] = -0.6f;
                _target[ArmLSide] = 0.5f;
                _target[ForearmL] = -0.2f;
                _target[ArmR] = 0.6f;
                _target[ArmRSide] = 0.3f;
                _target[ForearmR] = -0.6f;
                _target[Head] = 0.15f;
                break;
            case WeaponMove.Guard:
                _target[ArmR] = 1.15f;
                _target[ArmRSide] = -0.55f;
                _target[ForearmR] = -1.25f;
                _target[SwordTilt] = 0.15f;
                _target[SwordYaw] = 1.45f;
                _target[ArmL] = 1.2f;
                _target[ArmLSide] = 0.35f;
                _target[ForearmL] = -1.35f;
                _target[Lean] = 0.12f;
                _target[ThighL] = -0.35f;
                _target[ThighR] = 0.3f;
                _target[ShinL] = 0.3f;
                _target[ShinR] = 0.35f;
                _target[Bob] = -0.06f;
                break;
            case WeaponMove.Reel:
                _target[ArmR] = 0.9f;
                _target[ArmL] = 0.9f;
                _target[ArmRSide] = 1.0f;
                _target[ArmLSide] = 1.0f;
                _target[ForearmR] = -0.4f;
                _target[ForearmL] = -0.4f;
                _target[Lean] = -0.35f;
                _target[Head] = -0.35f;
                _target[ThighL] = 0.35f;
                _target[ThighR] = -0.2f;
                _target[ShinR] = 0.3f;
                break;
            case WeaponMove.Crouch:
                _target[ThighL] = -0.8f;
                _target[ThighR] = -0.8f;
                _target[ShinL] = 1.2f;
                _target[ShinR] = 1.2f;
                _target[Bob] = -0.27f;
                _target[Lean] = 0.3f;
                _target[ArmR] = -0.9f;
                _target[ArmL] = -0.9f;
                _target[ArmRSide] = 0.3f;
                _target[ArmLSide] = 0.3f;
                _target[ForearmR] = -0.4f;
                _target[ForearmL] = -0.4f;
                _target[Head] = 0.3f;
                break;
            case WeaponMove.Dive:
                _target[ArmR] = 3.0f;
                _target[ArmL] = 3.0f;
                _target[ArmRSide] = 0.15f;
                _target[ArmLSide] = 0.15f;
                _target[ForearmR] = -0.1f;
                _target[ForearmL] = -0.1f;
                _target[ThighL] = 0.35f;
                _target[ThighR] = 0.25f;
                _target[ShinL] = 0.15f;
                _target[ShinR] = 0.1f;
                _target[Head] = 0.4f;
                break;
            case WeaponMove.Impale:
                _target[ArmR] = 0.7f;
                _target[ArmL] = 0.7f;
                _target[ArmRSide] = -0.2f;
                _target[ArmLSide] = -0.2f;
                _target[ForearmR] = -0.15f;
                _target[ForearmL] = -0.15f;
                _target[Lean] = 0.95f;
                _target[Bob] = -0.3f;
                _target[ThighL] = -0.9f;
                _target[ThighR] = 0.5f;
                _target[ShinL] = 1.1f;
                _target[ShinR] = 0.7f;
                break;
            case WeaponMove.Tuck:
                _target[ThighL] = -1.9f;
                _target[ThighR] = -1.9f;
                _target[ShinL] = 2.2f;
                _target[ShinR] = 2.2f;
                _target[ArmR] = 1.2f;
                _target[ArmL] = 1.2f;
                _target[ForearmR] = -1.8f;
                _target[ForearmL] = -1.8f;
                _target[Lean] = 0.9f;
                _target[Head] = 0.5f;
                break;
            case WeaponMove.ThrowBack:
                // The throwing arm drawn back level with the shoulder, the forearm up, the spear
                // lying over the shoulder with its point forward; the free arm points at the target.
                _target[ArmR] = 0.2f;
                _target[ArmRSide] = 1.45f;
                _target[ForearmR] = -1.7f;
                _target[SwordTilt] = 0f;
                _target[SwordYaw] = 0f;
                _target[ArmL] = 1.5f;
                _target[ArmLSide] = 0.1f;
                _target[ForearmL] = -0.2f;
                _target[Twist] = 0.75f;
                _target[Lean] = -0.25f;
                _target[ThighL] = -0.45f;
                _target[ThighR] = 0.3f;
                _target[ShinR] = 0.3f;
                break;
            case WeaponMove.Throw:
                _target[ArmR] = 1.3f;
                _target[ArmRSide] = -0.2f;
                _target[ForearmR] = -0.1f;
                _target[SwordTilt] = alongArm;
                _target[SwordYaw] = 0f;
                _target[ArmL] = -0.4f;
                _target[ArmLSide] = 0.2f;
                _target[ForearmL] = -0.5f;
                _target[Twist] = -0.7f;
                _target[Lean] = 0.45f;
                _target[Bob] = -0.06f;
                _target[ThighL] = -0.65f;
                _target[ThighR] = 0.5f;
                _target[ShinR] = 0.4f;
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

    /// <summary>Set for someone on a bench: the hips come down to the seat and the legs fold.</summary>
    public bool Sitting { get; set; }

    private void SitPose()
    {
        var breath = Mathf.Sin(_phase);
        _target[Bob] = -0.37f + breath * 0.008f;
        _target[ThighL] = 1.48f;
        _target[ThighR] = 1.48f;
        _target[ShinL] = 1.5f;
        _target[ShinR] = 1.5f;
        _target[ArmL] = -0.55f;
        _target[ForearmL] = -0.9f;
        _target[ArmR] = -0.5f;
        _target[ForearmR] = -0.85f;
        _target[Lean] = 0.1f + breath * 0.01f;
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

    /// <summary>
    /// The dagger attacks. The flurry drives the right blade forward, then the left, the body
    /// twisting behind each. The leap crouches, springs with both blades raised over the head, and
    /// drives them down together as it lands.
    /// </summary>
    private void DaggerPose(AttackKind kind, int tick)
    {
        const float alongArm = -1.5708f;
        var attack = Tuning.Attack(kind);
        if (kind == AttackKind.DaggerFlurry)
        {
            // Each hand draws back to the ribs, then punches the blade straight out at the hit.
            var left = tick > attack.HitAt + 1;
            var strike = left ? attack.SecondHitAt : attack.HitAt;
            var drawn = tick < strike - 1;
            _target[Twist] = left ? (drawn ? -0.3f : 0.6f) : (drawn ? 0.4f : -0.6f);
            _target[Lean] = drawn ? 0.1f : 0.38f;
            _target[ThighL] = -0.75f;
            _target[ThighR] = 0.45f;
            _target[ShinL] = 0.3f;
            _target[ShinR] = 0.45f;
            _target[Bob] = drawn ? -0.06f : -0.13f;
            var (arm, side, forearm, tilt, yaw, other, otherForearm) = left
                ? (ArmL, ArmLSide, ForearmL, OffTilt, OffYaw, ArmR, ForearmR)
                : (ArmR, ArmRSide, ForearmR, SwordTilt, SwordYaw, ArmL, ForearmL);
            _target[arm] = drawn ? -0.35f : 1.55f;
            _target[side] = drawn ? 0.25f : -0.05f;
            _target[forearm] = drawn ? -2.0f : -0.05f;
            _target[tilt] = alongArm;
            _target[yaw] = 0f;
            // The free hand pulls back to guard the chest.
            _target[other] = 0.4f;
            _target[otherForearm] = -1.9f;
            if (tick == strike && _lastAttackTick < strike)
            {
                _sparks.Amount = 16;
                _sparks.Restart();
                _sparks.Emitting = true;
            }

            return;
        }

        var hit = attack.HitAt;
        if (tick < 4)
        {
            // Crouch to spring, blades drawn back.
            _target[Bob] = -0.22f;
            _target[Lean] = 0.45f;
            _target[ThighL] = -0.9f;
            _target[ThighR] = -0.9f;
            _target[ShinL] = 1.2f;
            _target[ShinR] = 1.2f;
            _target[ArmR] = -0.6f;
            _target[ArmL] = -0.6f;
            _target[ForearmR] = -0.8f;
            _target[ForearmL] = -0.8f;
        }
        else if (tick < hit - 3)
        {
            // In the air: both daggers raised high in front of the head, points down, knees drawn up.
            _target[Lean] = -0.1f;
            _target[ArmR] = 2.5f;
            _target[ArmL] = 2.5f;
            _target[ArmRSide] = 0.2f;
            _target[ArmLSide] = 0.2f;
            _target[ForearmR] = -0.7f;
            _target[ForearmL] = -0.7f;
            _target[SwordTilt] = 2.6f;
            _target[OffTilt] = 2.6f;
            _target[ThighL] = -1.1f;
            _target[ThighR] = -0.5f;
            _target[ShinL] = 1.3f;
            _target[ShinR] = 0.9f;
        }
        else
        {
            // The strike: both blades driven down in front, the body folded over them, one knee down.
            var recover = Mathf.Clamp((tick - hit) / (float)(attack.Total - hit), 0f, 1f);
            _target[Lean] = 0.85f * (1f - recover * 0.7f);
            _target[Bob] = -0.3f * (1f - recover);
            _target[ArmR] = 1.0f;
            _target[ArmL] = 1.0f;
            _target[ArmRSide] = -0.2f;
            _target[ArmLSide] = -0.2f;
            _target[ForearmR] = -0.2f;
            _target[ForearmL] = -0.2f;
            _target[SwordTilt] = 1.9f;
            _target[OffTilt] = 1.9f;
            _target[ThighL] = -0.9f * (1f - recover);
            _target[ThighR] = 0.4f * (1f - recover);
            _target[ShinL] = 0.6f * (1f - recover);
            _target[ShinR] = 1.6f * (1f - recover);
        }

        if (tick >= hit && _lastAttackTick < hit)
        {
            _sparks.Amount = 48;
            _sparks.Restart();
            _sparks.Emitting = true;
        }
    }

    /// <summary>A moment of a dagger attack: where each hand is, and where each blade points, in the rig's own space.</summary>
    private readonly record struct DaggerKey(int Tick, Vector3 Right, Vector3 Left, Vector3 BladeR, Vector3 BladeL);

    private static readonly Vector3 Ahead = new(0, 0.1f, -1f);
    private static readonly Vector3 PointDown = new(0, -0.75f, -0.66f);

    /// <summary>
    /// The flurry: the right hand draws back to the ribs and punches its blade straight out at the
    /// first hit, then snaps back to guard while the left does the same for the second.
    /// </summary>
    private static readonly DaggerKey[] FlurryKeys =
    {
        new(0, new(0.2f, 1.2f, -0.28f), new(-0.2f, 1.2f, -0.28f), Ahead, Ahead),
        new(3, new(0.24f, 1.12f, 0.04f), new(-0.16f, 1.28f, -0.3f), new(0, 0.2f, -1f), new(0.2f, 0.4f, -1f)),
        new(6, new(0.04f, 1.3f, -0.78f), new(-0.18f, 1.25f, -0.18f), new(0, 0f, -1f), new(0.3f, 0.5f, -1f)),
        new(8, new(0.14f, 1.27f, -0.42f), new(-0.22f, 1.13f, 0.02f), Ahead, new(0, 0.2f, -1f)),
        new(13, new(0.2f, 1.25f, -0.2f), new(-0.04f, 1.3f, -0.78f), new(-0.3f, 0.5f, -1f), new(0, 0f, -1f)),
        new(16, new(0.2f, 1.22f, -0.26f), new(-0.16f, 1.27f, -0.44f), Ahead, Ahead),
        new(24, new(0.2f, 1.2f, -0.28f), new(-0.2f, 1.2f, -0.28f), Ahead, Ahead),
    };

    /// <summary>
    /// The leap: a crouch with both blades swept low behind, then in the air both hands rise above
    /// the head in a reverse grip, points down, and cock back further at the top; at the hit both
    /// blades are driven down together into what stands in front, and stay there a moment.
    /// </summary>
    private static readonly DaggerKey[] LeapKeys =
    {
        new(0, new(0.22f, 1.15f, -0.25f), new(-0.22f, 1.15f, -0.25f), Ahead, Ahead),
        new(3, new(0.3f, 0.85f, 0.2f), new(-0.3f, 0.85f, 0.2f), new(0.2f, -0.4f, 1f), new(-0.2f, -0.4f, 1f)),
        new(9, new(0.16f, 2.02f, -0.12f), new(-0.16f, 2.02f, -0.12f), PointDown, PointDown),
        new(22, new(0.15f, 2.05f, -0.04f), new(-0.15f, 2.05f, -0.04f), new(0, -0.6f, -0.8f), new(0, -0.6f, -0.8f)),
        new(27, new(0.14f, 1.98f, 0.14f), new(-0.14f, 1.98f, 0.14f), new(0, -0.2f, -1f), new(0, -0.2f, -1f)),
        new(30, new(0.1f, 0.9f, -0.58f), new(-0.1f, 0.9f, -0.58f), new(0, -0.94f, -0.34f), new(0, -0.94f, -0.34f)),
        new(38, new(0.11f, 0.9f, -0.56f), new(-0.11f, 0.9f, -0.56f), new(0, -0.94f, -0.34f), new(0, -0.94f, -0.34f)),
        new(50, new(0.22f, 1.15f, -0.25f), new(-0.22f, 1.15f, -0.25f), Ahead, Ahead),
    };

    /// <summary>
    /// The staff, keyed like the daggers: the right hand holds it and its skull points where the key
    /// says; the left hand takes hold further up. A fireball: drawn back, then thrust skull first at
    /// the target. A wall: raised high in both hands, then driven butt first into the ground ahead.
    /// </summary>
    private static readonly DaggerKey[] StaffBoltKeys =
    {
        new(0, new(0.24f, 1.12f, -0.22f), new(-0.05f, 1.3f, -0.3f), new(0, 1f, -0.35f), Vector3.Zero),
        new(8, new(0.28f, 1.42f, 0.08f), new(0.02f, 1.62f, 0.02f), new(0, 1f, 0.3f), Vector3.Zero),
        new(12, new(0.14f, 1.36f, -0.5f), new(0.0f, 1.42f, -0.78f), new(0, 0.3f, -1f), Vector3.Zero),
        new(20, new(0.15f, 1.34f, -0.48f), new(0.0f, 1.4f, -0.76f), new(0, 0.32f, -1f), Vector3.Zero),
        new(30, new(0.24f, 1.12f, -0.22f), new(-0.05f, 1.3f, -0.3f), new(0, 1f, -0.35f), Vector3.Zero),
    };

    private static readonly DaggerKey[] StaffWallKeys =
    {
        new(0, new(0.24f, 1.12f, -0.22f), new(-0.05f, 1.3f, -0.3f), new(0, 1f, -0.35f), Vector3.Zero),
        new(14, new(0.12f, 1.95f, -0.12f), new(-0.02f, 2.15f, -0.18f), new(0, 1f, -0.15f), Vector3.Zero),
        new(18, new(0.12f, 2.0f, -0.08f), new(-0.02f, 2.2f, -0.14f), new(0, 1f, -0.05f), Vector3.Zero),
        new(22, new(0.1f, 0.78f, -0.6f), new(-0.02f, 1.08f, -0.64f), new(0, 1f, -0.12f), Vector3.Zero),
        new(36, new(0.1f, 0.8f, -0.6f), new(-0.02f, 1.1f, -0.64f), new(0, 1f, -0.12f), Vector3.Zero),
        new(50, new(0.24f, 1.12f, -0.22f), new(-0.05f, 1.3f, -0.3f), new(0, 1f, -0.35f), Vector3.Zero),
    };

    /// <summary>
    /// A player's dagger and staff attacks place the hands exactly, from keys in the rig's own space
    /// eased one into the next, and turn each weapon to point where its key says. The body follows
    /// the joint pose underneath.
    /// </summary>
    private void UpdateDaggers(in MotionState state, float delta)
    {
        if (_look.Goblin || _look.Weapon is not ("dagger" or "staff"))
        {
            _daggerReach = 0f;
            return;
        }

        var attacking = state.Attack is AttackKind.DaggerFlurry or AttackKind.DaggerLeap or AttackKind.StaffBolt or AttackKind.StaffWall;
        _daggerReach = Mathf.MoveToward(_daggerReach, attacking ? 1f : 0f, delta * (attacking ? 14f : 5f));
        if (_daggerReach <= 0f)
        {
            return;
        }

        var keys = state.Attack switch
        {
            AttackKind.DaggerLeap => LeapKeys,
            AttackKind.StaffBolt => StaffBoltKeys,
            AttackKind.StaffWall => StaffWallKeys,
            _ when _look.Weapon == "staff" => StaffBoltKeys,
            _ => FlurryKeys,
        };
        var tick = attacking ? state.AttackTick : keys[^1].Tick;
        var next = 1;
        while (next < keys.Length - 1 && keys[next].Tick <= tick)
        {
            next++;
        }

        var from = keys[next - 1];
        var to = keys[next];
        var t = Mathf.Clamp((tick - from.Tick) / (float)Mathf.Max(1, to.Tick - from.Tick), 0f, 1f);
        t = t * t * (3f - 2f * t);
        var w = _daggerReach * _daggerReach * (3f - 2f * _daggerReach);
        var basis = Root.GlobalBasis;
        var reach = 0.3f * Root.Scale.X;
        Reach(_armR, _forearmR, Root.ToGlobal(from.Right.Lerp(to.Right, t)), basis * new Vector3(1f, -1f, 0.4f), reach, w);
        Reach(_armL, _forearmL, Root.ToGlobal(from.Left.Lerp(to.Left, t)), basis * new Vector3(-1f, -1f, 0.4f), reach, w);
        Point(_sword, basis * from.BladeR.Lerp(to.BladeR, t), basis * Vector3.Right, w);
        if (_look.Weapon == "dagger")
        {
            Point(_offhand, basis * from.BladeL.Lerp(to.BladeL, t), basis * Vector3.Left, w);
        }
    }

    /// <summary>Turns a blade so its point (-Z) runs along a direction, its edge as near the hint as it can.</summary>
    private void Point(Node3D blade, Vector3 direction, Vector3 edge, float weight)
    {
        var z = -direction.Normalized();
        var x = edge - z * z.Dot(edge);
        x = x.LengthSquared() < 1e-6f ? z.Cross(Vector3.Up) : x.Normalized();
        var aimed = new Basis(x, z.Cross(x), z);
        var held = blade.GlobalTransform;
        blade.GlobalTransform = new Transform3D(Blend(held.Basis, aimed, weight).Scaled(Vector3.One * Root.Scale.X), held.Origin);
    }

    /// <summary>
    /// The club. A swing: the club drawn back over the right shoulder, then swept flat across the
    /// front from right to left with the whole body turning into it. A spin: both hands on the club,
    /// arms out level, a wind-up to the right and one full turn on the spot, then a stagger.
    /// </summary>
    private void ClubPose(AttackKind kind, int tick)
    {
        const float alongArm = -1.5708f;
        var attack = Tuning.Attack(kind);
        var hit = attack.HitAt;
        if (kind == AttackKind.ClubSwing)
        {
            var swung = tick >= hit - 3;
            var recover = Mathf.Clamp((tick - hit) / (float)(attack.Total - hit), 0f, 1f);
            _target[Twist] = swung ? -0.95f + recover * 0.6f : 0.85f;
            _target[Lean] = swung ? 0.3f - recover * 0.2f : -0.1f;
            _target[ArmR] = swung ? 1.45f - recover * 0.5f : 1.9f;
            _target[ArmRSide] = swung ? -0.55f : 1.0f;
            _target[ForearmR] = swung ? -0.15f : -1.6f;
            _target[SwordTilt] = swung ? alongArm + 0.25f : 0.4f;
            _target[SwordYaw] = swung ? 0.5f : -0.3f;
            _target[ArmL] = swung ? 0.3f : 0.9f;
            _target[ArmLSide] = swung ? 0.4f : -0.2f;
            _target[ForearmL] = -0.9f;
            _target[ThighL] = swung ? -0.55f : -0.2f;
            _target[ThighR] = swung ? 0.45f : 0.25f;
            _target[ShinR] = swung ? 0.35f : 0.2f;
            _target[Bob] = swung ? -0.08f : -0.02f;
            if (tick >= hit && _lastAttackTick < hit)
            {
                _sparks.Amount = 30;
                _sparks.Restart();
                _sparks.Emitting = true;
            }

            return;
        }

        // The spin: held out level in both hands.
        _target[ArmR] = 1.45f;
        _target[ArmRSide] = 1.25f;
        _target[ForearmR] = -0.1f;
        _target[ArmL] = 1.35f;
        _target[ArmLSide] = -0.55f;
        _target[ForearmL] = -0.6f;
        _target[SwordTilt] = alongArm;
        _target[SwordYaw] = 0f;
        _target[Lean] = tick < 12 ? 0.05f : 0.2f;
        _target[Twist] = tick < 12 ? 0.9f : -0.2f;
        _target[ThighL] = -0.45f;
        _target[ThighR] = 0.45f;
        _target[ShinL] = 0.4f;
        _target[ShinR] = 0.4f;
        _target[Bob] = -0.14f;
        if (tick >= hit && _lastAttackTick < hit)
        {
            _sparks.Amount = 46;
            _sparks.Restart();
            _sparks.Emitting = true;
        }
    }

    /// <summary>The spin turns the whole body once round: wind-up, a fast turn through the hit, and a settle.</summary>
    private void UpdateSpin(in MotionState state)
    {
        if (state.Attack == AttackKind.Whirlwind)
        {
            // Whirlwind: whole turns, one for each blow, slowing at the end.
            var whirl = Tuning.Attack(AttackKind.Whirlwind);
            var turns = Mathf.Min(state.AttackTick, whirl.LastHitAt + 6) / (float)whirl.HitEvery;
            _hips.Rotation = new Vector3(0, -Mathf.Tau * turns, 0);
            return;
        }

        if (state.Attack != AttackKind.ClubSpin)
        {
            if (_bow is null)
            {
                _hips.Rotation = Vector3.Zero;
            }

            return;
        }

        const int start = 12, end = 36;
        var t = Mathf.Clamp((state.AttackTick - start) / (float)(end - start), 0f, 1f);
        var eased = t * t * (3f - 2f * t);
        _hips.Rotation = new Vector3(0, -Mathf.Tau * eased + (state.AttackTick < start ? 0.5f * state.AttackTick / start : 0.5f * (1f - eased)), 0);
    }

    /// <summary>
    /// The staff. A fireball: the staff raised and thrust forward as the fire leaves its skull. A
    /// wall of fire: lifted high in both hands and driven down into the ground in front.
    /// </summary>
    private void StaffPose(AttackKind kind, int tick)
    {
        var hit = Tuning.Attack(kind).HitAt;
        if (kind == AttackKind.StaffBolt)
        {
            WeaponPose(tick < hit - 2 ? WeaponMove.StaffRaise : WeaponMove.StaffThrust);
        }
        else
        {
            WeaponPose(tick < hit - 4 ? WeaponMove.StaffRaise : WeaponMove.StaffPlant);
        }

        if (tick >= hit && _lastAttackTick < hit)
        {
            _sparks.Amount = kind == AttackKind.StaffWall ? 40 : 20;
            _sparks.Restart();
            _sparks.Emitting = true;
        }
    }

    /// <summary>Rising Slash: low with the blade trailing behind, then a cut from the ground up over the head.</summary>
    private void RisingPose(int tick)
    {
        var attack = Tuning.Attack(AttackKind.RisingSlash);
        var swung = tick >= attack.HitAt - 2;
        var recover = Mathf.Clamp((tick - attack.HitAt) / (float)(attack.Total - attack.HitAt), 0f, 1f);
        _target[Lean] = swung ? -0.35f + recover * 0.3f : 0.5f;
        _target[Twist] = swung ? -0.3f : 0.7f;
        _target[ArmR] = swung ? 3.0f - recover * 1.2f : -0.7f;
        _target[ArmRSide] = swung ? 0.15f : 0.35f;
        _target[ForearmR] = swung ? -0.2f : -0.4f;
        _target[SwordTilt] = swung ? 0.3f : 2.2f;
        _target[SwordYaw] = 0f;
        _target[ArmL] = swung ? 0.9f : -0.4f;
        _target[ForearmL] = -0.7f;
        _target[ThighL] = swung ? -0.2f : -0.9f;
        _target[ThighR] = swung ? 0.3f : 0.6f;
        _target[ShinL] = swung ? 0.2f : 1.0f;
        _target[ShinR] = swung ? 0.6f : 0.9f;
        _target[Bob] = swung ? 0.02f : -0.24f;
        var progress = (tick - (attack.HitAt - 4)) / 11f;
        _slash.Visible = progress is >= 0f and <= 1.6f;
        if (_slash.Visible)
        {
            _slashMaterial.SetShaderParameter("head", Mathf.Min(progress * 1.5f, 1.8f));
            _slashMaterial.SetShaderParameter("strength", progress < 1f ? 1f : 2.6f - progress * 1.6f);
            _slashMaterial.SetShaderParameter("inner_color", new Color("f77622"));
            _slashMaterial.SetShaderParameter("tip_color", new Color("fee761"));
            _slash.Rotation = new Vector3(0f, 0f, 1.5708f);
            _slash.Scale = Vector3.One * 1.3f;
        }

        if (tick >= attack.HitAt && _lastAttackTick < attack.HitAt)
        {
            _sparks.Amount = 40;
            _sparks.Restart();
            _sparks.Emitting = true;
        }
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
        var hunch = _look.Werewolf ? 0.62f : _look.Orc ? 0.12f : _look.Goblin ? 0.32f : 0f;
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
        _offhand.Rotation = new Vector3(_pose[OffTilt], _pose[OffYaw], 0);
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

        var clawed = Model(_look.Werewolf ? "were_paw" : _look.Sentinel ? "sentinel_foot" : _look.Orc ? "orc_boot" : "gob_foot");
        clawed.Position = new Vector3(0, foot + 0.045f, 0);
        shin.AddChild(clawed);
    }

    private void BuildArm(Node3D arm, Node3D forearm, float x, Color cloth, Color skin, Color glove)
    {
        var goblin = _look.Goblin;
        _torso.AddChild(arm);
        arm.Position = new Vector3(x, _look.Werewolf ? 0.47f : _look.Orc ? 0.43f : goblin ? 0.36f : 0.45f, _look.Werewolf ? -0.04f : 0f);
        var top = _look.Werewolf ? 0.1f : _look.Orc ? (_look.Female ? 0.068f : 0.082f) : goblin ? 0.09f : 0.058f;
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
            var hand = Model(_look.Werewolf ? "were_hand" : _look.Sentinel ? "sentinel_hand" : "gob_hand");
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
