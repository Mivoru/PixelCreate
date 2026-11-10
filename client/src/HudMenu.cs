using System;
using System.Collections.Generic;
using Godot;

namespace PixelCreate;

/// <summary>
/// The menu Esc opens: Resume, Settings, Save and Quit to Menu (when the main menu started the game) and Quit Game. Settings has sliders for the sound effects,
/// the music, the mouse and the mouse while aiming; a change is saved when the page is left.
/// </summary>
public partial class Hud
{
    private readonly List<(Rect2 Rect, string Action)> _menuButtons = new();
    private readonly List<(Rect2 Track, int Slider)> _menuSliders = new();
    private string _menuPage = "";
    private int _dragging = -1;

    public bool MenuOpen => _menuPage.Length > 0;

    /// <summary>Set when the player chose Quit Game; the game reads it and quits.</summary>
    public bool QuitRequested { get; private set; }

    /// <summary>Set when the player chose to save and go back to the main menu.</summary>
    public bool MenuRequested { get; private set; }

    private static readonly string[] SliderNames = { "Sound effects", "Music", "Mouse sensitivity", "Aim sensitivity (bow)" };

    public void ToggleMenu()
    {
        if (MenuOpen)
        {
            CloseMenu();
        }
        else
        {
            _menuPage = "main";
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    /// <summary>Asks to save and go back to the main menu, as the menu button does; for the menu check.</summary>
    public void RequestMainMenu() => MenuRequested = true;

    /// <summary>Shows a page of the menu, for bots that check it.</summary>
    public void ShowMenuPage(string page)
    {
        _menuPage = page;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void CloseMenu()
    {
        if (_menuPage == "settings")
        {
            Settings.Save();
        }

        _menuPage = "";
        _dragging = -1;
        Input.MouseMode = InventoryOpen || DialogOpen ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
    }

    /// <summary>A slider's value as a share of its track, from 0 to 1.</summary>
    private static float SliderShare(int slider) => slider switch
    {
        0 => Settings.SfxVolume,
        1 => Settings.MusicVolume,
        2 => (Settings.Sensitivity - Settings.MinSensitivity) / (Settings.MaxSensitivity - Settings.MinSensitivity),
        _ => (Settings.AimSensitivity - Settings.MinSensitivity) / (Settings.MaxSensitivity - Settings.MinSensitivity),
    };

    private static void SetSlider(int slider, float share)
    {
        share = Mathf.Clamp(share, 0f, 1f);
        var speed = Settings.MinSensitivity + share * (Settings.MaxSensitivity - Settings.MinSensitivity);
        switch (slider)
        {
            case 0: Settings.SfxVolume = share; break;
            case 1: Settings.MusicVolume = share; break;
            case 2: Settings.Sensitivity = speed; break;
            default: Settings.AimSensitivity = speed; break;
        }

        Settings.Apply();
    }

    private static string SliderText(int slider) => slider switch
    {
        0 => $"{Mathf.RoundToInt(Settings.SfxVolume * 100)} %",
        1 => $"{Mathf.RoundToInt(Settings.MusicVolume * 100)} %",
        2 => Settings.Sensitivity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "x",
        _ => Settings.AimSensitivity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "x",
    };

    private void DrawMenu()
    {
        _menuButtons.Clear();
        _menuSliders.Clear();
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 0, 0, 0.45f));
        var settings = _menuPage == "settings";
        var entries = Launch.FromMenu
            ? new[] { ("Resume", "resume"), ("Settings", "settings"), ("Save and Quit to Menu", "tomenu"), ("Quit Game", "quit") }
            : new[] { ("Resume", "resume"), ("Settings", "settings"), ("Quit Game", "quit") };
        var size = settings ? new Vector2(560, 470) : new Vector2(380, 140 + entries.Length * 74);
        var window = new Rect2(((Size - size) / 2f).Round(), size);
        Card(window, 22, GlassDeep, true);
        var title = settings ? "Settings" : "Menu";
        Label(title, window.Position + new Vector2((size.X - Width(title, 28, _bold)) / 2f, 62), 28, Text, _bold);
        var mouse = GetLocalMousePosition();
        if (!settings)
        {
            var y = 100f;
            foreach (var (label, action) in entries)
            {
                var rect = new Rect2(window.Position + new Vector2(50, y), size.X - 100, 56);
                MenuButton(rect, label, action, mouse, action == "quit");
                y += 74;
            }

            return;
        }

        for (var i = 0; i < SliderNames.Length; i++)
        {
            var top = window.Position.Y + 104 + i * 74;
            Label(SliderNames[i], new Vector2(window.Position.X + 46, top), 17, Text, _bold);
            var value = SliderText(i);
            Label(value, new Vector2(window.End.X - 46 - Width(value, 17, _bold), top), 17, Accent, _bold);
            var track = new Rect2(window.Position.X + 46, top + 18, size.X - 92, 26);
            _menuSliders.Add((track, i));
            var line = new Rect2(track.Position.X, track.Position.Y + 10, track.Size.X, 6);
            Rounded(line, 3, new Color(1, 1, 1, 0.12f), Colors.Transparent, 0);
            var share = SliderShare(i);
            Rounded(new Rect2(line.Position, line.Size.X * share, line.Size.Y), 3, Accent with { A = 0.85f }, Colors.Transparent, 0);
            var knob = new Vector2(track.Position.X + track.Size.X * share, track.Position.Y + 13);
            var hot = _dragging == i || track.HasPoint(mouse);
            DrawCircle(knob, hot ? 11f : 9f, new Color(0, 0, 0, 0.5f));
            DrawCircle(knob, hot ? 9f : 7.5f, hot ? Accent : Text);
        }

        MenuButton(new Rect2(window.Position + new Vector2(50, size.Y - 84), size.X - 100, 52), "Back", "back", mouse, false);
    }

    private void MenuButton(Rect2 rect, string label, string action, Vector2 mouse, bool danger)
    {
        _menuButtons.Add((rect, action));
        var hover = rect.HasPoint(mouse);
        var edge = danger ? new Color("e43b44") : Accent;
        Rounded(rect, 12, new Color(1, 1, 1, hover ? 0.14f : 0.06f), hover ? edge : new Color(1, 1, 1, 0.16f), hover ? 2 : 1);
        Label(label, rect.Position + new Vector2((rect.Size.X - Width(label, 19, _bold)) / 2f, rect.Size.Y / 2f + 7), 19, hover ? edge : Text, _bold);
    }

    /// <summary>Clicks and drags in the menu. Returns true when the menu took the event.</summary>
    private bool MenuInput(InputEvent @event)
    {
        if (!MenuOpen)
        {
            return false;
        }

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
                _dragging = -1;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click:
                foreach (var (track, slider) in _menuSliders)
                {
                    if (track.Grow(6).HasPoint(click.Position))
                    {
                        _dragging = slider;
                        SetSlider(slider, (click.Position.X - track.Position.X) / track.Size.X);
                        return true;
                    }
                }

                foreach (var (rect, action) in _menuButtons)
                {
                    if (!rect.HasPoint(click.Position))
                    {
                        continue;
                    }

                    switch (action)
                    {
                        case "resume": CloseMenu(); break;
                        case "settings": _menuPage = "settings"; break;
                        case "back": Settings.Save(); _menuPage = "main"; break;
                        case "quit": QuitRequested = true; break;
                        case "tomenu": MenuRequested = true; break;
                    }

                    return true;
                }

                break;
            case InputEventMouseMotion motion when _dragging >= 0:
                foreach (var (track, slider) in _menuSliders)
                {
                    if (slider == _dragging)
                    {
                        SetSlider(slider, (motion.Position.X - track.Position.X) / track.Size.X);
                    }
                }

                break;
        }

        return true;
    }
}
