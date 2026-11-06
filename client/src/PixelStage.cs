using System.Linq;
using Godot;

namespace PixelCreate;

/// <summary>A low-resolution 3D view scaled up by a whole number, with the pixel post shader.</summary>
public sealed class PixelStage
{
    public static readonly Vector2I RenderSize = new(480, 270);

    private static readonly string[] Palette =
    {
        "be4a2f", "d77643", "ead4aa", "e4a672", "b86f50", "733e39", "3e2731", "a22633",
        "e43b44", "f77622", "feae34", "fee761", "63c74d", "3e8948", "265c42", "193c3e",
        "124e89", "0099db", "2ce8f5", "ffffff", "c0cbdc", "8b9bb4", "5a6988", "3a4466",
        "262b44", "181425", "ff0044", "68386c", "b55088", "f6757a", "e8b796", "c28569",
    };

    public SubViewport View { get; }
    public Camera3D Camera { get; }

    public PixelStage(Node parent)
    {
        View = new SubViewport
        {
            Size = RenderSize,
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Disabled,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        parent.AddChild(View);

        var screen = new TextureRect
        {
            Texture = View.GetTexture(),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        parent.AddChild(screen);
        // SetAnchorsPreset alone keeps the old size, which is zero here.
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        View.AddChild(BuildEnvironment());
        View.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-48, -55, 0),
            LightColor = new Color("fee3b0"),
            LightEnergy = 1.2f,
            ShadowEnabled = true,
        });

        Camera = new Camera3D { Fov = 55, Far = 200 };
        View.AddChild(Camera);
        Camera.AddChild(BuildPostQuad());
    }

    private static WorldEnvironment BuildEnvironment()
    {
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color("68386c"),
            SkyHorizonColor = new Color("f6757a"),
            GroundHorizonColor = new Color("f6757a"),
            GroundBottomColor = new Color("b55088"),
        };
        return new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Sky,
                Sky = new Sky { SkyMaterial = sky },
                AmbientLightSource = Godot.Environment.AmbientSource.Sky,
                AmbientLightEnergy = 1.3f,
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
