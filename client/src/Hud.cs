using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// The game interface: dark glass panels with a warm gold trim, rounded bars and a smooth font, drawn
/// at the full window resolution over the pixel-art world. Sizes are given for a 1080 pixel tall window
/// and scale with it. All text is English.
/// </summary>
public partial class Hud : Control
{
    private const float DesignHeight = 1080f;
    private const float SlotSize = 76f;
    private const float SlotGap = 8f;
    private const float TagRange = 34f;
    private const float LootFeedSeconds = 4.5f;

    private static readonly Color Glass = new(0.055f, 0.065f, 0.095f, 0.8f);
    private static readonly Color GlassDeep = new(0.045f, 0.05f, 0.075f, 0.93f);
    private static readonly Color Rim = new(1f, 1f, 1f, 0.11f);
    private static readonly Color Text = new("eef1f6");
    private static readonly Color Muted = new("9aa4b5");
    private static readonly Color Accent = new("f2c35b");
    private static readonly Color AccentDim = new(0.95f, 0.76f, 0.36f, 0.35f);
    private static readonly Color HealthColor = new("e5484d");
    private static readonly Color HealthLight = new("ff7a7e");
    private static readonly Color StaminaColor = new("3fbf7a");
    private static readonly Color Track = new(1f, 1f, 1f, 0.08f);
    private static readonly Color Shadow = new(0f, 0f, 0f, 0.55f);
    private static readonly Color Ink = new("1d1608");

    /// <summary>Item tiers share the crystal names and colours.</summary>
    private static readonly (string Name, Color Color)[] Tiers =
    {
        ("Common", new Color("c9d1dc")),
        ("Rare", new Color("58b4ff")),
        ("Epic", new Color("d77be0")),
        ("Legendary", new Color("feae34")),
    };

    private readonly Dictionary<string, Texture2D> _icons = new();
    private readonly List<(Rect2 Rect, SlotRef Slot)> _slots = new();
    private readonly List<(Rect2 Rect, string Icon, float Alpha)> _iconDraws = new();
    private readonly List<LootLine> _lootFeed = new();
    private readonly Queue<(Command Command, ushort Arg)> _commands = new();
    private Font _font = null!;
    private Font _bold = null!;
    private TooltipLayer _tooltipLayer = null!;
    private IconLayer _iconLayer = null!;
    private float _shownHealth = 1f;
    private float _ghostHealth = 1f;
    private float _shownStamina = 1f;
    private float _time;
    private float _shownXp;
    private float _scale = 1f;
    private string _dialogName = "";
    private string _dialogText = "";
    private float _dialogAge;
    private (string Label, Command Command)[] _dialogOptions = Array.Empty<(string, Command)>();
    private string[] _story = Array.Empty<string>();
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

    private sealed class LootLine
    {
        public required Item Item;
        public int Count;
        public float Age;
    }

    /// <summary>Pixel-art icons need nearest filtering, unlike the smooth text, so they draw on their own layer.</summary>
    private sealed partial class IconLayer : Control
    {
        public Hud Hud = null!;

        public override void _Draw()
        {
            if (Hud.MenuOpen)
            {
                return;
            }

            // Pixel art grows only by whole steps: the largest that fits the place, centred in it.
            foreach (var (rect, icon, alpha) in Hud._iconDraws)
            {
                var texture = Hud.Icon(icon);
                var size = texture.GetSize();
                var scale = Mathf.Max(1f, Mathf.Floor(Mathf.Min(rect.Size.X / size.X, rect.Size.Y / size.Y)));
                var shown = size * scale;
                DrawTextureRect(texture, new Rect2((rect.Position + (rect.Size - shown) / 2f).Round(), shown), false, new Color(1, 1, 1, alpha));
            }
        }
    }

    /// <summary>The item card under the pointer draws last, above the icons, so no icon covers it.</summary>
    private sealed partial class TooltipLayer : Control
    {
        public Hud Hud = null!;

        public override void _Draw()
        {
            if (!Hud.MenuOpen)
            {
                Hud.DrawTooltip(this);
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

    /// <summary>F3 shows where the player stands, so a problem spot can be reported exactly.</summary>
    public bool ShowPosition { get; set; }

    /// <summary>How far the camera has moved in to aim a bow, from 0 to 1. Above 0 the crosshair shows.</summary>
    public float Aiming { get; set; }

    /// <summary>A short title under the speaker's name, such as Guild Receptionist.</summary>
    public string DialogRole { get; set; } = "";

    /// <summary>World positions shown this frame, by enemy id, so tags follow the drawn enemies.</summary>
    public Dictionary<int, Vector3> EnemyPositions { get; } = new();

    public override void _Ready()
    {
        var file = GD.Load<FontFile>("res://assets/ui/Nunito.ttf");
        _font = Weight(file, 600);
        _bold = Weight(file, 800);
        TextureFilter = TextureFilterEnum.Linear;
        MouseFilter = MouseFilterEnum.Stop;
        _iconLayer = new IconLayer { Hud = this, TextureFilter = TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_iconLayer);
        _tooltipLayer = new TooltipLayer { Hud = this, TextureFilter = TextureFilterEnum.Linear, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_tooltipLayer);
        GetViewport().SizeChanged += FitWindow;
        FitWindow();
    }

    private Texture2D Icon(string name)
    {
        if (!_icons.TryGetValue(name, out var texture))
        {
            var path = $"res://assets/ui/icons/{name}.png";
            texture = GD.Load<Texture2D>(ResourceLoader.Exists(path) ? path : $"res://assets/ui/{name}.png");
            _icons[name] = texture;
        }

        return texture;
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

    /// <summary>Lists picked up items on the right, merging repeats of the same item.</summary>
    public void AddLoot(Item item, int count)
    {
        if (_lootFeed.FirstOrDefault(l => l.Item == item && l.Age < LootFeedSeconds - 1f) is { } line)
        {
            line.Count += count;
            line.Age = Mathf.Min(line.Age, 0.3f);
            return;
        }

        _lootFeed.Add(new LootLine { Item = item, Count = count });
        if (_lootFeed.Count > 6)
        {
            _lootFeed.RemoveAt(0);
        }
    }

    /// <summary>Commands the interface itself asks for, such as eating, for the client to send.</summary>
    public (Command Command, ushort Arg) TakeCommand() => _commands.Count > 0 ? _commands.Dequeue() : (Command.None, (ushort)0);

    /// <summary>The Guild chest window is open beside the bag.</summary>
    public bool ChestOpen { get; private set; }

    /// <summary>Opens the bag with the Guild chest beside it.</summary>
    public void OpenChest()
    {
        ChestOpen = true;
        if (!InventoryOpen)
        {
            ToggleInventory();
        }
    }

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

    /// <summary>The minimap's size, and the line under it where the right column of the HUD begins.</summary>
    public const float MinimapSize = 210f;
    public const float MinimapBottom = 28f + MinimapSize;

    /// <summary>Signs over things a player can use: where they float and what they say.</summary>
    public List<(Vector3 At, string Text)> Signs { get; } = new();

    public string Objective { get; set; } = "";

    public bool DialogOpen => _dialogName.Length > 0;

    /// <summary>Opens a conversation. Options are chosen with the number keys or the mouse.</summary>
    public void ShowDialog(string name, string text, params (string Label, Command Command)[] options)
    {
        _dialogName = name;
        _dialogText = text;
        _dialogOptions = options;
        _dialogAge = 0f;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void CloseDialog()
    {
        _dialogName = "";
        DialogRole = "";
        if (!InventoryOpen && !CharacterOpen && Camera is not null)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    /// <summary>The character window: attributes and the points to spend on them.</summary>
    public bool CharacterOpen { get; private set; }

    public void ToggleCharacter()
    {
        CharacterOpen = !CharacterOpen;
        Input.MouseMode = CharacterOpen || InventoryOpen || DialogOpen ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
    }

    private readonly List<(Rect2 Rect, PlayerAttribute Attribute)> _raiseButtons = new();

    /// <summary>The character window: each attribute with its points, what a point does, and a button to add one.</summary>
    private void DrawCharacter()
    {
        _raiseButtons.Clear();
        var window = new Rect2((Size.X - 760f) / 2f, (Size.Y - 660f) / 2f, 760, 660);
        Card(window, 22, GlassDeep, true);
        Label("Character", window.Position + new Vector2(34, 56), 24, Text, _bold);
        var unspent = Snapshot?.UnspentPoints ?? 0;
        var points = unspent > 0 ? $"{unspent} point{(unspent == 1 ? "" : "s")} to spend" : "No points to spend";
        Label(points, window.Position + new Vector2(window.Size.X - 34 - Width(points, 17, _bold), 56), 17, unspent > 0 ? Accent : Muted, _bold);
        for (var i = 0; i < Attributes.Count; i++)
        {
            var attribute = (PlayerAttribute)i;
            var value = Snapshot?.Attributes[i] ?? 0;
            var row = new Rect2(window.Position + new Vector2(28, 90 + i * 74), window.Size.X - 56, 64);
            Rounded(row, 14, new Color(1, 1, 1, 0.05f), new Color(1, 1, 1, 0.08f), 1);
            Label(Attributes.Name(attribute), row.Position + new Vector2(20, 28), 20, Text, _bold);
            Label(Attributes.Describe(attribute), row.Position + new Vector2(20, 52), 14, Muted);
            var number = value.ToString();
            Label(number, new Vector2(row.End.X - 96 - Width(number, 26, _bold), row.Position.Y + 42), 26, value > 0 ? Accent : Text, _bold);
            var button = new Rect2(row.End.X - 64, row.Position.Y + 12, 44, 40);
            var hover = button.HasPoint(GetLocalMousePosition());
            Rounded(button, 12, unspent > 0 ? (hover ? Accent : Accent with { A = 0.75f }) : new Color(1, 1, 1, 0.06f), Colors.Transparent, 0);
            Label("+", new Vector2(button.Position.X + 14, button.Position.Y + 31), 26, unspent > 0 ? Ink : Muted, _bold);
            if (unspent > 0)
            {
                _raiseButtons.Add((button, attribute));
            }
        }

        // Skills learned from books: drag one onto the hotbar to cast it with that key.
        Label("Skills", window.Position + new Vector2(34, 482), 18, Text, _bold);
        for (var i = 0; i < SkillDef.All.Count; i++)
        {
            var rect = new Rect2(window.Position + new Vector2(34 + i * (SlotSize + 9), 494), SlotSize, SlotSize);
            DrawSlot(rect, Items.Skill(i), new SlotRef("skill", i));
        }

        if (Enumerable.Range(0, SkillDef.All.Count).All(i => Items.Skill(i) is null))
        {
            Label("None yet. Enemies that use a skill can drop the book that teaches it.", window.Position + new Vector2(34 + SkillDef.All.Count * (SlotSize + 9) + 10, 532), 14, Muted);
        }

        var maxHealth = Snapshot?.MaxHealth ?? Tuning.PlayerHealth;
        var summary = $"Health {maxHealth}     Stamina {Attributes.MaxStamina(Player):0}     Level {Level}";
        Label(summary, window.Position + new Vector2(34, window.Size.Y - 52), 17, Text, _bold);
        var reset = $"Reset all points with Mira at the Guild for {Attributes.ResetGoldPerLevel * Level} gold";
        Label(reset, window.Position + new Vector2(34, window.Size.Y - 24), 14, Muted);
        Keycap("K", new Vector2(window.End.X - 120, window.End.Y - 46), 14);
        Label("close", new Vector2(window.End.X - 86, window.End.Y - 24), 15, Muted);
    }

    /// <summary>Picks the option with this command, as a player would by reading the list.</summary>
    public Command ChooseCommand(Command wanted) => Choose(Array.FindIndex(_dialogOptions, o => o.Command == wanted) is var i and >= 0 ? i : int.MaxValue);

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
        ChestOpen &= InventoryOpen;
        if (!InventoryOpen && Items.Held is { } held)
        {
            // Whatever is still in hand goes back to the first free place.
            var free = Array.IndexOf(Items.Bag, null);
            if (free >= 0)
            {
                Items.Bag[free] = held;
            }

            Items.Held = null;
        }

        Input.MouseMode = InventoryOpen || DialogOpen ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
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

        foreach (var line in _lootFeed)
        {
            line.Age += d;
        }

        _floating.RemoveAll(t => t.Age > 1.2f);
        _lootFeed.RemoveAll(l => l.Age > LootFeedSeconds);
        var stamina = Mathf.Clamp(Player.Stamina / Attributes.MaxStamina(Player), 0f, 1f);
        _shownHealth = Mathf.Lerp(_shownHealth, health, 1f - Mathf.Exp(-16f * d));
        _shownStamina = Mathf.Lerp(_shownStamina, stamina, 1f - Mathf.Exp(-22f * d));

        // The pale part of the bar shows the damage just taken, then drains away.
        _ghostHealth = health > _ghostHealth ? health : Mathf.MoveToward(_ghostHealth, health, d * 0.35f);
        QueueRedraw();
        _iconLayer.QueueRedraw();
        _tooltipLayer.QueueRedraw();
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
        DrawLootFeed();
        if (ShowPosition)
        {
            var p = Player.Position;
            Label($"x {p.X:F1}   y {p.Y:F1}   z {p.Z:F1}", new Vector2(28, Size.Y - 24), 16, Text, _bold, true);
        }

        DrawDraw();
        DrawMessages();
        DrawDialog();
        if (InventoryOpen)
        {
            DrawInventory();
        }

        if (CharacterOpen)
        {
            DrawCharacter();
        }

        if (Items.Held is { } held)
        {
            _iconDraws.Add((new Rect2(GetLocalMousePosition() - new Vector2(32, 32), 64, 64), held.Icon, 1f));
        }

        if (MenuOpen)
        {
            DrawMenu();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (MenuInput(@event))
        {
            return;
        }

        if (@event is not InputEventMouseButton { Pressed: true } click)
        {
            return;
        }

        if (DialogOpen && !InventoryOpen && click.ButtonIndex == MouseButton.Left)
        {
            for (var i = 0; i < _optionRects.Count; i++)
            {
                if (_optionRects[i].HasPoint(click.Position))
                {
                    var command = Choose(i);
                    if (command != Command.None)
                    {
                        _commands.Enqueue((command, 0));
                    }

                    return;
                }
            }
        }

        if (CharacterOpen && click.ButtonIndex == MouseButton.Left)
        {
            foreach (var (rect, attribute) in _raiseButtons)
            {
                if (rect.HasPoint(click.Position))
                {
                    _commands.Enqueue((Command.RaiseAttribute, (ushort)attribute));
                    return;
                }
            }
        }

        if (!InventoryOpen && !CharacterOpen)
        {
            return;
        }

        foreach (var (rect, slot) in _slots)
        {
            if (!rect.HasPoint(click.Position))
            {
                continue;
            }

            // With the chest open, a left click moves things between the bag and the chest.
            if (ChestOpen && click.ButtonIndex == MouseButton.Left && slot.Area is "bag" or "chest")
            {
                Stow(slot);
                return;
            }

            if (click.ButtonIndex == MouseButton.Right && Read(slot) is { IsGear: true } piece)
            {
                // Right click puts a piece on or takes it off; with Shift it throws a piece away.
                var worn = slot.Area == "equip";
                if (click.ShiftPressed && slot.Area == "bag")
                {
                    _commands.Enqueue((GearCommands.Discard(piece.Place), 0));
                }
                else if (!click.ShiftPressed)
                {
                    _commands.Enqueue((GearCommands.Toggle(piece.Place), 0));
                }
            }
            else if (click.ButtonIndex == MouseButton.Right)
            {
                Use(Read(slot));
            }
            else if (click.ButtonIndex == MouseButton.Left)
            {
                Exchange(slot);
            }

            return;
        }
    }

    /// <summary>Puts a bag item in the chest, or takes a chest item back: a whole stack, or one piece of gear.</summary>
    private void Stow(SlotRef slot)
    {
        if (slot.Area == "chest")
        {
            var contents = Items.ChestContents();
            if (slot.Index < contents.Count)
            {
                var (item, _, gear) = contents[slot.Index];
                _commands.Enqueue(gear ? (Command.TakeGear, (ushort)item.Place) : (Command.TakeItem, (ushort)item.Index));
            }

            return;
        }

        if (Items.Bag[slot.Index] is { } bagged)
        {
            if (bagged.IsGear && !Items.IsWorn(bagged))
            {
                _commands.Enqueue((Command.StoreGear, (ushort)bagged.Place));
            }
            else if (!bagged.IsGear && bagged.Index >= 0)
            {
                _commands.Enqueue((Command.StoreItem, (ushort)bagged.Index));
            }
        }
    }

    /// <summary>Where a slot was drawn last frame, in window pixels, for bots that point at it.</summary>
    public Vector2? SlotCentre(string area, int index)
    {
        foreach (var (rect, slot) in _slots)
        {
            if (slot.Area == area && slot.Index == index)
            {
                return (rect.Position + rect.Size / 2f) * _scale;
            }
        }

        return null;
    }

    /// <summary>
    /// The number keys use a hotbar slot: food in it is eaten, and gear in it is put on, which for a
    /// weapon takes it in hand.
    /// </summary>
    public void SelectHotbar(int index)
    {
        if (Items.Hotbar[index] is { IsFood: true } or { IsPotion: true })
        {
            Use(Items.Hotbar[index]);
            return;
        }

        if (Items.Hotbar[index] is { IsSkill: true } skill)
        {
            Cast(skill);
            return;
        }

        if (Items.Hotbar[index] is { IsGear: true } piece && !Items.IsWorn(piece))
        {
            _commands.Enqueue((GearCommands.Toggle(piece.Place), 0));
        }

        Items.SelectedHotbar = index;
    }

    /// <summary>Casts a skill from the hotbar, unless it is still cooling down.</summary>
    private void Cast(Item skill)
    {
        if (SkillCooldown(skill.Index) > 0f)
        {
            return;
        }

        _commands.Enqueue((Command.CastSkill, (ushort)skill.Index));
        SkillCast = skill.Index;
    }

    /// <summary>The skill just cast from the hotbar, for the cast pose and sound, or -1. The game clears it.</summary>
    public int SkillCast { get; set; } = -1;

    /// <summary>Seconds of cooldown a skill has left, as the server last said.</summary>
    public float SkillCooldown(int index) =>
        Snapshot is { } s && index >= 0 && index < s.SkillCooldowns.Length ? s.SkillCooldowns[index] / 10f : 0f;

    private void Use(Item? item)
    {
        if (item is { Index: >= 0 } book && book.IsSkillBook && Items.Count(book) > 0)
        {
            _commands.Enqueue((Command.UseItem, (ushort)book.Index));
            return;
        }

        if (item is { Index: >= 0 } food && (food.IsFood || food.IsPotion) && Items.Count(food) > 0)
        {
            _commands.Enqueue((Command.UseItem, (ushort)food.Index));
            if (food.IsPotion)
            {
                _potionUsed = Time.GetTicksMsec();
            }
        }
    }

    /// <summary>Drinks the potion in a slot of the potion bar, unless the last one was too recent.</summary>
    public void DrinkPotion(int index)
    {
        if (Time.GetTicksMsec() - _potionUsed < PotionCooldownMs)
        {
            return;
        }

        Use(Items.Potions[index]);
    }

    private const ulong PotionCooldownMs = (ulong)(GameServer.PotionCooldownTicks * 1000 / Tuning.TickRate);
    private ulong _potionUsed;

    private void Exchange(SlotRef slot)
    {
        var current = Read(slot);

        // A learned skill is lifted as a shortcut, to put on the hotbar.
        if (slot.Area == "skill")
        {
            if (Items.Held is null && current is not null)
            {
                Items.Held = current;
                Items.HeldShortcut = true;
            }
            else if (Items.HeldShortcut)
            {
                Items.Held = null;
                Items.HeldShortcut = false;
            }

            return;
        }

        // The potion bar holds shortcuts to potions only, the same way the hotbar does.
        if (slot.Area == "potion")
        {
            if (Items.Held is { IsPotion: true } potion)
            {
                Items.Potions[slot.Index] = potion;
                if (!Items.HeldShortcut)
                {
                    Items.ReturnHeld(potion);
                }

                Items.Held = null;
                Items.HeldShortcut = false;
            }
            else if (Items.Held is null && current is not null)
            {
                Items.Held = current;
                Items.HeldShortcut = true;
                Items.Potions[slot.Index] = null;
            }

            return;
        }

        // The hotbar holds shortcuts: an item put there stays in the bag, and a shortcut lifted off
        // it vanishes when put down anywhere else.
        if (slot.Area == "hotbar")
        {
            if (Items.Held is { } item)
            {
                Items.PutOnHotbar(slot.Index, item);
                Items.Held = null;
                Items.HeldShortcut = false;
            }
            else if (current is not null)
            {
                Items.Held = current;
                Items.HeldShortcut = true;
                Items.Hotbar[slot.Index] = null;
            }

            return;
        }

        if (Items.HeldShortcut)
        {
            Items.Held = null;
            Items.HeldShortcut = false;
            return;
        }

        Items.HeldFrom = slot.Area == "bag" && current is not null && Items.Held is null ? slot.Index : -1;
        if (Items.Held is { } held && slot.Equip != EquipSlot.None && held.Slot != slot.Equip)
        {
            return;
        }

        // The server decides what is worn. Putting a piece in an equipment slot asks to wear it,
        // which takes off whatever was there; lifting a worn piece out asks to take it off.
        if (slot.Area == "equip")
        {
            if (Items.Held is { IsGear: true } wearing)
            {
                _commands.Enqueue((GearCommands.Toggle(wearing.Place), 0));
            }
            else if (Items.Held is null && current is { IsGear: true } worn)
            {
                _commands.Enqueue((GearCommands.Toggle(worn.Place), 0));
            }

        }

        if (slot.Area == "equip" && Items.Held is { IsGear: true })
        {
            // The server swaps the pieces; the one taken off returns to the bag with the next update.
            Items.Held = null;
            return;
        }

        Write(slot, Items.Held);
        Items.Held = current;
    }

    private Item? Read(SlotRef slot) => slot.Area switch
    {
        "bag" => Items.Bag[slot.Index],
        "hotbar" => Items.Hotbar[slot.Index],
        "potion" => Items.Potions[slot.Index],
        "skill" => Items.Skill(slot.Index),
        "chest" => slot.Index < Items.ChestContents().Count ? Items.ChestContents()[slot.Index].Item : null,
        _ => Items.Equipped[slot.Equip],
    };

    private void Write(SlotRef slot, Item? item)
    {
        switch (slot.Area)
        {
            case "bag":
                Items.Bag[slot.Index] = item;
                break;
            case "potion":
                Items.Potions[slot.Index] = item;
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

    /// <summary>
    /// A glass panel: a soft drop shadow, a body that is a little lighter at the top, a hairline rim
    /// and, for the main windows, a gold rule along the top with small diamonds at its ends.
    /// </summary>
    private void Card(Rect2 rect, float radius = 14f, Color? fill = null, bool trim = false)
    {
        var shadow = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.3f), AntiAliasing = true, ShadowColor = new Color(0, 0, 0, 0.22f), ShadowSize = 14 };
        shadow.SetCornerRadiusAll((int)radius + 2);
        DrawStyleBox(shadow, new Rect2(rect.Position + new Vector2(0, 5), rect.Size));
        var box = new StyleBoxFlat { BgColor = fill ?? Glass, BorderColor = Rim, AntiAliasing = true };
        box.SetBorderWidthAll(1);
        box.SetCornerRadiusAll((int)radius);
        DrawStyleBox(box, rect);

        // A sheen over the upper part, as light falling on glass.
        var sheen = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.035f), AntiAliasing = true };
        sheen.CornerRadiusTopLeft = sheen.CornerRadiusTopRight = (int)radius;
        DrawStyleBox(sheen, new Rect2(rect.Position + Vector2.One, rect.Size.X - 2, Mathf.Min(rect.Size.Y * 0.45f, 60f)));
        if (trim)
        {
            var y = rect.Position.Y + 1.5f;
            DrawLine(new Vector2(rect.Position.X + radius + 14, y), new Vector2(rect.End.X - radius - 14, y), AccentDim, 2f, true);
            foreach (var x in new[] { rect.Position.X + radius + 8, rect.End.X - radius - 8 })
            {
                Diamond(new Vector2(x, y), 5f, Accent);
            }
        }
    }

    private void Diamond(Vector2 at, float size, Color color) =>
        DrawColoredPolygon(new[] { at + new Vector2(0, -size), at + new Vector2(size, 0), at + new Vector2(0, size), at + new Vector2(-size, 0) }, color);

    private void Pill(Rect2 rect, Color color)
    {
        var box = new StyleBoxFlat { BgColor = color, AntiAliasing = true };
        box.SetCornerRadiusAll((int)(rect.Size.Y / 2f));
        DrawStyleBox(box, rect);
    }

    private void Rounded(Rect2 rect, float radius, Color fill, Color border, int width = 1)
    {
        var box = new StyleBoxFlat { BgColor = fill, BorderColor = border, AntiAliasing = true };
        box.SetCornerRadiusAll((int)radius);
        box.SetBorderWidthAll(width);
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

    private static Color TierColor(int tier) => Tiers[Math.Clamp(tier, 0, Tiers.Length - 1)].Color;

    // --- Screens ----------------------------------------------------------------------------------

    private void DrawStatus()
    {
        var card = new Rect2(28, 28, 384, 110);
        Card(card);
        Label("Wanderer", card.Position + new Vector2(22, 40), 24, Text, _bold);
        var level = $"Lv {Level}";
        var levelWidth = Width(level, 15, _bold) + 20;
        Pill(new Rect2(card.End.X - levelWidth - 18, card.Position.Y + 18, levelWidth, 26), Accent);
        Label(level, new Vector2(card.End.X - levelWidth - 8, card.Position.Y + 37), 15, Ink, _bold);

        // Health is the wide bar with its numbers inside; stamina is the thin bar under it.
        var maxHealth = Snapshot?.MaxHealth ?? Tuning.PlayerHealth;
        var health = new Rect2(card.Position + new Vector2(22, 56), card.Size.X - 44, 22);
        Bar(health, _shownHealth, _ghostHealth, HealthColor, HealthLight, _shownHealth < 0.3f);
        var numbers = $"{Math.Max(Player.Health, (short)0)} / {maxHealth}";
        Label(numbers, new Vector2(health.End.X - 12 - Width(numbers, 15, _bold), health.Position.Y + 17), 15, Colors.White, _bold, true);
        Bar(new Rect2(card.Position + new Vector2(22, 87), card.Size.X - 44, 6), _shownStamina, _shownStamina, StaminaColor, new Color("8ff0b8"), Player.Stamina < 15f);

        // Unspent attribute points wait under the card, with the key that opens the window.
        if ((Snapshot?.UnspentPoints ?? 0) > 0 && !CharacterOpen)
        {
            var label = $"{Snapshot!.UnspentPoints} attribute points";
            var pulse = 0.75f + Mathf.Sin(Time.GetTicksMsec() / 260f) * 0.25f;
            var rect = new Rect2(card.End.X - Width(label, 15, _bold) - 70, card.End.Y + 8, Width(label, 15, _bold) + 70, 32);
            Card(rect, 16, new Color(0.35f, 0.27f, 0.08f, 0.85f * pulse));
            Keycap("K", rect.Position + new Vector2(8, 5), 13);
            Label(label, rect.Position + new Vector2(42, 22), 15, Accent, _bold);
        }

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
        // The minimap holds the top right corner; the gold and the objective sit under it.
        var coin = new Rect2(Size.X - 74 - goldWidth, MinimapBottom + 10, 46 + goldWidth, 40);
        Card(coin, 20);
        DrawCircle(coin.Position + new Vector2(22, 20), 10, Accent);
        DrawCircle(coin.Position + new Vector2(22, 20), 6, new Color("d9a032"));
        Label(gold, coin.Position + new Vector2(38, 28), 20, Text, _bold);

        if (Objective.Length > 0)
        {
            var width = Mathf.Max(Width(Objective, 19), 180f) + 44f;
            var rect = new Rect2(Size.X - 28 - width, MinimapBottom + 62, width, 72);
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

    /// <summary>Picked up items slide in on the right under the objective, then fade.</summary>
    private void DrawLootFeed()
    {
        var y = MinimapBottom + 146f;
        foreach (var line in _lootFeed.AsEnumerable().Reverse())
        {
            var slide = 1f - Mathf.Clamp(line.Age * 5f, 0f, 1f);
            var alpha = Mathf.Clamp((LootFeedSeconds - line.Age) * 1.5f, 0f, 1f);
            var name = line.Item.Name;
            var count = line.Count > 1 ? $"x{line.Count}" : "";
            var width = 74f + Width(name, 17, _bold) + (count.Length > 0 ? Width(count, 15, _bold) + 10 : 0);
            var rect = new Rect2(Size.X - 28 - width + slide * 60f, y, width, 44);
            var color = TierColor(line.Item.Tier);
            Card(rect, 12, new Color(Glass.R, Glass.G, Glass.B, Glass.A * alpha));
            Pill(new Rect2(rect.Position + new Vector2(6, 8), 3, 28), new Color(color.R, color.G, color.B, alpha));
            _iconDraws.Add((new Rect2(rect.Position + new Vector2(16, 6), 32, 32), line.Item.Icon, alpha));
            Label(name, rect.Position + new Vector2(58, 28), 17, new Color(color.R, color.G, color.B, alpha), _bold);
            if (count.Length > 0)
            {
                Label(count, rect.Position + new Vector2(64 + Width(name, 17, _bold), 28), 15, new Color(Text.R, Text.G, Text.B, alpha * 0.8f), _bold);
            }

            y += 52f;
        }
    }

    private void DrawMessages()
    {
        var line = (int)(_storyTime / 4f);
        if (line < _story.Length && !DialogOpen)
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
            var lineColor = new Color(1, 1, 1, alpha * 0.7f);
            DrawLine(new Vector2(middle - width / 2f - 90, y - 13), new Vector2(middle - width / 2f - 26, y - 13), lineColor, 2f, true);
            DrawLine(new Vector2(middle + width / 2f + 26, y - 13), new Vector2(middle + width / 2f + 90, y - 13), lineColor, 2f, true);
            Diamond(new Vector2(middle - width / 2f - 16, y - 13), 5f, new Color(Accent.R, Accent.G, Accent.B, alpha));
            Diamond(new Vector2(middle + width / 2f + 16, y - 13), 5f, new Color(Accent.R, Accent.G, Accent.B, alpha));
        }

        if (_bannerTime > 0f)
        {
            var alpha = Mathf.Clamp(_bannerTime, 0f, 1f) * Mathf.Clamp((3.2f - _bannerTime) * 3f, 0f, 1f);
            var lift = (1f - Mathf.Clamp((3.2f - _bannerTime) * 3f, 0f, 1f)) * 12f;
            Centered(_banner, 260f + lift, 44, new Color(_bannerColor.R, _bannerColor.G, _bannerColor.B, alpha), _bold);
        }
    }

    private readonly List<Rect2> _optionRects = new();

    /// <summary>
    /// The conversation window: a name plate with the speaker's title, text that types itself out
    /// word by word, and choices as buttons that answer to the mouse and to the number keys.
    /// </summary>
    private void DrawDialog()
    {
        _optionRects.Clear();
        if (!DialogOpen)
        {
            return;
        }

        const float width = 900f;
        const int size = 21;
        const float pad = 36f;
        var textHeight = _font.GetMultilineStringSize(_dialogText, HorizontalAlignment.Left, width - pad * 2, size).Y;
        const float row = 48f;
        var height = 88f + textHeight + 22f + _dialogOptions.Length * row + 18f;
        var rise = (1f - Mathf.Clamp(_dialogAge * 6f, 0f, 1f)) * 20f;
        var box = new Rect2((Size.X - width) / 2f, Size.Y - 200 - height + rise, width, height);
        Card(box, 20, GlassDeep, true);

        // The name plate sits on the top edge, the title beside it.
        var nameWidth = Width(_dialogName, 19, _bold) + 36;
        var plate = new Rect2(box.Position + new Vector2(30, -20), nameWidth, 40);
        Rounded(plate, 12, Accent, new Color(1, 1, 1, 0.35f));
        Label(_dialogName, plate.Position + new Vector2(18, 27), 19, Ink, _bold);
        if (DialogRole.Length > 0)
        {
            Label(DialogRole.ToUpperInvariant(), new Vector2(plate.End.X + 12, plate.Position.Y + 16), 13, Accent with { A = 0.85f }, _bold, true);
        }

        // Words appear one at a time, so a line never jumps when a long word wraps.
        var shown = Typed(_dialogText, _dialogAge * 70f);
        DrawMultilineString(_font, box.Position + new Vector2(pad, 62), shown, HorizontalAlignment.Left, width - pad * 2, size, -1, Text);

        var top = box.Position.Y + 62 + textHeight + 6;
        DrawLine(new Vector2(box.Position.X + pad, top), new Vector2(box.End.X - pad, top), Rim, 1f);
        var mouse = GetLocalMousePosition();
        var ready = shown.Length == _dialogText.Length;
        for (var i = 0; i < _dialogOptions.Length; i++)
        {
            var rect = new Rect2(box.Position.X + pad - 10, top + 14 + i * row, width - pad * 2 + 20, row - 8);
            _optionRects.Add(rect);
            var hover = rect.HasPoint(mouse);
            var appear = ready ? 1f : 0.35f;
            Rounded(rect, 12, new Color(1, 1, 1, hover ? 0.1f : 0.04f), hover ? AccentDim : new Color(1, 1, 1, 0.07f));
            Keycap((i + 1).ToString(), rect.Position + new Vector2(10, 7), 14);
            var color = _dialogOptions[i].Command == Command.None ? Muted : Text;
            Label(_dialogOptions[i].Label, rect.Position + new Vector2(52, 27), 19, color with { A = appear }, hover ? _bold : _font);
            if (hover)
            {
                Diamond(new Vector2(rect.End.X - 20, rect.Position.Y + rect.Size.Y / 2f), 4f, Accent);
            }
        }
    }

    /// <summary>The start of a text up to whole words, revealing about this many characters.</summary>
    private static string Typed(string text, float characters)
    {
        if (characters >= text.Length)
        {
            return text;
        }

        var end = text.LastIndexOf(' ', Math.Max(0, (int)characters));
        return end <= 0 ? "" : text[..end];
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
        DrawPotionBar(left + width + 14, top);
    }

    /// <summary>The potion bar beside the hotbar: three slots, Z X C, dimmed while potions cool down.</summary>
    private void DrawPotionBar(float left, float top)
    {
        var width = Inventory.PotionSlots * (SlotSize + SlotGap) - SlotGap + 24;
        Card(new Rect2(left, top - 12, width, SlotSize + 24), 18);
        var cooling = Mathf.Clamp(1f - (Time.GetTicksMsec() - _potionUsed) / (float)PotionCooldownMs, 0f, 1f);
        for (var i = 0; i < Inventory.PotionSlots; i++)
        {
            var rect = new Rect2(left + 12 + i * (SlotSize + SlotGap), top, SlotSize, SlotSize);
            DrawSlot(rect, Items.Potions[i], new SlotRef("potion", i));
            if (cooling > 0f && Items.Potions[i] is not null)
            {
                // A shade that drains away from the top as the cooldown runs out.
                DrawRect(new Rect2(rect.Position.X, rect.Position.Y + rect.Size.Y * (1f - cooling), rect.Size.X, rect.Size.Y * cooling), new Color(0, 0, 0, 0.45f));
            }

            Label("ZXC"[i].ToString(), rect.Position + new Vector2(7, 18), 13, Muted, _bold);
        }
    }

    /// <summary>A slot: its rim takes the item's tier colour, and a stack shows its count in the corner.</summary>
    private void DrawSlot(Rect2 rect, Item? item, SlotRef slot, bool selected = false, int? shownCount = null)
    {
        _slots.Add((rect, slot));
        var hover = InventoryOpen && rect.HasPoint(GetLocalMousePosition());
        var tier = item is null ? new Color(1, 1, 1, 0.1f) : TierColor(item.Tier) with { A = item.Tier > 0 ? 0.75f : 0.22f };
        var fill = new Color(1, 1, 1, hover ? 0.14f : 0.055f);
        if (item is { Tier: > 0 })
        {
            // Better items glow faintly from the bottom of the slot.
            var glow = TierColor(item.Tier);
            Rounded(rect, 12, fill, Colors.Transparent, 0);
            var shine = new StyleBoxFlat { BgColor = glow with { A = 0.16f }, AntiAliasing = true };
            shine.CornerRadiusBottomLeft = shine.CornerRadiusBottomRight = 12;
            DrawStyleBox(shine, new Rect2(rect.Position.X, rect.Position.Y + rect.Size.Y * 0.55f, rect.Size.X, rect.Size.Y * 0.45f));
            fill = Colors.Transparent;
        }

        Rounded(rect, 12, fill, selected ? Accent : tier, selected ? 3 : item is { Tier: > 0 } ? 2 : 1);
        if (item is null)
        {
            return;
        }

        var count = shownCount ?? Items.Count(item);
        _iconDraws.Add((rect.Grow(-6), item.Icon, count > 0 ? 1f : 0.35f));
        if (item.IsSkill && SkillCooldown(item.Index) is var left and > 0f)
        {
            // A skill cooling down: a shade over the slot and the seconds left.
            DrawRect(rect.Grow(-2), new Color(0, 0, 0, 0.55f));
            var seconds = left >= 1f ? $"{Mathf.CeilToInt(left)}" : $"{left:0.0}".Replace(',', '.');
            Label(seconds, rect.Position + new Vector2((rect.Size.X - Width(seconds, 20, _bold)) / 2f, rect.Size.Y / 2f + 8), 20, Colors.White, _bold);
        }
        if (slot.Area == "hotbar" && Items.IsWorn(item))
        {
            // A small gold mark: this piece is the one worn.
            DrawCircle(rect.Position + new Vector2(rect.Size.X - 10, 10), 4.5f, new Color(0, 0, 0, 0.6f));
            DrawCircle(rect.Position + new Vector2(rect.Size.X - 10, 10), 3f, Accent);
        }
        if (count > 1)
        {
            var text = count.ToString();
            var at = new Vector2(rect.End.X - 6 - Width(text, 15, _bold), rect.End.Y - 7);
            DrawStringOutline(_bold, at, text, HorizontalAlignment.Left, -1, 15, 5, new Color(0, 0, 0, 0.8f));
            Label(text, at, 15, Colors.White, _bold);
        }
    }

    private void DrawCrystals(Rect2 area)
    {
        var counts = Snapshot?.Crystals ?? new int[Crystals.Tiers];
        var weight = 0f;
        Label("MAGIC CRYSTALS", area.Position, 13, Accent, _bold);
        for (var i = 0; i < Crystals.Tiers; i++)
        {
            var def = Crystals.All[i];
            weight += counts[i] * def.Weight;
            var x = area.Position.X + i * 96;
            _iconDraws.Add((new Rect2(x, area.Position.Y + 12, 36, 36), ItemDef.All[i].Icon, counts[i] > 0 ? 1f : 0.4f));
            Label(counts[i].ToString(), new Vector2(x + 42, area.Position.Y + 38), 20, counts[i] > 0 ? Text : Muted, _bold);
        }

        Label($"{weight:F1} kg of crystals", new Vector2(area.Position.X, area.Position.Y + 76), 15, Muted);
    }

    private void DrawInventory()
    {
        // With the chest open, the bag moves right to make room for the chest beside it.
        var chestWidth = ChestOpen ? 486f : 0f;
        var window = new Rect2((Size.X - 1064f - chestWidth) / 2f + chestWidth, (Size.Y - 690f) / 2f, 1064, 690);
        if (ChestOpen)
        {
            DrawChest(new Rect2(window.Position.X - chestWidth, window.Position.Y, chestWidth - 14, 690));
        }

        Card(window, 22, GlassDeep, true);
        Label("Equipment", window.Position + new Vector2(34, 56), 24, Text, _bold);
        Label("Bag", window.Position + new Vector2(340, 56), 24, Text, _bold);
        var used = Items.Bag.Count(i => i is not null);
        Label($"{used} / {Inventory.BagSlots}", window.Position + new Vector2(340 + Width("Bag", 24, _bold) + 12, 56), 15, Muted, _bold);
        DrawLine(window.Position + new Vector2(310, 34), window.Position + new Vector2(310, window.Size.Y - 34), Rim, 1f);

        var equip = new (EquipSlot Slot, string Label, int X, int Y)[]
        {
            (EquipSlot.Head, "Head", 116, 84),
            (EquipSlot.Chest, "Chest", 116, 188),
            (EquipSlot.Legs, "Legs", 116, 292),
            (EquipSlot.Boots, "Boots", 116, 396),
            (EquipSlot.Weapon, "Weapon", 26, 188),
            (EquipSlot.Ring, "Ring", 206, 188),
        };
        foreach (var (slot, label, x, y) in equip)
        {
            var rect = new Rect2(window.Position + new Vector2(x, y), SlotSize, SlotSize);
            DrawSlot(rect, Items.Equipped[slot], new SlotRef("equip", 0, slot));
            Label(label, rect.Position + new Vector2((SlotSize - Width(label, 13)) / 2f, SlotSize + 18), 13, Muted);
        }

        const int columns = 8;
        for (var i = 0; i < Inventory.BagSlots; i++)
        {
            var rect = new Rect2(window.Position + new Vector2(340 + i % columns * (SlotSize + 9), 86 + i / columns * (SlotSize + 9)), SlotSize, SlotSize);
            DrawSlot(rect, Items.Bag[i], new SlotRef("bag", i));
        }

        DrawCrystals(new Rect2(window.Position + new Vector2(34, 540), 260, 90));
        var worth = Items.SaleValue();
        if (worth > 0)
        {
            Label($"Materials worth {worth} gold to the appraiser", window.Position + new Vector2(340, 452), 15, Muted);
        }

        var bottom = window.Position + new Vector2(340, window.Size.Y - 30);
        Label(ChestOpen ? "Left click: store or take    Right click: eat or wear" : "Left click: move    Right click: eat or wear    Shift + right click: discard", bottom, 15, Muted);
        Keycap("I", new Vector2(window.End.X - 120, bottom.Y - 22), 14);
        Label("close", new Vector2(window.End.X - 86, bottom.Y), 15, Muted);
    }

    /// <summary>The Guild chest: everything stored, stacks first and then gear, six to a row.</summary>
    private void DrawChest(Rect2 window)
    {
        Card(window, 22, GlassDeep, true);
        Label("Guild Chest", window.Position + new Vector2(30, 56), 24, Text, _bold);
        var contents = Items.ChestContents();
        const int columns = 6;
        const int rows = 7;
        for (var i = 0; i < columns * rows; i++)
        {
            var rect = new Rect2(window.Position + new Vector2(28 + i % columns * (SlotSize + 9), 86 + i / columns * (SlotSize + 9)), SlotSize, SlotSize);
            var entry = i < contents.Count ? contents[i] : default;
            DrawSlot(rect, i < contents.Count ? entry.Item : null, new SlotRef("chest", i), false, i < contents.Count ? entry.Count : null);
        }

        Label(contents.Count == 0 ? "Empty. Click things in your bag to store them." : "Click to take back.",
            window.Position + new Vector2(30, window.Size.Y - 30), 15, Muted);
    }

    /// <summary>A small card next to the pointer naming the item under it, drawn on the given layer.</summary>
    private void DrawTooltip(CanvasItem canvas)
    {
        var mouse = GetLocalMousePosition();
        var hovered = _slots.Where(s => s.Rect.HasPoint(mouse)).Select(s => Read(s.Slot)).FirstOrDefault(i => i is not null);
        if (!InventoryOpen || hovered is not { } item || Items.Held is not null)
        {
            return;
        }

        var tier = Tiers[Math.Clamp(item.Tier, 0, 3)];
        var kind = item.Kind switch
        {
            "food" => $"Food  ·  restores {item.Heal} health",
            "material" => "Material",
            _ => item.Slot == EquipSlot.None ? "Item" : item.Slot == EquipSlot.Weapon && item.Gear is { } weapon
                ? $"{char.ToUpperInvariant(weapon.Def.Weapon[0])}{weapon.Def.Weapon[1..]}" : $"{item.Slot} equipment",
        };
        var price = item.Price > 0 && item.Kind != "equipment" ? $"Sells for {item.Price} gold" : "";
        var lines = GearLines(item);
        const float width = 340f;
        var textHeight = _font.GetMultilineStringSize(item.Description, HorizontalAlignment.Left, width - 36, 16).Y;
        var height = 92f + textHeight + (price.Length > 0 ? 26f : 0f) + (lines.Count > 0 ? 14f + lines.Count * 21f : 0f);
        var at = mouse + new Vector2(22, 10);
        at.X = Mathf.Min(at.X, Size.X - width - 12);
        at.Y = Mathf.Min(at.Y, Size.Y - height - 12);
        var rect = new Rect2(at, width, height);
        var shadow = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.3f), AntiAliasing = true, ShadowColor = new Color(0, 0, 0, 0.22f), ShadowSize = 14 };
        shadow.SetCornerRadiusAll(16);
        canvas.DrawStyleBox(shadow, new Rect2(rect.Position + new Vector2(0, 5), rect.Size));
        var box = new StyleBoxFlat { BgColor = new Color(0.03f, 0.035f, 0.055f, 0.97f), BorderColor = Rim, AntiAliasing = true };
        box.SetBorderWidthAll(1);
        box.SetCornerRadiusAll(14);
        canvas.DrawStyleBox(box, rect);
        var pill = new StyleBoxFlat { BgColor = tier.Color, AntiAliasing = true };
        pill.SetCornerRadiusAll(1);
        canvas.DrawStyleBox(pill, new Rect2(rect.Position + new Vector2(0, 14), 3, 30));
        canvas.DrawString(_bold, rect.Position + new Vector2(18, 32), item.Name, HorizontalAlignment.Left, -1, 20, tier.Color);
        canvas.DrawString(_bold, rect.Position + new Vector2(18, 56), $"{tier.Name.ToUpperInvariant()}  ·  {kind}", HorizontalAlignment.Left, -1, 13, Muted);
        var y = rect.Position.Y + 84;
        if (lines.Count > 0)
        {
            foreach (var (text, color) in lines)
            {
                canvas.DrawString(_bold, new Vector2(rect.Position.X + 18, y + 2), text, HorizontalAlignment.Left, -1, 15, color);
                y += 21;
            }

            y += 14;
        }

        canvas.DrawMultilineString(_font, new Vector2(rect.Position.X + 18, y), item.Description, HorizontalAlignment.Left, width - 36, 16, -1, Muted);
        if (price.Length > 0)
        {
            canvas.DrawCircle(rect.Position + new Vector2(25, height - 24), 6, Accent);
            canvas.DrawString(_bold, rect.Position + new Vector2(38, height - 18), price, HorizontalAlignment.Left, -1, 15, Accent);
        }
    }

    /// <summary>
    /// While a bow is drawn, a ring round the middle of the screen fills with the draw and turns
    /// gold at a full draw, so the player knows when to let go.
    /// </summary>
    /// <summary>
    /// The bow's crosshair in the middle of the screen, where the arrow will fly. Its four ticks close
    /// in as the draw fills; they stay dim until the bow is drawn far enough to shoot, and turn gold
    /// at a full draw.
    /// </summary>
    private void DrawDraw()
    {
        if (Aiming <= 0f)
        {
            return;
        }

        var centre = (Size / 2f).Round();
        var drawn = Tuning.Drawn(Player.DrawTicks);
        var ready = Player.DrawTicks >= Tuning.MinDraw;
        var full = Player.DrawTicks >= Tuning.FullDraw;
        var colour = full ? Accent : new Color(1, 1, 1, ready ? 0.95f : 0.45f);
        colour.A *= Aiming;
        var shadow = new Color(0, 0, 0, 0.5f * Aiming);
        var gap = Player.DrawTicks > 0 ? Mathf.Lerp(22f, 5f, drawn) : 14f;
        foreach (var direction in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
        {
            var from = centre + direction * gap;
            var to = centre + direction * (gap + 9f);
            DrawLine(from, to, shadow, 4f);
            DrawLine(from, to, colour, 2f);
        }

        DrawCircle(centre, 2.5f, shadow);
        DrawCircle(centre, 1.5f, colour);
    }

    /// <summary>
    /// The stat lines of a piece of gear: what its base gives (weapon damage against the wooden
    /// sword, defense, health) in white, then each affix in the colour of the piece's rarity.
    /// </summary>
    private List<(string Text, Color Color)> GearLines(Item item)
    {
        var lines = new List<(string, Color)>();
        if (item.Gear is not { } piece)
        {
            return lines;
        }

        if (piece.Def.Slot == "weapon")
        {
            lines.Add(($"{piece.Power * 100f:F0}% weapon damage", Text));
        }

        if (piece.Defense > 0)
        {
            lines.Add(($"{piece.Defense} defense", Text));
        }

        if (piece.Health > 0)
        {
            lines.Add(($"+{piece.Health} maximum health", Text));
        }

        var color = TierColor((int)piece.Rarity);
        foreach (var affix in piece.Affixes)
        {
            lines.Add((affix.Def.Describe(affix.Value), color));
        }

        lines.Add(($"Item level {piece.Level}", Muted));
        return lines;
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
            var head = position + Vector3.Up * (def.TagHeight > 0f ? def.TagHeight * def.Size : def.Size * 1.5f + 0.25f);

            // Peaceful animals only name themselves up close, so a herd does not fill the sky with labels.
            var range = def.Kind == "animal" && def.Temper == "passive" && enemy.Health == enemy.MaxHealth ? 14f : TagRange;
            if (Camera.IsPositionBehind(head) || Camera.GlobalPosition.DistanceTo(head) > range)
            {
                continue;
            }

            DrawTag(ToHud(head), enemy, def);
        }

        foreach (var (at, text) in Signs)
        {
            var distance = Camera.GlobalPosition.DistanceTo(at);
            if (Camera.IsPositionBehind(at) || distance > 16f)
            {
                continue;
            }

            // A gold name with the E key beside it, bobbing gently.
            var bob = Mathf.Sin(Time.GetTicksMsec() / 400f) * 3f;
            var point = ToHud(at) + new Vector2(0, bob);
            var width = Width(text, 16, _bold);
            var plate = new Rect2(point.X - width / 2f - 40, point.Y - 22, width + 56, 32);
            Rounded(plate, 10, new Color(0.05f, 0.05f, 0.08f, 0.72f), Accent with { A = 0.6f }, 1);
            Keycap("E", new Vector2(plate.Position.X + 8, plate.Position.Y + 6), 13);
            Label(text, new Vector2(plate.Position.X + 40, point.Y), 16, Accent, _bold);
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
