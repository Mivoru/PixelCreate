using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// A mirefin: a long olive pike with a pale belly, a jaw full of teeth and a tall barbed dorsal fin. Under the
/// water only the fin shows, cutting the surface with a little wake; winding up a leap the fish rises with its
/// back out of the water; in the air it arcs; stranded on the bank it lies on its side and flops.
/// </summary>
public sealed class FishView : IEnemyView
{
    private readonly EnemyDef _def;
    private readonly float _surface;
    private readonly Node3D _body = new();
    private readonly Node3D _tail = new();
    private readonly Node3D _jaw = new();
    private readonly MeshInstance3D _fin;
    private readonly StandardMaterial3D _skin;
    private readonly CpuParticles3D _wake;
    private readonly float _phase = GD.Randf() * Mathf.Tau;
    private float _rise;
    private float _roll;
    private float _deadTime;
    private EnemyAction _lastAction;
    private short _lastHealth;
    private bool _first = true;

    public Node3D Root { get; } = new();
    public int DamageTaken { get; private set; }

    /// <param name="surface">The height of the lake's water, which the fin cuts.</param>
    public FishView(EnemyDef def, float surface)
    {
        _def = def;
        _surface = surface;
        Root.AddChild(_body);
        _skin = new StandardMaterial3D { AlbedoColor = new Color(def.Rare ? "4a5a3a" : "5a7a4a"), Roughness = 0.4f, Metallic = 0.2f };
        var belly = new StandardMaterial3D { AlbedoColor = new Color("d8dcb8") };
        var fin = new StandardMaterial3D { AlbedoColor = new Color(def.Rare ? "6a3a3a" : "3e6a4a") };
        var teeth = new StandardMaterial3D { AlbedoColor = new Color("f4eedc") };
        var eye = new StandardMaterial3D { AlbedoColor = new Color("fee761"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };

        // A long body along -Z (the front), a pale belly under it.
        _body.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.22f, Height = 0.44f, RadialSegments = 10, Rings = 6 }, Scale = new Vector3(0.8f, 0.9f, 3f), MaterialOverride = _skin });
        _body.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.2f, Height = 0.4f, RadialSegments = 8, Rings = 5 }, Position = new Vector3(0, -0.07f, -0.05f), Scale = new Vector3(0.7f, 0.5f, 2.6f), MaterialOverride = belly });
        foreach (var side in new[] { -1f, 1f })
        {
            _body.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 6, Rings = 3 }, Position = new Vector3(side * 0.15f, 0.07f, -0.5f), MaterialOverride = eye });
            _body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.2f, 0.02f, 0.14f) }, Position = new Vector3(side * 0.2f, -0.08f, -0.25f), Rotation = new Vector3(0, side * 0.4f, side * 0.4f), MaterialOverride = fin });
        }

        // The lower jaw, hinged under the snout, with a row of teeth.
        _jaw.Position = new Vector3(0, -0.05f, -0.45f);
        _body.AddChild(_jaw);
        _jaw.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.2f, 0.05f, 0.32f) }, Position = new Vector3(0, -0.02f, -0.16f), MaterialOverride = belly });
        for (var i = 0; i < 5; i++)
        {
            _jaw.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.015f, Height = 0.05f, RadialSegments = 4 }, Position = new Vector3(-0.07f + i * 0.035f, 0.02f, -0.28f), MaterialOverride = teeth });
        }

        // The tall barbed dorsal fin: what shows above the water.
        _fin = new MeshInstance3D
        {
            Mesh = new PrismMesh { Size = new Vector3(0.5f, 0.38f, 0.04f), LeftToRight = 0.75f },
            Position = new Vector3(0, 0.32f, 0.1f), Rotation = new Vector3(0, Mathf.Pi / 2f, 0), MaterialOverride = fin,
        };
        _body.AddChild(_fin);

        // The tail swings from behind the body.
        _tail.Position = new Vector3(0, 0, 0.6f);
        _body.AddChild(_tail);
        // A forked tail: a triangle whose point joins the body, spread up and down behind it.
        _tail.AddChild(new MeshInstance3D
        {
            Mesh = new PrismMesh { Size = new Vector3(0.44f, 0.3f, 0.03f), LeftToRight = 0.5f },
            Position = new Vector3(0, 0, 0.12f), Rotation = new Vector3(Mathf.Pi / 2f, Mathf.Pi / 2f, 0), MaterialOverride = fin,
        });

        _wake = new CpuParticles3D
        {
            Amount = 16, Lifetime = 0.8f, LocalCoords = false, Direction = Vector3.Up, Spread = 60f, InitialVelocityMin = 0.2f, InitialVelocityMax = 0.6f,
            Gravity = new Vector3(0, -1f, 0), Mesh = new BoxMesh { Size = Vector3.One * 0.06f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("e8f4ff"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
        };
        Root.AddChild(_wake);
        Root.Scale = Vector3.One * def.Size;
    }

    public void Update(in EnemyView enemy, Vector3 position, float delta, Sfx sfx)
    {
        DamageTaken = !_first && enemy.Health < _lastHealth ? _lastHealth - enemy.Health : 0;
        if (!_first && enemy.Action != _lastAction && sfx is not null)
        {
            var sound = enemy.Action switch
            {
                EnemyAction.Leap => "dash",
                EnemyAction.Stagger => "slime_splat",
                EnemyAction.Dead => "animal_fall",
                _ => null,
            };
            if (sound is not null)
            {
                sfx.Play(sound, position + Vector3.Up, -3f);
            }
        }

        _first = false;
        _lastHealth = enemy.Health;
        _lastAction = enemy.Action;
        var time = (float)Time.GetTicksMsec() / 1000f + _phase;
        var dead = enemy.Action == EnemyAction.Dead;
        var stranded = enemy.Action == EnemyAction.Stagger || dead;
        var leaping = enemy.Action == EnemyAction.Leap;
        var surfacing = enemy.Action == EnemyAction.Cast;
        var swimming = !stranded && !leaping && !surfacing;

        // Under water the body hangs just below the surface so only the fin cuts it; surfacing it rises with the
        // back out of the water; leaping and stranded it is where the server says.
        var scale = _def.Size;
        var below = _surface - 0.42f * scale;
        var wanted = swimming ? below : surfacing ? _surface - 0.1f * scale : position.Y + 0.15f * scale;
        _rise = Mathf.Lerp(_rise == 0f ? wanted : _rise, wanted, 1f - Mathf.Exp(-(leaping || stranded ? 30f : 6f) * delta));
        Root.Position = new Vector3(position.X, swimming || surfacing ? _rise : position.Y + 0.15f * scale, position.Z);

        _roll = Mathf.Lerp(_roll, stranded ? 1.45f : 0f, 1f - Mathf.Exp(-10f * delta));
        var pitch = leaping ? -0.4f : 0f;
        var flop = stranded && !dead ? Mathf.Sin(time * 14f) * 0.35f : 0f;
        _body.Rotation = new Vector3(pitch + flop * 0.3f, enemy.Facing, _roll);
        _tail.Rotation = new Vector3(0, Mathf.Sin(time * (swimming ? 6f : stranded ? 16f : 10f)) * (dead ? 0f : 0.5f), 0);
        _jaw.Rotation = new Vector3(surfacing || leaping ? 0.6f : stranded && !dead ? Mathf.Max(0f, Mathf.Sin(time * 5f)) * 0.4f : 0.05f, 0, 0);
        _wake.Emitting = swimming && enemy.Fighting || surfacing;
        _wake.Position = new Vector3(0, (_surface - Root.Position.Y) / scale, 0);
        _skin.AlbedoColor = enemy.HitFlash > 0 ? Colors.White : new Color(_def.Rare ? "4a5a3a" : "5a7a4a");
        _deadTime = dead ? _deadTime + delta : 0f;
        Root.Visible = _deadTime < 4f;
    }
}
