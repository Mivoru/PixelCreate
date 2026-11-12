using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// A barrow wisp: a cold light hanging at chest height over the grass, with a ragged tail of motes, a
/// glow round it and a small shadow on the ground under it. A lantern's false lights cast no shadow,
/// which is the only way to tell them from the true one. Put out by a standing stone it greys and sinks;
/// winding up a bolt it swells and flickers; at dawn or death it shrinks away.
/// </summary>
public sealed class WispView : IEnemyView
{
    private float _humIn = (float)GD.RandRange(1.0, 8.0);

    private static readonly Color Cold = new("2ce8f5");
    private static readonly Color Pale = new("ffffff");
    private static readonly Color Out = new("5a6988");

    private readonly EnemyDef _def;
    private readonly Node3D _body = new();
    private readonly MeshInstance3D _core;
    private readonly MeshInstance3D _halo;
    private readonly MeshInstance3D _flame;
    private readonly StandardMaterial3D _coreMaterial;
    private readonly ShaderMaterial _haloMaterial;
    private readonly CpuParticles3D _tail;
    private readonly OmniLight3D _light;
    private readonly MeshInstance3D? _shadow;
    private readonly float _phase = GD.Randf() * Mathf.Tau;
    private float _height;
    private float _swell = 1f;
    private float _fade;
    private EnemyAction _lastAction;
    private short _lastHealth;
    private bool _lastFighting;
    private bool _first = true;

    public Node3D Root { get; } = new();
    public int DamageTaken { get; private set; }

    /// <summary>How high the light hangs over the ground, as a share of its size, when lit.</summary>
    private const float Hover = 1.15f;

    public WispView(EnemyDef def)
    {
        _def = def;
        var stipple = GD.Load<Shader>("res://shaders/stipple.gdshader");
        Root.AddChild(_body);

        _coreMaterial = new StandardMaterial3D
        {
            AlbedoColor = Pale, EmissionEnabled = true, Emission = Cold, EmissionEnergyMultiplier = 2.2f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        _core = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.17f, Height = 0.34f, RadialSegments = 10, Rings = 6 },
            MaterialOverride = _coreMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _body.AddChild(_core);

        _haloMaterial = new ShaderMaterial { Shader = stipple };
        _haloMaterial.SetShaderParameter("color", Cold);
        _haloMaterial.SetShaderParameter("strength", 0.28f);
        _halo = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.3f, Height = 0.6f, RadialSegments = 12, Rings = 7 },
            MaterialOverride = _haloMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _body.AddChild(_halo);

        // A tongue of cold flame licking up off the light, like a candle's.
        _flame = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.13f, Height = 0.42f, RadialSegments = 8 },
            MaterialOverride = _coreMaterial,
            Position = new Vector3(0, 0.24f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _body.AddChild(_flame);

        // The rare lantern carries a ring of pale glass round its light.
        if (def.Rare)
        {
            var glass = new ShaderMaterial { Shader = stipple };
            glass.SetShaderParameter("color", new Color("c0cbdc"));
            glass.SetShaderParameter("strength", 0.8f);
            foreach (var tilt in new[] { 0f, Mathf.Pi / 2f })
            {
                _body.AddChild(new MeshInstance3D
                {
                    Mesh = new TorusMesh { InnerRadius = 0.42f, OuterRadius = 0.47f, Rings = 16, RingSegments = 4 },
                    Rotation = new Vector3(Mathf.Pi / 2f, tilt, 0),
                    MaterialOverride = glass,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
            }
        }

        var moteMaterial = new ShaderMaterial { Shader = stipple };
        moteMaterial.SetShaderParameter("color", Cold);
        moteMaterial.SetShaderParameter("strength", 1f);
        _tail = new CpuParticles3D
        {
            Amount = 22, Lifetime = 0.9f, LocalCoords = false, Spread = 40f, Direction = Vector3.Down,
            InitialVelocityMin = 0.1f, InitialVelocityMax = 0.5f, Gravity = new Vector3(0, -0.6f, 0),
            ScaleAmountMin = 0.4f, ScaleAmountMax = 1f,
            Mesh = new BoxMesh { Size = Vector3.One * 0.07f },
            MaterialOverride = moteMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _body.AddChild(_tail);

        _light = new OmniLight3D { LightColor = Cold, OmniRange = 6f, LightEnergy = 1.6f, ShadowEnabled = false };
        _body.AddChild(_light);

        // Only a true light throws a shadow.
        if (!def.Decoy)
        {
            _shadow = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0.32f, Height = 0.02f, RadialSegments = 12 },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("181425"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(0, 0.04f, 0),
            };
            Root.AddChild(_shadow);
        }

        Root.Scale = Vector3.One * def.Size;
        _height = Hover;
    }

    public void Update(in EnemyView enemy, Vector3 position, float delta, Sfx sfx)
    {
        PlaySounds(enemy, position, sfx, delta);
        Root.Position = position;
        var time = (float)Time.GetTicksMsec() / 1000f + _phase;
        var dead = enemy.Action == EnemyAction.Dead;
        var dim = enemy.Action == EnemyAction.Dim;
        var casting = enemy.Action == EnemyAction.Cast;

        // Hangs and bobs; a put-out light sinks towards the grass.
        var wanted = dim ? 0.55f : Hover;
        _height = Mathf.Lerp(_height, wanted, 1f - Mathf.Exp(-4f * delta));
        _body.Position = new Vector3(Mathf.Sin(time * 0.9f) * 0.08f, _height + Mathf.Sin(time * 2.1f) * 0.1f, Mathf.Cos(time * 1.3f) * 0.08f);

        // Swells and flickers as a bolt gathers; shrinks to nothing when it goes for good.
        _fade = Mathf.MoveToward(_fade, dead ? 1f : 0f, delta * 2.5f);
        var swell = casting ? 1.35f + Mathf.Sin(time * 30f) * 0.12f : dim ? 0.7f : 1f;
        _swell = Mathf.Lerp(_swell, swell, 1f - Mathf.Exp(-10f * delta));
        _body.Scale = Vector3.One * Mathf.Max(0.01f, _swell * (1f - _fade));
        Root.Visible = _fade < 0.99f;

        var flash = enemy.HitFlash > 0;
        var glow = dim ? Out : Cold;
        _coreMaterial.Emission = flash ? Pale : glow;
        _coreMaterial.EmissionEnergyMultiplier = flash ? 4f : dim ? 0.4f : casting ? 3.2f : 2.2f;
        _coreMaterial.AlbedoColor = dim ? new Color("8b9bb4") : Pale;
        _haloMaterial.SetShaderParameter("color", glow);
        _haloMaterial.SetShaderParameter("strength", 0.28f);
        _halo.Visible = !dim;
        _flame.Visible = !dim;
        _flame.Rotation = new Vector3(Mathf.Sin(time * 5.3f) * 0.25f, 0, Mathf.Sin(time * 4.1f) * 0.25f);
        _flame.Scale = new Vector3(1f, 0.8f + Mathf.Sin(time * 9f) * 0.2f, 1f);
        _tail.Emitting = !dead && !dim;
        _light.LightEnergy = dead ? 0f : dim ? 0.25f : casting ? 2.6f + Mathf.Sin(time * 30f) * 0.6f : 1.6f + Mathf.Sin(time * 3f) * 0.2f;
        _light.LightColor = glow;
        if (_shadow is not null)
        {
            var spread = 1f - Mathf.Clamp((_height - 0.5f) / 1.5f, 0f, 0.5f);
            _shadow.Scale = new Vector3(spread, 1f, spread) * (1f - _fade);
        }
    }

    private void PlaySounds(in EnemyView enemy, Vector3 position, Sfx sfx, float delta)
    {
        DamageTaken = 0;
        var at = position + Vector3.Up * Hover;
        if (!_first)
        {
            if (enemy.Health < _lastHealth)
            {
                DamageTaken = _lastHealth - enemy.Health;
            }

            if (enemy.Fighting && !_lastFighting)
            {
                sfx.Play("wisp_hum", at, -4f);
            }

            // A wisp hums to itself as it drifts, quietly, each on its own clock.
            _humIn -= delta;
            if (_humIn <= 0f)
            {
                _humIn = 6f + (float)GD.RandRange(0.0, 9.0);
                if (enemy.Action != EnemyAction.Dead && !enemy.Fighting)
                {
                    sfx.Play("wisp_hum", at, -14f);
                }
            }

            if (enemy.Action != _lastAction)
            {
                var sound = enemy.Action switch
                {
                    EnemyAction.Cast when _def.Abilities is { } list && enemy.Ability < list.Length && list[enemy.Ability].Effect == "wispSplit" => "shadow_rise",
                    EnemyAction.Cast => "wisp_bolt",
                    EnemyAction.Dim => "shadow_sink",
                    EnemyAction.Dead => "wisp_die",
                    _ => null,
                };
                if (sound is not null)
                {
                    sfx.Play(sound, at, enemy.Action == EnemyAction.Cast ? -8f : -4f);
                }
            }
        }

        _first = false;
        _lastAction = enemy.Action;
        _lastHealth = enemy.Health;
        _lastFighting = enemy.Fighting;
    }
}
