using System.Numerics;
using System.Text;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>
/// Writes design/balance.md: for every hostile enemy, at the level it lives at, how many light blows of the starting
/// sword kill it and how long that takes, how hard its worst blow hits a player of the same level, and how many
/// kills a level takes. Measured with the real server (one real blow on each enemy), not guessed. Runs only with
/// PIXEL_BALANCE=1, so the normal test run stays quick.
/// </summary>
public class BalanceReport
{
    [Fact]
    public void WriteBalanceReport()
    {
        if (Environment.GetEnvironmentVariable("PIXEL_BALANCE") != "1")
        {
            return;
        }

        var rows = new List<(int Level, string Line)>();
        foreach (var def in EnemyDef.All.Where(d => d.Temper == "hostile" && !d.Decoy && !Guild.IsExaminer(d.Id) && d.Id != "shadow_clone" && d.Id != "page_wisp" && d.Id != "gallows_crow"))
        {
            var level = Math.Max(1, def.MinLevel);
            var blow = OneBlow(def.Id, (byte)level);
            var health = def.Health + def.HealthPerLevel * (level - 1);
            var hits = blow > 0 ? (int)Math.Ceiling(health / (double)blow) : -1;
            var seconds = hits * Tuning.Attack(AttackKind.Light).Total / (double)Tuning.TickRate;
            var worst = def.Abilities?.Max(a => (int)a.Damage) ?? def.SplashDamage;
            var playerHealth = Progression.MaxHealth(level);
            var xp = Progression.KillXp(level, level, def.Rare);
            var kills = Math.Ceiling(Progression.XpToNext(level) / (double)xp);
            rows.Add((level, $"| {def.Name}{(def.Rare ? " (rare)" : "")} | {level} | {health} | {blow} | {(hits < 0 ? "-" : hits.ToString())} | {(hits < 0 ? "-" : seconds.ToString("0.0"))} | {worst} | {playerHealth} | {(worst > 0 ? Math.Ceiling(playerHealth / (double)worst).ToString() : "-")} | {xp} | {kills} |"));
        }

        var text = new StringBuilder();
        text.AppendLine("# Balance report (measured)");
        text.AppendLine();
        text.AppendLine($"Written by `net-tests/BalanceReport.cs` (`PIXEL_BALANCE=1 dotnet test --filter BalanceReport`) on {DateTime.Now:yyyy-MM-dd}.");
        text.AppendLine("Each enemy at its lowest level against a player of the same level with Hanne's Shortsword (the starting");
        text.AppendLine("sword), no attributes and no other gear. Light blow: one real blow on the server. Kill time counts light");
        text.AppendLine("blows back to back, so a real fight (dodging, blocking, a running enemy) takes longer.");
        text.AppendLine();
        text.AppendLine("| Enemy | Lv | Health | Light blow | Blows to kill | Seconds | Its worst blow | Player health | Its blows to kill you | XP | Kills per level |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var (_, line) in rows.OrderBy(r => r.Level))
        {
            text.AppendLine(line);
        }

        text.AppendLine();
        text.AppendLine("## What stands out (for the author to decide; nothing is tuned yet)");
        text.AppendLine();
        text.AppendLine("- Kills per level climb from 5 at level 1 to 30 to 55 from level 9: jobs soften it, but later levels grind.");
        text.AppendLine("- With the starting sword the Goblin Brute (15 blows) and the Stoneback (17) are tanks beside the 5 to 12 of their level.");
        text.AppendLine("- The Goblin Sharpshooter's power shot and the Archive Warden's rune wave hit hardest for their level (5 and 6 to kill).");
        text.AppendLine("- Gear, attributes and mastery raise the player's blow; this table is the floor, the starting kit.");
        text.AppendLine();
        text.AppendLine("Levels: " + string.Join(", ", Enumerable.Range(1, 12).Select(l => $"Lv {l} needs {Progression.XpToNext(l)} XP")));
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        File.WriteAllText(Path.Combine(root, "design", "balance.md"), text.ToString());
    }

    /// <summary>One light blow of the starting sword on an enemy standing open in front of the player.</summary>
    private static int OneBlow(string id, byte level)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var camp = new EnemySpawn(new Vector3(0, 0, -40), 0.1f, 1, Types: new[] { id });
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 3 });
        var client = new ClientPrediction(network.Connect(1), world);
        Run(server, client, network, 10, Buttons.None);
        server.EnemiesFrozen = true;
        server.SetLevel(1, level);
        var place = server.GiveGear(1, new GearPiece(ItemDef.Index("hannes_shortsword"), Rarity.Common, 1, Array.Empty<AffixRoll>()));
        Run(server, client, network, 12, Buttons.None, GearCommands.Toggle(place));
        var enemy = server.Enemies[0];
        enemy.Position = server.GetPlayer(1)!.Value.Position + new Vector3(0, 0, -1.6f);
        // Open to blows: not aloft, not in a shell, not under water.
        enemy.Action = EnemyAction.Strike;
        enemy.ActionTicks = 999;
        var before = enemy.Health;
        Run(server, client, network, 1, Buttons.Light);
        Run(server, client, network, 40, Buttons.None);
        return before - enemy.Health;
    }

    private static void Run(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Buttons buttons, Command command = Command.None)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, buttons, 0, i == 0 ? command : Command.None);
        }
    }
}
