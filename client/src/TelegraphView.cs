using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>A warning marked on the ground. The mesh follows the terrain, so slopes cannot hide it.</summary>
public sealed class TelegraphView
{
    private const float Lift = 0.14f;
    private const int Rings = 5;
    private const int Segments = 28;

    private readonly World _world;
    private readonly ArrayMesh _mesh = new();
    private readonly ShaderMaterial _fill;
    private readonly MeshInstance3D _node;
    private Vector3 _lastCenter;
    private Vector3 _lastEnd;
    private float _lastRadius;

    public TelegraphView(World world, Fx fx, Node parent)
    {
        _world = world;
        _fill = fx.Stipple(new Color("e43b44"), 0.55f);
        _node = new MeshInstance3D { Mesh = _mesh, MaterialOverride = _fill, Visible = false };
        parent.AddChild(_node);
    }

    public void Hide() => _node.Visible = false;

    /// <summary>Shows a filled circle. The fill grows stronger as the attack draws near.</summary>
    public void ShowCircle(Vector3 center, float radius, float progress)
    {
        _node.Visible = true;
        _fill.SetShaderParameter("strength", 0.4f + progress * 0.45f);
        if (center.DistanceSquaredTo(_lastCenter) > 0.01f || !Mathf.IsEqualApprox(radius, _lastRadius))
        {
            _lastCenter = center;
            _lastRadius = radius;
            BuildCircle(center, radius);
        }
    }

    /// <summary>Shows a strip along the path of a charge or an arrow.</summary>
    public void ShowLine(Vector3 from, Vector3 to, float width, float progress)
    {
        _node.Visible = true;
        _fill.SetShaderParameter("strength", 0.3f + progress * 0.3f);
        if (from.DistanceSquaredTo(_lastCenter) > 0.01f || to.DistanceSquaredTo(_lastEnd) > 0.01f)
        {
            _lastCenter = from;
            _lastEnd = to;
            BuildLine(from, to, width);
        }
    }

    private void BuildCircle(Vector3 center, float radius)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (var ring = 0; ring < Rings; ring++)
        {
            var inner = radius * ring / Rings;
            var outer = radius * (ring + 1) / Rings;
            for (var i = 0; i < Segments; i++)
            {
                var a = i / (float)Segments * Mathf.Tau;
                var b = (i + 1) / (float)Segments * Mathf.Tau;
                var p0 = Ground(center, a, inner);
                var p1 = Ground(center, b, inner);
                var p2 = Ground(center, a, outer);
                var p3 = Ground(center, b, outer);
                surface.AddVertex(p0);
                surface.AddVertex(p2);
                surface.AddVertex(p3);
                surface.AddVertex(p0);
                surface.AddVertex(p3);
                surface.AddVertex(p1);
            }
        }

        Commit(surface);
    }

    private void BuildLine(Vector3 from, Vector3 to, float width)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var flat = new Vector3(to.X - from.X, 0, to.Z - from.Z);
        var length = flat.Length();
        if (length < 0.1f)
        {
            _node.Visible = false;
            return;
        }

        var forward = flat / length;
        var side = new Vector3(-forward.Z, 0, forward.X) * width / 2f;
        var steps = Mathf.Clamp((int)(length / 1.2f), 2, 40);
        for (var i = 0; i < steps; i++)
        {
            var a = from + forward * (length * i / steps);
            var b = from + forward * (length * (i + 1) / steps);
            var p0 = OnGround(a - side);
            var p1 = OnGround(a + side);
            var p2 = OnGround(b - side);
            var p3 = OnGround(b + side);
            surface.AddVertex(p0);
            surface.AddVertex(p2);
            surface.AddVertex(p1);
            surface.AddVertex(p1);
            surface.AddVertex(p2);
            surface.AddVertex(p3);
        }

        Commit(surface);
    }

    private void Commit(SurfaceTool surface)
    {
        _mesh.ClearSurfaces();
        surface.Commit(_mesh);
    }

    private Vector3 Ground(Vector3 center, float angle, float distance) =>
        OnGround(center + new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance));

    private Vector3 OnGround(Vector3 point) => new(point.X, _world.GroundHeight(point.X, point.Z) + Lift, point.Z);
}
