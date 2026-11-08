using System.Reflection;
using System.Text.Json;

namespace PixelCreate.Core;

/// <summary>
/// A skill a player can learn from a book, loaded from world/skills.json. A skill reuses the effect
/// an enemy ability of the same name has, turned against enemies. Teachers are the enemies that
/// use it; each can drop its book, rare ones with their own chance.
/// </summary>
public sealed record SkillDef(
    string Id, string Name, string Description, string Effect, string Icon, short Damage, float Stamina, int CooldownTicks,
    float Radius = 0f, string[]? Teachers = null, double BookChance = 0, double RareBookChance = 0)
{
    private static readonly Lazy<IReadOnlyList<SkillDef>> Loaded = new(Load);

    public static IReadOnlyList<SkillDef> All => Loaded.Value;

    /// <summary>A skill's place in the list, which is how commands and snapshots name it.</summary>
    public static int Index(string id) => All.ToList().FindIndex(s => s.Id == id);

    /// <summary>The item index of this skill's book.</summary>
    public ushort Book => ItemDef.Index($"book_{Id}");

    /// <summary>The chance this enemy drops this skill's book on dying.</summary>
    public double ChanceFrom(EnemyDef enemy) =>
        Teachers?.Contains(enemy.Id) == true ? (enemy.Rare && RareBookChance > 0 ? RareBookChance : BookChance) : 0;

    private static IReadOnlyList<SkillDef> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("skills")!;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<SkillDef[]>(stream, options)!;
    }
}

/// <summary>Skill rules shared by both sides.</summary>
public static class Skills
{
    /// <summary>Each Intelligence point takes this share off every skill cooldown, up to the limit.</summary>
    public const float CooldownCutPerIntelligence = 0.01f;
    public const float MaxCooldownCut = 0.3f;

    public static int Cooldown(SkillDef skill, int intelligence) =>
        (int)MathF.Round(skill.CooldownTicks * (1f - MathF.Min(MaxCooldownCut, intelligence * CooldownCutPerIntelligence)));
}
