using System.Linq;
using Godot;

namespace PixelCreate;

/// <summary>Renders the village at low resolution through the pixel post shader.</summary>
public partial class PixelTest : Node
{
    private static readonly Vector2I RenderSize = new(480, 270);
    private const int ShotCount = 4;
    private const int FramesPerShot = 20;

    private static readonly string[] Palette =
    {
        "be4a2f", "d77643", "ead4aa", "e4a672", "b86f50", "733e39", "3e2731", "a22633",
        "e43b44", "f77622", "feae34", "fee761", "63c74d", "3e8948", "265c42", "193c3e",
        "124e89", "0099db", "2ce8f5", "ffffff", "c0cbdc", "8b9bb4", "5a6988", "3a4466",
        "262b44", "181425", "ff0044", "68386c", "b55088", "f6757a", "e8b796", "c28569",
    };

    private SubViewport _view = null!;
    private Camera3D _camera = null!;
    private string? _captureDir;
    private int _frame;
    private int _shot;

    public override void _Ready()
    {
        _captureDir = OS.GetCmdlineUserArgs().FirstOrDefault();
        _view = new SubViewport
        {
            Size = RenderSize,
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Disabled,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        AddChild(_view);

        var screen = new TextureRect
        {
            Texture = _view.GetTexture(),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        AddChild(screen);
        screen.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        _view.AddChild(GD.Load<PackedScene>("res://assets/village.glb").Instantiate());
        _view.AddChild(BuildEnvironment());
        _view.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-38, -60, 0),
            LightColor = new Color("fee3b0"),
            LightEnergy = 1.5f,
            ShadowEnabled = true,
        });

        _camera = new Camera3D { Fov = 30, Far = 200 };
        _view.AddChild(_camera);
        _camera.AddChild(BuildPostQuad());
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

        _view.GetTexture().GetImage().SavePng($"{_captureDir}/pixel-test-{_shot}.png");
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
        _camera.LookAtFromPosition(offset + target, target);
    }

    private static WorldEnvironment BuildEnvironment()
    {
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color("68386c"),
            SkyHorizonColor = new Color("f6757a"),
            GroundHorizonColor = new Color("f6757a"),
            GroundBottomColor = new Color("3e2731"),
        };
        return new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Sky,
                Sky = new Sky { SkyMaterial = sky },
                AmbientLightSource = Godot.Environment.AmbientSource.Sky,
                AmbientLightEnergy = 0.7f,
                TonemapMode = Godot.Environment.ToneMapper.Linear,
            },
        };
    }

    private static MeshInstance3D BuildPostQuad()
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/pixel_post.gdshader") };
        var colors = Palette.Select(hex => new Color(hex)).Select(c => new Vector3(c.R, c.G, c.B)).ToArray();
        material.SetShaderParameter("palette", colors);
        material.SetShaderParameter("outline_color", new Vector3(0x18, 0x14, 0x25) / 255f);
        return new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(2, 2) },
            MaterialOverride = material,
            ExtraCullMargin = 16384,
        };
    }
}
