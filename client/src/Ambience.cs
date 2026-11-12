using System.Collections.Generic;
using System.Linq;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// Beds of sound under the game: songbirds by day, crickets at night, rain when it rains, running water near a river
/// or the falls, and dripping stone underground. Each bed is a 20 s recording faded in and out at its ends; two
/// players take turns and overlap by the fade, so the loop has no seam. Levels ease, so walking into the archive or
/// out of the rain fades rather than cuts.
/// </summary>
public sealed class Ambience
{
    private const double Overlap = 0.5;

    private sealed class Bed
    {
        public required AudioStreamPlayer A { get; init; }
        public required AudioStreamPlayer B { get; init; }
        public double Length { get; init; }
        public bool OnA { get; set; } = true;
        public float Shown { get; set; }
    }

    private readonly Dictionary<string, Bed> _beds = new();
    private readonly List<(System.Numerics.Vector2[] Points, float Width)> _rivers;
    private readonly Vector3? _falls;
    private float _water;
    private float _waterIn;

    public Ambience(Node parent, Zone zone)
    {
        _rivers = zone.RiverCourses.ToList();
        _falls = zone.Waterfall is { } w ? new Vector3(w.X, w.Y, w.Z) : null;
        foreach (var name in new[] { "amb_day", "amb_night", "amb_rain", "amb_river", "amb_cave" })
        {
            if (GD.Load<AudioStream>($"res://assets/sounds/{name}.wav") is not { } stream)
            {
                continue;
            }

            var a = new AudioStreamPlayer { Stream = stream, Bus = "SFX", VolumeDb = -80f };
            var b = new AudioStreamPlayer { Stream = stream, Bus = "SFX", VolumeDb = -80f };
            parent.AddChild(a);
            parent.AddChild(b);
            _beds[name] = new Bed { A = a, B = b, Length = stream.GetLength() };
        }
    }

    /// <param name="at">Where the player is shown.</param>
    /// <param name="indoors">Inside a building's interior, where only a little rain comes through.</param>
    /// <param name="underground">In a dungeon.</param>
    public void Update(Vector3 at, float delta, bool indoors, bool underground, bool muted)
    {
        _waterIn -= delta;
        if (_waterIn <= 0f)
        {
            // How near running water is: full on a river bank or under the falls, nothing 25 m off.
            _waterIn = 0.25f;
            var nearest = float.MaxValue;
            foreach (var (points, width) in _rivers)
            {
                nearest = Mathf.Min(nearest, Distance(points, at.X, at.Z) - width);
            }

            var river = Mathf.Clamp(1f - nearest / 25f, 0f, 1f);
            var falls = _falls is { } f ? Mathf.Clamp(1.3f - new Vector2(at.X - f.X, at.Z - f.Z).Length() / 40f, 0f, 1.3f) : 0f;
            _water = Mathf.Max(river, falls);
        }

        var outside = indoors || underground ? 0f : 1f;
        var rain = Ambient.Rain;
        var night = Ambient.Night;
        var quiet = 1f - rain * 0.7f;
        Set("amb_day", (1f - night) * quiet * outside, delta, muted);
        Set("amb_night", night * quiet * outside, delta, muted);
        Set("amb_rain", underground ? 0f : rain * (indoors ? 0.3f : 1f), delta, muted);
        Set("amb_river", _water * outside, delta, muted);
        Set("amb_cave", underground ? 1f : 0f, delta, muted);
    }

    private void Set(string name, float level, float delta, bool muted)
    {
        if (!_beds.TryGetValue(name, out var bed))
        {
            return;
        }

        bed.Shown = Mathf.MoveToward(bed.Shown, muted ? 0f : level, delta * 0.6f);
        if (bed.Shown <= 0.001f)
        {
            bed.A.Stop();
            bed.B.Stop();
            return;
        }

        var current = bed.OnA ? bed.A : bed.B;
        if (!bed.A.Playing && !bed.B.Playing)
        {
            // Start somewhere inside the recording, so two beds never begin together.
            current.Play((float)(GD.Randf() * (bed.Length - 2 * Overlap)));
        }
        else if (current.Playing && current.GetPlaybackPosition() >= bed.Length - Overlap)
        {
            bed.OnA = !bed.OnA;
            (bed.OnA ? bed.A : bed.B).Play();
        }

        var db = Mathf.LinearToDb(bed.Shown);
        bed.A.VolumeDb = db;
        bed.B.VolumeDb = db;
    }

    private static float Distance(System.Numerics.Vector2[] points, float x, float z)
    {
        var best = float.MaxValue;
        for (var i = 0; i + 1 < points.Length; i++)
        {
            var a = new Vector2(points[i].X, points[i].Y);
            var b = new Vector2(points[i + 1].X, points[i + 1].Y);
            var p = new Vector2(x, z);
            var ab = b - a;
            var t = ab.LengthSquared() > 0.0001f ? Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f) : 0f;
            best = Mathf.Min(best, p.DistanceTo(a + ab * t));
        }

        return best;
    }
}
