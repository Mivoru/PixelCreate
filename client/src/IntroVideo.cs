using Godot;

namespace PixelCreate;

/// <summary>
/// A scene's film over everything (the intro): black behind, the video fitted to the window with its
/// shape kept, and a caption from a second of the film (over the black after its end, for the title).
/// The film keeps its own clock: a client that loads slowly would otherwise lose the end. When the
/// film and its title have played, <see cref="Done"/> is set and GameClient moves the scene on.
/// Any of Space, Enter or Esc skips it (GameClient).
/// </summary>
public partial class IntroVideo : Control
{
    private readonly ColorRect _black = new() { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
    private readonly VideoStreamPlayer _player = new() { Expand = true, MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _caption = new() { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
    private string _file = "";
    private double _shown;
    private double _length;

    /// <summary>Set once the film and the time after it (the title) have played.</summary>
    public bool Done { get; private set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_black);
        AddChild(_player);
        AddChild(_caption);
        _player.Bus = AudioServer.GetBusIndex("Music") >= 0 ? "Music" : "Master";
        _caption.AddThemeFontOverride("font", Hud.UiFont(800));
        _caption.AddThemeFontSizeOverride("font_size", 56);
        _caption.AddThemeColorOverride("font_color", new Color("f4f0e6"));
        Visible = false;
    }

    /// <summary>
    /// Shows the film <paramref name="file"/>. The step lasts <paramref name="seconds"/> of film time;
    /// the caption shows from <paramref name="captionFrom"/>.
    /// </summary>
    public void Show(string file, float seconds, string caption, float captionFrom, double delta)
    {
        if (file != _file)
        {
            _file = file;
            _player.Stream = ResourceLoader.Exists($"res://assets/video/{file}.ogv") ? GD.Load<VideoStream>($"res://assets/video/{file}.ogv") : null;
            if (_player.Stream is null)
            {
                GD.PrintErr($"No video res://assets/video/{file}.ogv");
            }
        }

        if (!Visible)
        {
            Visible = true;
            Done = false;
            _shown = 0;
            _length = _player.Stream is null ? 0 : _player.GetStreamLength();
            _player.Play();
        }

        // Film time: the player's position while it plays, then the time spent on the black after it.
        _shown = _player.IsPlaying() ? _player.StreamPosition : Mathf.Max(_shown, _length) + delta;
        Done = !_player.IsPlaying() && _shown >= seconds;

        // Fit 16:9 inside the window: bars rather than a stretched picture. The HUD does not lay out its
        // children, so the window's size is taken here (anchors left this control at 0 x 0).
        var size = GetViewportRect().Size;
        Position = Vector2.Zero;
        Size = size;
        _black.Size = size;
        var fit = Mathf.Min(size.X / 16f, size.Y / 9f);
        var shown = new Vector2(16f * fit, 9f * fit);
        _player.Position = (size - shown) / 2f;
        _player.Size = shown;
        _player.Visible = _player.IsPlaying();

        _caption.Text = caption.Length > 0 && _shown >= captionFrom ? caption : "";
        _caption.Position = new Vector2(0, size.Y / 2f - 30f);
        _caption.Size = new Vector2(size.X, 60f);
    }

    public new void Hide()
    {
        if (Visible)
        {
            _player.Stop();
            Visible = false;
        }
    }
}
