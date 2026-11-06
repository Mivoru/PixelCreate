using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>What the game needs from any enemy drawn on screen.</summary>
public interface IEnemyView
{
    Node3D Root { get; }
    int DamageTaken { get; }
    void Update(in EnemyView enemy, Vector3 position, float delta, Sfx sfx);
}

/// <summary>A goblin: the player's jointed rig with a goblin's look and weapon.</summary>
public sealed class GoblinView : IEnemyView
{
    private readonly PlayerRig _rig;
    private readonly EnemyDef _def;
    private EnemyAction _lastAction;
    private short _lastHealth;
    private bool _lastFighting;
    private bool _first = true;
    private float _actionTime;
    private float _spin;
    private Vector3 _lastPosition;

    public Node3D Root => _rig.Root;
    public int DamageTaken { get; private set; }

    public GoblinView(EnemyDef def)
    {
        _def = def;
        var tunic = new Color(def.Weapon switch
        {
            "club" => "733e39",
            "bow" => "3e8948",
            "staff" => "68386c",
            _ => "b86f50",
        });
        _rig = new PlayerRig(tunic, new RigLook(new Color("63a84d"), def.Size, false, false, true, def.Weapon));
    }

    public void Update(in EnemyView enemy, Vector3 position, float delta, Sfx sfx)
    {
        var speed = (position - _lastPosition).Length() / Mathf.Max(delta, 0.001f);
        _lastPosition = position;
        _actionTime = enemy.Action == _lastAction ? _actionTime + delta : 0f;
        PlaySounds(enemy, position, sfx);

        var ability = _def.Abilities is { } list && enemy.Ability < list.Length ? list[enemy.Ability] : null;
        var attack = AttackKind.None;
        var tick = 0;
        var heavy = ability is { Effect: "slam" or "spin" or "firewall" or "powershot" or "volley" };
        if (ability is not null)
        {
            var kind = heavy ? AttackKind.Heavy : AttackKind.Light;
            var hitAt = Tuning.Attack(kind).HitAt;
            if (enemy.Action == EnemyAction.Cast)
            {
                attack = kind;
                tick = (int)(_actionTime / (ability.WindupTicks * Tuning.TickSeconds) * (hitAt - 1));
            }
            else if (enemy.Action == EnemyAction.Strike)
            {
                attack = kind;
                tick = hitAt + (int)(_actionTime * 60f);
            }
        }

        _spin = enemy.Action == EnemyAction.Strike && ability?.Effect == "spin" ? _spin + delta * 22f : 0f;
        Root.Position = position;
        Root.Rotation = new Vector3(0, enemy.Facing + _spin, 0);
        Root.Visible = enemy.Action != EnemyAction.Dead;
        _rig.Flash = enemy.HitFlash > 0;
        _rig.Animate(new MotionState(speed > 20f ? 0f : speed, true, 0f, enemy.Action == EnemyAction.Leap, attack, tick), delta);
    }

    private void PlaySounds(in EnemyView enemy, Vector3 position, Sfx sfx)
    {
        DamageTaken = 0;
        if (!_first)
        {
            if (enemy.Health < _lastHealth)
            {
                DamageTaken = _lastHealth - enemy.Health;
                sfx.Play(enemy.Action == EnemyAction.Dead ? "goblin_die" : "goblin_hit", position + Vector3.Up);
            }

            if (enemy.Fighting && !_lastFighting)
            {
                sfx.Play("goblin_alert", position + Vector3.Up);
            }

            if (enemy.Action != _lastAction && enemy.Action == EnemyAction.Strike && _def.Abilities is { } list)
            {
                var sound = list[enemy.Ability].Effect switch
                {
                    "slam" => "club_slam",
                    "spin" => "club_spin",
                    "melee" or "dashStrike" => "goblin_swing",
                    _ => null,
                };
                if (sound is not null)
                {
                    sfx.Play(sound, position + Vector3.Up);
                }
            }
        }

        _first = false;
        _lastAction = enemy.Action;
        _lastHealth = enemy.Health;
        _lastFighting = enemy.Fighting;
    }
}
