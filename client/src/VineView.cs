using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// A vinelasher: a fat green bulb rooted in the ground with a red maw at the top, ringed by thorny vines that
/// sway idly. Winding up a lash, one vine draws back and rises; the lash throws it straight out along the
/// ground to its full reach. The Stranglethorn throws three at once and wears a ring of red thorns.
/// </summary>
public sealed class VineView : IEnemyView
{
    private readonly EnemyDef _def;
    private readonly Node3D _body = new();
    private readonly MeshInstance3D _maw;
    private readonly StandardMaterial3D _skin;
    private readonly List<(Node3D Root, List<Node3D> Segments, float Phase)> _vines = new();
    private readonly float _phase = GD.Randf() * Mathf.Tau;
    private float _lash;
    private float _rear;
    private float _fall;
    private short _lastHealth;
    private bool _first = true;
    private EnemyAction _lastAction;

    private const int Segments = 8;
    private const float SegmentLength = 0.42f;

    public Node3D Root { get; } = new();
    public int DamageTaken { get; private set; }

    public VineView(EnemyDef def)
    {
        _def = def;
        Root.AddChild(_body);
        _skin = new StandardMaterial3D { AlbedoColor = new Color(def.Rare ? "3e6a3a" : "4e8a3e"), Roughness = 0.8f };
        var dark = new StandardMaterial3D { AlbedoColor = new Color("265c42"), Roughness = 0.9f };
        var red = new StandardMaterial3D { AlbedoColor = new Color("a22633") };
        var thorn = new StandardMaterial3D { AlbedoColor = new Color(def.Rare ? "e43b44" : "e8dcc0") };

        // The bulb: wide at the root, a lip of leaves at the top round the maw.
        _body.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.55f, Height = 1.1f, RadialSegments = 12, Rings = 7 },
            Position = new Vector3(0, 0.55f, 0),
            Scale = new Vector3(1f, 1.15f, 1f),
            MaterialOverride = _skin,
        });
        for (var i = 0; i < 6; i++)
        {
            var a = i / 6f * Mathf.Tau;
            _body.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.32f, 0.04f, 0.55f) },
                Position = new Vector3(Mathf.Sin(a) * 0.38f, 1.12f, Mathf.Cos(a) * 0.38f),
                Rotation = new Vector3(-0.7f, a, 0),
                MaterialOverride = dark,
            });
        }

        _maw = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.26f, BottomRadius = 0.14f, Height = 0.14f, RadialSegments = 10 },
            Position = new Vector3(0, 1.18f, 0),
            MaterialOverride = red,
        };
        _body.AddChild(_maw);
        for (var i = 0; i < 8; i++)
        {
            var a = i / 8f * Mathf.Tau;
            _maw.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.025f, Height = 0.12f, RadialSegments = 4 },
                Position = new Vector3(Mathf.Sin(a) * 0.2f, 0.08f, Mathf.Cos(a) * 0.2f),
                MaterialOverride = thorn,
            });
        }

        // The vines: chains of segments from the bulb's foot, each with thorns.
        var count = def.Rare ? 7 : 5;
        for (var v = 0; v < count; v++)
        {
            var a = v / (float)count * Mathf.Tau;
            var root = new Node3D { Position = new Vector3(Mathf.Sin(a) * 0.45f, 0.35f, Mathf.Cos(a) * 0.45f), Rotation = new Vector3(0, a, 0) };
            _body.AddChild(root);
            var segments = new List<Node3D>();
            Node3D parent = root;
            for (var s = 0; s < Segments; s++)
            {
                var joint = new Node3D { Position = s == 0 ? Vector3.Zero : new Vector3(0, 0, SegmentLength) };
                parent.AddChild(joint);
                var width = 0.08f * (1f - s / (float)Segments * 0.7f);
                joint.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(width, width, SegmentLength) },
                    Position = new Vector3(0, 0, SegmentLength / 2f),
                    MaterialOverride = _skin,
                });
                if (s % 2 == 1)
                {
                    joint.AddChild(new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.02f, Height = 0.1f, RadialSegments = 4 },
                        Position = new Vector3(0, width * 0.7f, SegmentLength / 2f),
                        MaterialOverride = thorn,
                    });
                }

                segments.Add(joint);
                parent = joint;
            }

            _vines.Add((root, segments, v * 1.3f));
        }

        Root.Scale = Vector3.One * def.Size;
    }

    public void Update(in EnemyView enemy, Vector3 position, float delta, Sfx sfx)
    {
        DamageTaken = !_first && enemy.Health < _lastHealth ? _lastHealth - enemy.Health : 0;
        if (!_first && enemy.Action != _lastAction && sfx is not null)
        {
            if (enemy.Action == EnemyAction.Strike)
            {
                sfx.Play("air_slash", position + Vector3.Up, -6f);
            }
            else if (enemy.Action == EnemyAction.Dead)
            {
                sfx.Play("animal_fall", position, -2f);
            }
        }

        _first = false;
        _lastHealth = enemy.Health;
        _lastAction = enemy.Action;
        Root.Position = position;
        _body.Rotation = new Vector3(0, enemy.Facing, 0);
        var time = (float)Time.GetTicksMsec() / 1000f + _phase;
        var effect = _def.Abilities is { } list && enemy.Ability < list.Length ? list[enemy.Ability].Effect : "";
        var lashing = effect is "vineWhip" or "vineGrab";
        var winding = enemy.Action == EnemyAction.Cast && lashing;
        var striking = enemy.Action == EnemyAction.Strike && lashing;
        _rear = Mathf.Lerp(_rear, winding ? 1f : 0f, 1f - Mathf.Exp(-8f * delta));
        _lash = Mathf.Lerp(_lash, striking ? 1f : 0f, 1f - Mathf.Exp(-(striking ? 26f : 5f) * delta));
        var biting = enemy.Action is EnemyAction.Cast or EnemyAction.Strike && effect == "melee";
        _maw.Scale = biting ? new Vector3(1.4f, 1f, 1.4f) : Vector3.One * (1f + Mathf.Sin(time * 2f) * 0.05f);

        var dead = enemy.Action == EnemyAction.Dead;
        _fall = Mathf.MoveToward(_fall, dead ? 1f : 0f, delta * 2f);
        _body.Scale = new Vector3(1f, 1f - _fall * 0.6f, 1f);
        _skin.AlbedoColor = enemy.HitFlash > 0 ? Colors.White : new Color(_def.Rare ? "3e6a3a" : "4e8a3e");
        var reach = _def.Abilities is { } abilities ? Mathf.Max(1f, abilities[^1].Range) : 6f;
        for (var v = 0; v < _vines.Count; v++)
        {
            var (root, segments, phase) = _vines[v];
            // The lashing vines: the first one, and for the Stranglethorn the first three in a fan. They swing round
            // to the front (the figure's front is -Z), draw back and up as it winds up, then fly out flat.
            var lashes = _def.Rare ? v < 3 : v == 0;
            if (lashes && (winding || _lash > 0.02f))
            {
                var fan = _def.Rare ? (v - 1) * 0.42f : 0f;
                root.Rotation = new Vector3(0, Mathf.Pi + fan, 0);
                var stretch = 1f + _lash * (reach / (Segments * SegmentLength) - 1f);
                for (var s = 0; s < segments.Count; s++)
                {
                    segments[s].Rotation = new Vector3(-_rear * (s == 0 ? 1.2f : 0.25f) + _lash * (s == 0 ? 0.25f : 0.02f), 0, 0);
                    segments[s].Scale = new Vector3(1f, 1f, stretch);
                }

                continue;
            }

            root.Rotation = new Vector3(0, v / (float)_vines.Count * Mathf.Tau, 0);
            for (var s = 0; s < segments.Count; s++)
            {
                // Idle: each vine curls up from the ground and sways.
                var curl = s == 0 ? -0.6f : 0.18f;
                segments[s].Rotation = new Vector3(curl + Mathf.Sin(time * 1.4f + phase + s * 0.6f) * 0.12f, Mathf.Sin(time + phase + s) * 0.08f, 0);
                segments[s].Scale = Vector3.One;
            }
        }
    }
}
