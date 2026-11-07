using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// A four-legged animal built from rounded parts: cow, goat, deer, pig or sheep. Legs swing in a
/// diagonal gait that quickens into a gallop, the head dips to graze when it stands, and the ears
/// and tail twitch on their own.
/// </summary>
public sealed class AnimalView : IEnemyView
{
    private readonly Node3D _body = new();
    private readonly Node3D _head = new();
    private readonly Node3D _tail = new();
    private readonly List<(Node3D Leg, float Phase)> _legs = new();
    private readonly List<Node3D> _ears = new();
    private readonly float _height;
    private Vector3 _lastPosition;
    private short _lastHealth;
    private bool _first = true;
    private float _phase;
    private float _clock;
    private float _graze;
    private float _speed;

    public Node3D Root { get; } = new();
    public int DamageTaken { get; private set; }

    public AnimalView(EnemyDef def, int id)
    {
        Root.AddChild(_body);
        var spec = Spec(def.Id, id);
        _height = spec.Height;
        _body.Position = new Vector3(0, spec.Height, 0);
        Ellipsoid(_body, spec.Body, spec.Coat, Vector3.Zero);
        foreach (var (size, offset, color) in spec.Patches)
        {
            Ellipsoid(_body, size, color, offset);
        }

        _body.AddChild(_head);
        _head.Position = spec.Neck;

        // The neck runs from the chest to the head, so the head never floats.
        var chest = new Vector3(0, spec.Body.Y * 0.35f, -spec.Body.Z * 0.72f);
        var neck = Ellipsoid(_body, Vector3.One, spec.Coat, (chest + spec.Neck) / 2f);
        var length = chest.DistanceTo(spec.Neck);
        neck.Scale = new Vector3(spec.Head.X * 0.85f, length * 0.62f, spec.Head.X * 0.95f);
        neck.Rotation = new Vector3(Mathf.Atan2(spec.Neck.Z - chest.Z, spec.Neck.Y - chest.Y), 0, 0);
        Ellipsoid(_head, spec.Head, spec.Face, new Vector3(0, 0.05f, -spec.Head.Z * 0.7f));
        Ellipsoid(_head, spec.Muzzle, spec.Nose, new Vector3(0, -0.02f, -spec.Head.Z * 1.55f));
        foreach (var side in new[] { -1f, 1f })
        {
            var eye = Ellipsoid(_head, Vector3.One * 0.035f, new Color("15161c"), new Vector3(side * spec.Head.X * 0.85f, spec.Head.Y * 0.45f, -spec.Head.Z * 1.0f));
            eye.Scale = new Vector3(0.025f, 0.035f, 0.025f);
            var ear = new Node3D { Position = new Vector3(side * spec.Head.X * 0.95f, spec.Head.Y * 0.8f, -spec.Head.Z * 0.35f) };
            Ellipsoid(ear, new Vector3(0.05f, 0.03f, 0.1f) * spec.EarSize, spec.Face, new Vector3(side * 0.06f * spec.EarSize, 0, 0));
            ear.Rotation = new Vector3(0, 0, side * (spec.Floppy ? -0.6f : 0.35f));
            _head.AddChild(ear);
            _ears.Add(ear);
            if (spec.Horns is { } horn)
            {
                var piece = Part(new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.035f, Height = horn }, spec.HornColor,
                    new Vector3(side * spec.Head.X * 0.5f, spec.Head.Y * 1.05f, -spec.Head.Z * 0.2f));
                piece.Rotation = new Vector3(spec.HornSweep, 0, side * -0.35f);
                _head.AddChild(piece);
            }

            if (spec.Antlers)
            {
                var antler = new Node3D { Position = new Vector3(side * 0.06f, spec.Head.Y * 1.1f, -0.02f), Rotation = new Vector3(0.25f, 0, side * -0.45f) };
                antler.AddChild(Part(new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.022f, Height = 0.42f }, spec.HornColor, new Vector3(0, 0.2f, 0)));
                for (var tine = 0; tine < 3; tine++)
                {
                    var spike = Part(new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.014f, Height = 0.16f }, spec.HornColor, new Vector3(0, 0.12f + tine * 0.11f, -0.05f));
                    spike.Rotation = new Vector3(-0.8f, 0, 0);
                    antler.AddChild(spike);
                }

                _head.AddChild(antler);
            }
        }

        if (spec.Beard)
        {
            var beard = Part(new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.005f, Height = 0.16f }, new Color("e8e4dc"), new Vector3(0, -spec.Head.Y * 1.1f, -spec.Head.Z * 1.2f));
            _head.AddChild(beard);
        }

        var legLength = spec.Height - spec.Body.Y * 0.6f;
        foreach (var (x, z) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
        {
            var hip = new Node3D { Position = new Vector3(x * spec.Body.X * 0.6f, -spec.Body.Y * 0.4f, z * spec.Body.Z * 0.62f) };
            Ellipsoid(hip, new Vector3(spec.LegWidth * 2.2f, spec.Body.Y * 0.55f, spec.LegWidth * 2.6f), spec.Coat, new Vector3(0, 0.02f, 0));
            hip.AddChild(Part(new CylinderMesh { TopRadius = spec.LegWidth * 1.3f, BottomRadius = spec.LegWidth, Height = legLength }, spec.Legs, new Vector3(0, -legLength / 2f, 0)));
            hip.AddChild(Part(new CylinderMesh { TopRadius = spec.LegWidth * 1.05f, BottomRadius = spec.LegWidth * 1.2f, Height = 0.07f }, new Color("2a2224"), new Vector3(0, -legLength + 0.03f, 0)));
            _body.AddChild(hip);
            _legs.Add((hip, x * z > 0 ? 0f : Mathf.Pi));
        }

        _tail.Position = new Vector3(0, spec.Body.Y * 0.3f, spec.Body.Z * 0.95f);
        _tail.AddChild(Part(new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.01f, Height = spec.Tail }, spec.Coat.Darkened(0.1f), new Vector3(0, -spec.Tail / 2f, 0)));
        _tail.Rotation = new Vector3(spec.Tail < 0.2f ? -2.2f : 0.25f, 0, 0);
        _body.AddChild(_tail);
        Root.Scale = Vector3.One * def.Size;
    }

    private sealed record Shape(
        float Height, Vector3 Body, Color Coat, Vector3 Neck, Vector3 Head, Color Face, Vector3 Muzzle, Color Nose, Color Legs,
        float LegWidth, float Tail, float EarSize, bool Floppy, float? Horns, float HornSweep, Color HornColor, bool Antlers, bool Beard,
        (Vector3 Size, Vector3 Offset, Color Color)[] Patches);

    private static Shape Spec(string id, int seed)
    {
        var none = System.Array.Empty<(Vector3, Vector3, Color)>();
        return id switch
        {
            "cow" => new Shape(1.05f, new Vector3(0.42f, 0.4f, 0.85f), new Color("f2eee6"), new Vector3(0, 0.22f, -0.85f),
                new Vector3(0.2f, 0.2f, 0.22f), new Color("f2eee6"), new Vector3(0.16f, 0.13f, 0.12f), new Color("e8a8a0"), new Color("f2eee6"),
                0.07f, 0.6f, 1.1f, false, 0.14f, -0.3f, new Color("e8dcc0"), false, false,
                new[] { (new Vector3(0.3f, 0.26f, 0.3f), new Vector3(0.22f, 0.12f, -0.2f), new Color("2a2628")),
                        (new Vector3(0.28f, 0.24f, 0.32f), new Vector3(-0.2f, 0.06f, 0.35f), new Color("2a2628")),
                        (new Vector3(0.12f, 0.08f, 0.14f), new Vector3(0, -0.38f, 0.4f), new Color("e8a8a0")) }),
            "goat" => new Shape(0.66f, new Vector3(0.24f, 0.24f, 0.5f), new Color("dcd6cc"), new Vector3(0, 0.22f, -0.5f),
                new Vector3(0.12f, 0.12f, 0.15f), new Color("dcd6cc"), new Vector3(0.08f, 0.07f, 0.08f), new Color("8a7a70"), new Color("b8b0a4"),
                0.04f, 0.12f, 0.9f, false, 0.26f, 0.9f, new Color("7a6a5a"), false, true, none),
            "deer" => new Shape(0.95f, new Vector3(0.25f, 0.26f, 0.6f), new Color("a8703c"), new Vector3(0, 0.36f, -0.58f),
                new Vector3(0.12f, 0.12f, 0.16f), new Color("a8703c"), new Vector3(0.08f, 0.07f, 0.09f), new Color("2a2224"), new Color("8a5a30"),
                0.035f, 0.12f, 1.4f, false, null, 0f, new Color("d8c8a8"), seed % 2 == 0, false,
                new[] { (new Vector3(0.2f, 0.12f, 0.45f), new Vector3(0, -0.16f, 0), new Color("eadcc8")),
                        (new Vector3(0.1f, 0.1f, 0.06f), new Vector3(0, 0.08f, 0.58f), new Color("f4ece0")) }),
            "pig" => new Shape(0.5f, new Vector3(0.3f, 0.27f, 0.52f), new Color("f0b0aa"), new Vector3(0, 0.08f, -0.52f),
                new Vector3(0.18f, 0.16f, 0.15f), new Color("f0b0aa"), new Vector3(0.09f, 0.08f, 0.06f), new Color("e08a88"), new Color("e8a29c"),
                0.055f, 0.12f, 1.3f, true, null, 0f, Colors.White, false, false, none),
            _ => new Shape(0.62f, new Vector3(0.34f, 0.32f, 0.5f), new Color("f6f2ea"), new Vector3(0, 0.16f, -0.52f),
                new Vector3(0.11f, 0.12f, 0.14f), new Color("2a2628"), new Vector3(0.07f, 0.07f, 0.07f), new Color("2a2628"), new Color("2a2628"),
                0.035f, 0.12f, 1f, true, null, 0f, Colors.White, false, false,
                new[] { (new Vector3(0.24f, 0.24f, 0.24f), new Vector3(0.14f, 0.16f, -0.2f), new Color("f6f2ea")),
                        (new Vector3(0.24f, 0.24f, 0.24f), new Vector3(-0.14f, 0.16f, 0.15f), new Color("f6f2ea")),
                        (new Vector3(0.22f, 0.2f, 0.22f), new Vector3(0.12f, 0.12f, 0.35f), new Color("eee8de")) }),
        };
    }

    private static MeshInstance3D Ellipsoid(Node3D parent, Vector3 radius, Color color, Vector3 position)
    {
        var part = Part(new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 12, Rings = 7 }, color, position);
        part.Scale = radius;
        parent.AddChild(part);
        return part;
    }

    private static MeshInstance3D Part(Mesh mesh, Color color, Vector3 position) => new()
    {
        Mesh = mesh,
        Position = position,
        MaterialOverride = PlayerRig.ToonMaterial(color),
    };

    public void Update(in EnemyView enemy, Vector3 position, float delta, Sfx sfx)
    {
        var moved = (position - _lastPosition).Length() / Mathf.Max(delta, 0.001f);
        _speed = Mathf.Lerp(_speed, moved > 20f ? 0f : moved, 1f - Mathf.Exp(-10f * delta));
        _lastPosition = position;
        _clock += delta;
        DamageTaken = !_first && enemy.Health < _lastHealth ? _lastHealth - enemy.Health : 0;
        _first = false;
        _lastHealth = enemy.Health;

        Root.Position = position;
        Root.Rotation = new Vector3(0, enemy.Facing, enemy.Action == EnemyAction.Dead ? Mathf.Pi / 2f : 0f);

        // A diagonal walk that stretches into a gallop as the animal speeds up.
        var gallop = Mathf.Clamp(_speed / 5f, 0f, 1f);
        _phase += delta * (4f + _speed * 2.2f);
        var swing = _speed > 0.3f ? 0.35f + gallop * 0.45f : 0f;
        foreach (var (leg, offset) in _legs)
        {
            var target = Mathf.Sin(_phase + offset) * swing;
            leg.Rotation = new Vector3(Mathf.Lerp(leg.Rotation.X, target, 1f - Mathf.Exp(-20f * delta)), 0, 0);
        }

        var bob = _speed > 0.3f ? Mathf.Abs(Mathf.Sin(_phase)) * (0.03f + gallop * 0.08f) : Mathf.Sin(_clock * 1.6f) * 0.006f;
        var shake = enemy.HitFlash > 0 ? (float)GD.RandRange(-0.04, 0.04) : 0f;
        _body.Position = new Vector3(shake, _height + bob, 0);
        _body.Rotation = new Vector3(gallop * Mathf.Sin(_phase * 2f) * 0.06f, 0, 0);

        // Standing animals lower their heads to graze now and then.
        var grazing = _speed < 0.3f && Mathf.Sin(_clock * 0.35f + Root.Position.X) > 0.1f;
        _graze = Mathf.Lerp(_graze, grazing ? 1f : 0f, 1f - Mathf.Exp(-2.5f * delta));
        _head.Rotation = new Vector3(-_graze * 0.95f + Mathf.Sin(_clock * 2.1f) * 0.03f, Mathf.Sin(_clock * 0.5f) * 0.15f * (1f - _graze), 0);
        _tail.Rotation = _tail.Rotation with { Z = Mathf.Sin(_clock * 3.2f + Root.Position.Z) * 0.35f };
        for (var i = 0; i < _ears.Count; i++)
        {
            var twitch = Mathf.Sin(_clock * 0.9f + i * 2f) > 0.97f ? 0.4f : 0f;
            _ears[i].Rotation = _ears[i].Rotation with { X = twitch };
        }

        if (DamageTaken > 0)
        {
            sfx.Play("slime_hit", position + Vector3.Up);
        }
    }
}
