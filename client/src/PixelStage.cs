using System.Linq;
using Godot;

namespace PixelCreate;

/// <summary>A low-resolution 3D view scaled up by a whole number, with the pixel post shader.</summary>
public sealed class PixelStage
{
    /// <summary>The render height to aim for. Lower it for bigger pixels, raise it for a finer picture.</summary>
    /// <summary>
    /// The 3D view renders at about this many lines and is scaled up by whole pixels. 1080 keeps a
    /// 1080p window sharp; PIXEL_LINES=540 brings back the chunky look.
    /// </summary>
    private static readonly float TargetHeight = float.TryParse(OS.GetEnvironment("PIXEL_LINES"), out var lines) && lines > 100 ? lines : 1080f;

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

        var environment = BuildEnvironment();
        _environment = environment.Environment;
        View.AddChild(environment);
        View.AddChild(_sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-38, -50, 0),
            LightColor = new Color("fff0d8"),
            LightEnergy = 1.2f,
            ShadowEnabled = !Off("shadow"),
            ShadowBlur = 1.6f,
            DirectionalShadowMaxDistance = 90f,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits,
        });

        Camera = new Camera3D { Fov = 58, Far = 260 };
        View.AddChild(Camera);
        BuildMoon();
        PostQuad = BuildPostQuad();
        Camera.AddChild(PostQuad);
    }

    /// <summary>
    /// Stops a figure drawing beyond a distance, where it would be a few pixels anyway, and keeps
    /// its tiny parts out of the shadow pass.
    /// </summary>
    public static void Limit(Node node, float range)
    {
        if (node is GeometryInstance3D geometry)
        {
            geometry.VisibilityRangeEnd = range;
            if (geometry.GetAabb().Size.Length() < 0.12f)
            {
                geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }

        foreach (var child in node.GetChildren())
        {
            Limit(child, range);
        }
    }

    private Godot.Environment _environment = null!;
    private DirectionalLight3D _sun = null!;

    /// <summary>
    /// Darkens the world towards a cold, misty night, from 0 (a clear day) to 1 (Moonfell): the light
    /// turns blue and weak, the fog thickens and greys the sky.
    /// </summary>
    public void SetGloom(float gloom)
    {
        var g = Mathf.Clamp(Mathf.Max(gloom, _night * 0.55f), 0f, 1f);
        _environment.AmbientLightColor = new Color("cfd8ea").Lerp(new Color("6a7898"), g);
        _environment.AmbientLightEnergy = Mathf.Lerp(0.82f, 0.66f, g);
        _environment.FogLightColor = new Color("bcd8ea").Lerp(new Color("4e5c7a"), g);
        _environment.FogDensity = Mathf.Lerp(0.0024f, 0.0085f, g);
        _environment.FogSkyAffect = 0.6f * g;
        _environment.VolumetricFogDensity = Mathf.Lerp(0.002f, 0.009f, g);
        _environment.VolumetricFogAlbedo = new Color("fff3e0").Lerp(new Color("9aa8c8"), g);
        _sun.LightColor = new Color("fff0d8").Lerp(new Color("a8b8e8"), g).Lerp(new Color("ffb070"), _dusk * (1f - _night));
        _sun.LightEnergy = Mathf.Lerp(1.2f, 0.5f, g) * Mathf.Lerp(1f, 0.35f, _night);
        _environment.AmbientLightEnergy *= Mathf.Lerp(1f, 0.62f, _night);
        _sun.RotationDegrees = new Vector3(Mathf.Lerp(-38f, -14f, Mathf.Max(_dusk, _night)), -50f + _sunTurn, 0);
        if (_environment.Sky?.SkyMaterial is ShaderMaterial sky)
        {
            var top = new Color(0.24f, 0.53f, 0.86f).Lerp(new Color(0.05f, 0.07f, 0.16f), _night);
            var horizon = new Color(0.78f, 0.9f, 0.96f).Lerp(new Color(0.98f, 0.62f, 0.42f), _dusk * (1f - _night)).Lerp(new Color(0.14f, 0.17f, 0.3f), _night);
            sky.SetShaderParameter("top_color", top);
            sky.SetShaderParameter("horizon_color", horizon);
            sky.SetShaderParameter("cloud_color", new Color(1f, 0.99f, 0.96f).Lerp(new Color(0.3f, 0.33f, 0.45f), _night));
            sky.SetShaderParameter("cloud_shade", new Color(0.72f, 0.8f, 0.9f).Lerp(new Color(0.18f, 0.2f, 0.3f), _night));
        }

        var moon = Mathf.Max(g, _night);
        _moon.Visible = moon > 0.02f;
        _moonMaterial.SetShaderParameter("strength", moon);
    }

    private float _night;
    private float _dusk;
    private float _sunTurn;

    /// <summary>
    /// The time of day from the story calendar: night from about nine in the evening to five in
    /// the morning, a warm dusk and dawn between. Takes effect at the next <see cref="SetGloom"/>.
    /// </summary>
    public void SetTime(int minute)
    {
        var hour = minute / 60f;
        _night = hour switch
        {
            < 5f => 1f,
            < 7f => 1f - (hour - 5f) / 2f,
            < 18.5f => 0f,
            < 21f => (hour - 18.5f) / 2.5f,
            _ => 1f,
        };
        _dusk = Mathf.Clamp(Mathf.Max(1f - Mathf.Abs(hour - 19.2f) / 1.6f, 1f - Mathf.Abs(hour - 6.3f) / 1.2f), 0f, 1f);
        _sunTurn = Mathf.Clamp((hour - 12f) * 9f, -80f, 80f);
    }

    /// <summary>A great pale moon low over the north-west, seen where the world is cursed.</summary>
    private MeshInstance3D _moon = null!;
    private ShaderMaterial _moonMaterial = null!;

    private void BuildMoon()
    {
        _moonMaterial = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, fog_disabled;
uniform float strength = 0.0;
void vertex() {
    // Always face the camera.
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float r = length(p);
    float disc = 1.0 - smoothstep(0.42, 0.45, r);
    float halo = (1.0 - smoothstep(0.45, 1.0, r)) * 0.25;
    // Grey seas on the face of the moon.
    float seas = 0.08 * step(0.6, sin(p.x * 9.0 + 1.3) * sin(p.y * 7.0 - 0.4) + 0.5);
    vec3 face = vec3(0.92, 0.94, 0.86) - seas;
    ALBEDO = (face * disc + vec3(0.55, 0.62, 0.8) * halo) * strength;
}" } };
        _moon = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(70, 70) }, MaterialOverride = _moonMaterial, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        View.AddChild(_moon);
    }

    /// <summary>Keeps the moon far off in the same part of the sky, wherever the camera goes.</summary>
    public void PlaceMoon()
    {
        if (_moon.Visible)
        {
            _moon.GlobalPosition = Camera.GlobalPosition + new Vector3(-0.86f, 0.3f, 0.42f).Normalized() * 230f;
        }
    }

    public static bool Off(string feature) => OS.GetEnvironment("PIXEL_OFF").Split(',').Contains(feature);

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
                VolumetricFogEnabled = !Off("fog"),
                VolumetricFogDensity = 0.002f,
                VolumetricFogAlbedo = new Color("fff3e0"),
                VolumetricFogLength = 90f,
                VolumetricFogAnisotropy = 0.55f,
                GlowEnabled = !Off("glow"),
                GlowIntensity = 0.35f,
                GlowBloom = 0.04f,
                GlowHdrThreshold = 1.05f,
                SsaoEnabled = !Off("ssao"),
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
