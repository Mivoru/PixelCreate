using System;
using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// The ground split by an orc's axe: jagged dark cracks fan out in a small V, broken slabs of rock
/// push up as the front passes and dust kicks off them. The front follows the length from the server.
/// </summary>
public sealed class EarthCrackNode : IGroundHazard
{
    private const float BandLength = 0.5f;
    private const float Lift = 0.05f;

    private readonly Fx _fx;
    private readonly List<Band> _bands = new();
    private readonly TelegraphView _scorch;
    private readonly Vector3 _origin;
    private readonly float _yaw;
    private float _age;

    public Node3D Root { get; } = new();

    private sealed class Band
    {
        public required float Distance;
        public required MeshInstance3D Cracks;
        public readonly List<(MeshInstance3D Rock, Vector3 Home)> Rocks = new();
        public Vector3 Centre;
        public float Shown = -1f;
    }

    public EarthCrackNode(World world, Fx fx, Vector3 origin, float yaw)
    {
        _fx = fx;
        _origin = origin;
        _yaw = yaw;
        var random = new Random((int)(origin.X * 37f + origin.Z * 11f));
        var forward = new Vector3(-Mathf.Sin(yaw), 0, -Mathf.Cos(yaw));
        var side = new Vector3(-forward.Z, 0, forward.X);
        var count = Mathf.CeilToInt(EarthCrack.Length / BandLength);
        var surfaces = new SurfaceTool[count];
        for (var i = 0; i < count; i++)
        {
            surfaces[i] = new SurfaceTool();
            surfaces[i].Begin(Mesh.PrimitiveType.Triangles);
        }

        // Three main cracks fan across the V; each throws off a short branch or two on the way.
        // A branch starts on its parent crack, so each crack also keeps the heading it starts from.
        var cracks = new List<(float Angle, float From, float Start, float End, float Width)>
        {
            (0f, 0f, 0f, EarthCrack.Length, 0.24f),
            (-EarthCrack.HalfAngle * 0.75f, -EarthCrack.HalfAngle * 0.75f, 0.3f, EarthCrack.Length * 0.92f, 0.17f),
            (EarthCrack.HalfAngle * 0.75f, EarthCrack.HalfAngle * 0.75f, 0.3f, EarthCrack.Length * 0.92f, 0.17f),
        };
        for (var c = 0; c < 3; c++)
        {
            var at = 1.2f + (float)random.NextDouble() * 2.5f;
            var turn = (random.Next(2) == 0 ? -1f : 1f) * (0.35f + (float)random.NextDouble() * 0.3f);
            cracks.Add((cracks[c].Angle + turn, cracks[c].Angle, at, at + 0.9f + (float)random.NextDouble() * 1.2f, cracks[c].Width * 0.6f));
        }

        foreach (var (angle, from, start, end, width) in cracks)
        {
            var heading = new Vector3(-Mathf.Sin(yaw + angle), 0, -Mathf.Cos(yaw + angle));
            var across = new Vector3(-heading.Z, 0, heading.X);
            var startPoint = origin + new Vector3(-Mathf.Sin(yaw + from), 0, -Mathf.Cos(yaw + from)) * start;
            var previous = startPoint;
            for (var distance = start + 0.35f; distance <= end; distance += 0.35f)
            {
                var wiggle = ((float)random.NextDouble() - 0.5f) * 0.32f;
                var point = startPoint + heading * (distance - start) + across * wiggle;
                var taper = width * (1f - 0.75f * (distance - start) / Mathf.Max(end - start, 0.1f));
                var band = Math.Clamp((int)(point.DistanceTo(origin) / BandLength), 0, count - 1);
                Ribbon(surfaces[band], world, previous, point, taper);
                previous = point;
            }
        }

        var dark = fx.Stipple(new Color("181425"), 1f);
        var rockColours = new[] { fx.Stipple(new Color("5a6988"), 1f), fx.Stipple(new Color("3a4466"), 1f), fx.Stipple(new Color("733e39"), 1f) };
        for (var i = 0; i < count; i++)
        {
            var mesh = new ArrayMesh();
            surfaces[i].Commit(mesh);
            var node = new MeshInstance3D { Mesh = mesh, MaterialOverride = dark, Visible = false };
            Root.AddChild(node);
            var distance = (i + 0.5f) * BandLength;
            var band = new Band { Distance = i * BandLength, Cracks = node, Centre = origin + forward * distance };

            // Broken slabs push up along the V, more of them where it is wide.
            var slabs = 1 + (int)(distance / 2.2f);
            for (var r = 0; r < slabs && distance > 0.6f; r++)
            {
                var spread = distance * Mathf.Tan(EarthCrack.HalfAngle);
                var spot = origin + forward * (distance + ((float)random.NextDouble() - 0.5f) * 0.4f) + side * (((float)random.NextDouble() * 2f - 1f) * spread);
                spot.Y = world.GroundHeight(spot.X, spot.Z);
                var size = 0.18f + (float)random.NextDouble() * 0.22f;
                var rock = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(size * 1.3f, size * 1.6f, size) },
                    MaterialOverride = rockColours[random.Next(rockColours.Length)],
                    Rotation = new Vector3(((float)random.NextDouble() - 0.5f) * 0.9f, (float)random.NextDouble() * Mathf.Tau, ((float)random.NextDouble() - 0.5f) * 0.9f),
                    Visible = false,
                };
                Root.AddChild(rock);
                band.Rocks.Add((rock, spot + Vector3.Up * size * 0.35f));
            }

            _bands.Add(band);
        }

        _scorch = new TelegraphView(world, fx, Root, new Color("3e2731"));
    }

    public void Update(float length, float delta)
    {
        _age += delta;
        _scorch.ShowWedge(_origin, _yaw, length, 0.45f, EarthCrack.HalfAngle);
        foreach (var band in _bands)
        {
            if (band.Shown < 0f && length >= band.Distance)
            {
                band.Shown = 0f;
                band.Cracks.Visible = true;
                _fx.Burst(band.Centre + Vector3.Up * 0.2f, new Color("b86f50"), 6, 3.5f, 0.45f, 0.09f, -9f, 60f);
                if (band.Rocks.Count > 0)
                {
                    _fx.Burst(band.Centre + Vector3.Up * 0.2f, new Color("5a6988"), 4, 5f, 0.5f, 0.07f, -14f, 50f);
                }
            }

            if (band.Shown < 0f)
            {
                continue;
            }

            // Each slab bursts up from below the ground and settles, quickly.
            band.Shown += delta;
            var rise = Mathf.Clamp(band.Shown / 0.12f, 0f, 1f);
            var overshoot = rise < 1f ? rise * 1.25f : 1f + 0.25f * Mathf.Max(0f, 1f - (band.Shown - 0.12f) / 0.12f);
            foreach (var (rock, home) in band.Rocks)
            {
                rock.Visible = true;
                rock.Position = home + Vector3.Down * 0.45f * (1f - overshoot);
            }
        }
    }

    /// <summary>One straight piece of a crack: a thin dark strip laid on the ground.</summary>
    private static void Ribbon(SurfaceTool surface, World world, Vector3 from, Vector3 to, float width)
    {
        var flat = (to - from) with { Y = 0 };
        if (flat.LengthSquared() < 1e-4f)
        {
            return;
        }

        var side = new Vector3(-flat.Z, 0, flat.X).Normalized() * width / 2f;
        Vector3 Ground(Vector3 p) => new(p.X, world.GroundHeight(p.X, p.Z) + Lift, p.Z);
        var p0 = Ground(from - side);
        var p1 = Ground(from + side);
        var p2 = Ground(to - side * 0.8f);
        var p3 = Ground(to + side * 0.8f);
        surface.AddVertex(p0);
        surface.AddVertex(p2);
        surface.AddVertex(p1);
        surface.AddVertex(p1);
        surface.AddVertex(p2);
        surface.AddVertex(p3);
    }
}
