using System.Collections.Generic;
using System.Linq;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// The big map on M: the whole valley from above, north up. The wheel zooms round the cursor, a drag
/// moves it, a click sets the player's own point and leads there (a click on the point, or a right
/// click, takes it away). It shows the places found, the buildings, the player, the target of the
/// tracked quest and the way to it, which the minimap and the trail on the ground also follow.
/// </summary>
public sealed partial class WorldMap : Control
{
    private readonly float _zoneSize;
    private readonly Texture2D? _picture;
    private readonly List<(Vector2 At, string Name)> _places;
    private readonly List<(Vector2 At, string Kind, string Label)> _marks;
    private readonly Navigator _navigator;
    private readonly World _world;
    private Font _font = null!;

    // The view: the point in the middle of the screen (metres) and how many metres one pixel shows.
    private Vector2 _centre;
    private float _metresPerPixel = 0.6f;
    private Vector2? _pressedAt;
    private bool _dragging;

    /// <summary>Where the player stands (metres, x and z) and where the camera looks, as on the minimap.</summary>
    public Vector2 Player { get; set; }
    public float Yaw { get; set; }

    public WorldMap(Zone zone, Navigator navigator)
    {
        _zoneSize = zone.Size;
        _world = zone.World;
        _navigator = navigator;
        _picture = ResourceLoader.Exists("res://assets/ui/minimap.png") ? GD.Load<Texture2D>("res://assets/ui/minimap.png") : null;
        _places = zone.Places.Select(p => (new Vector2(p.X, p.Z), p.Name)).ToList();
        _marks = Minimap.Landmarks(zone).ToList();
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
    }

    public override void _Ready() => _font = Hud.UiFont(800);

    /// <summary>Opens on the player, showing about a third of the valley.</summary>
    public void Open()
    {
        _centre = Shown(Player);
        _metresPerPixel = Mathf.Clamp(_zoneSize / 3f / Mathf.Max(Size.Y > 0 ? Size.Y : GetViewportRect().Size.Y, 1f), 0.15f, 2f);
        _pressedAt = null;
        _dragging = false;
        Visible = true;
    }

    /// <summary>Inside a building the player is shown at its door, out in the valley.</summary>
    private Vector2 Shown(Vector2 player)
    {
        if (player.X < Guild.InteriorX)
        {
            return player;
        }

        var door = _world.Portals.Where(p => p.From.X < Guild.InteriorX && p.To.X >= Guild.InteriorX)
            .OrderBy(p => new Vector2(p.To.X - player.X, p.To.Z - player.Y).LengthSquared()).Cast<Portal?>().FirstOrDefault();
        return door is { } d ? new Vector2(d.From.X, d.From.Z) : player;
    }

    private Vector2 ToScreen(Vector2 metres) => Size / 2f + (metres - _centre) / _metresPerPixel;

    private Vector2 ToMetres(Vector2 screen) => _centre + (screen - Size / 2f) * _metresPerPixel;

    public override void _Process(double delta)
    {
        // The HUD scales to the window; fill the HUD, as every other window does.
        Position = Vector2.Zero;
        Size = GetParent<Control>()?.Size ?? GetViewportRect().Size;
        if (Visible)
        {
            QueueRedraw();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown, Pressed: true } wheel:
            {
                // Zoom round the point under the cursor.
                var before = ToMetres(wheel.Position);
                var factor = wheel.ButtonIndex == MouseButton.WheelUp ? 0.82f : 1f / 0.82f;
                _metresPerPixel = Mathf.Clamp(_metresPerPixel * factor, 0.08f, _zoneSize / Mathf.Max(Size.Y, 1f) * 1.2f);
                _centre += before - ToMetres(wheel.Position);
                AcceptEvent();
                break;
            }

            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
                _pressedAt = press.Position;
                _dragging = false;
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release:
                if (_pressedAt is not null && !_dragging)
                {
                    Click(release.Position);
                }

                _pressedAt = null;
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }:
                _navigator.SetWaypoint(null);
                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _pressedAt is { } from:
                if (_dragging || motion.Position.DistanceTo(from) > 6f)
                {
                    _dragging = true;
                    _centre -= motion.Relative * _metresPerPixel;
                }

                AcceptEvent();
                break;
        }
    }

    /// <summary>A click sets the own point there, or takes it away when it lands on it.</summary>
    private void Click(Vector2 screen)
    {
        if (_navigator.Waypoint is { } own && ToScreen(own).DistanceTo(screen) < 14f)
        {
            _navigator.SetWaypoint(null);
            return;
        }

        var at = ToMetres(screen);
        var half = _zoneSize / 2f;
        if (Mathf.Abs(at.X) <= half && Mathf.Abs(at.Y) <= half)
        {
            _navigator.SetWaypoint(at);
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("0b0a12"));
        if (_picture is not null)
        {
            var half = _zoneSize / 2f;
            var topLeft = ToScreen(new Vector2(-half, -half));
            var bottomRight = ToScreen(new Vector2(half, half));
            DrawTextureRect(_picture, new Rect2(topLeft, bottomRight - topLeft), false);
        }

        foreach (var (at, kind, label) in _marks)
        {
            var point = ToScreen(at);
            DrawCircle(point, 7f, new Color("181425"));
            DrawCircle(point, 5.5f, kind == "guild" ? new Color("f2d080") : new Color("c0cbdc"));
            if (label.Length > 0 && _metresPerPixel < 0.22f)
            {
                Text(label, point + new Vector2(0, 20), 13, Colors.White);
            }
        }

        foreach (var (at, name) in _places.Where(p => Settings.Discovered.Contains(p.Name)))
        {
            var point = ToScreen(at);
            DrawCircle(point, 4f, new Color("f2d080"));
            Text(name, point + new Vector2(0, -8), 17, new Color("fff2c8"));
        }

        if (_navigator.Target is { } target)
        {
            var colour = target.Own ? Minimap.OwnColour : Minimap.RouteColour;
            var route = _navigator.Route.Select(ToScreen).ToArray();
            if (route.Length >= 2)
            {
                // The way from where the player stands; indoors it starts at the door.
                route[0] = ToScreen(Shown(Player));
                DrawPolyline(route, new Color(0.05f, 0.05f, 0.08f, 0.85f), 7f, true);
                DrawPolyline(route, colour, 3.5f, true);
            }

            var at = ToScreen(target.At);
            if (target.Radius > 0f)
            {
                var ring = target.Radius / _metresPerPixel;
                DrawCircle(at, ring, colour with { A = 0.18f });
                DrawArc(at, ring, 0, Mathf.Tau, 64, colour, 2.5f, true);
            }

            Diamond(at, 10f, colour);
            Text(target.Label, at + new Vector2(0, -16), 16, colour);
        }

        // The player: the same arrow as on the minimap, larger.
        var centre = ToScreen(Shown(Player));
        var forward = new Vector2(-Mathf.Sin(Yaw), -Mathf.Cos(Yaw));
        var side = new Vector2(-forward.Y, forward.X);
        var tip = centre + forward * 14;
        var tail = centre - forward * 8;
        DrawColoredPolygon(new[] { tip, tail + side * 9, centre - forward * 3, tail - side * 9 }, Colors.White);
        DrawPolyline(new[] { tip, tail + side * 9, centre - forward * 3, tail - side * 9, tip }, new Color("181425"), 2f, true);

        // Title and help.
        DrawRect(new Rect2(0, 0, Size.X, 54), new Color(0, 0, 0, 0.55f));
        Text("Map", new Vector2(Size.X / 2f, 37), 26, new Color("f4f0e6"));
        var help = "Click: set a mark and go there    Click the mark or right click: clear    Wheel: zoom    Drag: move    M or Esc: close";
        DrawRect(new Rect2(0, Size.Y - 40, Size.X, 40), new Color(0, 0, 0, 0.55f));
        Text(help, new Vector2(Size.X / 2f, Size.Y - 14), 15, new Color("c9c2b4"));
    }

    private void Diamond(Vector2 at, float size, Color colour)
    {
        var edge = size + 2.5f;
        DrawColoredPolygon(new[] { at + new Vector2(0, -edge), at + new Vector2(edge, 0), at + new Vector2(0, edge), at + new Vector2(-edge, 0) }, new Color("181425"));
        DrawColoredPolygon(new[] { at + new Vector2(0, -size), at + new Vector2(size, 0), at + new Vector2(0, size), at + new Vector2(-size, 0) }, colour);
    }

    private void Text(string text, Vector2 at, int size, Color colour)
    {
        var width = _font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        var start = at - new Vector2(width / 2f, 0);
        DrawStringOutline(_font, start, text, HorizontalAlignment.Left, -1, size, 5, new Color(0, 0, 0, 0.8f));
        DrawString(_font, start, text, HorizontalAlignment.Left, -1, size, colour);
    }
}
