using System.Linq;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>Lines the character up in each pose, seen from behind and the side, to check the sword.</summary>
public partial class PoseTest : Node
{
    private const int SettleFrames = 50;

    private static readonly (string Name, MotionState State)[] Poses =
    {
        ("idle", new MotionState(0, true, 0, false, AttackKind.None, 0)),
        ("walk", new MotionState(3f, true, 0, false, AttackKind.None, 0)),
        ("run", new MotionState(Tuning.MoveSpeed, true, 0, false, AttackKind.None, 0)),
        ("dash", new MotionState(Tuning.DashSpeed, true, 0, true, AttackKind.None, 0)),
        ("jump", new MotionState(5f, false, 6f, false, AttackKind.None, 0)),
        ("fall", new MotionState(5f, false, -6f, false, AttackKind.None, 0)),
        ("light", new MotionState(2f, true, 0, false, AttackKind.Light, 6)),
        ("heavy", new MotionState(2f, true, 0, false, AttackKind.Heavy, 18)),
    };

    private readonly PlayerRig[] _rigs = new PlayerRig[Poses.Length];
    private PixelStage _stage = null!;
    private int _frame;
    private int _shot;

    public override void _Ready()
    {
        _stage = new PixelStage(this);
        _stage.Camera.Fov = 22;
        var ground = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(40, 0.1f, 40) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("3e8948") },
            Position = new Vector3(0, -0.05f, 0),
        };
        _stage.View.AddChild(ground);
        for (var i = 0; i < Poses.Length; i++)
        {
            _rigs[i] = new PlayerRig(new Color("0099db"));
            _stage.View.AddChild(_rigs[i].Root);
            _rigs[i].Root.Position = new Vector3((i - (Poses.Length - 1) / 2f) * 2.4f, 0, 0);
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
        if (_shot == 2)
        {
            GetTree().Quit();
            return;
        }

        Aim();
    }

    private void Aim()
    {
        var position = _shot == 0 ? new Vector3(0, 3f, 27f) : new Vector3(0, 3f, -27f);
        foreach (var rig in _rigs)
        {
            rig.Root.Rotation = new Vector3(0, _shot == 0 ? 0.45f : -0.5f, 0);
        }

        _stage.Camera.LookAtFromPosition(position, new Vector3(0, 1.1f, 0));
    }
}
