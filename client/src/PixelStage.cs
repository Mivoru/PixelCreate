using System.Linq;
using Godot;

namespace PixelCreate;

/// <summary>A low-resolution 3D view scaled up by a whole number, with the pixel post shader.</summary>
public sealed class PixelStage
{
    /// <summary>The render height to aim for. Lower it for bigger pixels, raise it for a finer picture.</summary>
    private const float TargetHeight = 540f;

    /// <summary>How much the final colours lean towards the palette: 1 is hard pixel art, 0 is none.</summary>
    private const float PaletteStrength = 0.22f;

    public SubViewport View { get; }

    /// <summary>Window pixels for each rendered pixel.</summary>
    public int PixelScale { get; private set; } = 1;
    public Camera3D Camera { get; }

    /// <summary>The full-screen quad of the post shader. Hide it to see unprocessed debug lines.</summary>
    public MeshInstance3D PostQuad { get; }

    public PixelStage(Node parent)
    {
        View = new SubViewport
        {
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Disabled,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            AudioListenerEnable3D = true,
        };
        parent.AddChild(View);
        var window = parent.GetViewport();
        window.SizeChanged += () => Resize(window);
        Resize(window);

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
            RotationDegrees = new Vector3(-38, -50, 0),
            LightColor = new Color("fff0d8"),
            LightEnergy = 1.2f,
            ShadowEnabled = true,
            ShadowBlur = 1.6f,
            DirectionalShadowMaxDistance = 130f,
        });

        Camera = new Camera3D { Fov = 58, Far = 260 };
        View.AddChild(Camera);
        PostQuad = BuildPostQuad();
        Camera.AddChild(PostQuad);
    }

    private void Resize(Viewport window)
    {
        var size = window.GetVisibleRect().Size;
        PixelScale = Mathf.Max(1, Mathf.RoundToInt(size.Y / TargetHeight));
        View.Size = new Vector2I(Mathf.CeilToInt(size.X / PixelScale), Mathf.CeilToInt(size.Y / PixelScale));
    }

    private static WorldEnvironment BuildEnvironment()
    {
        var sky = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/sky.gdshader") };
        return new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Sky,
                Sky = new Sky { SkyMaterial = sky },
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("cfd8ea"),
                AmbientLightEnergy = 0.82f,
                FogEnabled = true,
                FogLightColor = new Color("bcd8ea"),
                FogDensity = 0.0024f,
                FogSkyAffect = 0f,
                VolumetricFogEnabled = true,
                VolumetricFogDensity = 0.002f,
                VolumetricFogAlbedo = new Color("fff3e0"),
                VolumetricFogLength = 90f,
                VolumetricFogAnisotropy = 0.55f,
                GlowEnabled = true,
                GlowIntensity = 0.35f,
                GlowBloom = 0.04f,
                GlowHdrThreshold = 1.05f,
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
        material.SetShaderParameter("palette_strength", PaletteStrength);
        material.SetShaderParameter("outline_color", new Vector3(0x18, 0x14, 0x25) / 255f);
        return new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(2, 2) },
            MaterialOverride = material,
            ExtraCullMargin = 16384,
        };
    }
}
