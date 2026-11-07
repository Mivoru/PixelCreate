using Godot;

namespace PixelCreate;

/// <summary>How a bow is made: its size, how strongly its limbs curve, and its colours.</summary>
public sealed record BowLook(float Length, float Recurve, Color Wood, Color WoodLight, Color Grip, Color Tips, Color String)
{
    /// <summary>A plain self bow of pale wood with a leather grip.</summary>
    public static readonly BowLook Plain = new(1.2f, 0.1f, new Color("733e39"), new Color("b86f50"), new Color("3e2731"), new Color("ead4aa"), new Color("ead4aa"));
}

/// <summary>
/// A bow that bends. Its two limbs are rebuilt as tapered strips whenever the draw changes: they
/// curve back towards the archer as the string is pulled, keeping their length, so the tips move
/// in. The string runs from each tip to the nocking point, which follows the drawing hand, and an
/// arrow lies on the string while the bow is drawn.
///
/// The bow's own frame: the limbs run along Z (the grip at the origin), and the string side, towards
/// the archer, is +Y. The arrow points along -Y.
/// </summary>
public sealed partial class BowView : Node3D
{
    private const int Segments = 9;

    private readonly BowLook _look;
    private readonly MeshInstance3D _limbs = new();
    private readonly MeshInstance3D _upperString = new();
    private readonly MeshInstance3D _lowerString = new();
    private readonly Node3D _arrow = new();
    private float _builtBend = -1f;
    private Vector3 _topTip;
    private Vector3 _bottomTip;

    /// <summary>How far the string sits behind the grip when the bow is at rest.</summary>
    private float Brace => _look.Length * 0.14f;

    /// <summary>How far the string is drawn, from 0 (at rest) to 1 (full draw).</summary>
    public float Draw { get; set; }

    /// <summary>Where the drawing hand holds the string, in the bow's frame, or null when it does not.</summary>
    public Vector3? Hand { get; set; }

    /// <summary>Shows an arrow on the string.</summary>
    public bool Nocked { get; set; }

    public BowView(BowLook look)
    {
        _look = look;
        AddChild(_limbs);
        foreach (var part in new[] { _upperString, _lowerString })
        {
            part.Mesh = new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.006f, Height = 1f, RadialSegments = 4, Rings = 1 };
            part.MaterialOverride = Material(look.String);
            AddChild(part);
        }

        // Grip wrap and arrow rest.
        var grip = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.055f, 0.07f, 0.17f) }, MaterialOverride = Material(look.Grip) };
        AddChild(grip);
        BuildArrow();
        AddChild(_arrow);
        Update();
    }

    /// <summary>Rebuilds what changed since the last frame. Call it after setting Draw and Hand.</summary>
    public void Update()
    {
        var bend = Mathf.Clamp(Draw, 0f, 1f);
        if (Mathf.Abs(bend - _builtBend) > 0.01f)
        {
            BuildLimbs(bend);
            _builtBend = bend;
        }

        // The nocking point: on the string's line, pulled back by the hand, never past a full draw.
        var rest = new Vector3(0, _topTip.Y, 0);
        var nock = rest;
        if (Hand is { } hand)
        {
            var pull = Mathf.Clamp(hand.Y, rest.Y, rest.Y + _look.Length * 0.55f);
            nock = new Vector3(0, pull, Mathf.Clamp(hand.Z, -0.05f, 0.05f));
        }

        Stretch(_upperString, _topTip, nock);
        Stretch(_lowerString, _bottomTip, nock);
        _arrow.Visible = Nocked;
        _arrow.Position = nock;
    }

    /// <summary>
    /// Both limbs as tapered strips that curve back by the bend. Each limb is laid out segment by
    /// segment at an angle that grows towards the tip, so its length stays the same however far it bends.
    /// </summary>
    private void BuildLimbs(float bend)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var half = _look.Length / 2f;
        const float gripHalf = 0.08f;
        var step = (half - gripHalf) / Segments;
        foreach (var side in new[] { -1f, 1f })
        {
            var point = new Vector3(0, 0.015f, side * gripHalf);
            var angle = 0.1f;
            var points = new Vector3[Segments + 1];
            points[0] = point;
            for (var i = 1; i <= Segments; i++)
            {
                var s = i / (float)Segments;
                // A gentle curve at rest, a deep one at full draw, and a flick at the tips for a recurve.
                angle += (0.03f + 0.07f * bend) * (0.6f + s) - (s > 0.8f ? _look.Recurve * 1.8f : 0f);
                point += new Vector3(0, Mathf.Sin(angle), side * Mathf.Cos(angle)) * step;
                points[i] = point;
            }

            for (var i = 0; i < Segments; i++)
            {
                var s0 = i / (float)Segments;
                var s1 = (i + 1) / (float)Segments;
                Strip(surface, points[i], points[i + 1], Width(s0), Width(s1), Thick(s0), Thick(s1), side,
                    _look.Wood.Lerp(_look.WoodLight, 0.5f - 0.5f * Mathf.Cos(s0 * Mathf.Pi)));
            }

            var tip = points[Segments];
            if (side < 0f) _bottomTip = tip; else _topTip = tip;
            // Horn nocks at the tips.
            Box(surface, tip, new Vector3(0.03f, 0.03f, 0.05f), _look.Tips);
        }

        surface.GenerateNormals();
        _limbs.Mesh = surface.Commit();
        if (_limbs.MaterialOverride is null)
        {
            _limbs.MaterialOverride = VertexMaterial();
        }
    }

    private static float Width(float s) => Mathf.Lerp(0.05f, 0.022f, s);

    private static float Thick(float s) => Mathf.Lerp(0.04f, 0.018f, s);

    /// <summary>One segment of a limb: a box tapering from one point to the next, coloured by vertex.</summary>
    private static void Strip(SurfaceTool surface, Vector3 a, Vector3 b, float wa, float wb, float ta, float tb, float side, Color color)
    {
        var along = (b - a).Normalized();
        var across = Vector3.Right;
        var up = across.Cross(along).Normalized() * side;
        Vector3[] Ring(Vector3 c, float w, float t) => new[]
        {
            c - across * w / 2 - up * t / 2, c + across * w / 2 - up * t / 2,
            c + across * w / 2 + up * t / 2, c - across * w / 2 + up * t / 2,
        };
        var r0 = Ring(a, wa, ta);
        var r1 = Ring(b, wb, tb);
        surface.SetColor(color);
        for (var k = 0; k < 4; k++)
        {
            var n = (k + 1) % 4;
            Quad(surface, r0[k], r0[n], r1[n], r1[k]);
        }
    }

    private static void Box(SurfaceTool surface, Vector3 centre, Vector3 size, Color color)
    {
        surface.SetColor(color);
        var h = size / 2f;
        Vector3 P(float x, float y, float z) => centre + new Vector3(x * h.X, y * h.Y, z * h.Z);
        Quad(surface, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1));
        Quad(surface, P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1));
        Quad(surface, P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1));
        Quad(surface, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1));
        Quad(surface, P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1));
        Quad(surface, P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1));
    }

    private static void Quad(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        surface.AddVertex(a);
        surface.AddVertex(b);
        surface.AddVertex(c);
        surface.AddVertex(a);
        surface.AddVertex(c);
        surface.AddVertex(d);
    }

    /// <summary>A thin cylinder from one point to another.</summary>
    private static void Stretch(MeshInstance3D part, Vector3 from, Vector3 to)
    {
        var length = from.DistanceTo(to);
        var direction = (to - from) / Mathf.Max(length, 0.0001f);
        var basis = new Basis(new Quaternion(Vector3.Up, direction));
        part.Transform = new Transform3D(basis.Scaled(new Vector3(1, length, 1)), (from + to) / 2f);
    }

    /// <summary>An arrow lying on the string: shaft, iron head and fletching, pointing along -Y.</summary>
    private void BuildArrow()
    {
        const float length = 0.78f;
        var shaft = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.009f, BottomRadius = 0.009f, Height = length, RadialSegments = 5, Rings = 1 },
            MaterialOverride = Material(new Color("b86f50")),
            Position = new Vector3(0, -length / 2f, 0),
        };
        _arrow.AddChild(shaft);
        var head = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.025f, Height = 0.08f, RadialSegments = 4, Rings = 1 },
            MaterialOverride = Material(new Color("8b9bb4")),
            Position = new Vector3(0, -length - 0.04f, 0),
            RotationDegrees = new Vector3(180, 0, 0),
        };
        _arrow.AddChild(head);
        foreach (var angle in new[] { 0f, 120f, 240f })
        {
            var feather = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.004f, 0.11f, 0.035f) },
                MaterialOverride = Material(new Color("e43b44")),
                Position = new Vector3(0, -0.09f, 0),
                RotationDegrees = new Vector3(0, angle, 0),
            };
            feather.Position += feather.Basis * new Vector3(0, 0, 0.02f);
            _arrow.AddChild(feather);
        }
    }

    private static ShaderMaterial Material(Color color)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/toon.gdshader") };
        material.SetShaderParameter("base", color);
        material.SetShaderParameter("shade", color.Lerp(new Color("4a3e78"), 0.3f));
        return material;
    }

    /// <summary>The limbs carry their colours per vertex, so the wood can shade from dark at the grip to light at the tips.</summary>
    private static ShaderMaterial VertexMaterial()
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/toon.gdshader") };
        material.SetShaderParameter("vertex_tint", 1f);
        return material;
    }
}
