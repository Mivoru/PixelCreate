using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// The screen the game opens on: Continue (the newest save), New Game (a named slot), Load Game (every
/// save with its day, level and place, and Delete with a question first), Settings and Quit. A choice
/// starts the game's own server for that slot (<see cref="LocalServer"/>) and then the game.
/// With a host on the command line (scripts, bots, a server elsewhere) the menu is skipped.
/// <c>-- menu &lt;folder&gt;</c> photographs every page into the folder and quits, to check the look.
/// </summary>
public partial class MainMenu : Control
{
    private const string GameScene = "res://scenes/arena.tscn";
    private const string CheckSlot = "Menu Check";
    private static readonly Color Ink = new("f4f0e6");
    private static readonly Color Dim = new("c9c2b4");
    private static readonly Color Accent = new("f2c14e");
    private static readonly Color Danger = new("e46a6a");
    private static readonly Color Glass = new(0.05f, 0.06f, 0.09f, 0.82f);

    private readonly VBoxContainer _panel = new();
    private PanelContainer _card = null!;
    private (string Name, float X, float Z, float Radius)[] _places = Array.Empty<(string, float, float, float)>();
    private string _page = "";
    private bool _busy;

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        if (args.Length > 0 && args[0] != "menu")
        {
            // Started with a host: straight into the game, as scripts and bots expect.
            Launch.FromMenu = false;
            CallDeferred(MethodName.EnterGame);
            return;
        }

        GetTree().AutoAcceptQuit = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Settings.Load();
        Settings.Apply();
        try
        {
            _places = Zone.RegionsOf("meadow-valley");
        }
        catch (ArgumentException)
        {
            // No places to name: saves show without one.
        }

        Build();
        ShowMain();
        if (args.Length > 1)
        {
            _ = Tour(args[1]);
        }
        else if (OS.GetEnvironment("PIXEL_MENU_CHECK") == "1")
        {
            // The whole loop once: a new game through the menu, back to the menu with Esc, the save there.
            if (SaveFile.List().FirstOrDefault(s => s.Slot == CheckSlot) is { } back)
            {
                GD.Print($"MENU check passed: back in the menu, {CheckSlot} saved on day {back.Day} at level {back.Level}");
                SaveFile.Delete(CheckSlot);
                GetTree().Quit();
            }
            else
            {
                GD.Print("MENU check: starting a new game");
                Begin(CheckSlot);
            }
        }
    }

    private void EnterGame() => GetTree().ChangeSceneToFile(GameScene);

    private void Build()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = MenuTheme();
        var background = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/ui/menu_bg.jpg"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(background);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Darker on the left, where the words are.
        var shade = new TextureRect { MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize };
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0, 0, 0, 0.78f));
        gradient.SetColor(1, new Color(0, 0, 0, 0.05f));
        shade.Texture = new GradientTexture2D { Gradient = gradient, FillFrom = new Vector2(0, 0), FillTo = new Vector2(1, 0), Width = 256, Height = 4 };
        AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var title = new Label { Text = "Forty Crowns" };
        title.AddThemeFontOverride("font", Hud.UiFont(800));
        title.AddThemeFontSizeOverride("font_size", 72);
        title.AddThemeColorOverride("font_color", Ink);
        title.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
        title.AddThemeConstantOverride("shadow_offset_y", 4);
        AddChild(title);
        title.Position = new Vector2(96, 84);

        _card = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = Glass, BorderColor = new Color(1, 1, 1, 0.12f), ContentMarginLeft = 28, ContentMarginRight = 28,
            ContentMarginTop = 24, ContentMarginBottom = 24,
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(18);
        _card.AddThemeStyleboxOverride("panel", style);
        AddChild(_card);
        _panel.AddThemeConstantOverride("separation", 12);
        _card.AddChild(_panel);
        GetViewport().SizeChanged += Place;
        Place();
    }

    private void Place()
    {
        var size = GetViewportRect().Size;
        _card.Position = new Vector2(96, 200);
        _card.CustomMinimumSize = new Vector2(Mathf.Min(620, size.X - 192), 0);
        _card.Size = new Vector2(_card.CustomMinimumSize.X, 0);
    }

    private static Theme MenuTheme()
    {
        var theme = new Theme { DefaultFont = Hud.UiFont(600), DefaultFontSize = 20 };
        StyleBoxFlat Box(Color fill, Color edge, int width)
        {
            var box = new StyleBoxFlat { BgColor = fill, BorderColor = edge, ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 10, ContentMarginBottom = 10 };
            box.SetBorderWidthAll(width);
            box.SetCornerRadiusAll(12);
            return box;
        }

        theme.SetStylebox("normal", "Button", Box(new Color(1, 1, 1, 0.06f), new Color(1, 1, 1, 0.16f), 1));
        theme.SetStylebox("hover", "Button", Box(new Color(1, 1, 1, 0.14f), Accent, 2));
        theme.SetStylebox("pressed", "Button", Box(new Color(1, 1, 1, 0.2f), Accent, 2));
        theme.SetStylebox("focus", "Button", Box(new Color(0, 0, 0, 0), Accent with { A = 0.6f }, 2));
        theme.SetStylebox("disabled", "Button", Box(new Color(1, 1, 1, 0.03f), new Color(1, 1, 1, 0.08f), 1));
        theme.SetColor("font_color", "Button", Ink);
        theme.SetColor("font_hover_color", "Button", Accent);
        theme.SetColor("font_pressed_color", "Button", Accent);
        theme.SetColor("font_focus_color", "Button", Ink);
        theme.SetColor("font_disabled_color", "Button", new Color(1, 1, 1, 0.35f));
        theme.SetColor("font_color", "Label", Ink);
        theme.SetStylebox("normal", "LineEdit", Box(new Color(0, 0, 0, 0.35f), new Color(1, 1, 1, 0.2f), 1));
        theme.SetStylebox("focus", "LineEdit", Box(new Color(0, 0, 0, 0.35f), Accent, 2));
        theme.SetColor("font_color", "LineEdit", Ink);
        theme.SetColor("caret_color", "LineEdit", Accent);
        return theme;
    }

    // ---- Pages ----

    private void Clear(string page)
    {
        _page = page;
        foreach (var child in _panel.GetChildren())
        {
            child.QueueFree();
        }
    }

    private Label Text(string text, int size = 20, Color? color = null, bool bold = false)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Ink);
        if (bold)
        {
            label.AddThemeFontOverride("font", Hud.UiFont(800));
        }

        _panel.AddChild(label);
        return label;
    }

    private Button Choice(string text, Action pressed, Container? into = null, bool danger = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 52), FocusMode = FocusModeEnum.All };
        if (danger)
        {
            button.AddThemeColorOverride("font_hover_color", Danger);
            if (text == "Delete")
            {
                button.AddThemeColorOverride("font_color", Danger);
            }
        }

        button.Pressed += () =>
        {
            if (!_busy)
            {
                pressed();
            }
        };
        (into ?? _panel).AddChild(button);
        return button;
    }

    private void ShowMain()
    {
        Clear("main");
        var saves = SaveFile.List();
        var newest = saves.FirstOrDefault(s => !s.Broken);
        if (newest is not null)
        {
            var resume = Choice("Continue", () => Begin(newest.Slot));
            resume.TooltipText = Describe(newest);
            Text($"{newest.Slot}: {Describe(newest)}", 15, Dim);
        }

        Choice("New Game", ShowNew);
        if (saves.Count > 0)
        {
            Choice("Load Game", ShowLoad);
        }

        Choice("Settings", ShowSettings);
        Choice("Quit", () => GetTree().Quit(), danger: true);
        FocusFirst();
    }

    private void ShowNew()
    {
        Clear("new");
        Text("New Game", 28, bold: true);
        Text("Name this save. It keeps your game; you can have as many as you like.", 17, Dim);
        var name = new LineEdit { Text = SaveFile.NextFreeName(), MaxLength = 32, CustomMinimumSize = new Vector2(0, 48), SelectAllOnFocus = true };
        _panel.AddChild(name);
        var problem = Text("", 16, Danger);
        void Start()
        {
            var slot = SaveFile.SlotFor(name.Text);
            problem.Text = slot.Length == 0 ? "Use letters or digits."
                : SaveFile.Exists(slot) ? "A save with this name exists. Pick another name."
                : "";
            if (problem.Text.Length == 0)
            {
                Begin(slot);
            }
        }

        name.TextSubmitted += _ => Start();
        var row = Row();
        Choice("Start", Start, row);
        Choice("Back", ShowMain, row);
        name.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void ShowLoad()
    {
        Clear("load");
        Text("Load Game", 28, bold: true);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 360), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 10);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(list);
        _panel.AddChild(scroll);
        foreach (var save in SaveFile.List())
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            var words = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var name = new Label { Text = save.Slot, ClipText = true };
            name.AddThemeFontOverride("font", Hud.UiFont(800));
            var detail = new Label { Text = save.Broken ? "This save cannot be read." : Describe(save), AutowrapMode = TextServer.AutowrapMode.WordSmart };
            detail.AddThemeFontSizeOverride("font_size", 15);
            detail.AddThemeColorOverride("font_color", save.Broken ? Danger : Dim);
            words.AddChild(name);
            words.AddChild(detail);
            row.AddChild(words);
            var load = Choice("Load", () => Begin(save.Slot), row);
            load.Disabled = save.Broken;
            load.CustomMinimumSize = new Vector2(96, 48);
            var delete = Choice("Delete", () => ShowDelete(save.Slot), row, true);
            delete.CustomMinimumSize = new Vector2(96, 48);
            list.AddChild(row);
        }

        Choice("Back", ShowMain);
        FocusFirst();
    }

    private void ShowDelete(string slot)
    {
        Clear("delete");
        Text("Delete this save?", 28, bold: true);
        Text($"\"{slot}\" and its backup will be gone for good. This cannot be undone.", 18, Dim);
        var row = Row();
        Choice("Delete", () =>
        {
            SaveFile.Delete(slot);
            if (SaveFile.List().Count > 0)
            {
                ShowLoad();
            }
            else
            {
                ShowMain();
            }
        }, row, true);
        Choice("Keep it", ShowLoad, row);
        FocusFirst(1);
    }

    private void ShowSettings()
    {
        Clear("settings");
        Text("Settings", 28, bold: true);
        Slider("Sound effects", () => Settings.SfxVolume, v => Settings.SfxVolume = v, 0, 1, v => $"{Mathf.RoundToInt(v * 100)} %");
        Slider("Music", () => Settings.MusicVolume, v => Settings.MusicVolume = v, 0, 1, v => $"{Mathf.RoundToInt(v * 100)} %");
        Slider("Mouse sensitivity", () => Settings.Sensitivity, v => Settings.Sensitivity = v, Settings.MinSensitivity, Settings.MaxSensitivity, v => v.ToString("0.00", CultureInfo.InvariantCulture) + "x");
        Slider("Aim sensitivity (bow)", () => Settings.AimSensitivity, v => Settings.AimSensitivity = v, Settings.MinSensitivity, Settings.MaxSensitivity, v => v.ToString("0.00", CultureInfo.InvariantCulture) + "x");
        Choice("Back", () =>
        {
            Settings.Save();
            ShowMain();
        });
    }

    private void Slider(string name, Func<float> read, Action<float> write, float min, float max, Func<float, string> show)
    {
        var head = new HBoxContainer();
        var label = new Label { Text = name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", 17);
        var value = new Label { Text = show(read()) };
        value.AddThemeFontSizeOverride("font_size", 17);
        value.AddThemeColorOverride("font_color", Accent);
        head.AddChild(label);
        head.AddChild(value);
        _panel.AddChild(head);
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = (max - min) / 100f, Value = read(), CustomMinimumSize = new Vector2(0, 28) };
        slider.ValueChanged += v =>
        {
            write((float)v);
            Settings.Apply();
            value.Text = show((float)v);
        };
        _panel.AddChild(slider);
    }

    private HBoxContainer Row()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        _panel.AddChild(row);
        return row;
    }

    private void FocusFirst(int index = 0) => CallDeferred(MethodName.FocusButton, index);

    private void FocusButton(int index)
    {
        var buttons = FindButtons(_panel).Where(b => !b.Disabled).ToList();
        if (buttons.Count > index)
        {
            buttons[index].GrabFocus();
        }
    }

    private static IEnumerable<Button> FindButtons(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Button button && !button.IsQueuedForDeletion())
            {
                yield return button;
            }

            foreach (var inner in FindButtons(child))
            {
                yield return inner;
            }
        }
    }

    private void ShowMessage(string text, bool back)
    {
        Clear("message");
        Text(text, 22, bold: true);
        if (back)
        {
            Choice("Back", ShowMain);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && !_busy)
        {
            switch (_page)
            {
                case "new" or "load" or "message":
                    ShowMain();
                    break;
                case "settings":
                    Settings.Save();
                    ShowMain();
                    break;
                case "delete":
                    ShowLoad();
                    break;
            }

            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>A save in one line: the day and hour of the story, the level, where, and when it was written.</summary>
    private string Describe(SaveSummary save)
    {
        var parts = new List<string> { $"Day {save.Day}, {save.Minute / 60:00}:{save.Minute % 60:00}", $"Level {save.Level}" };
        if (!save.PrologueDone)
        {
            parts.Add("Prologue");
        }

        if (save.Position.Length == 3)
        {
            var place = _places.FirstOrDefault(p => new Vector2(save.Position[0] - p.X, save.Position[2] - p.Z).Length() < p.Radius).Name;
            parts.Add(string.IsNullOrEmpty(place) ? "the wilds" : place);
        }

        parts.Add("saved " + save.Saved.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture));
        return string.Join("  ·  ", parts);
    }

    /// <summary>Starts the server for the slot, then the game. A slot without a save begins a new game.</summary>
    private async void Begin(string slot)
    {
        _busy = true;
        ShowMessage((SaveFile.Exists(slot) ? $"Loading {slot}." : "Starting a new game.") + " The world is being built; this takes about half a minute.", false);
        var error = await LocalServer.Start(slot, Launch.Port);
        _busy = false;
        if (error is not null)
        {
            ShowMessage(error, true);
            return;
        }

        Launch.Host = "127.0.0.1";
        Launch.FromMenu = true;
        Launch.Slot = slot;
        EnterGame();
    }

    /// <summary>Photographs each page into a folder and quits: <c>-- menu &lt;folder&gt;</c>.</summary>
    private async Task Tour(string folder)
    {
        System.IO.Directory.CreateDirectory(folder);
        async Task Shot(string name)
        {
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(folder, $"menu-{name}.png"));
            GD.Print($"MENU shot {name}");
        }

        await Shot("main");
        ShowNew();
        await Shot("new");
        ShowLoad();
        await Shot("load");
        if (SaveFile.List().FirstOrDefault() is { } first)
        {
            ShowDelete(first.Slot);
            await Shot("delete");
        }

        ShowSettings();
        await Shot("settings");
        GD.Print("MENU done");
        GetTree().Quit();
    }
}
