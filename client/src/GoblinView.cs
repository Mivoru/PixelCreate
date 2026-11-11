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

    private float _pitch;
    private EnemyAction _lastLeapAction;

    /// <summary>Set for one frame when an orc starts to beat its war drum.</summary>
    public bool DrumStarted { get; private set; }

    private int _beats;

    /// <summary>Beats of the war drum a second.</summary>
    private const float DrumBeats = 4f;

    /// <summary>Set for one frame when the enemy flies into a rage.</summary>
    public bool Enraged { get; private set; }

    private bool _lastEnraged;

    /// <summary>Set for one frame when the enemy starts to reel from a parry.</summary>
    public bool Staggered { get; private set; }

    /// <summary>Where a great leap just left the ground, once; the game cracks the earth there.</summary>
    public Vector3? Launch { get; set; }

    /// <summary>Which blow of a combo is under way: the strikes counted since the orc began it.</summary>
    private int _comboStep;
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
            "werewolf_redclaw" => ("werewolf", "2a1416"),
            "werewolf_moonleaper" => ("werewolf", "3a4466"),
            "orc_warrior" => ("orc_warrior", "5a3a2c"),
            "orc_berserker" => ("orc_berserker", "5a3a2c"),
            "orc_spearwoman" => ("orc_spearwoman", "6a3a2a"),
            "orc_huntress" => ("orc_huntress", "4a5a3a"),
            // The rare orcs wear their camp's war red over a darker hide.
            "orc_swordmaster" => ("orc_warrior", "8a2a2a"),
            "orc_spearcaller" => ("orc_spearwoman", "7a2a3a"),
            "orc_stormaxe" => ("orc_berserker", "6a1e1e"),
            "goblin_brute" => ("brute", "5a4a3a"),
            "goblin_rapid_archer" => ("archer", "3a5a7a"),
            "goblin_sharpshooter" => ("sharpshooter", "5a3a5a"),
            "goblin_raider_archer" => ("archer", "4a5a3a"),
            "goblin_warchief" => ("brute", "8a2a2a"),
            "goblin_trapper" => ("skirmisher", "4a5a2a"),
            "goblin_shaman" => ("shaman", "733e39"),
            "auren_sentinel" => ("sentinel", "8e909c"),
            "archive_warden" => ("sentinel_warden", "6a6c78"),
            _ => ("skirmisher", "7a4a2e"),
        };
        // Each kind has its own shade of skin, so a camp reads as a crowd and not as copies.
        var skin = new Color(def.Id switch
        {
            "werewolf" => "5e5248",
            "werewolf_alpha" => "26242c",
            "werewolf_redclaw" => "5a2226",
            "werewolf_moonleaper" => "8a94a8",
            "orc_warrior" => "6e8a3e",
            "orc_berserker" => "5e7a34",
            "orc_spearwoman" => "7a9448",
            "orc_huntress" => "6a8a50",
            "orc_swordmaster" => "4e6a3a",
            "orc_spearcaller" => "587040",
            "orc_stormaxe" => "46602e",
            "goblin_brute" => "6a9a3a",
            "goblin_sharpshooter" => "6aa07a",
            "goblin_shaman" => "8a9a6a",
            "goblin_rapid_archer" => "8ab04a",
            "auren_sentinel" => "c8c8d0",
            "archive_warden" => "b4b8c6",
            _ => "7fb04a",
        });
        var clothes = new Color(cloth);
        var look = def.Id == Guild.ExaminerId ? NpcLooks.Examiner : new RigLook(kind, skin, def.Size, def.Weapon)
        {
            Item = def.Kind == "sentinel" ? "sentinel_blade" : null,
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
        // A tornado turns the orc five times round over its whole length.
        var whirling = enemy.Action == EnemyAction.Tornado;
        // A shaman's war dance: it turns slowly on the spot with its staff held high.
        var dancing = enemy.Action == EnemyAction.Cast && ability?.Effect == "warDance";
        _spin = whirling ? _spin + delta * TornadoTurnSpeed : spinning ? _spin + delta * 20f : dancing ? _spin + delta * 7f : 0f;
        if (enemy.Action == EnemyAction.Strike && _lastStrikeAction != EnemyAction.Strike)
        {
            _comboStep++;
        }

        if (enemy.Action is not (EnemyAction.Cast or EnemyAction.Strike))
        {
            _comboStep = 0;
        }

        _lastStrikeAction = enemy.Action;

        // A werewolf's leap: it tips forward into an arrow in flight, and after a miss it rolls once
        // head over heels round its middle. Thighs swell while it gathers itself for the great leap.
        // Tipping forward is a negative turn about X; a forward roll turns the same way.
        var great = ability?.Effect == "superJump";
        var leaping = enemy.Action == EnemyAction.Leap && ability?.Effect is "pounce" or "superJump";
        var pitch = leaping ? (great ? -1.15f : -0.45f) : 0f;
        _pitch = Mathf.Lerp(_pitch, pitch, 1f - Mathf.Exp(-14f * delta));
        var roll = enemy.Action == EnemyAction.Roll ? -Mathf.Tau * Mathf.Clamp(_actionTime / (Enemy.RollTicks * Tuning.TickSeconds), 0f, 1f) : 0f;
        var tilt = _pitch + roll;
        var bulk = enemy.Action == EnemyAction.Cast && great ? 1f + 0.4f * Mathf.Clamp(_actionTime / (ability!.WindupTicks * Tuning.TickSeconds), 0f, 1f) : 1f;
        _rig.LegBulk = bulk;
        if (enemy.Action == EnemyAction.Leap && _lastLeapAction != EnemyAction.Leap && great)
        {
            Launch = position;
        }

        _lastLeapAction = enemy.Action;
        var middle = new Vector3(0, _def.Size * 0.9f, 0);
        var turn = new Basis(Vector3.Up, enemy.Facing + _spin);
        var pivot = turn * (middle - new Basis(Vector3.Right, tilt) * middle);
        Root.Position = position + pivot;
        Root.Rotation = new Vector3(tilt, enemy.Facing + _spin, 0);
        Root.Visible = enemy.Action != EnemyAction.Dead;
        _rig.Flash = enemy.HitFlash > 0;

        // A thrown spear leaves the hand and is back in it once she recovers; she never fetches it.
        _rig.SwordVisible = !(enemy.Action == EnemyAction.Strike && ability?.Effect == "spearThrow");
        // A duelist's dash uses the hero's own dash pose.
        var dodging = enemy.Action == EnemyAction.Dodge;
        // In the air or rolling, the legs hold the pose instead of running.
        var flying = enemy.Action is EnemyAction.Leap or EnemyAction.Roll;
        // A guardian that has not woken settles into its stance once and then stands as still as the
        // statue it looks like, without breathing or shifting its weight.
        var standing = _def.Statue && !enemy.Fighting && enemy.Action == EnemyAction.Idle && speed < 0.2f;
        _stillFor = standing ? _stillFor + delta : 0f;
        if (_stillFor > StatueSettle)
        {
            return;
        }

        _rig.Animate(new MotionState(speed > 20f || flying ? 0f : speed, true, 0f, dodging, AttackKind.None, 0, dodging ? WeaponMove.None : MoveFor(enemy.Action, ability)), delta);
    }

    private float _stillFor;

    /// <summary>How long a statue eases into its stance before it freezes, in seconds.</summary>
    private const float StatueSettle = 0.8f;

    /// <summary>Five turns in the server's tornado time, in radians a second.</summary>
    private const float TornadoTurnSpeed = 5f * Mathf.Tau / (Enemy.TornadoTicks * Tuning.TickSeconds);

    private EnemyAction _lastStrikeAction;

    private WeaponMove MoveFor(EnemyAction action, Ability? ability)
    {
        // Beating the war drum: the weapon comes down on the skin four times a second.
        if (action == EnemyAction.Drum)
        {
            return (int)(_actionTime * DrumBeats * 2f) % 2 == 0 ? WeaponMove.ClubRaise : WeaponMove.ClubSmash;
        }

        if (action == EnemyAction.Tornado)
        {
            return WeaponMove.ClubSpin;
        }

        if (action == EnemyAction.Roll)
        {
            return WeaponMove.Tuck;
        }

        if (action == EnemyAction.Stagger)
        {
            return WeaponMove.Reel;
        }

        if (ability is null || action is not (EnemyAction.Cast or EnemyAction.Strike or EnemyAction.Leap))
        {
            return WeaponMove.None;
        }

        var winding = action == EnemyAction.Cast;
        if (ability.Effect == "combo")
        {
            // Right to left, back left to right, then down from overhead. A wind-up holds the last blow's end.
            var blow = winding ? _comboStep : _comboStep - 1;
            return (blow, winding) switch
            {
                (0, true) => WeaponMove.SwingBack,
                (0, false) => WeaponMove.SwingAcross,
                (1, true) => WeaponMove.SwingAcross,
                (1, false) => WeaponMove.SwingBack,
                (_, true) => WeaponMove.ClubRaise,
                _ => WeaponMove.ClubSmash,
            };
        }
        if (_def.Kind == "werewolf")
        {
            switch (ability.Effect)
            {
                case "pounce" or "superJump":
                    return action switch
                    {
                        EnemyAction.Cast => WeaponMove.Crouch,
                        EnemyAction.Leap => WeaponMove.Dive,
                        _ => ability.Effect == "superJump" ? WeaponMove.Impale : WeaponMove.ClawRake,
                    };
                case "fangSlash":
                    return winding ? WeaponMove.ClawRaise : WeaponMove.ClawRake;
                case "melee" or "dashStrike" or "slam":
                    return winding ? WeaponMove.ClawRaise : WeaponMove.ClawRake;
            }
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
            "spearThrow" or "axeThrow" => winding ? WeaponMove.ThrowBack : WeaponMove.Throw,
            "airSlashWide" => winding ? WeaponMove.SwingBack : WeaponMove.SwingAcross,
            "summonSpears" => winding ? WeaponMove.StaffRaise : WeaponMove.StaffThrust,
            "runeWave" => winding ? WeaponMove.ClubRaise : WeaponMove.GroundChop,
            "warDance" => winding ? WeaponMove.StaffRaise : WeaponMove.StaffPlant,
            "tornado" => WeaponMove.SwingBack,
            _ => WeaponMove.None,
        };
    }

    private bool Female => _def.Id is "orc_spearwoman" or "orc_huntress" or "orc_spearcaller";

    private void PlaySounds(in EnemyView enemy, Vector3 position, Sfx sfx)
    {
        if (!_first && enemy.Action == EnemyAction.Tornado && _lastAction != EnemyAction.Tornado)
        {
            sfx.Play("tornado", position + Vector3.Up);
        }

        if (!_first && enemy.Action == EnemyAction.Dodge && _lastAction != EnemyAction.Dodge)
        {
            sfx.Play("dash", position + Vector3.Up);
        }

        Staggered = !_first && enemy.Action == EnemyAction.Stagger && _lastAction != EnemyAction.Stagger;

        // A war drum: a boom on every beat, and a shout as the drummer starts.
        DrumStarted = !_first && enemy.Action == EnemyAction.Drum && _lastAction != EnemyAction.Drum;
        if (DrumStarted)
        {
            sfx.Play(Female ? "orc_f_alert" : "orc_alert", position + Vector3.Up);
            _beats = 0;
        }

        if (enemy.Action == EnemyAction.Drum && _actionTime * DrumBeats >= _beats)
        {
            _beats++;
            sfx.Play("club_thud", position + Vector3.Up * 0.8f, 4f);
        }

        // The moment it flies into a rage it roars.
        Enraged = !_first && enemy.Enraged && !_lastEnraged;
        if (Enraged)
        {
            sfx.Play(_def.Kind == "orc" ? "orc_alert" : "goblin_alert", position + Vector3.Up);
        }

        _lastEnraged = enemy.Enraged;
        if (!_first && enemy.Action == EnemyAction.Cast && _lastAction != EnemyAction.Cast && _def.Abilities is { } dances &&
            enemy.Ability < dances.Length && dances[enemy.Ability].Effect == "warDance")
        {
            sfx.Play("goblin_cackle", position + Vector3.Up);
        }

        DamageTaken = 0;
        if (!_first)
        {
            if (enemy.Health < _lastHealth)
            {
                DamageTaken = _lastHealth - enemy.Health;
                if (_def.Kind == "sentinel")
                {
                    // Stone: a dull knock when struck, and a crash of falling masonry at the end.
                    sfx.Play(enemy.Action == EnemyAction.Dead ? "earth_crack" : "club_thud", position + Vector3.Up);
                }
                else
                {
                    var voice = _def.Kind == "werewolf" ? "werewolf" : _def.Kind == "orc" ? (Female ? "orc_f" : "orc") : "goblin";
                    sfx.Play(enemy.Action == EnemyAction.Dead ? $"{voice}_die" : $"{voice}_hit", position + Vector3.Up);
                }
            }

            if (enemy.Fighting && !_lastFighting)
            {
                sfx.Play(_def.Kind == "sentinel" ? "shockwave" : _def.Kind == "werewolf" ? "werewolf_roar" : _def.Kind == "orc" ? (Female ? "orc_f_alert" : "orc_alert") : "goblin_alert", position + Vector3.Up);
            }

            if (enemy.Action != _lastAction && enemy.Action == EnemyAction.Strike && _def.Abilities is { } list)
            {
                var sound = list[enemy.Ability].Effect switch
                {
                    "slam" => "club_slam",
                    "spin" => "club_spin",
                    "airSlash" => "air_slash",
                    "spearThrow" or "axeThrow" => "spear_throw",
                    "earthCrack" => "earth_crack",
                    "combo" => "orc_swing",
                    "airSlashWide" => "air_slash",
                    "summonSpears" => "spear_summon",
                    "fangSlash" => "fang_slash",
                    "pounce" => "werewolf_swipe",
                    "superJump" => "earth_crack",
                    "runeWave" => "swing_heavy",
                    "melee" or "dashStrike" => _def.Kind == "werewolf" ? "werewolf_swipe" : _def.Kind == "orc" ? "orc_swing" : _def.Kind == "sentinel" ? "swing_heavy" : "goblin_swing",
                    _ => null,
                };
                if (sound is not null)
                {
                    sfx.Play(sound, position + Vector3.Up);
                }

                // Goblins scream as they strike, not only when struck.
                if (_def.Kind is not ("orc" or "werewolf" or "animal" or "slime" or "sentinel") && System.Random.Shared.NextDouble() < 0.5)
                {
                    sfx.Play(System.Random.Shared.NextDouble() < 0.7 ? "goblin_shriek" : "goblin_cackle", position + Vector3.Up);
                }
            }
        }

        _first = false;
        _lastAction = enemy.Action;
        _lastHealth = enemy.Health;
        _lastFighting = enemy.Fighting;
    }
}
