using System.Linq;
using Godot;

namespace PixelCreate;

/// <summary>Orbits the generated village through the pixel stage, to check the look.</summary>
public partial class PixelTest : Node
{
    private const int ShotCount = 4;
    private const int FramesPerShot = 20;

    private PixelStage _stage = null!;
    private string? _captureDir;
    private int _frame;
    private int _shot;

    public override void _Ready()
    {
        _captureDir = OS.GetCmdlineUserArgs().FirstOrDefault();
        _stage = new PixelStage(this);
        _stage.Camera.Fov = 30;
        _stage.View.AddChild(GD.Load<PackedScene>("res://assets/village.glb").Instantiate());
        PlaceCamera(0);
    }

    public override void _Process(double delta)
    {
        if (_captureDir is null)
        {
            PlaceCamera(Time.GetTicksMsec() / 4000f);
            return;
        }

        if (++_frame < FramesPerShot)
        {
            return;
        }

        _stage.View.GetTexture().GetImage().SavePng($"{_captureDir}/pixel-test-{_shot}.png");
        _frame = 0;
        if (++_shot == ShotCount)
        {
            GetTree().Quit();
            return;
        }

        PlaceCamera(_shot * 0.35f);
    }

    private void PlaceCamera(float angle)
    {
        var target = new Vector3(0, 1.5f, 0);
        var offset = new Vector3(Mathf.Sin(angle + 0.5f) * 19f, 8f, Mathf.Cos(angle + 0.5f) * 19f);
        _stage.Camera.LookAtFromPosition(offset + target, target);
    }
}
