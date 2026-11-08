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
        var (kind, cloth) = def.Id switch
        {
            "werewolf" => ("werewolf", "4a4e5a"),
            "werewolf_alpha" => ("werewolf_alpha", "2e3038"),
            "orc_warrior" => ("orc_warrior", "5a3a2c"),
            "orc_berserker" => ("orc_berserker", "5a3a2c"),
            "orc_spearwoman" => ("orc_spearwoman", "6a3a2a"),
            "orc_huntress" => ("orc_huntress", "4a5a3a"),
            "goblin_brute" => ("brute", "5a4a3a"),
            "goblin_rapid_archer" => ("archer", "3a5a7a"),
            "goblin_sharpshooter" => ("sharpshooter", "5a3a5a"),
            "goblin_shaman" => ("shaman", "733e39"),
            _ => ("skirmisher", "7a4a2e"),
        };
        // Each kind has its own shade of skin, so a camp reads as a crowd and not as copies.
        var skin = new Color(def.Id switch
        {
            "werewolf" => "5e5248",
            "werewolf_alpha" => "26242c",
            "orc_warrior" => "6e8a3e",
            "orc_berserker" => "5e7a34",
            "orc_spearwoman" => "7a9448",
            "orc_huntress" => "6a8a50",
            "goblin_brute" => "6a9a3a",
            "goblin_sharpshooter" => "6aa07a",
            "goblin_shaman" => "8a9a6a",
            "goblin_rapid_archer" => "8ab04a",
            _ => "7fb04a",
        });
        var clothes = new Color(cloth);
        var look = def.Id == Guild.ExaminerId ? NpcLooks.Examiner : new RigLook(kind, skin, def.Size, def.Weapon)
        {
            Palette = new System.Collections.Generic.Dictionary<string, Color>
            {
                ["gob_skin"] = skin,
                ["gob_skin_dark"] = skin.Darkened(0.25f),
                ["gob_cloth"] = clothes,
                ["gob_cloth_dark"] = clothes.Darkened(0.25f),
            },
        };
        _rig = new PlayerRig(new Color(cloth), look);
    }

    public void Update(in EnemyView enemy, Vector3 position, float delta, Sfx sfx)
    {
        var speed = (position - _lastPosition).Length() / Mathf.Max(delta, 0.001f);
        _lastPosition = position;
        _actionTime = enemy.Action == _lastAction ? _actionTime + delta : 0f;
        PlaySounds(enemy, position, sfx);

        var ability = _def.Abilities is { } list && enemy.Ability < list.Length ? list[enemy.Ability] : null;
        var spinning = enemy.Action == EnemyAction.Strike && ability?.Effect == "spin" && _actionTime < 0.5f;
        _spin = spinning ? _spin + delta * 20f : 0f;
        Root.Position = position;
        Root.Rotation = new Vector3(0, enemy.Facing + _spin, 0);
        Root.Visible = enemy.Action != EnemyAction.Dead;
        _rig.Flash = enemy.HitFlash > 0;

        // A thrown spear leaves the hand and is back in it once she recovers; she never fetches it.
        _rig.SwordVisible = !(enemy.Action == EnemyAction.Strike && ability?.Effect == "spearThrow");
        _rig.Animate(new MotionState(speed > 20f ? 0f : speed, true, 0f, false, AttackKind.None, 0, MoveFor(enemy.Action, ability)), delta);
    }

    private WeaponMove MoveFor(EnemyAction action, Ability? ability)
    {
        if (ability is null || action is not (EnemyAction.Cast or EnemyAction.Strike or EnemyAction.Leap))
        {
            return WeaponMove.None;
        }

        var winding = action == EnemyAction.Cast;
        if (_def.Kind == "werewolf" && ability.Effect is "melee" or "dashStrike" or "slam")
        {
            return winding ? WeaponMove.ClawRaise : WeaponMove.ClawRake;
        }

        if (_def.Weapon is "longsword" or "axe" && ability.Effect is "melee" or "dashStrike")
        {
            // An orc's plain blow with a heavy blade is a flat swing from the right.
            return winding ? WeaponMove.SwingBack : WeaponMove.SwingAcross;
        }

        if (_def.Weapon == "spear" && ability.Effect == "melee")
        {
            return winding ? WeaponMove.StabWindup : WeaponMove.SpearThrust;
        }

        return ability.Effect switch
        {
            "arrow" or "volley" or "powershot" => winding ? WeaponMove.BowDraw : WeaponMove.BowRelease,
            "melee" or "dashStrike" => winding ? WeaponMove.StabWindup : WeaponMove.Stab,
            "slam" => winding ? WeaponMove.ClubRaise : WeaponMove.ClubSmash,
            "spin" => winding ? WeaponMove.ClubRaise : WeaponMove.ClubSpin,
            "fireball" => winding ? WeaponMove.StaffRaise : WeaponMove.StaffThrust,
            "firewall" => WeaponMove.StaffPlant,
            "airSlash" => winding ? WeaponMove.ClubRaise : WeaponMove.ClubSmash,
            "earthCrack" => winding ? WeaponMove.ClubRaise : WeaponMove.GroundChop,
            "spearThrow" => winding ? WeaponMove.ThrowBack : WeaponMove.Throw,
            _ => WeaponMove.None,
        };
    }

    private void PlaySounds(in EnemyView enemy, Vector3 position, Sfx sfx)
    {
        DamageTaken = 0;
        if (!_first)
        {
            if (enemy.Health < _lastHealth)
            {
                DamageTaken = _lastHealth - enemy.Health;
                var voice = _def.Kind == "werewolf" ? "werewolf" : _def.Kind == "orc" ? (_def.Id is "orc_spearwoman" or "orc_huntress" ? "orc_f" : "orc") : "goblin";
                sfx.Play(enemy.Action == EnemyAction.Dead ? $"{voice}_die" : $"{voice}_hit", position + Vector3.Up);
            }

            if (enemy.Fighting && !_lastFighting)
            {
                sfx.Play(_def.Kind == "werewolf" ? "werewolf_roar" : _def.Kind == "orc" ? (_def.Id is "orc_spearwoman" or "orc_huntress" ? "orc_f_alert" : "orc_alert") : "goblin_alert", position + Vector3.Up);
            }

            if (enemy.Action != _lastAction && enemy.Action == EnemyAction.Strike && _def.Abilities is { } list)
            {
                var sound = list[enemy.Ability].Effect switch
                {
                    "slam" => "club_slam",
                    "spin" => "club_spin",
                    "airSlash" => "air_slash",
                    "spearThrow" => "spear_throw",
                    "earthCrack" => "earth_crack",
                    "melee" or "dashStrike" => _def.Kind == "werewolf" ? "werewolf_swipe" : _def.Kind == "orc" ? "orc_swing" : "goblin_swing",
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
