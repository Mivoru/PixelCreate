using System.Linq;
using Godot;

namespace PixelCreate;

/// <summary>A low-resolution 3D view scaled up by a whole number, with the pixel post shader.</summary>
public sealed class PixelStage
{
    public static readonly Vector2I RenderSize = new(640, 360);

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
            RotationDegrees = new Vector3(-42, -50, 0),
            LightColor = new Color("fff3df"),
            LightEnergy = 1.25f,
            ShadowEnabled = true,
        });

        Camera = new Camera3D { Fov = 58, Far = 260 };
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
                AmbientLightEnergy = 1.7f,
                FogEnabled = true,
                FogLightColor = new Color("c7b6c9"),
                FogDensity = 0.0025f,
                FogSkyAffect = 0f,
                SsaoEnabled = true,
                SsaoRadius = 1.4f,
                SsaoIntensity = 0.8f,
                TonemapMode = Godot.Environment.ToneMapper.Linear,
            },
        };
    }

    private static MeshInstance3D BuildPostQuad()
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/pixel_post.gdshader") };
        var colors = PaletteData.Colors.Select(hex => new Color(hex)).Select(c => new Vector3(c.R, c.G, c.B)).ToArray();
        material.SetShaderParameter("palette", colors);
        material.SetShaderParameter("palette_size", colors.Length);
        material.SetShaderParameter("outline_color", new Vector3(0x18, 0x14, 0x25) / 255f);
        return new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(2, 2) },
            MaterialOverride = material,
            ExtraCullMargin = 16384,
        };
    }
}
