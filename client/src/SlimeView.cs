using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>A jelly body that squashes and stretches from what the server says it is doing.</summary>
public sealed class SlimeView : IEnemyView
{
    private readonly MeshInstance3D _body;
    private readonly ShaderMaterial _material;
    private readonly Node3D _face = new();
    private readonly StandardMaterial3D _splashMaterial = new() { Roughness = 0.3f };
    private readonly CpuParticles3D _droplets;
    private readonly MeshInstance3D _puddle;
    private int _type = -1;
    private Vector3 _scale = Vector3.One;
    private float _lastY;
    private float _rise;
    private float _puddleTime;
    private EnemyAction _lastAction;
    private byte _lastFlash;
    private short _lastHealth;
    private bool _lastFighting;
    private bool _first = true;

    private readonly List<MeshInstance3D> _eyes = new();
    private readonly List<MeshInstance3D> _brows = new();
    private readonly MeshInstance3D _mouth;
    private readonly Node3D _crown;
    private float _blink;

    public Node3D Root { get; } = new();

    /// <summary>A little gold crown with five points and a gem, for the rare slime.</summary>
    private static Node3D BuildCrown()
    {
        var gold = new StandardMaterial3D { AlbedoColor = new Color("feae34"), Metallic = 0.6f, Roughness = 0.3f };
        var crown = new Node3D { Position = new Vector3(0, 0.66f, 0), Rotation = new Vector3(0.12f, 0, -0.18f) };
        crown.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.17f, BottomRadius = 0.15f, Height = 0.09f, RadialSegments = 10 },
            MaterialOverride = gold,
        });
        for (var i = 0; i < 5; i++)
        {
            var angle = i / 5f * Mathf.Tau;
            crown.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.045f, Height = 0.12f, RadialSegments = 4 },
                Position = new Vector3(Mathf.Sin(angle) * 0.15f, 0.1f, Mathf.Cos(angle) * 0.15f),
                MaterialOverride = gold,
            });
        }

        crown.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.035f, Height = 0.07f, RadialSegments = 6, Rings = 3 },
            Position = new Vector3(0, 0.01f, -0.16f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("2ce8f5"), EmissionEnabled = true, Emission = new Color("2ce8f5"), EmissionEnergyMultiplier = 0.6f },
        });
        return crown;
    }

    /// <summary>Damage shown when the last update found a hit, or zero.</summary>
    public int DamageTaken { get; private set; }

    public SlimeView()
    {

        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/slime.gdshader") };

        _body = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.6f, Height = 1.2f, RadialSegments = 16, Rings = 9 },
            MaterialOverride = _material,
            Position = new Vector3(0, 0.5f, 0),
        };
        Root.AddChild(_body);

        _body.AddChild(_face);
        // Big glossy eyes with a catch light, a small mouth, and brows that only show in a fight.
        var ink = new StandardMaterial3D { AlbedoColor = new Color("181425"), Roughness = 0.15f };
        var shine = new StandardMaterial3D { AlbedoColor = Colors.White, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        foreach (var x in new[] { -0.19f, 0.19f })
        {
            var eye = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.1f, Height = 0.2f, RadialSegments = 10, Rings = 6 },
                Position = new Vector3(x, 0.12f, -0.5f),
                Scale = new Vector3(0.9f, 1.25f, 0.6f),
                MaterialOverride = ink,
            };
            eye.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.035f, Height = 0.07f, RadialSegments = 6, Rings = 3 },
                Position = new Vector3(0.03f, 0.04f, -0.08f),
                MaterialOverride = shine,
            });
            _face.AddChild(eye);
            _eyes.Add(eye);
            var brow = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.16f, 0.035f, 0.04f) },
                Position = new Vector3(x, 0.3f, -0.5f),
                Rotation = new Vector3(0, 0, x < 0 ? -0.45f : 0.45f),
                MaterialOverride = ink,
                Visible = false,
            };
            _face.AddChild(brow);
            _brows.Add(brow);
        }

        _mouth = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.025f, Height = 0.12f, RadialSegments = 6, Rings = 2 },
            Position = new Vector3(0, -0.04f, -0.56f),
            Rotation = new Vector3(0, 0, Mathf.Pi / 2f),
            MaterialOverride = ink,
        };
        _face.AddChild(_mouth);

        _crown = BuildCrown();
        _crown.Visible = false;
        _body.AddChild(_crown);

        _droplets = new CpuParticles3D
        {
            Emitting = false,
            OneShot = true,
            Explosiveness = 1f,
            Amount = 22,
            Lifetime = 0.7f,
            Direction = Vector3.Up,
            Spread = 75f,
            InitialVelocityMin = 3f,
            InitialVelocityMax = 7f,
            Gravity = new Vector3(0, -16f, 0),
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.4f,
            Mesh = new SphereMesh { Radius = 0.09f, Height = 0.18f, RadialSegments = 5, Rings = 3 },
            MaterialOverride = _splashMaterial,
            Position = new Vector3(0, 0.1f, 0),
        };
        Root.AddChild(_droplets);

        _puddle = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 0.03f, RadialSegments = 14 },
            Position = new Vector3(0, 0.03f, 0),
            Visible = false,
            MaterialOverride = _splashMaterial,
        };
        Root.AddChild(_puddle);
    }

    /// <summary>An enemy can come back as another type after it respawns.</summary>
    private void ApplyType(byte type)
    {
        _type = type;
        var def = EnemyDef.All[type];
        var rare = def.SpitRange > 0;
        _material.SetShaderParameter("body", new Color(rare ? "b55088" : "3e8948"));
        _material.SetShaderParameter("rim", new Color(rare ? "f6757a" : "8fe05a"));
        _material.SetShaderParameter("core", new Color(rare ? "68386c" : "265c42"));
        _material.SetShaderParameter("glow", rare ? 0.35f : 0f);
        _splashMaterial.AlbedoColor = new Color(rare ? "f6757a" : "63c74d");
        _crown.Visible = rare;
        Root.Scale = Vector3.One * def.Size;
    }

    public void Update(in EnemyView enemy, Vector3 position, float delta, Sfx sfx)
    {
        if (enemy.Type != _type)
        {
            ApplyType(enemy.Type);
        }

        PlaySounds(enemy, position, sfx);
        Root.Position = position;
        _rise = Mathf.Lerp(_rise, (position.Y - _lastY) / Mathf.Max(delta, 0.001f), 0.3f);
        _lastY = position.Y;
        _body.Rotation = new Vector3(0, enemy.Facing, 0);
        var charging = enemy.Action == EnemyAction.Spit && Mathf.Sin(Time.GetTicksMsec() / 40f) > 0f;
        _material.SetShaderParameter("flash", enemy.HitFlash > 0 ? 0.8f : charging ? 0.45f : 0f);

        if (enemy.Action != _lastAction)
        {
            if (enemy.Action == EnemyAction.Splat)
            {
                Splash();
            }

            _lastAction = enemy.Action;
        }

        var target = TargetShape(enemy.Action);
        var speed = enemy.Action == EnemyAction.Splat ? 28f : 12f;
        _scale = _scale.Lerp(target, 1f - Mathf.Exp(-speed * delta));
        _body.Scale = _scale;
        _body.Position = new Vector3(0, 0.6f * _scale.Y - 0.02f, 0);
        Root.Visible = enemy.Action != EnemyAction.Dead || _scale.Y > 0.08f;
        _material.SetShaderParameter("wobble", enemy.Action == EnemyAction.Splat ? 0.11f : 0.04f);

        // Blinks now and then; squints and frowns while fighting; the mouth opens as it leaps.
        _blink -= delta;
        if (_blink < -0.12f)
        {
            _blink = (float)GD.RandRange(2.0, 5.0);
        }

        var squint = enemy.Fighting ? 0.75f : 1f;
        foreach (var eye in _eyes)
        {
            eye.Scale = new Vector3(0.9f, (_blink < 0f ? 0.12f : 1.25f) * squint, 0.6f);
        }

        foreach (var brow in _brows)
        {
            brow.Visible = enemy.Fighting;
        }

        var open = enemy.Action is EnemyAction.Leap or EnemyAction.Windup or EnemyAction.Spit;
        _mouth.Scale = open ? new Vector3(1.6f, 1.4f, 1.6f) : Vector3.One;

        if (_puddle.Visible)
        {
            _puddleTime -= delta;
            _puddle.Scale = new Vector3(1.8f, 1f, 1.8f) * Mathf.Clamp(_puddleTime / 1.2f, 0.2f, 1f);
            _puddle.Visible = _puddleTime > 0;
        }
    }

    private void PlaySounds(in EnemyView enemy, Vector3 position, Sfx sfx)
    {
        DamageTaken = 0;
        if (!_first)
        {
            if (enemy.Health < _lastHealth)
            {
                DamageTaken = _lastHealth - enemy.Health;
            }

            if (enemy.HitFlash > _lastFlash)
            {
                sfx.Play("slime_hit", position);
            }

            if (enemy.Fighting && !_lastFighting)
            {
                sfx.Play("slime_alert", position, -4f);
            }

            if (enemy.Action != _lastAction)
            {
                var sound = enemy.Action switch
                {
                    EnemyAction.Hop => "slime_hop",
                    EnemyAction.Windup => "slime_charge",
                    EnemyAction.Spit => "slime_charge",
                    EnemyAction.Splat => "slime_splat",
                    EnemyAction.Dead => "slime_die",
                    _ => null,
                };
                if (sound is not null)
                {
                    sfx.Play(sound, position, enemy.Action == EnemyAction.Hop ? -8f : 0f);
                }
            }
        }

        _first = false;
        _lastFlash = enemy.HitFlash;
        _lastHealth = enemy.Health;
        _lastFighting = enemy.Fighting;
    }

    private Vector3 TargetShape(EnemyAction action)
    {
        var stretch = Mathf.Clamp(_rise * 0.07f, -0.3f, 0.45f);
        return action switch
        {
            EnemyAction.Windup => new Vector3(1.4f, 0.5f, 1.4f),
            EnemyAction.Spit => new Vector3(1.25f, 0.7f, 1.25f),
            EnemyAction.Leap => new Vector3(0.8f, 1.35f, 0.8f),
            EnemyAction.Hop => new Vector3(1f - stretch * 0.5f, 1f + Mathf.Abs(stretch), 1f - stretch * 0.5f),
            EnemyAction.Splat => new Vector3(2.1f, 0.2f, 2.1f),
            EnemyAction.Dead => new Vector3(1.9f, 0.04f, 1.9f),
            _ => new Vector3(1f, 0.9f + Mathf.Sin(Time.GetTicksMsec() / 260f) * 0.05f, 1f),
        };
    }

    private void Splash()
    {
        _droplets.Restart();
        _droplets.Emitting = true;
        _puddle.Visible = true;
        _puddleTime = 1.2f;
    }
}
