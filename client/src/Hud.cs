using System;
using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// The game interface, drawn in the same 640 x 360 pixels as the world and scaled up with it.
/// All text is English.
/// </summary>
public partial class Hud : Control
{
    private const float UiHeight = 360f;
    private const int Slot = 22;
    private const float TagRange = 34f;

    private static readonly Color Panel = new("262b44");
    private static readonly Color PanelLight = new("3a4466");
    private static readonly Color Edge = new("8b9bb4");
    private static readonly Color Ink = new("181425");
    private static readonly Color Text = new("ead4aa");
    private static readonly Color HealthColor = new("e43b44");
    private static readonly Color HealthDark = new("a22633");
    private static readonly Color StaminaColor = new("63c74d");
    private static readonly Color StaminaDark = new("3e8948");

    private readonly Dictionary<string, Texture2D> _icons = new();
    private readonly List<(Rect2 Rect, SlotRef Slot)> _slots = new();
    private Font _font = null!;
    private float _shownHealth = 1f;
    private float _ghostHealth = 1f;
    private float _shownStamina = 1f;
    private float _time;
    private Item? _held;
    private float _shownXp;
    private int _uiScale = 2;
    private string _dialogName = "";
    private string _dialogText = "";
    private (string Label, Command Command)[] _dialogOptions = System.Array.Empty<(string, Command)>();
    private string[] _story = System.Array.Empty<string>();
    private float _storyTime;
    private float _bannerTime;
    private string _banner = "";
    private Color _bannerColor = Colors.White;

    private sealed class FloatingText
    {
        public required string Text;
        public required Vector3 Position;
        public required Color Color;
        public float Age;
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
        _font = GD.Load<Font>("res://assets/ui/pixel_font.fnt");
        _icons["wooden_sword"] = GD.Load<Texture2D>("res://assets/ui/wooden_sword.png");
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Stop;
        GetViewport().SizeChanged += FitWindow;
        FitWindow();
    }

    private void FitWindow()
    {
        var window = GetViewport().GetVisibleRect().Size;
        _uiScale = Mathf.Max(1, Mathf.RoundToInt(window.Y / UiHeight));
        Scale = Vector2.One * _uiScale;
        Size = window / _uiScale;
    }

    /// <summary>Turns a position in the 3D view into a position in this interface.</summary>
    private Vector2 ToHud(Vector3 world) => Camera!.UnprojectPosition(world) * WorldScale / _uiScale;

    /// <summary>Shows a line of text in the world that rises and fades.</summary>
    public void AddFloating(string text, Vector3 position, Color color) =>
        _floating.Add(new FloatingText { Text = text, Position = position, Color = color });

    /// <summary>Shows a large message across the top of the screen for a few seconds.</summary>
    public void ShowBanner(string text, Color color)
    {
        _banner = text;
        _bannerColor = color;
        _bannerTime = 3f;
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
    }

    public void CloseDialog() => _dialogName = "";

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
        var maxHealth = Snapshot?.MaxHealth ?? Tuning.PlayerHealth;
        var health = Mathf.Clamp(Player.Health / (float)maxHealth, 0f, 1f);
        var xp = Snapshot is { XpToNext: > 0 } current ? current.Xp / (float)current.XpToNext : 0f;
        _shownXp = xp < _shownXp ? xp : Mathf.Lerp(_shownXp, xp, 1f - Mathf.Exp(-8f * d));
        _bannerTime = Mathf.Max(0f, _bannerTime - d);
        _storyTime += d;
        foreach (var text in _floating)
        {
            text.Age += d;
        }

        _floating.RemoveAll(t => t.Age > 1.1f);
        var stamina = Mathf.Clamp(Player.Stamina / Tuning.MaxStamina, 0f, 1f);
        _shownHealth = Mathf.Lerp(_shownHealth, health, 1f - Mathf.Exp(-16f * d));
        _shownStamina = Mathf.Lerp(_shownStamina, stamina, 1f - Mathf.Exp(-22f * d));

        // The pale part of the bar shows the damage just taken, then drains away.
        _ghostHealth = health > _ghostHealth ? health : Mathf.MoveToward(_ghostHealth, health, d * 0.35f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        _slots.Clear();
        DrawNametags();
        DrawFloating();
        DrawStatus();
        DrawHotbar();
        DrawGuide();
        if (InventoryOpen)
        {
            DrawInventory();
        }

        if (_held is not null)
        {
            DrawTextureRect(_icons[_held.Icon], new Rect2(GetLocalMousePosition() - new Vector2(8, 8), 16, 16), false);
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

    private void DrawStatus()
    {
        DrawPanel(new Rect2(6, 6, 134, 36));
        DrawString(_font, new Vector2(11, 16), $"Wanderer   Lv {Level}", HorizontalAlignment.Left, -1, 11, Text);
        DrawBar(new Rect2(11, 19, 124, 9), _shownHealth, _ghostHealth, HealthColor, HealthDark,
                $"{Math.Max(Player.Health, (short)0)} / {Snapshot?.MaxHealth ?? Tuning.PlayerHealth}", _shownHealth < 0.3f);
        DrawBar(new Rect2(11, 30, 124, 7), _shownStamina, _shownStamina, StaminaColor, StaminaDark, "", Player.Stamina < 15f);
        if (Player.SlowTicks > 0)
        {
            var label = $"Slowed {Player.SlowTicks / 60f:F1}s";
            var width = _font.GetStringSize(label, HorizontalAlignment.Left, -1, 11).X;
            DrawPanel(new Rect2(6, 45, width + 12, 14));
            DrawString(_font, new Vector2(12, 55), label, HorizontalAlignment.Left, -1, 11, new Color("8fe05a"));
        }

        if (Notice.Length > 0)
        {
            DrawString(_font, new Vector2(6, 354), Notice, HorizontalAlignment.Left, -1, 11, Text);
        }

        if (_bannerTime > 0f)
        {
            var size = _font.GetStringSize(_banner, HorizontalAlignment.Left, -1, 22);
            var at = new Vector2((Size.X - size.X) / 2f, 62f - Mathf.Min(_bannerTime - 2.7f, 0f) * 0f);
            DrawString(_font, at + Vector2.One, _banner, HorizontalAlignment.Left, -1, 22, Ink);
            DrawString(_font, at, _banner, HorizontalAlignment.Left, -1, 22, _bannerColor);
        }
    }

    private void DrawGuide()
    {
        var gold = $"{Snapshot?.Gold ?? 0} gold";
        var goldWidth = _font.GetStringSize(gold, HorizontalAlignment.Left, -1, 11).X;
        DrawOutlined(gold, new Vector2(Size.X - goldWidth - 8, 16), new Color("feae34"));
        if (Objective.Length > 0)
        {
            var width = _font.GetStringSize(Objective, HorizontalAlignment.Left, -1, 11).X;
            DrawPanel(new Rect2(Size.X - width - 16, 22, width + 10, 14));
            DrawString(_font, new Vector2(Size.X - width - 11, 33), Objective, HorizontalAlignment.Left, -1, 11, Text);
        }

        if (Prompt.Length > 0 && !DialogOpen)
        {
            var width = _font.GetStringSize(Prompt, HorizontalAlignment.Left, -1, 11).X;
            DrawOutlined(Prompt, new Vector2((Size.X - width) / 2f, Size.Y - 70f), new Color("fee761"));
        }

        var line = (int)(_storyTime / 4f);
        if (line < _story.Length)
        {
            var text = _story[line];
            var width = _font.GetStringSize(text, HorizontalAlignment.Left, -1, 11).X;
            DrawPanel(new Rect2((Size.X - width) / 2f - 8, 92, width + 16, 16));
            DrawString(_font, new Vector2((Size.X - width) / 2f, 104), text, HorizontalAlignment.Left, -1, 11, Text);
        }

        if (!DialogOpen)
        {
            return;
        }

        var textHeight = _font.GetMultilineStringSize(_dialogText, HorizontalAlignment.Left, 364, 11).Y;
        var height = 32f + textHeight + _dialogOptions.Length * 11f;
        var box = new Rect2((Size.X - 380f) / 2f, Size.Y - 58f - height, 380, height);
        DrawPanel(box);
        DrawString(_font, box.Position + new Vector2(8, 12), _dialogName, HorizontalAlignment.Left, -1, 11, new Color("fee761"));
        DrawMultilineString(_font, box.Position + new Vector2(8, 25), _dialogText, HorizontalAlignment.Left, 364, 11, -1, Text);
        for (var i = 0; i < _dialogOptions.Length; i++)
        {
            DrawString(_font, box.Position + new Vector2(8, 29 + textHeight + i * 11), $"[{i + 1}] {_dialogOptions[i].Label}", HorizontalAlignment.Left, -1, 11, Colors.White);
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
            var width = _font.GetStringSize(text.Text, HorizontalAlignment.Left, -1, 11).X;
            DrawOutlined(text.Text, new Vector2(at.X - width / 2f, at.Y), text.Color);
        }
    }

    private void DrawBar(Rect2 rect, float value, float ghost, Color color, Color dark, string label, bool warn)
    {
        DrawRect(rect.Grow(1), Ink);
        DrawRect(rect, new Color("3e2731"));
        var ghostRect = new Rect2(rect.Position, rect.Size.X * ghost, rect.Size.Y);
        DrawRect(ghostRect, new Color("feae34"));
        var fill = new Rect2(rect.Position, rect.Size.X * value, rect.Size.Y);
        DrawRect(fill, dark);
        DrawRect(new Rect2(fill.Position, fill.Size.X, Mathf.Ceil(rect.Size.Y * 0.6f)), color);

        // A band of light runs along the bar; it speeds up when the value is low.
        var sweep = Mathf.PosMod(_time * (warn ? 160f : 46f), rect.Size.X + 60f) - 30f;
        var shine = rect.Position.X + sweep;
        var left = Mathf.Max(shine, rect.Position.X);
        var right = Mathf.Min(shine + 7f, rect.Position.X + fill.Size.X);
        if (right > left)
        {
            DrawRect(new Rect2(left, rect.Position.Y, right - left, 2), new Color(1, 1, 1, 0.75f));
        }

        if (warn && Mathf.Sin(_time * 10f) > 0)
        {
            DrawRect(rect.Grow(1), new Color("fee761"), false);
        }

        if (label.Length > 0)
        {
            DrawOutlined(label, new Vector2(rect.Position.X + rect.Size.X / 2 - label.Length * 2.6f, rect.End.Y - 1), Colors.White);
        }
    }

    private void DrawHotbar()
    {
        var width = Inventory.HotbarSlots * (Slot + 2) + 6;
        var left = (Size.X - width) / 2f;
        var top = Size.Y - Slot - 12f;
        DrawPanel(new Rect2(left, top, width, Slot + 8));
        DrawXpBar(left, top - 14f, width);
        for (var i = 0; i < Inventory.HotbarSlots; i++)
        {
            var rect = new Rect2(left + 4 + i * (Slot + 2), top + 4, Slot, Slot);
            DrawSlot(rect, Items.Hotbar[i], new SlotRef("hotbar", i));
            if (i == Items.SelectedHotbar)
            {
                var pulse = 0.65f + Mathf.Sin(_time * 5f) * 0.35f;
                DrawRect(rect.Grow(1), new Color("fee761", pulse), false);
            }

            DrawOutlined((i + 1).ToString(), rect.Position + new Vector2(2, 9), Text);
        }
    }

    /// <summary>Experience sits right above the hotbar, with the level on the left and the numbers on it.</summary>
    private void DrawXpBar(float left, float top, float width)
    {
        var levelBox = new Rect2(left - 2, top, 30, 11);
        DrawPanel(levelBox);
        DrawOutlined($"Lv {Level}", levelBox.Position + new Vector2(3, 9), new Color("fee761"));

        var bar = new Rect2(left + 32, top + 1, width - 32, 9);
        DrawRect(bar.Grow(1), Ink);
        DrawRect(bar, new Color("3e2731"));
        var fill = new Rect2(bar.Position, bar.Size.X * _shownXp, bar.Size.Y);
        DrawRect(fill, new Color("d77643"));
        DrawRect(new Rect2(fill.Position, fill.Size.X, 4), new Color("feae34"));
        var sweep = Mathf.PosMod(_time * 40f, bar.Size.X + 30f) - 15f;
        var from = Mathf.Max(bar.Position.X + sweep, bar.Position.X);
        var to = Mathf.Min(bar.Position.X + sweep + 8f, bar.Position.X + fill.Size.X);
        if (to > from)
        {
            DrawRect(new Rect2(from, bar.Position.Y, to - from, 2), new Color("fee761"));
        }

        var label = $"XP {Snapshot?.Xp ?? 0} / {Snapshot?.XpToNext ?? 0}";
        var size = _font.GetStringSize(label, HorizontalAlignment.Left, -1, 11).X;
        DrawOutlined(label, new Vector2(bar.Position.X + (bar.Size.X - size) / 2f, bar.End.Y - 1), Colors.White);
    }

    private void DrawCrystals(Rect2 window)
    {
        var counts = Snapshot?.Crystals ?? new int[Crystals.Tiers];
        var weight = 0f;
        var y = window.Position.Y + 146;
        for (var i = 0; i < Crystals.Tiers; i++)
        {
            var def = Crystals.All[i];
            weight += counts[i] * def.Weight;
            var x = window.Position.X + 180 + i * 37;
            DrawRect(new Rect2(x, y, 7, 9), new Color(def.Color));
            DrawRect(new Rect2(x + 1, y + 1, 2, 3), Colors.White);
            DrawString(_font, new Vector2(x + 10, y + 9), counts[i].ToString(), HorizontalAlignment.Left, -1, 11, Text);
        }

        DrawString(_font, new Vector2(window.Position.X + 180, y + 22), $"Crystals: {weight:F1} kg", HorizontalAlignment.Left, -1, 11, Edge);
    }

    private void DrawInventory()
    {
        var window = new Rect2((Size.X - 340f) / 2f, (Size.Y - 220f) / 2f, 340, 196);
        DrawPanel(window);
        DrawString(_font, window.Position + new Vector2(10, 13), "Equipment", HorizontalAlignment.Left, -1, 11, Text);
        DrawString(_font, window.Position + new Vector2(180, 13), "Inventory", HorizontalAlignment.Left, -1, 11, Text);

        var equip = new (EquipSlot Slot, string Label, int X, int Y)[]
        {
            (EquipSlot.Head, "Head", 62, 22),
            (EquipSlot.Chest, "Chest", 62, 56),
            (EquipSlot.Legs, "Legs", 62, 90),
            (EquipSlot.Boots, "Boots", 62, 124),
            (EquipSlot.Weapon, "Weapon", 16, 56),
            (EquipSlot.Ring, "Ring", 108, 56),
        };
        foreach (var (slot, label, x, y) in equip)
        {
            var rect = new Rect2(window.Position + new Vector2(x, y), Slot, Slot);
            DrawSlot(rect, Items.Equipped[slot], new SlotRef("equip", 0, slot));
            DrawString(_font, rect.Position + new Vector2(-3, Slot + 10), label, HorizontalAlignment.Left, -1, 11, Edge);
        }

        for (var i = 0; i < Inventory.BagSlots; i++)
        {
            var rect = new Rect2(window.Position + new Vector2(180 + i % 5 * (Slot + 8), 20 + i / 5 * (Slot + 8)), Slot, Slot);
            DrawSlot(rect, Items.Bag[i], new SlotRef("bag", i));
        }

        DrawCrystals(window);
        DrawTooltip(window);
    }

    private void DrawTooltip(Rect2 window)
    {
        var mouse = GetLocalMousePosition();
        foreach (var (rect, slot) in _slots)
        {
            if (rect.HasPoint(mouse) && Read(slot) is { } item)
            {
                var position = window.Position + new Vector2(10, window.Size.Y - 30);
                DrawString(_font, position, item.Name, HorizontalAlignment.Left, -1, 11, new Color("fee761"));
                DrawString(_font, position + new Vector2(0, 12), item.Description, HorizontalAlignment.Left, -1, 11, Text);
                return;
            }
        }

        DrawString(_font, window.Position + new Vector2(10, window.Size.Y - 8), "Click to pick up and place items.  [I] closes.", HorizontalAlignment.Left, -1, 11, Edge);
    }

    private void DrawSlot(Rect2 rect, Item? item, SlotRef slot)
    {
        _slots.Add((rect, slot));
        DrawRect(rect, Ink);
        DrawRect(rect.Grow(-1), PanelLight);
        DrawRect(new Rect2(rect.Position + Vector2.One, rect.Size.X - 2, 1), Panel);
        if (rect.HasPoint(GetLocalMousePosition()) && InventoryOpen)
        {
            DrawRect(rect, Text, false);
        }

        if (item is not null)
        {
            DrawTextureRect(_icons[item.Icon], new Rect2(rect.Position + new Vector2(3, 3), 16, 16), false);
        }
    }

    private void DrawPanel(Rect2 rect)
    {
        DrawRect(rect.Grow(1), Ink);
        DrawRect(rect, Panel);
        DrawRect(new Rect2(rect.Position, rect.Size.X, 1), Edge);
        DrawRect(new Rect2(rect.Position, 1, rect.Size.Y), PanelLight);
    }

    private void DrawOutlined(string text, Vector2 position, Color color)
    {
        foreach (var offset in new[] { Vector2.Left, Vector2.Right, Vector2.Up, Vector2.Down })
        {
            DrawString(_font, position + offset, text, HorizontalAlignment.Left, -1, 11, Ink);
        }

        DrawString(_font, position, text, HorizontalAlignment.Left, -1, 11, color);
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
        var rare = def.Rare;
        var threat = ThreatRules.Compare(enemy.Level, Level);
        var color = threat switch
        {
            Threat.Strong => new Color("e43b44"),
            Threat.Weak => new Color("63c74d"),
            _ => Colors.White,
        };
        var label = $"Lv {enemy.Level} {def.Name}";
        var width = _font.GetStringSize(label, HorizontalAlignment.Left, -1, 11).X;
        var left = at.X - width / 2f;

        if (rare)
        {
            // Rare enemies get a moving glow behind the tag, and sparks that run around it.
            var pulse = 0.5f + Mathf.Sin(_time * 6f) * 0.5f;
            DrawRect(new Rect2(left - 3, at.Y - 11, width + 6, 13), new Color("b55088", 0.35f + pulse * 0.3f));
            var runner = Mathf.PosMod(_time * 60f, width + 6f);
            DrawRect(new Rect2(left - 3 + runner, at.Y - 11, 3, 1), new Color("fee761"));
            DrawRect(new Rect2(left + width + 3 - runner - 3, at.Y + 1, 3, 1), new Color("fee761"));
        }

        DrawOutlined(label, new Vector2(left, at.Y), color);
        if (enemy.Fighting)
        {
            DrawOutlined("!", new Vector2(left - 7, at.Y), new Color("feae34"));
        }

        if (enemy.Health < enemy.MaxHealth)
        {
            var bar = new Rect2(at.X - 14, at.Y + 3, 28, 2);
            DrawRect(bar.Grow(1), Ink);
            DrawRect(new Rect2(bar.Position, bar.Size.X * enemy.Health / enemy.MaxHealth, bar.Size.Y), HealthColor);
        }
    }
}
