using System.Linq;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>Lines the character up in each pose, seen from behind and the side, to check the sword.</summary>
public partial class PoseTest : Node
{
    private const int SettleFrames = 50;

    private static readonly (RigLook Look, Color Cloth, MotionState State)[] Poses =
    {
        (RigLook.Player, new Color("0099db"), new MotionState(0, true, 0, false, AttackKind.None, 0)),
        (RigLook.Player, new Color("0099db"), new MotionState(Tuning.MoveSpeed, true, 0, false, AttackKind.None, 0)),
        (Goblin("skirmisher", 0.85f, "dagger"), new Color("3e7a48"), Move(WeaponMove.None)),
        (Goblin("skirmisher", 0.85f, "dagger"), new Color("3e7a48"), Move(WeaponMove.Stab)),
        (Goblin("archer", 0.85f, "bow"), new Color("4f6b3a"), Move(WeaponMove.None)),
        (Goblin("archer", 0.85f, "bow"), new Color("4f6b3a"), Move(WeaponMove.BowDraw)),
        (Goblin("sharpshooter", 0.9f, "bow"), new Color("3e5a68"), Move(WeaponMove.BowRelease)),
        (Goblin("brute", 1.3f, "club"), new Color("5a4a3a"), Move(WeaponMove.None)),
        (Goblin("brute", 1.3f, "club"), new Color("5a4a3a"), Move(WeaponMove.ClubRaise)),
        (Goblin("brute", 1.3f, "club"), new Color("5a4a3a"), Move(WeaponMove.ClubSmash)),
        (Goblin("shaman", 1f, "staff"), new Color("733e39"), Move(WeaponMove.None)),
        (Goblin("shaman", 1f, "staff"), new Color("733e39"), Move(WeaponMove.StaffPlant)),
    };

    private static RigLook Goblin(string kind, float scale, string weapon) => new(kind, new Color("7fb04a"), scale, weapon);

    private static MotionState Move(WeaponMove move) => new(0, true, 0, false, AttackKind.None, 0, move);

    private readonly PlayerRig[] _rigs = new PlayerRig[Poses.Length];
    private PixelStage _stage = null!;
    private int _frame;
    private int _shot;

    public override void _Ready()
    {
        _stage = new PixelStage(this);
        _stage.Camera.Fov = 26;
        var ground = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(40, 0.1f, 40) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("3e8948") },
            Position = new Vector3(0, -0.05f, 0),
        };
        _stage.View.AddChild(ground);
        for (var i = 0; i < Poses.Length; i++)
        {
            _rigs[i] = new PlayerRig(Poses[i].Cloth, Poses[i].Look);
            _stage.View.AddChild(_rigs[i].Root);
            _rigs[i].Root.Position = new Vector3((i % 6 - 2.5f) * 2.6f, 0, i < 6 ? -2.5f : 2.5f);
        }

        Aim();
    }

    public override void _Process(double delta)
    {
        for (var i = 0; i < _rigs.Length; i++)
        {
            _rigs[i].Animate(Poses[i].State, 1f / 60f);
        }

        if (++_frame < SettleFrames)
        {
            return;
        }

        var folder = OS.GetCmdlineUserArgs().First();
        _stage.View.GetTexture().GetImage().SavePng($"{folder}/poses-{_shot}.png");
        _frame = 0;
        _shot++;
        if (_shot == 3)
        {
            GetTree().Quit();
            return;
        }

        Aim();
    }

    private void Aim()
    {
        if (_shot == 2)
        {
            // A close look at the two player figures.
            _stage.Camera.Fov = 20;
            var at = _rigs[0].Root.Position + new Vector3(1.3f, 1f, 0);
            _rigs[0].Root.Rotation = new Vector3(0, 0.35f, 0);
            _rigs[1].Root.Rotation = new Vector3(0, -2.4f, 0);
            _stage.Camera.LookAtFromPosition(at + new Vector3(0, 0.4f, -7f), at);
            return;
        }

        var position = _shot == 0 ? new Vector3(0, 6f, 25f) : new Vector3(0, 6f, -25f);
        foreach (var rig in _rigs)
        {
            rig.Root.Rotation = new Vector3(0, _shot == 0 ? 2.5f : -0.6f, 0);
        }

        _stage.Camera.LookAtFromPosition(position, new Vector3(0, 1.1f, 0));
    }
}
