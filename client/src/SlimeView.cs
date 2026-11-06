using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>A jelly body that squashes and stretches from what the server says it is doing.</summary>
public sealed class SlimeView
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

    public Node3D Root { get; } = new();

    /// <summary>Damage shown when the last update found a hit, or zero.</summary>
    public int DamageTaken { get; private set; }

    public MeshInstance3D Warning { get; }

    public SlimeView(Fx fx)
    {
        Warning = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 0.03f, RadialSegments = 20 },
            MaterialOverride = fx.Stipple(new Color("e43b44"), 0.45f),
            Visible = false,
        };

        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/slime.gdshader") };

        _body = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.6f, Height = 1.2f, RadialSegments = 16, Rings = 9 },
            MaterialOverride = _material,
            Position = new Vector3(0, 0.5f, 0),
        };
        Root.AddChild(_body);

        _body.AddChild(_face);
        foreach (var x in new[] { -0.2f, 0.2f })
        {
            _face.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.09f, Height = 0.18f, RadialSegments = 6, Rings = 3 },
                Position = new Vector3(x, 0.14f, -0.52f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("181425"), Roughness = 0.2f },
            });
        }

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
        Root.Scale = Vector3.One * def.Size;
    }

    public void Update(in EnemyView enemy, Vector3 position, float delta, Sfx sfx)
    {
        if (enemy.Type != _type)
        {
            ApplyType(enemy.Type);
        }

        PlaySounds(enemy, position, sfx);
        var def = EnemyDef.All[enemy.Type];
        var warn = enemy.Action is EnemyAction.Windup or EnemyAction.Leap;
        Warning.Visible = warn;
        if (warn)
        {
            var landing = enemy.LeapTarget;
            Warning.Position = new Vector3(landing.X, landing.Y + 0.08f, landing.Z);
            Warning.Scale = new Vector3(def.SplashRadius, 1f, def.SplashRadius);
        }

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
