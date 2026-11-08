using System.Collections.Generic;
using System.Linq;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// A round map in the top right corner, north up. It shows the land round the player from a
/// top-down picture of the zone, the four points of the compass on its rim, marks for the Guild,
/// the shops and other buildings, and the names of the places the player has found. The arrow in
/// the middle points where the camera looks.
/// </summary>
public sealed partial class Minimap : Control
{
    /// <summary>How many metres across the circle shows.</summary>
    public const float ViewMetres = 130f;

    private readonly float _zoneSize;
    private readonly ShaderMaterial _material;
    private readonly TextureRect _picture;
    private readonly List<(Vector2 At, string Kind, string Label)> _marks;
    private readonly List<(Vector2 At, string Name)> _places;
    private Font _font = null!;
    private Font _bold = null!;

    /// <summary>The player's place (metres, x and z) and the way the camera looks, in radians.</summary>
    public Vector2 Player { get; set; }
    public float Yaw { get; set; }

    public Minimap(Zone zone)
    {
        _zoneSize = zone.Size;
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/minimap.gdshader") };
        var texture = ResourceLoader.Exists("res://assets/ui/minimap.png") ? GD.Load<Texture2D>("res://assets/ui/minimap.png") : null;
        _picture = new TextureRect
        {
            Texture = texture,
            Material = _material,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,

            // The picture lies under the marks this control draws on top of it.
            ShowBehindParent = true,
        };
        AddChild(_picture);
        MouseFilter = MouseFilterEnum.Ignore;
        _marks = Landmarks(zone).ToList();
        _places = zone.Places.Select(p => (new Vector2(p.X, p.Z), p.Name)).ToList();
    }

    /// <summary>What a building or stand is called on the map, by its model; others are not marked.</summary>
    private static IEnumerable<(Vector2, string, string)> Landmarks(Zone zone)
    {
        foreach (var prop in zone.Props)
        {
            var mark = prop.Model switch
            {
                "guild_hall" => ("guild", "Guild"),
                "alchemy_stall" => ("potion", "Alchemist"),
                "stall" => ("shop", "Market"),
                "tavern" => ("inn", "Tavern"),
                "windmill" => ("mill", "Mill"),
                "watchtower" => ("tower", ""),
                _ => ("", ""),
            };
            if (mark.Item1.Length > 0)
            {
                yield return (new Vector2(prop.Position.X, prop.Position.Z), mark.Item1, mark.Item2);
            }
        }
    }

    public override void _Ready()
    {
        _font = GD.Load<Font>("res://assets/ui/Nunito.ttf");
        _bold = new FontVariation { BaseFont = _font, VariationEmbolden = 0.8f };
    }

    public override void _Process(double delta)
    {
        _picture.Size = Size;
        var inside = Mathf.Abs(Player.X) <= _zoneSize / 2f && Mathf.Abs(Player.Y) <= _zoneSize / 2f;
        _picture.Visible = inside;
        _material.SetShaderParameter("centre", new Vector2(Player.X / _zoneSize + 0.5f, Player.Y / _zoneSize + 0.5f));
        _material.SetShaderParameter("span", ViewMetres / _zoneSize);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var centre = Size / 2f;
        var radius = Size.X / 2f;
        var scale = Size.X / ViewMetres;
        if (!_picture.Visible)
        {
            // Inside a building there is no map: just the disc and the place's name.
            DrawCircle(centre, radius, new Color(0.09f, 0.1f, 0.13f, 0.92f));
            DrawArc(centre, radius - 2, 0, Mathf.Tau, 48, new Color("f2d080"), 3f, true);
            Text("Indoors", centre + new Vector2(0, 6), 16, new Color("dff6f5"), true);
        }
        else
        {
            // First every mark, then the labels: a label goes only where it covers no mark and no other label.
            var icons = new List<Vector2>();
            var labels = new List<Rect2>();
            bool Room(string text, Vector2 at, int size)
            {
                var width = _bold.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
                var box = new Rect2(at.X - width / 2f - 2, at.Y - size, width + 4, size + 3);
                if (box.Position.DistanceTo(centre) > radius - 4 || box.End.DistanceTo(centre) > radius - 4
                    || labels.Any(l => l.Intersects(box)) || icons.Any(i => box.Grow(8.5f).HasPoint(i)))
                {
                    return false;
                }

                labels.Add(box);
                return true;
            }

            var places = new List<(Vector2 Point, string Name)>();
            foreach (var (at, name) in _places)
            {
                var point = centre + (at - Player) * scale;
                if (Settings.Discovered.Contains(name) && point.DistanceTo(centre) < radius - 18)
                {
                    DrawCircle(point, 3.5f, new Color("f2d080"));
                    places.Add((point, name));
                }
            }

            var marks = new List<(Vector2 Point, string Label)>();
            foreach (var (at, kind, label) in _marks)
            {
                var point = centre + (at - Player) * scale;
                if (point.DistanceTo(centre) < radius - 10)
                {
                    Mark(point, kind);
                    icons.Add(point);
                    marks.Add((point, label));
                }
            }

            foreach (var (point, label) in marks)
            {
                if (label.Length > 0 && !marks.Any(m => m.Label == label && m.Point.X < point.X && m.Point.DistanceTo(point) < 40f)
                    && Room(label, point + new Vector2(0, 20), 11))
                {
                    Text(label, point + new Vector2(0, 20), 11, new Color("ffffff"), true);
                }
            }

            foreach (var (point, name) in places)
            {
                if (Room(name, point + new Vector2(0, -7), 12))
                {
                    Text(name, point + new Vector2(0, -7), 12, new Color("fff2c8"), true);
                }
            }
        }

        // The player: an arrow that points where the camera looks; north is up.
        var forward = new Vector2(-Mathf.Sin(Yaw), -Mathf.Cos(Yaw));
        var side = new Vector2(-forward.Y, forward.X);
        var tip = centre + forward * 10;
        var tail = centre - forward * 6;
        DrawColoredPolygon(new[] { tip, tail + side * 7, centre - forward * 2, tail - side * 7 }, new Color("ffffff"));
        DrawPolyline(new[] { tip, tail + side * 7, centre - forward * 2, tail - side * 7, tip }, new Color("181425"), 1.5f, true);

        foreach (var (letter, direction) in new[] { ("N", new Vector2(0, -1)), ("E", new Vector2(1, 0)), ("S", new Vector2(0, 1)), ("W", new Vector2(-1, 0)) })
        {
            var at = centre + direction * (radius - 1);
            DrawCircle(at, 11, new Color(0.07f, 0.07f, 0.1f, 0.95f));
            Text(letter, at + new Vector2(0, 5), 14, letter == "N" ? new Color("ff6b6b") : new Color("f2d080"), true);
        }
    }

    /// <summary>A small round badge with a picture for a kind of place.</summary>
    private void Mark(Vector2 at, string kind)
    {
        var colour = kind switch
        {
            "guild" => new Color("f2d080"),
            "potion" => new Color("b55088"),
            "shop" => new Color("63c74d"),
            "inn" => new Color("f77622"),
            _ => new Color("c0cbdc"),
        };
        DrawCircle(at, 8.5f, new Color("181425"));
        DrawCircle(at, 7f, colour);
        var ink = new Color("181425");
        switch (kind)
        {
            case "guild":
                // A shield.
                DrawColoredPolygon(new[] { at + new Vector2(-4, -4), at + new Vector2(4, -4), at + new Vector2(4, 1), at + new Vector2(0, 5), at + new Vector2(-4, 1) }, ink);
                break;
            case "potion":
                DrawCircle(at + new Vector2(0, 1.5f), 3.5f, ink);
                DrawRect(new Rect2(at + new Vector2(-1.2f, -5), 2.4f, 4), ink);
                break;
            case "shop":
                DrawRect(new Rect2(at + new Vector2(-4, -1), 8, 5), ink);
                DrawColoredPolygon(new[] { at + new Vector2(-5, -1), at + new Vector2(0, -5), at + new Vector2(5, -1) }, ink);
                break;
            case "inn":
                DrawRect(new Rect2(at + new Vector2(-3, -4), 6, 8), ink);
                DrawRect(new Rect2(at + new Vector2(3, -2), 2, 4), ink);
                break;
            default:
                DrawCircle(at, 2.5f, ink);
                break;
        }
    }

    private void Text(string text, Vector2 at, int size, Color colour, bool centred)
    {
        var width = _bold.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        var start = centred ? at - new Vector2(width / 2f, 0) : at;
        DrawStringOutline(_bold, start, text, HorizontalAlignment.Left, -1, size, 4, new Color(0, 0, 0, 0.75f));
        DrawString(_bold, start, text, HorizontalAlignment.Left, -1, size, colour);
    }
}
