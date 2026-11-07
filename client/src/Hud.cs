using System;
using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// The game interface: clean glass panels, rounded bars and a smooth font, drawn at the full window
/// resolution over the pixel-art world. Sizes are given for a 1080 pixel tall window and scale with
/// it. All text is English.
/// </summary>
public partial class Hud : Control
{
    private const float DesignHeight = 1080f;
    private const float SlotSize = 64f;
    private const float SlotGap = 8f;
    private const float TagRange = 34f;

    private static readonly Color Glass = new(0.06f, 0.08f, 0.12f, 0.74f);
    private static readonly Color GlassDeep = new(0.04f, 0.05f, 0.08f, 0.88f);
    private static readonly Color Rim = new(1f, 1f, 1f, 0.1f);
    private static readonly Color Text = new("eef1f6");
    private static readonly Color Muted = new("9aa4b5");
    private static readonly Color Accent = new("f2c35b");
    private static readonly Color HealthColor = new("e5484d");
    private static readonly Color HealthLight = new("ff7a7e");
    private static readonly Color StaminaColor = new("3fbf7a");
    private static readonly Color Track = new(1f, 1f, 1f, 0.08f);
    private static readonly Color Shadow = new(0f, 0f, 0f, 0.55f);

    private readonly Dictionary<string, Texture2D> _icons = new();
    private readonly List<(Rect2 Rect, SlotRef Slot)> _slots = new();
    private readonly List<(Rect2 Rect, string Icon)> _iconDraws = new();
    private Font _font = null!;
    private Font _bold = null!;
    private IconLayer _iconLayer = null!;
    private float _shownHealth = 1f;
    private float _ghostHealth = 1f;
    private float _shownStamina = 1f;
    private float _time;
    private Item? _held;
    private float _shownXp;
    private float _scale = 1f;
    private string _dialogName = "";
    private string _dialogText = "";
    private float _dialogAge;
    private (string Label, Command Command)[] _dialogOptions = System.Array.Empty<(string, Command)>();
    private string[] _story = System.Array.Empty<string>();
    private float _storyTime;
    private float _bannerTime;
    private string _banner = "";
    private Color _bannerColor = Colors.White;
    private float _placeTime;
    private string _place = "";

    private sealed class FloatingText
    {
        public required string Text;
        public required Vector3 Position;
        public required Color Color;
        public float Age;
    }

    /// <summary>Pixel-art icons need nearest filtering, unlike the smooth text, so they draw on their own layer.</summary>
    private sealed partial class IconLayer : Control
    {
        public Hud Owner = null!;

        public override void _Draw()
        {
            foreach (var (rect, icon) in Owner._iconDraws)
            {
                DrawTextureRect(Owner._icons[icon], rect, false);
            }
        }
    }

    private readonly List<FloatingText> _floating = new();

    public Inventory Items { get; } = new();
    public bool InventoryOpen { get; private set; }
    public PlayerState Player { get; set; }
    public Snapshot? Snapshot { get; set; }

    public int Level => Snapshot?.Level ?? 1;

    /// <summary>Window pixels for each pixel of the 3D view.</summary>
    public float WorldScale { get; set; } = 1f;
    public Camera3D? Camera { get; set; }
    public string Notice { get; set; } = "";

    /// <summary>World positions shown this frame, by enemy id, so tags follow the drawn enemies.</summary>
    public Dictionary<int, Vector3> EnemyPositions { get; } = new();

    public override void _Ready()
    {
        var file = GD.Load<FontFile>("res://assets/ui/Nunito.ttf");
        _font = Weight(file, 600);
        _bold = Weight(file, 800);
        _icons["wooden_sword"] = GD.Load<Texture2D>("res://assets/ui/wooden_sword.png");
        TextureFilter = TextureFilterEnum.Linear;
        MouseFilter = MouseFilterEnum.Stop;
        _iconLayer = new IconLayer { Owner = this, TextureFilter = TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_iconLayer);
        GetViewport().SizeChanged += FitWindow;
        FitWindow();
    }

    private static FontVariation Weight(FontFile file, int weight)
    {
        var tag = TextServerManager.GetPrimaryInterface().NameToTag("wght");
        return new FontVariation { BaseFont = file, VariationOpentype = new Godot.Collections.Dictionary { [tag] = weight } };
    }

    private void FitWindow()
    {
        var window = GetViewport().GetVisibleRect().Size;
        _scale = window.Y / DesignHeight;
        Scale = Vector2.One * _scale;
        Size = window / _scale;
        _iconLayer.Size = Size;
    }

    /// <summary>Turns a position in the 3D view into a position in this interface.</summary>
    private Vector2 ToHud(Vector3 world) => Camera!.UnprojectPosition(world) * WorldScale / _scale;

    /// <summary>Shows a line of text in the world that rises and fades.</summary>
    public void AddFloating(string text, Vector3 position, Color color) =>
        _floating.Add(new FloatingText { Text = text, Position = position, Color = color });

    /// <summary>Shows a large message across the top of the screen for a few seconds.</summary>
    public void ShowBanner(string text, Color color)
    {
        _banner = text;
        _bannerColor = color;
        _bannerTime = 3.2f;
    }

    /// <summary>Names the place the player has just entered.</summary>
    public void ShowPlace(string name)
    {
        _place = name;
        _placeTime = 3.6f;
    }

    public string Prompt { get; set; } = "";

    public string Objective { get; set; } = "";

    public bool DialogOpen => _dialogName.Length > 0;

    /// <summary>Opens a conversation. Options are chosen with the number keys.</summary>
    public void ShowDialog(string name, string text, params (string Label, Command Command)[] options)
    {
        _dialogName = name;
        _dialogText = text;
        _dialogOptions = options;
        _dialogAge = 0f;
    }

    public void CloseDialog() => _dialogName = "";

    /// <summary>Picks the option with this command, as a player would by reading the list.</summary>
    public Command ChooseCommand(Command wanted) => Choose(System.Array.FindIndex(_dialogOptions, o => o.Command == wanted) is var i and >= 0 ? i : int.MaxValue);

    /// <summary>Picks an option and closes the conversation, returning the command to send.</summary>
    public Command Choose(int index)
    {
        if (index >= _dialogOptions.Length)
        {
            return Command.None;
        }

        var command = _dialogOptions[index].Command;
        CloseDialog();
        return command;
    }

    /// <summary>Shows lines of the story one after another in the middle of the screen.</summary>
    public void ShowStory(string[] lines)
    {
        _story = lines;
        _storyTime = 0f;
    }

    public void ToggleInventory()
    {
        InventoryOpen = !InventoryOpen;
        Input.MouseMode = InventoryOpen ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
    }

    public override void _Process(double delta)
    {
        var d = (float)delta;
        _time += d;
        _dialogAge += d;
        var maxHealth = Snapshot?.MaxHealth ?? Tuning.PlayerHealth;
        var health = Mathf.Clamp(Player.Health / (float)maxHealth, 0f, 1f);
        var xp = Snapshot is { XpToNext: > 0 } current ? current.Xp / (float)current.XpToNext : 0f;
        _shownXp = xp < _shownXp ? xp : Mathf.Lerp(_shownXp, xp, 1f - Mathf.Exp(-8f * d));
        _bannerTime = Mathf.Max(0f, _bannerTime - d);
        _placeTime = Mathf.Max(0f, _placeTime - d);
        _storyTime += d;
        foreach (var text in _floating)
        {
            text.Age += d;
        }

        _floating.RemoveAll(t => t.Age > 1.2f);
        var stamina = Mathf.Clamp(Player.Stamina / Tuning.MaxStamina, 0f, 1f);
        _shownHealth = Mathf.Lerp(_shownHealth, health, 1f - Mathf.Exp(-16f * d));
        _shownStamina = Mathf.Lerp(_shownStamina, stamina, 1f - Mathf.Exp(-22f * d));

        // The pale part of the bar shows the damage just taken, then drains away.
        _ghostHealth = health > _ghostHealth ? health : Mathf.MoveToward(_ghostHealth, health, d * 0.35f);
        QueueRedraw();
        _iconLayer.QueueRedraw();
    }

    public override void _Draw()
    {
        _slots.Clear();
        _iconDraws.Clear();
        DrawNametags();
        DrawFloating();
        DrawStatus();
        DrawHotbar();
        DrawGuide();
        DrawMessages();
        DrawDialog();
        if (InventoryOpen)
        {
            DrawInventory();
        }

        if (_held is not null)
        {
            _iconDraws.Add((new Rect2(GetLocalMousePosition() - new Vector2(24, 24), 48, 48), _held.Icon));
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!InventoryOpen || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            return;
        }

        var point = click.Position;
        foreach (var (rect, slot) in _slots)
        {
            if (rect.HasPoint(point))
            {
                Exchange(slot);
                return;
            }
        }
    }

    public void SelectHotbar(int index) => Items.SelectedHotbar = index;

    private void Exchange(SlotRef slot)
    {
        var current = Read(slot);
        if (_held is not null && slot.Equip != EquipSlot.None && _held.Slot != slot.Equip)
        {
            return;
        }

        Write(slot, _held);
        _held = current;
    }

    private Item? Read(SlotRef slot) => slot.Area switch
    {
        "bag" => Items.Bag[slot.Index],
        "hotbar" => Items.Hotbar[slot.Index],
        _ => Items.Equipped[slot.Equip],
    };

    private void Write(SlotRef slot, Item? item)
    {
        switch (slot.Area)
        {
            case "bag":
                Items.Bag[slot.Index] = item;
                break;
            case "hotbar":
                Items.Hotbar[slot.Index] = item;
                break;
            default:
                Items.Equipped[slot.Equip] = item;
                break;
        }
    }

    // --- Drawing helpers -------------------------------------------------------------------------

    private void Card(Rect2 rect, float radius = 14f, Color? fill = null)
    {
        var shadow = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.28f), AntiAliasing = true };
        shadow.SetCornerRadiusAll((int)radius + 2);
        DrawStyleBox(shadow, new Rect2(rect.Position + new Vector2(0, 4), rect.Size));
        var box = new StyleBoxFlat { BgColor = fill ?? Glass, BorderColor = Rim, AntiAliasing = true };
        box.SetBorderWidthAll(1);
        box.SetCornerRadiusAll((int)radius);
        DrawStyleBox(box, rect);
    }

    private void Pill(Rect2 rect, Color color)
    {
        var box = new StyleBoxFlat { BgColor = color, AntiAliasing = true };
        box.SetCornerRadiusAll((int)(rect.Size.Y / 2f));
        DrawStyleBox(box, rect);
    }

    private float Width(string text, int size, Font? font = null) => (font ?? _font).GetStringSize(text, HorizontalAlignment.Left, -1, size).X;

    private void Label(string text, Vector2 at, int size, Color color, Font? font = null, bool shadow = false)
    {
        if (shadow)
        {
            DrawString(font ?? _font, at + new Vector2(0, 2), text, HorizontalAlignment.Left, -1, size, new Color(Shadow.R, Shadow.G, Shadow.B, Shadow.A * color.A));
        }

        DrawString(font ?? _font, at, text, HorizontalAlignment.Left, -1, size, color);
    }

    private void Centered(string text, float y, int size, Color color, Font? font = null, bool shadow = true) =>
        Label(text, new Vector2((Size.X - Width(text, size, font)) / 2f, y), size, color, font, shadow);

    /// <summary>A rounded bar. The ghost shows recent loss in a paler colour.</summary>
    private void Bar(Rect2 rect, float value, float ghost, Color color, Color light, bool warn)
    {
        Pill(rect, Track);
        if (ghost > value)
        {
            Pill(new Rect2(rect.Position, Mathf.Max(rect.Size.X * ghost, rect.Size.Y), rect.Size.Y), new Color(1f, 0.9f, 0.7f, 0.55f));
        }

        if (value > 0.001f)
        {
            var fill = new Rect2(rect.Position, Mathf.Max(rect.Size.X * value, rect.Size.Y), rect.Size.Y);
            Pill(fill, warn && Mathf.Sin(_time * 9f) > 0 ? light : color);
            if (rect.Size.Y >= 10f)
            {
                Pill(new Rect2(fill.Position + new Vector2(3, 2), Mathf.Max(fill.Size.X - 6, 1f), rect.Size.Y * 0.3f), new Color(1, 1, 1, 0.22f));
            }
        }
    }

    private void Keycap(string key, Vector2 at, int size = 15)
    {
        var width = Mathf.Max(Width(key, size, _bold) + 14f, size + 12f);
        var rect = new Rect2(at, width, size + 12f);
        var box = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.92f), AntiAliasing = true, BorderColor = new Color(0, 0, 0, 0.25f) };
        box.SetCornerRadiusAll(6);
        box.BorderWidthBottom = 3;
        DrawStyleBox(box, rect);
        Label(key, new Vector2(at.X + (width - Width(key, size, _bold)) / 2f, at.Y + size + 2f), size, new Color("161a22"), _bold);
    }

    // --- Screens ----------------------------------------------------------------------------------

    private void DrawStatus()
    {
        var card = new Rect2(28, 28, 384, 110);
        Card(card);
        Label("Wanderer", card.Position + new Vector2(22, 40), 24, Text, _bold);
        var level = $"Lv {Level}";
        var levelWidth = Width(level, 15, _bold) + 20;
        Pill(new Rect2(card.End.X - levelWidth - 18, card.Position.Y + 18, levelWidth, 26), Accent);
        Label(level, new Vector2(card.End.X - levelWidth - 8, card.Position.Y + 37), 15, new Color("1d1608"), _bold);

        // Health is the wide bar with its numbers inside; stamina is the thin bar under it.
        var maxHealth = Snapshot?.MaxHealth ?? Tuning.PlayerHealth;
        var health = new Rect2(card.Position + new Vector2(22, 56), card.Size.X - 44, 22);
        Bar(health, _shownHealth, _ghostHealth, HealthColor, HealthLight, _shownHealth < 0.3f);
        var numbers = $"{Math.Max(Player.Health, (short)0)} / {maxHealth}";
        Label(numbers, new Vector2(health.End.X - 12 - Width(numbers, 15, _bold), health.Position.Y + 17), 15, Colors.White, _bold, true);
        Bar(new Rect2(card.Position + new Vector2(22, 87), card.Size.X - 44, 6), _shownStamina, _shownStamina, StaminaColor, new Color("8ff0b8"), Player.Stamina < 15f);

        if (Player.SlowTicks > 0)
        {
            var label = $"Slowed  {Player.SlowTicks / 60f:F1}s";
            var rect = new Rect2(28, 148, Width(label, 16, _bold) + 32, 34);
            Card(rect, 17, new Color(0.2f, 0.35f, 0.1f, 0.8f));
            Label(label, rect.Position + new Vector2(16, 23), 16, new Color("c8f59a"), _bold);
        }
    }

    private void DrawGuide()
    {
        var gold = (Snapshot?.Gold ?? 0).ToString();
        var goldWidth = Width(gold, 20, _bold);
        var coin = new Rect2(Size.X - 74 - goldWidth, 34, 46 + goldWidth, 40);
        Card(coin, 20);
        DrawCircle(coin.Position + new Vector2(22, 20), 10, Accent);
        DrawCircle(coin.Position + new Vector2(22, 20), 6, new Color("d9a032"));
        Label(gold, coin.Position + new Vector2(38, 28), 20, Text, _bold);

        if (Objective.Length > 0)
        {
            var width = Mathf.Max(Width(Objective, 19), 180f) + 44f;
            var rect = new Rect2(Size.X - 28 - width, 88, width, 72);
            Card(rect);
            Pill(new Rect2(rect.Position + new Vector2(10, 16), 4, 40), Accent);
            Label("OBJECTIVE", rect.Position + new Vector2(24, 28), 13, Accent, _bold);
            Label(Objective, rect.Position + new Vector2(24, 54), 19, Text);
        }

        if (Prompt.Length > 0 && !DialogOpen)
        {
            var text = Prompt.Replace("[E] ", "");
            var width = Width(text, 19, _bold) + 70f;
            var rect = new Rect2((Size.X - width) / 2f, Size.Y - 220, width, 46);
            Card(rect, 23);
            Keycap("E", rect.Position + new Vector2(10, 9));
            Label(text, rect.Position + new Vector2(50, 30), 19, Text, _bold);
        }

        if (Notice.Length > 0)
        {
            Centered(Notice, Size.Y / 2f, 26, Text, _bold);
        }
    }

    private void DrawMessages()
    {
        var line = (int)(_storyTime / 4f);
        if (line < _story.Length)
        {
            var within = _storyTime - line * 4f;
            var alpha = Mathf.Clamp(within * 2f, 0f, 1f) * Mathf.Clamp((4f - within) * 2f, 0f, 1f);
            Centered(_story[line], Size.Y * 0.42f, 28, new Color(1f, 0.98f, 0.93f, alpha), _font);
        }

        if (_placeTime > 0f)
        {
            var alpha = Mathf.Clamp(_placeTime, 0f, 1f) * Mathf.Clamp((3.6f - _placeTime) * 2.5f, 0f, 1f);
            const float y = 170f;
            var width = Width(_place, 40, _bold);
            Centered(_place, y, 40, new Color(1, 1, 1, alpha), _bold);
            var middle = Size.X / 2f;
            var line_color = new Color(1, 1, 1, alpha * 0.7f);
            DrawLine(new Vector2(middle - width / 2f - 90, y - 13), new Vector2(middle - width / 2f - 18, y - 13), line_color, 2f, true);
            DrawLine(new Vector2(middle + width / 2f + 18, y - 13), new Vector2(middle + width / 2f + 90, y - 13), line_color, 2f, true);
        }

        if (_bannerTime > 0f)
        {
            var alpha = Mathf.Clamp(_bannerTime, 0f, 1f) * Mathf.Clamp((3.2f - _bannerTime) * 3f, 0f, 1f);
            var lift = (1f - Mathf.Clamp((3.2f - _bannerTime) * 3f, 0f, 1f)) * 12f;
            Centered(_banner, 260f + lift, 44, new Color(_bannerColor.R, _bannerColor.G, _bannerColor.B, alpha), _bold);
        }
    }

    private void DrawDialog()
    {
        if (!DialogOpen)
        {
            return;
        }

        const float width = 860f;
        const int size = 20;
        var textHeight = _font.GetMultilineStringSize(_dialogText, HorizontalAlignment.Left, width - 64, size).Y;
        var height = 92f + textHeight + _dialogOptions.Length * 44f;
        var rise = (1f - Mathf.Clamp(_dialogAge * 6f, 0f, 1f)) * 20f;
        var box = new Rect2((Size.X - width) / 2f, Size.Y - 220 - height + rise, width, height);
        Card(box, 18, GlassDeep);

        var nameWidth = Width(_dialogName, 18, _bold) + 32;
        Pill(new Rect2(box.Position + new Vector2(28, -18), nameWidth, 38), Accent);
        Label(_dialogName, box.Position + new Vector2(44, 7), 18, new Color("1d1608"), _bold);

        DrawMultilineString(_font, box.Position + new Vector2(32, 54), _dialogText, HorizontalAlignment.Left, width - 64, size, -1, Text);
        var mouse = GetLocalMousePosition();
        for (var i = 0; i < _dialogOptions.Length; i++)
        {
            var row = new Rect2(box.Position + new Vector2(22, 66 + textHeight + i * 44), width - 44, 38);
            if (row.HasPoint(mouse))
            {
                Pill(row, new Color(1, 1, 1, 0.07f));
            }

            Keycap((i + 1).ToString(), row.Position + new Vector2(8, 6), 14);
            Label(_dialogOptions[i].Label, row.Position + new Vector2(48, 26), 19, Text);
        }
    }

    private void DrawFloating()
    {
        if (Camera is null)
        {
            return;
        }

        foreach (var text in _floating)
        {
            var world = text.Position + Vector3.Up * (text.Age * 1.1f);
            if (Camera.IsPositionBehind(world))
            {
                continue;
            }

            var at = ToHud(world);
            var alpha = Mathf.Clamp((1.2f - text.Age) * 3f, 0f, 1f);
            var pop = 1f + Mathf.Max(0f, 0.15f - text.Age) * 3f;
            var size = (int)(26 * pop);
            var width = Width(text.Text, size, _bold);
            DrawStringOutline(_bold, new Vector2(at.X - width / 2f, at.Y), text.Text, HorizontalAlignment.Left, -1, size, 6, new Color(0.05f, 0.05f, 0.08f, alpha * 0.85f));
            DrawString(_bold, new Vector2(at.X - width / 2f, at.Y), text.Text, HorizontalAlignment.Left, -1, size, new Color(text.Color.R, text.Color.G, text.Color.B, alpha));
        }
    }

    private void DrawHotbar()
    {
        var width = Inventory.HotbarSlots * (SlotSize + SlotGap) - SlotGap + 24;
        var left = (Size.X - width) / 2f;
        var top = Size.Y - SlotSize - 44f;
        Card(new Rect2(left, top - 12, width, SlotSize + 24), 18);
        for (var i = 0; i < Inventory.HotbarSlots; i++)
        {
            var rect = new Rect2(left + 12 + i * (SlotSize + SlotGap), top, SlotSize, SlotSize);
            DrawSlot(rect, Items.Hotbar[i], new SlotRef("hotbar", i), i == Items.SelectedHotbar);
            Label((i + 1).ToString(), rect.Position + new Vector2(7, 18), 13, Muted, _bold);
        }

        // Experience runs as a thin line above the hotbar, with the numbers on its right.
        var bar = new Rect2(left + 12, top - 30, width - 24, 6);
        Bar(bar, _shownXp, _shownXp, Accent, new Color("ffe29a"), false);
        var label = $"{Snapshot?.Xp ?? 0} / {Snapshot?.XpToNext ?? 0} XP";
        Label(label, new Vector2(bar.End.X - Width(label, 14, _bold), bar.Position.Y - 8), 14, Text, _bold, true);
        Label($"Level {Level}", new Vector2(bar.Position.X, bar.Position.Y - 8), 14, Accent, _bold, true);
    }

    private void DrawSlot(Rect2 rect, Item? item, SlotRef slot, bool selected = false)
    {
        _slots.Add((rect, slot));
        var box = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.06f), BorderColor = selected ? Accent : new Color(1, 1, 1, 0.1f), AntiAliasing = true };
        box.SetCornerRadiusAll(12);
        box.SetBorderWidthAll(selected ? 3 : 1);
        if (InventoryOpen && rect.HasPoint(GetLocalMousePosition()))
        {
            box.BgColor = new Color(1, 1, 1, 0.14f);
        }

        DrawStyleBox(box, rect);
        if (item is not null)
        {
            _iconDraws.Add((rect.Grow(-10), item.Icon));
        }
    }

    private void DrawCrystals(Rect2 window)
    {
        var counts = Snapshot?.Crystals ?? new int[Crystals.Tiers];
        var weight = 0f;
        var y = window.Position.Y + 330;
        Label("MAGIC CRYSTALS", new Vector2(window.Position.X + 340, y), 13, Accent, _bold);
        for (var i = 0; i < Crystals.Tiers; i++)
        {
            var def = Crystals.All[i];
            weight += counts[i] * def.Weight;
            var x = window.Position.X + 340 + i * 92;
            var gem = new Vector2(x + 12, y + 32);
            DrawColoredPolygon(new[] { gem + new Vector2(0, -12), gem + new Vector2(9, 0), gem + new Vector2(0, 12), gem + new Vector2(-9, 0) }, new Color(def.Color));
            DrawColoredPolygon(new[] { gem + new Vector2(0, -12), gem + new Vector2(4, -2), gem + new Vector2(-4, -2) }, new Color(1, 1, 1, 0.6f));
            Label(counts[i].ToString(), new Vector2(x + 30, y + 40), 20, Text, _bold);
        }

        Label($"{weight:F1} kg carried", new Vector2(window.Position.X + 340, y + 76), 15, Muted);
    }

    private void DrawInventory()
    {
        var window = new Rect2((Size.X - 820f) / 2f, (Size.Y - 520f) / 2f, 820, 520);
        Card(window, 22, GlassDeep);
        Label("Equipment", window.Position + new Vector2(34, 52), 24, Text, _bold);
        Label("Bag", window.Position + new Vector2(340, 52), 24, Text, _bold);
        DrawLine(window.Position + new Vector2(310, 30), window.Position + new Vector2(310, window.Size.Y - 70), Rim, 1f);

        var equip = new (EquipSlot Slot, string Label, int X, int Y)[]
        {
            (EquipSlot.Head, "Head", 120, 80),
            (EquipSlot.Chest, "Chest", 120, 170),
            (EquipSlot.Legs, "Legs", 120, 260),
            (EquipSlot.Boots, "Boots", 120, 350),
            (EquipSlot.Weapon, "Weapon", 34, 170),
            (EquipSlot.Ring, "Ring", 206, 170),
        };
        foreach (var (slot, label, x, y) in equip)
        {
            var rect = new Rect2(window.Position + new Vector2(x, y), SlotSize, SlotSize);
            DrawSlot(rect, Items.Equipped[slot], new SlotRef("equip", 0, slot));
            Label(label, rect.Position + new Vector2((SlotSize - Width(label, 13)) / 2f, SlotSize + 18), 13, Muted);
        }

        for (var i = 0; i < Inventory.BagSlots; i++)
        {
            var rect = new Rect2(window.Position + new Vector2(340 + i % 6 * (SlotSize + 10), 80 + i / 6 * (SlotSize + 10)), SlotSize, SlotSize);
            DrawSlot(rect, Items.Bag[i], new SlotRef("bag", i));
        }

        DrawCrystals(window);
        DrawTooltip(window);
    }

    private void DrawTooltip(Rect2 window)
    {
        var mouse = GetLocalMousePosition();
        var bottom = window.Position + new Vector2(34, window.Size.Y - 34);
        foreach (var (rect, slot) in _slots)
        {
            if (rect.HasPoint(mouse) && Read(slot) is { } item)
            {
                Label(item.Name, bottom + new Vector2(0, -20), 18, Accent, _bold);
                Label(item.Description, bottom + new Vector2(Width(item.Name, 18, _bold) + 14, -20), 16, Text);
                return;
            }
        }

        Label("Click to pick up and place items", bottom + new Vector2(0, -20), 15, Muted);
        Keycap("I", bottom + new Vector2(window.Size.X - 160, -42), 14);
        Label("close", bottom + new Vector2(window.Size.X - 126, -20), 15, Muted);
    }

    private void DrawNametags()
    {
        if (Snapshot is null || Camera is null)
        {
            return;
        }

        foreach (var enemy in Snapshot.Enemies)
        {
            if (enemy.Action == EnemyAction.Dead || !EnemyPositions.TryGetValue(enemy.Id, out var position))
            {
                continue;
            }

            var def = EnemyDef.All[enemy.Type];
            var head = position + Vector3.Up * (def.Size * 1.5f + 0.25f);
            if (Camera.IsPositionBehind(head) || Camera.GlobalPosition.DistanceTo(head) > TagRange)
            {
                continue;
            }

            DrawTag(ToHud(head), enemy, def);
        }
    }

    private void DrawTag(Vector2 at, in EnemyView enemy, EnemyDef def)
    {
        var threat = ThreatRules.Compare(enemy.Level, Level);
        var color = threat switch
        {
            Threat.Strong => new Color("ff6b6b"),
            Threat.Weak => new Color("8fe0a6"),
            _ => Text,
        };
        var animal = def.Kind == "animal";
        if (animal && def.Temper == "passive")
        {
            color = new Color("dff6f5");
        }

        const int size = 17;
        var name = def.Name;
        var level = animal ? "" : $"{enemy.Level}";
        var nameWidth = Width(name, size, _bold);
        var levelWidth = level.Length > 0 ? Width(level, 13, _bold) + 16 : 0f;
        var left = at.X - (nameWidth + levelWidth + (levelWidth > 0 ? 6 : 0)) / 2f;
        if (def.Rare)
        {
            var glow = 0.35f + Mathf.Sin(_time * 5f) * 0.15f;
            Pill(new Rect2(left - 12, at.Y - 22, nameWidth + levelWidth + 30, 30), new Color(0.8f, 0.3f, 0.6f, glow));
        }

        if (levelWidth > 0)
        {
            Pill(new Rect2(left, at.Y - 18, levelWidth, 22), threat == Threat.Strong ? new Color("e5484d") : new Color(1, 1, 1, 0.2f));
            Label(level, new Vector2(left + 8, at.Y - 2), 13, Colors.White, _bold);
            left += levelWidth + 6;
        }

        DrawStringOutline(_bold, new Vector2(left, at.Y), name, HorizontalAlignment.Left, -1, size, 5, new Color(0, 0, 0, 0.7f));
        DrawString(_bold, new Vector2(left, at.Y), name, HorizontalAlignment.Left, -1, size, color);
        if (enemy.Fighting && !animal)
        {
            Label("!", new Vector2(left + nameWidth + 6, at.Y), size, Accent, _bold, true);
        }

        if (enemy.Health < enemy.MaxHealth)
        {
            var bar = new Rect2(at.X - 40, at.Y + 8, 80, 7);
            Pill(bar.Grow(1.5f), new Color(0, 0, 0, 0.55f));
            Pill(new Rect2(bar.Position, Mathf.Max(bar.Size.X * enemy.Health / enemy.MaxHealth, bar.Size.Y), bar.Size.Y), HealthColor);
        }
    }
}
